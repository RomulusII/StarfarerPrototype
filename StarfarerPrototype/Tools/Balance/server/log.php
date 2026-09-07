<?php
/**
 * Starfarer denge kaydı toplayıcı — tek dosya, bağımlılık yok.
 *
 * KURULUM
 *   1. Bu dosyayı web köküne kopyala (Apache/IIS fark etmez), örn:
 *        .../httpdocs/Starfarer/log/log.php
 *   2. Aşağıdaki ÜÇ token'ı da değiştir; hangisinin nereye gittiği yanlarında
 *      yazıyor. Değiştirilmezse script hiçbir isteği kabul etmez.
 *   3. Kayıtlar script'in yanındaki `logs/` klasörüne yazılır; script onu
 *      kendisi oluşturur ve içine erişimi kapatan bir web.config koyar.
 *
 * UÇLAR
 *   GET  ...?ping=1                          → "pong" (token gerekmez, teşhis için)
 *   GET  ...?t=OKUMA&ping=1                  → ayrıntılı durum (PHP sürümü, kota)
 *   GET  ...?t=YAZMA&d=CIHAZ&f=DOSYA&size=1  → dosyanın sunucudaki boyutu
 *   POST ...?t=YAZMA&d=CIHAZ&f=DOSYA&o=OFS   gövde = kaydın devamı (EKLEME)
 *   POST ...?t=YAZMA&d=CIHAZ&f=DOSYA         gövde = kaydın tamamı (eski yol)
 *   GET  ...?t=OKUMA&list=1                  → JSON dosya listesi
 *   GET  ...?t=OKUMA&get=DOSYA               → dosyanın kendisi
 *
 * NEDEN YAZMA VE OKUMA TOKEN'I AYRI
 *   Yazma token'ı istemcinin İÇİNDEDİR ve gizli değildir — APK'dan da,
 *   tarayıcıya inen WebGL paketinden de çıkarılabilir. Tek token varken bunun
 *   anlamı şuydu: paketi açan herkes `list=1` ile bütün kayıtları listeleyip
 *   indirebilir. Okuma token'ı artık yalnızca pull.config.json içinde yaşar,
 *   hiçbir build'e girmez; liste ve indirme uçları istemciye kapalıdır.
 *
 * NEDEN WEB VE NATIVE İÇİN AYRI YAZMA TOKEN'I
 *   Kimliklendirme için değil: kaydın hangi platformdan geldiği zaten log'un
 *   `platform` alanında yazılı. Sebep İPTAL EDİLEBİLİRLİK. Tarayıcı sürümünün
 *   linki herkese açık olacak; o token kötüye kullanılırsa yalnızca onu
 *   değiştirirsin ve APK'sı olan testçiler etkilenmez.
 *
 * TOKEN GİZLİLİK İÇİN DEĞİL: içerik oyun olayları. Yazma ucunu token'la
 * kapatmanın tek sebebi, açık bir POST ucunun birkaç gün içinde tarayıcı
 * botlarınca bulunup diski doldurmasıdır. Botu durdurur, paketi açan birini
 * durdurmaz — bu yüzden asıl savunma aşağıdaki KOTA ve OFSET kurallarıdır, ve
 * son sözü analiz tarafındaki tutarlılık kontrolleri söyler. İstemci tarafında
 * tutulamayan bir sırla veri bütünlüğü garanti edilemez.
 *
 * EKLEME UCU güvenliği ZAYIFLATMAZ, güçlendirir. Üzerine yazan bir uçta token'ı
 * olan biri bir kaydı baştan sona değiştirebiliyordu; ofset denetimli eklemede
 * bayt yok edilemiyor, yalnızca ekleniyor. Eski üzerine-yazma yolu yalnızca
 * dağıtılmış paketlerle uyum için duruyor.
 *
 * SÜRÜM NOTU: bu dosya PHP 5.3 ile de çalışır. İlk sürümde `fn() =>` (7.4+) ve
 * `str_ends_with` (8.0+) kullanılmıştı; eski bir PHP'de dosya PARSE EDİLEMİYOR,
 * yani token kontrolüne bile gelmeden 500 dönüyordu. Bir teşhis ucunun
 * token'sız çalışması tam da bu yüzden gerekli: "500 mü, 403 mü" sorusu
 * "kod mu bozuk, ayar mı yanlış" sorusunun cevabıdır.
 *
 * Sunucudaki PHP 5.4.45 ve DESTEĞİ 2015'te BİTTİ. Buradaki hiçbir kural onu
 * yamamaz; Plesk'te alan adının PHP sürümünü yükseltmek bu dosyadaki bütün
 * düzeltmelerden değerlidir. Kod 5.3 uyumlu olduğu için yükseltme hiçbir şeyi
 * bozmaz.
 */

// Hata ayıklama: 500 hataları tarayıcıda boş sayfa olarak görünür. Bu satır
// asıl mesajı ekrana basar. PARSE hatasında işe yaramaz (kod hiç çalışmaz);
// o durumda sunucunun kendi hata günlüğüne bakmak gerekir.
ini_set('display_errors', '1');
error_reporting(E_ALL);

// ── Eski PHP yedekleri ───────────────────────────────────────────────────────
//
// Sunucudaki PHP 5.6'dan eski çıktı (hash_equals tanımsız). Sürümü baştan
// bilmek yerine üç turda deneyerek öğrendik; teşhis ucunun token'sız
// çalışmasının sebebi bu.
//
// Polyfill'ler, "sunucuyu güncelle" demekten daha ucuz: bu bir test aracı ve
// tek işi log dosyası biriktirmek.

if (!function_exists('hash_equals')) {
    // Sabit süreli karşılaştırma (PHP 5.6+ ile aynı davranış). Zamanlama
    // saldırısı bu senaryoda gerçekçi bir tehdit değil ama basit `===` yazıp
    // "neden farklı" sorusunu geride bırakmaya değmez.
    function hash_equals($known, $given) {
        if (!is_string($known) || !is_string($given))   return false;
        if (strlen($known) !== strlen($given))          return false;
        $r = 0;
        for ($i = 0; $i < strlen($known); $i++) $r |= ord($known[$i]) ^ ord($given[$i]);
        return $r === 0;
    }
}

if (!function_exists('http_response_code')) {
    // PHP 5.4 öncesi
    function http_response_code($code) {
        header('X-PHP-Response-Code: ' . $code, true, $code);
    }
}

// ── Token'lar ────────────────────────────────────────────────────────────────
//
// GERÇEK DEĞERLER BURAYA YAZILMAZ. Repo herkese açık; token'ı buraya gömmek
// onu da herkese açık yapar ve bot engellemenin anlamı kalmaz. Değerler üç
// yerde yaşar: sunucudaki bu dosyada, Unity'deki UploadConfig asset'inde
// (yalnızca yazma token'ları) ve Tools/Balance/pull.config.json içinde
// (yalnızca okuma token'ı). Son ikisi .gitignore kapsamında.

const WRITE_TOKEN_NATIVE = 'BUNU_DEGISTIR_NATIVE';   // APK ve PC build'leri
const WRITE_TOKEN_WEB    = 'BUNU_DEGISTIR_WEB';      // tarayıcı build'i
const READ_TOKEN         = 'BUNU_DEGISTIR_OKUMA';    // yalnızca pull.js

// Kayıt klasörü SCRIPT'İN YANINDA. İlk sürüm '/var/starfarer-logs' idi ve
// sunucunun Linux olduğunu varsayıyordu — oysa IIS/Windows/Plesk. Mutlak yol
// yazmak, sunucunun ne olduğunu bilmeyi gerektirir; __DIR__ her ikisinde de
// doğru yeri gösterir ve Plesk'te izin sorunu da çıkmaz (script kendi
// klasörüne zaten yazabilir).
//
// Bedeli: klasör web kökünün ALTINDA, yani dosyalar prensipte doğrudan
// indirilebilir. Bu yüzden klasöre erişimi kapatan bir web.config yazılıyor,
// bkz. korumayi_kur(). Dışarı taşımak daha temiz olurdu — o zaman buraya
// mutlak bir yol yaz (Windows'ta örn. 'C:/starfarer-logs') — ama Plesk'in
// open_basedir kısıtı httpdocs dışına yazmayı engelleyebilir; taşırsan
// ?ping=1 çıktısında "yazilir: evet" gördüğünü doğrula.
define('LOG_DIR', __DIR__ . '/logs');

// ── Kota ─────────────────────────────────────────────────────────────────────
//
// Yazma token'ı olan biri (yani paketi açan herkes) farklı d/f değerleriyle
// sınırsız sayıda dosya açabilir. Dosya başına sınır vardı, TOPLAMA sınır
// yoktu: 5 MB'lık isteği tekrarlamak diski doldurmaya yeterdi. Bu sayılar
// kötü niyet olmasa bile (döngüye giren bir istemci) sunucuyu ayakta tutar.
const MAX_BYTES = 5242880;     // dosya başına 5 MB — normal oturum ~100 KB
const MAX_FILES = 500;         // dizindeki dosya sayısı
const MAX_TOTAL = 209715200;   // toplam 200 MB

/**
 * Kayıt klasörüne DOĞRUDAN HTTP erişimini kapatan web.config'i yerine koyar.
 *
 * Klasör web kökünün altında olduğu için `/Starfarer/log/logs/xxx.jsonl`
 * adresi prensipte indirilebilir. Ölçtük, 404 döndü — ama IIS ayrıntılı hata
 * sayfasını yalnızca yerel isteklere gösterdiği için SEBEBİNİ ayırt edemedik:
 * "dosya yok" ile "IIS .jsonl uzantısını tanımıyor" dışarıdan aynı görünüyor.
 * İkincisi korunma değil kazadır; üst klasöre bir gün .jsonl eşlemesi ekleyen
 * bir web.config konsa sessizce açılırdı. Kural artık açıkça yazılı.
 *
 * Her istekte bir is_file çağrısı: klasör elle silinip yeniden açıldığında
 * koruma kendiliğinden geri gelsin.
 */
function korumayi_kur() {
    $path = LOG_DIR . '/web.config';
    if (!is_dir(LOG_DIR) || is_file($path)) return;

    $xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
         . "<!-- Bu dosyayi log.php yazdi. Kayitlara yalnizca log.php uzerinden,\n"
         . "     okuma token'i ile erisilir; klasorun kendisi disariya kapali. -->\n"
         . "<configuration>\n"
         . "  <system.webServer>\n"
         . "    <directoryBrowse enabled=\"false\" />\n"
         . "    <security>\n"
         . "      <requestFiltering>\n"
         . "        <fileExtensions>\n"
         . "          <add fileExtension=\".jsonl\" allowed=\"false\" />\n"
         . "        </fileExtensions>\n"
         . "      </requestFiltering>\n"
         . "    </security>\n"
         . "  </system.webServer>\n"
         . "</configuration>\n";

    @file_put_contents($path, $xml);
}

// ── Teşhis ucu (token GEREKMEZ) ──────────────────────────────────────────────
//
// Yalnızca "PHP bu dosyayı çalıştırabiliyor mu" sorusuna cevap verir.
//
// SÜRÜM BASMAZ. Eskiden PHP ve OS sürümünü token'sız duyuruyordu; kurulum
// sırasında faydalıydı ama kalıcı bir sızıntı: desteği bitmiş bir PHP'nin
// sürümünü ilan etmek, tarayan bota hangi açığı deneyeceğini söylemektir.
// Ayrıntı artık okuma token'ının arkasında.

if (isset($_GET['ping']) && !isset($_GET['t'])) {
    header('Content-Type: text/plain');
    exit("pong\n");
}

// ── Yetki ────────────────────────────────────────────────────────────────────
//
// Token'lar değiştirilmemişse HİÇBİR ŞEY kabul edilmez. Kurulmuş ama
// yapılandırılmamış bir kopya, herkesin bildiği bir parolayla açık duran bir
// yazma ucudur; sessizce çalışmasındansa gürültüyle durması yeğdir.
//
// Yer tutucunun TAMAMIYLA karşılaştırmak yerine ÖNEKİ aranıyor: tam
// karşılaştırma aynı metni iki yerde tutardı ve dosyayı otomatik dolduran bir
// araç ikisini birden değiştirip kontrolü sessizce her zaman doğru hâle
// getirirdi — koruma, koruduğu şeyle birlikte kaybolurdu. Dört harflik önek
// ise değiştirilen metnin parçası değil, o yüzden yerinde kalıyor.
//
// Uzunluk tabanlı bir kural (deploy.php'deki gibi ≥24) burada KULLANILMADI:
// hâlihazırda dağıtılmış APK'ların içindeki token 19 karakter ve o kurala
// takılırdı. Token'ı değiştirmek, testçilerin elindeki paketin sessizce veri
// göndermeyi bırakması demek olurdu — ölçümün sürekliliği, bu eşiğin
// getireceğinden değerli. Alt sınır yine de var ki boş/kısa bir değer
// geçmesin.
function token_gecerli($t) {
    return strlen($t) >= 16 && strpos($t, 'BUNU') !== 0;
}
// HANGİ token'ın geçersiz olduğu yazılır, "biri bozuk" değil. Değer
// basılmaz — yalnızca adı ve uzunluğu. Bu ayrıntı bir teşhis turunu
// tamamen ortadan kaldırıyor: eksik yapıştırılmış tek bir satırı
// aramak için dosyanın tamamını gözle taramak gerekmiyor.
$bozuk = array();
if (!token_gecerli(WRITE_TOKEN_NATIVE)) $bozuk[] = 'WRITE_TOKEN_NATIVE (' . strlen(WRITE_TOKEN_NATIVE) . ' karakter)';
if (!token_gecerli(WRITE_TOKEN_WEB))    $bozuk[] = 'WRITE_TOKEN_WEB ('    . strlen(WRITE_TOKEN_WEB)    . ' karakter)';
if (!token_gecerli(READ_TOKEN))         $bozuk[] = 'READ_TOKEN ('         . strlen(READ_TOKEN)         . ' karakter)';

if (count($bozuk) > 0) {
    http_response_code(500);
    exit("kurulmadi, gecersiz token: " . implode(', ', $bozuk)
       . "\nkural: en az 16 karakter ve 'BUNU' ile baslamamali\n");
}

$given = isset($_GET['t']) ? $_GET['t'] : '';

$yazabilir  = hash_equals(WRITE_TOKEN_NATIVE, $given)
           || hash_equals(WRITE_TOKEN_WEB,    $given);
$okuyabilir = hash_equals(READ_TOKEN, $given);

if (!$yazabilir && !$okuyabilir) {
    http_response_code(403);
    exit("nope\n");
}

// Klasörü kendisi kurmayı dener — en sık karşılaşılan kurulum hatası buydu.
if (!is_dir(LOG_DIR)) {
    @mkdir(LOG_DIR, 0775, true);
}
korumayi_kur();

// Okuma token'ıyla ayrıntılı durum: kurulumun neresi eksik, tek istekte
// görünür. Yazma token'ı buraya giremez — sunucunun iç durumunu istemciye
// anlatmanın bir sebebi yok.
if (isset($_GET['ping'])) {
    if (!$okuyabilir) { http_response_code(403); exit("nope\n"); }

    // glob() hata durumunda false döner; display_errors açık olduğu için
    // foreach uyarısı doğrudan cevabın içine basılırdı.
    $files = glob(LOG_DIR . '/*.jsonl');
    if (!is_array($files)) $files = array();
    $total = 0;
    foreach ($files as $p) $total += filesize($p);

    header('Content-Type: text/plain');
    echo "pong\n";
    echo "php:       " . PHP_VERSION . "\n";
    echo "log_dir:   " . LOG_DIR . "\n";
    echo "var mi:    " . (is_dir(LOG_DIR)      ? "evet" : "HAYIR") . "\n";
    echo "yazilir:   " . (is_writable(LOG_DIR) ? "evet" : "HAYIR") . "\n";
    echo "korumali:  " . (is_file(LOG_DIR . '/web.config') ? "evet" : "HAYIR") . "\n";
    // Windows'ta posix_* yok; ayrıca fonksiyon dönüşünü doğrudan dizi olarak
    // indekslemek (foo()['x']) PHP 5.4+ gerektirir ve bu sunucu daha eski
    // olabilir — PARSE hatası, function_exists koruması bile devreye giremez.
    echo "kullanici: " . get_current_user() . "\n";
    echo "os:        " . PHP_OS . "\n";
    echo "kayit:     " . count($files) . " / " . MAX_FILES . " dosya\n";
    echo "boyut:     " . round($total / 1048576, 1) . " / " . round(MAX_TOTAL / 1048576) . " MB\n";
    exit;
}

if (!is_dir(LOG_DIR)) {
    http_response_code(500);
    exit("log dizini yok ve olusturulamadi: " . LOG_DIR . "\n");
}
if (!is_writable(LOG_DIR)) {
    http_response_code(500);
    exit("log dizinine yazilamiyor: " . LOG_DIR . " (chown www-data)\n");
}

// ── Okuma uçları ─────────────────────────────────────────────────────────────
//
// Yalnızca okuma token'ı. Paketten yazma token'ı çıkaran biri başkalarının
// kayıtlarını ne listeleyebilir ne indirebilir.

if (isset($_GET['list'])) {
    if (!$okuyabilir) { http_response_code(403); exit("nope\n"); }

    $out = array();
    foreach (glob(LOG_DIR . '/*.jsonl') as $p) {
        $out[] = array(
            'file'  => basename($p),
            'bytes' => filesize($p),
            'mtime' => date('c', filemtime($p)),
        );
    }
    // En yeni önce — indirici hangilerinin yeni olduğunu böyle görür.
    // Ok fonksiyonu (fn) DEĞİL: PHP 7.4 öncesinde parse hatası verir.
    usort($out, function ($a, $b) { return strcmp($b['mtime'], $a['mtime']); });

    header('Content-Type: application/json');
    exit(json_encode($out));
}

if (isset($_GET['get'])) {
    if (!$okuyabilir) { http_response_code(403); exit("nope\n"); }

    // basename(): ".." ile dizin dışına çıkma girişimini keser
    $path = LOG_DIR . '/' . basename($_GET['get']);
    if (!is_file($path)) { http_response_code(404); exit("yok\n"); }
    header('Content-Type: application/x-ndjson');
    header('X-Content-Type-Options: nosniff');
    readfile($path);
    exit;
}

// ── Yazma ucu ────────────────────────────────────────────────────────────────

if (!$yazabilir) { http_response_code(403); exit("nope\n"); }

/**
 * Hedef dosya yolu. İstemciden gelen ad temizlenir: süzgeç dizin ayıracını da
 * eler ('/' ve '\' listede yok), yani ".." ile klasörün dışına çıkmak mümkün
 * değil; uzantı .jsonl'a zorlandığı için çalıştırılabilir bir dosya yazdırmak
 * da mümkün değil. Cihaz kimliği önek olur, böylece iki telefonun aynı saniyede
 * başlattığı oturum birbirini ezmez.
 */
function hedef_yol() {
    $device = preg_replace('/[^A-Za-z0-9_-]/', '', isset($_GET['d']) ? $_GET['d'] : 'anon');
    $name   = preg_replace('/[^A-Za-z0-9._-]/', '', isset($_GET['f']) ? $_GET['f'] : 'session.jsonl');

    // str_ends_with DEĞİL: PHP 8.0 öncesinde tanımsız fonksiyon = ölümcül hata.
    if ($name === '' || substr($name, -6) !== '.jsonl') $name = 'session.jsonl';
    if ($device === '') $device = 'anon';

    return LOG_DIR . '/' . substr($device, 0, 16) . '-' . $name;
}

// ── Boyut sorgusu ────────────────────────────────────────────────────────────
//
// İstemci nereden devam edeceğini buradan öğrenir. Ofseti YERELDE tutmak daha
// ucuz olurdu ama tam da kaybolduğundan şüphelendiğimiz depolamaya yazmak
// demekti (tarayıcıda IndexedDB); sunucuya sormak durumsuzdur ve istemcinin
// deposu silinse bile doğru cevabı verir.
//
// Yazma token'ı yeter: istemci zaten kendi dosyasına yazıyor, boyutunu
// öğrenmesi yeni bir yetki değil.
if (isset($_GET['size'])) {
    $p = hedef_yol();
    header('Content-Type: text/plain');
    exit(is_file($p) ? (string)filesize($p) . "\n" : "0\n");
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405);
    exit("POST bekleniyor\n");
}

$body = file_get_contents('php://input', false, null, 0, MAX_BYTES + 1);
if ($body === false || strlen($body) === 0) {
    http_response_code(400);
    exit("bos govde\n");
}
if (strlen($body) > MAX_BYTES) {
    http_response_code(413);
    exit("cok buyuk\n");
}

$path   = hedef_yol();
$vardi  = is_file($path);
$mevcut = $vardi ? filesize($path) : 0;

// EKLEME Mİ, ÜZERİNE YAZMA MI: `o` parametresinin VARLIĞI belirler.
//
// `o` VARSA — ekleme. Gelen ofset dosyanın mevcut boyutuna eşit olmalı, değilse
// reddedilir. Bu kural sayesinde bayt YOK EDİLEMEZ, yalnızca eklenir: bir kaydı
// kısaltmak ya da sahtesiyle değiştirmek yapısal olarak imkânsız hâle gelir.
// Eskiden bu işi "küçülme yasağı" görüyordu, ama o yalnızca üzerine yazmanın
// açtığı deliği yamalayan bir kuraldı; ofset denetimi deliği kapatıyor.
//
// `o` YOKSA — eski davranış: dosyanın tamamı gelir, üzerine yazılır.
// KALDIRILAMAZ, çünkü testçilerin elindeki dağıtılmış APK'lar tam olarak böyle
// gönderiyor. Kaldırsaydık o paketler ilk gönderimden sonra 409 alırdı ve
// istemci 4xx'i yapılandırma hatası sayıp gönderimi o oturum için tamamen
// kapatırdı — sahadaki veri sessizce kesilirdi. Bu yolda küçülme yasağı duruyor.
$ekleme = isset($_GET['o']);
$ofset  = $ekleme ? (int)$_GET['o'] : 0;

if ($ekleme) {
    if ($ofset < 0 || $ofset !== $mevcut) {
        http_response_code(409);
        exit("ofset uyusmuyor: dosyada " . $mevcut . ", gelen " . $ofset . "\n");
    }
    if ($ofset + strlen($body) > MAX_BYTES) {
        http_response_code(413);
        exit("dosya siniri asildi\n");
    }
} else if ($vardi && strlen($body) < $mevcut) {
    http_response_code(409);
    exit("kucuk govde: mevcut kayit " . $mevcut . " bayt\n");
}

// İlk satır JSON olmalı — yanlışlıkla gelen HTML/çöp diske yazılmasın.
// Yalnızca dosyanın BAŞINDA anlamlı: ekleme parçaları kaydın ortasından devam
// eder ve bir satırın tam başına denk gelmek zorunda değildir.
if ($ofset === 0) {
    $nl    = strpos($body, "\n");
    $first = ($nl === false) ? $body : substr($body, 0, $nl);
    if (json_decode($first) === null) {
        http_response_code(400);
        exit("JSONL degil\n");
    }
}

// Kota YALNIZCA yeni dosyada işler: süren bir oturumun büyümesini kesmek,
// elimizde yarım bir kayıt bırakır — oysa amaç kaydı korumak. Yeni dosya
// açmayı durdurmak ise zararsız.
if (!$vardi) {
    $files = glob(LOG_DIR . '/*.jsonl');
    if (!is_array($files)) $files = array();
    if (count($files) >= MAX_FILES) {
        http_response_code(507);
        exit("kota: " . MAX_FILES . " dosya siniri dolu\n");
    }
    $total = 0;
    foreach ($files as $p) $total += filesize($p);
    if ($total + strlen($body) > MAX_TOTAL) {
        http_response_code(507);
        exit("kota: toplam boyut siniri dolu\n");
    }
}

// Ekleme yolunda FILE_APPEND, eski yolda üzerine yazma. Ofset denetimi
// yukarıda yapıldı, yani buraya gelen bir ekleme dosyanın tam sonuna denk
// geliyor demektir.
$bayrak = $ekleme ? (FILE_APPEND | LOCK_EX) : LOCK_EX;
if (file_put_contents($path, $body, $bayrak) === false) {
    http_response_code(500);
    exit("yazilamadi\n");
}

// Yeni boyut döndürülür: istemci bir sonraki parçayı nereden göndereceğini
// ayrı bir istek atmadan öğrenir. Ofsetin tek doğruluk kaynağı sunucudur.
clearstatcache(false, $path);
http_response_code(200);
echo "ok " . filesize($path) . "\n";
