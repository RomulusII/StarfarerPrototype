<?php
/**
 * Starfarer web dağıtımı — tek dosya, bağımlılık yok.
 *
 * NEDEN FTP DEĞİL: kurumsal ağ (Zscaler) FTP'yi proxy'liyor ve şifrelemeden
 * ÖNCE giriş bilgisi istiyor ("220 Zscaler/6.2: USER expected", AUTH TLS'e
 * "503 Login with USER first"). Yani o yoldan dağıtmak, FTP parolasını
 * proxy'ye açık metin vermek demekti. Bu uç 443'ten geçer; ağ TLS'i açıp
 * yeniden imzalasa bile parola değil, tek amaçlı bir dağıtım token'ı taşır ve
 * o token yalnızca bu dizine dosya yazabilir.
 *
 * KURULUM
 *   1. DEPLOY_TOKEN'ı değiştir (aynısını deploy.config.json'a yaz).
 *   2. Bu dosyayı oyunun ÜST klasörüne koy:
 *        C:\inetpub\vhosts\akinayan.de\httpdocs\Starfarer\deploy.php
 *      Oyunun kendi klasörüne DEĞİL: her dağıtımda `game` klasörü takas
 *      ediliyor, içinde dursaydı kendi altındaki zemini çekerdi.
 *
 * UÇLAR
 *   GET  ...?ping=1                        → "pong" (token gerekmez)
 *   GET  ...?t=TOKEN&ping=1                → sınırlar, dizinler, boş alan
 *   POST ...?t=TOKEN&reset=1               → game-next/ temizlenir
 *   POST ...?t=TOKEN&p=YOL&o=OFSET         gövde = dosya parçası
 *   POST ...?t=TOKEN&swap=1                → game-next ⇄ game
 *
 * NEDEN PARÇALI YÜKLEME: PHP'nin post_max_size'ı (Plesk'te çoğu zaman 8 MB)
 * tek parça .data dosyasını reddederdi ve hata "dosya çok büyük" diye değil,
 * BOŞ GÖVDE olarak görünürdü. İstemci sınırı ?ping=1'den okuyup parçayı ona
 * göre seçiyor.
 *
 * NEDEN TAKAS: yükleme sürerken kimse yarım build'e düşmesin. Dosyalar önce
 * game-next/ altında toplanır; hepsi yerine oturunca tek bir rename oyunu
 * yayına alır. Eski sürüm game-YYYYAAGG-SSDDSS adıyla saklanır, yani geri
 * dönüş de tek rename.
 *
 * GÜVENLİK SINIRI TOKEN'DIR. Token'ı olan bu klasörün altına dosya yazabilir;
 * amaç zaten bu. Buradaki kuralların işi, token sızarsa bunun ÇALIŞTIRILABİLİR
 * koda dönüşmesini engellemek:
 *   · Uzantı BEYAZ LİSTESİ — .php/.aspx/.exe kabul edilmez, listede yoksa yok.
 *   · web.config yalnızca TAM BU ADLA yazılabilir; başka .config yazılamaz.
 *     Oyunun MIME eşlemesi build ile birlikte gelmek zorunda (bkz. şablondaki
 *     web.config), o yüzden büsbütün yasaklanamıyor.
 *   · Yol her segmentte süzülür, ".." ve dizin ayıracı elenir, derinlik 4.
 *   · Silme yalnızca bu klasörün altında ve yalnızca adı kalıba uyan
 *     dizinlerde çalışır (realpath ile doğrulanır).
 *
 * PHP 5.3 uyumlu yazıldı: sunucudaki sürüm 5.4.45 ve `fn()`, `str_ends_with`,
 * `[]` dizi sözdizimi orada PARSE HATASI verir — token kontrolüne bile
 * gelmeden 500 döner.
 */

ini_set('display_errors', '1');
error_reporting(E_ALL);

// GERÇEK DEĞER BURAYA YAZILMAZ: repo herkese açık. Değer iki yerde yaşar —
// sunucudaki bu dosyada ve Tools/Deploy/deploy.config.json içinde (.gitignore).
// Log token'larından AYRI olmalı: biri veri toplar, bu dosya yazar.
const DEPLOY_TOKEN = 'BUNU_DEGISTIR_DEPLOY';

define('BASE_DIR', __DIR__);

const STAGE     = 'game-next';
const LIVE      = 'game';
const KEEP      = 2;            // saklanan eski sürüm sayısı (geri dönüş için)
const MAX_CHUNK = 8388608;      // 8 MB — istemci ping'den okuduğu sınırla küçültür
const MAX_FILE  = 268435456;    // 256 MB — tek dosya tavanı
const MAX_DEPTH = 4;

if (!function_exists('hash_equals')) {
    function hash_equals($known, $given) {
        if (!is_string($known) || !is_string($given))   return false;
        if (strlen($known) !== strlen($given))          return false;
        $r = 0;
        for ($i = 0; $i < strlen($known); $i++) $r |= ord($known[$i]) ^ ord($given[$i]);
        return $r === 0;
    }
}
if (!function_exists('http_response_code')) {
    function http_response_code($code) {
        header('X-PHP-Response-Code: ' . $code, true, $code);
    }
}

/**
 * Yazılmasına izin verilen uzantılar. Beyaz liste, kara liste DEĞİL: kara
 * listede unutulan tek uzantı (.phtml, .php5, .ashx...) çalıştırılabilir kod
 * demektir, beyaz listede unutulan uzantı yalnızca eksik bir dosyadır.
 */
function izinli_uzantilar() {
    return array(
        'html', 'htm', 'js', 'json', 'css', 'txt',
        'wasm', 'data', 'br', 'gz', 'unityweb', 'symbols',   // Unity WebGL çıktısı
        'png', 'jpg', 'jpeg', 'gif', 'svg', 'ico', 'webp',
        'apk',                                                // sayfadaki indirme
    );
}

/**
 * İstemciden gelen göreli yolu, STAGE altındaki mutlak bir yola çevirir.
 * Kabul edilmezse null döner — çağıran taraf 400 verir.
 */
function guvenli_yol($rel) {
    $rel = str_replace('\\', '/', (string)$rel);
    if ($rel === '' || $rel[0] === '/' || strpos($rel, ':') !== false) return null;

    $parcalar = explode('/', $rel);
    if (count($parcalar) > MAX_DEPTH) return null;

    foreach ($parcalar as $p) {
        if ($p === '' || $p === '.' || $p === '..') return null;
        if (!preg_match('/^[A-Za-z0-9._-]{1,80}$/', $p)) return null;
    }

    $ad   = $parcalar[count($parcalar) - 1];
    $nokta = strrpos($ad, '.');
    if ($nokta === false) return null;
    $uz = strtolower(substr($ad, $nokta + 1));

    // web.config: MIME eşlemesi build ile birlikte gelmek zorunda, ama
    // yalnızca bu ad. Başka .config yazılabilseydi token sızıntısı sunucu
    // yapılandırmasını değiştirme yetkisine dönüşürdü.
    if ($uz === 'config') {
        if (strtolower($ad) !== 'web.config') return null;
    } else if (!in_array($uz, izinli_uzantilar())) {
        return null;
    }

    return BASE_DIR . '/' . STAGE . '/' . implode('/', $parcalar);
}

/**
 * Dizini içeriğiyle siler. İKİ KORUMA: yol gerçekten BASE_DIR'in altında mı
 * (realpath, sembolik bağ oyunlarına karşı) ve adı beklediğimiz kalıba uyuyor
 * mu. Bu fonksiyon yanlış bir yolla çağrılırsa geri dönüşü olmayan bir iş
 * yapar; o yüzden kendi kendini doğruluyor.
 */
function sil_dizin($path) {
    $gercek = realpath($path);
    $kok    = realpath(BASE_DIR);
    if ($gercek === false || $kok === false) return false;

    $gercek = str_replace('\\', '/', $gercek);
    $kok    = str_replace('\\', '/', $kok);
    if (strpos($gercek, $kok . '/') !== 0) return false;      // BASE_DIR altında değil

    $ad = basename($gercek);
    if (!preg_match('/^(' . STAGE . '|' . LIVE . '-\d{8}-\d{6})$/', $ad)) return false;

    return sil_icerik($gercek);
}

function sil_icerik($dir) {
    $ogeler = scandir($dir);
    if ($ogeler === false) return false;
    foreach ($ogeler as $o) {
        if ($o === '.' || $o === '..') continue;
        $p = $dir . '/' . $o;
        if (is_dir($p)) sil_icerik($p);
        else @unlink($p);
    }
    return @rmdir($dir);
}

// ── Teşhis (token gerekmez) ──────────────────────────────────────────────────
// Sürüm basmaz: desteği bitmiş bir PHP'nin sürümünü ilan etmek, tarayan bota
// hangi açığı deneyeceğini söylemektir. Tek işi "dosya parse edildi mi".
if (isset($_GET['ping']) && !isset($_GET['t'])) {
    header('Content-Type: text/plain');
    exit("pong\n");
}

// Token DEĞİŞTİRİLMİŞ Mİ. Yer tutucunun kendisiyle karşılaştırmak yerine
// BİÇİM aranıyor, çünkü karşılaştırma iki yerde aynı metni tutardı ve dosyayı
// otomatik dolduran her araç (sed dahil) ikisini birden değiştirip kontrolü
// sessizce her zaman doğru hâle getirirdi — koruma, koruduğu şeyle birlikte
// kaybolurdu. Ayrıca bu hâli asgari bir token gücü de dayatıyor: yukarıdaki
// yer tutucu 20 karakter, yani doldurulmadan elemeye takılır.
if (!preg_match('/^[A-Za-z0-9_-]{24,}$/', DEPLOY_TOKEN)) {
    http_response_code(500);
    exit("kurulmadi: DEPLOY_TOKEN en az 24 karakter olmali (A-Z a-z 0-9 _ -)\n");
}

$given = isset($_GET['t']) ? $_GET['t'] : '';
if (!hash_equals(DEPLOY_TOKEN, $given)) {
    http_response_code(403);
    exit("nope\n");
}

$stage = BASE_DIR . '/' . STAGE;
$live  = BASE_DIR . '/' . LIVE;

if (isset($_GET['ping'])) {
    header('Content-Type: application/json');
    // İstemci parça boyutunu buradan seçiyor: post_max_size'ı bilmeden
    // yüklemek, "boş gövde" diye görünen sessiz bir kesilme üretir.
    exit(json_encode(array(
        'php'             => PHP_VERSION,
        'base'            => BASE_DIR,
        'stage_var'       => is_dir($stage),
        'live_var'        => is_dir($live),
        'yazilir'         => is_writable(BASE_DIR),
        'post_max_size'   => ini_get('post_max_size'),
        'max_chunk'       => MAX_CHUNK,
        'max_file'        => MAX_FILE,
        'bos_alan_mb'     => round(@disk_free_space(BASE_DIR) / 1048576),
    )));
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    exit("POST bekleniyor\n");
}

// ── Sahneyi temizle ──────────────────────────────────────────────────────────
// Her dağıtım sıfırdan başlar. Yoksa önceki build'den kalan ve artık
// üretilmeyen bir dosya (adı değişmiş bir .br, silinmiş bir sahne) yeni
// build'in içinde yaşamaya devam eder ve ne zaman okunduğu belli olmaz.
if (isset($_GET['reset'])) {
    if (is_dir($stage) && !sil_dizin($stage)) {
        http_response_code(500);
        exit("game-next temizlenemedi\n");
    }
    if (!@mkdir($stage, 0775, true)) {
        http_response_code(500);
        exit("game-next olusturulamadi\n");
    }
    exit("ok reset\n");
}

// ── Takas ────────────────────────────────────────────────────────────────────
if (isset($_GET['swap'])) {
    if (!is_dir($stage)) {
        http_response_code(400);
        exit("game-next yok: once dosyalari yukle\n");
    }
    // index.html olmadan takas etmek, çalışan oyunu boş bir klasörle
    // değiştirmek demek. Ucuz kontrol, pahalı hatayı önlüyor.
    if (!is_file($stage . '/index.html')) {
        http_response_code(400);
        exit("game-next icinde index.html yok: eksik yukleme\n");
    }

    $yedek = null;
    if (is_dir($live)) {
        $yedek = BASE_DIR . '/' . LIVE . '-' . date('Ymd-His');
        if (!@rename($live, $yedek)) {
            http_response_code(500);
            exit("eski surum tasinamadi (dosya kilitli olabilir)\n");
        }
    }
    if (!@rename($stage, $live)) {
        // Yeni sürüm yerine oturmadı: eskisini geri koy, oyun ayakta kalsın.
        if ($yedek !== null) @rename($yedek, $live);
        http_response_code(500);
        exit("takas basarisiz, eski surum geri alindi\n");
    }

    // Eski sürümleri buda — KEEP tanesi geri dönüş için durur.
    $eski = glob(BASE_DIR . '/' . LIVE . '-*');
    if (!is_array($eski)) $eski = array();
    sort($eski);                                  // ad zaman damgası: sıra kronolojik
    $silinecek = count($eski) - KEEP;
    for ($i = 0; $i < $silinecek; $i++) sil_dizin($eski[$i]);

    exit("ok swap" . ($yedek === null ? "" : " yedek=" . basename($yedek)) . "\n");
}

// ── Dosya parçası ────────────────────────────────────────────────────────────

$rel = isset($_GET['p']) ? $_GET['p'] : '';
$hedef = guvenli_yol($rel);
if ($hedef === null) {
    http_response_code(400);
    exit("kabul edilmeyen yol: " . $rel . "\n");
}

$ofset = isset($_GET['o']) ? (int)$_GET['o'] : 0;
if ($ofset < 0 || $ofset > MAX_FILE) {
    http_response_code(400);
    exit("kabul edilmeyen ofset\n");
}

$body = file_get_contents('php://input', false, null, 0, MAX_CHUNK + 1);
if ($body === false) {
    http_response_code(400);
    exit("govde okunamadi\n");
}
if (strlen($body) > MAX_CHUNK) {
    http_response_code(413);
    exit("parca cok buyuk\n");
}
if ($ofset + strlen($body) > MAX_FILE) {
    http_response_code(413);
    exit("dosya cok buyuk\n");
}

$dizin = dirname($hedef);
if (!is_dir($dizin) && !@mkdir($dizin, 0775, true)) {
    http_response_code(500);
    exit("dizin olusturulamadi\n");
}

// Ofset, dosyanın MEVCUT boyutuyla birebir tutmalı. Tutmuyorsa parçalar
// karışmış demektir (tekrar gönderim, sıra bozulması); sessizce yanlış yere
// yazmaktansa durmak yeğdir — bozuk bir .wasm'ın belirtisi, günler sonra
// "oyun bazen açılmıyor" olur.
$mevcut = is_file($hedef) ? filesize($hedef) : 0;
if ($ofset === 0) {
    $ok = @file_put_contents($hedef, $body, LOCK_EX);
} else {
    if ($mevcut !== $ofset) {
        http_response_code(409);
        exit("ofset uyusmuyor: dosyada " . $mevcut . ", gelen " . $ofset . "\n");
    }
    $ok = @file_put_contents($hedef, $body, FILE_APPEND | LOCK_EX);
}

if ($ok === false) {
    http_response_code(500);
    exit("yazilamadi: " . $rel . "\n");
}

http_response_code(200);
echo "ok " . filesize($hedef) . "\n";
