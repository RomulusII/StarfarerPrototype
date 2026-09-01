<?php
/**
 * Starfarer denge kaydı toplayıcı — tek dosya, bağımlılık yok.
 *
 * KURULUM
 *   1. Bu dosyayı web köküne kopyala (Apache/IIS fark etmez), örn:
 *        .../httpdocs/starfarer/log/log.php
 *   2. Aşağıdaki TOKEN'ı değiştir (aynısını Unity'deki UploadConfig'e yaz).
 *   3. Kayıtlar script'in yanındaki `logs/` klasörüne yazılır; script onu
 *      kendisi oluşturur. Oluşturamazsa elle aç ve yazma izni ver.
 *
 * UÇLAR
 *   GET  ...?ping=1                          → "pong" (token gerekmez, teşhis için)
 *   GET  ...?t=TOKEN&ping=1                  → ayrıntılı durum (PHP sürümü, dizin)
 *   POST ...?t=TOKEN&d=CIHAZ&f=DOSYA         gövde = JSONL
 *   GET  ...?t=TOKEN&list=1                  → JSON dosya listesi
 *   GET  ...?t=TOKEN&get=DOSYA               → dosyanın kendisi
 *
 * TOKEN gizlilik için değil: içerik oyun olayları. Açık bir POST ucu ise
 * birkaç gün içinde tarayıcı botlarınca bulunur ve diski doldurur.
 *
 * SÜRÜM NOTU: bu dosya PHP 7.0 ile de çalışır. İlk sürümde `fn() =>` (7.4+) ve
 * `str_ends_with` (8.0+) kullanılmıştı; eski bir PHP'de dosya PARSE EDİLEMİYOR,
 * yani token kontrolüne bile gelmeden 500 dönüyordu. Bir teşhis ucunun
 * token'sız çalışması tam da bu yüzden gerekli: "500 mü, 403 mü" sorusu
 * "kod mu bozuk, ayar mı yanlış" sorusunun cevabıdır.
 */

// Hata ayıklama: 500 hataları tarayıcıda boş sayfa olarak görünür. Bu satır
// asıl mesajı ekrana basar. PARSE hatasında işe yaramaz (kod hiç çalışmaz);
// o durumda sunucunun kendi hata günlüğüne bakmak gerekir.
ini_set('display_errors', '1');
error_reporting(E_ALL);

// ── Eski PHP yedekleri ───────────────────────────────────────────────────────
//
// Sunucudaki PHP 5.6'dan eski çıktı (hash_equals tanımsız). Sürümü baştan
// bilmek yerine üç turda deneyerek öğrendik; bu yüzden teşhis ucu artık sürümü
// TOKEN'SIZ da basıyor.
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

// Bot engeli, sır değil: APK'nın içinde de duracak ve istemci tarafı bir
// anahtar gizli tutulamaz. Tek işi, açık bir POST ucunun tarayıcı botlarınca
// bulunup diski doldurmasını engellemek.
//
// GERÇEK DEĞER BURAYA YAZILMAZ. Repo herkese açık; token'ı buraya gömmek onu
// da herkese açık yapar ve bot engellemenin anlamı kalmaz. Gerçek değer üç
// yerde yaşar: sunucudaki dosyada, Unity'deki UploadConfig asset'inde ve
// Tools/Balance/pull.config.json içinde (ikincisi .gitignore'da).
const TOKEN = 'BUNU_DEGISTIR';

// Kayıt klasörü SCRIPT'İN YANINDA. İlk sürüm '/var/starfarer-logs' idi ve
// sunucunun Linux olduğunu varsayıyordu — oysa IIS/Windows/Plesk. Mutlak yol
// yazmak, sunucunun ne olduğunu bilmeyi gerektirir; __DIR__ her ikisinde de
// doğru yeri gösterir ve Plesk'te izin sorunu da çıkmaz (script kendi
// klasörüne zaten yazabilir).
//
// Bedeli: klasör web kökünün altında, yani dosyalar doğrudan indirilebilir.
// İçerik oyun olayları olduğu için kabul edildi. Dışarı taşımak istersen
// buraya mutlak bir yol yaz (Windows'ta örn. 'C:/starfarer-logs').
define('LOG_DIR', __DIR__ . '/logs');

const MAX_BYTES = 5242880;   // 5 MB — normal oturum ~100 KB

// ── Teşhis ucu (token GEREKMEZ) ──────────────────────────────────────────────
// Yalnızca "PHP bu dosyayı çalıştırabiliyor mu" sorusuna cevap verir.

if (isset($_GET['ping']) && !isset($_GET['t'])) {
    header('Content-Type: text/plain');
    // Sürüm burada da basılır: hangi PHP özelliklerinin kullanılabileceğini
    // öğrenmek için token'ın doğru olmasını beklemek gereksiz bir tur demekti.
    // Sunucu zaten IIS ve ASP.NET sürümünü kendi başlıklarında duyuruyor.
    exit("pong\nphp: " . PHP_VERSION . "\nos:  " . PHP_OS . "\n");
}

// ── Yetki ────────────────────────────────────────────────────────────────────

$given = isset($_GET['t']) ? $_GET['t'] : '';
if (!hash_equals(TOKEN, $given)) {
    http_response_code(403);
    exit("nope\n");
}

// Klasörü kendisi kurmayı dener — en sık karşılaşılan kurulum hatası buydu.
if (!is_dir(LOG_DIR)) {
    @mkdir(LOG_DIR, 0775, true);
}

// Token doğruysa ayrıntılı durum: kurulumun neresi eksik, tek istekte görünür.
if (isset($_GET['ping'])) {
    header('Content-Type: text/plain');
    echo "pong\n";
    echo "php:       " . PHP_VERSION . "\n";
    echo "log_dir:   " . LOG_DIR . "\n";
    echo "var mi:    " . (is_dir(LOG_DIR)      ? "evet" : "HAYIR") . "\n";
    echo "yazilir:   " . (is_writable(LOG_DIR) ? "evet" : "HAYIR") . "\n";
    // Windows'ta posix_* yok; ayrıca fonksiyon dönüşünü doğrudan dizi olarak
    // indekslemek (foo()['x']) PHP 5.4+ gerektirir ve bu sunucu daha eski
    // olabilir — PARSE hatası, function_exists koruması bile devreye giremez.
    echo "kullanici: " . get_current_user() . "\n";
    echo "os:        " . PHP_OS . "\n";
    echo "kayit:     " . count(glob(LOG_DIR . '/*.jsonl')) . " dosya\n";
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

if (isset($_GET['list'])) {
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
    // basename(): ".." ile dizin dışına çıkma girişimini keser
    $path = LOG_DIR . '/' . basename($_GET['get']);
    if (!is_file($path)) { http_response_code(404); exit("yok\n"); }
    header('Content-Type: application/x-ndjson');
    readfile($path);
    exit;
}

// ── Yazma ucu ────────────────────────────────────────────────────────────────

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

// İlk satır JSON olmalı — yanlışlıkla gelen HTML/çöp diske yazılmasın
$nl    = strpos($body, "\n");
$first = ($nl === false) ? $body : substr($body, 0, $nl);
if (json_decode($first) === null) {
    http_response_code(400);
    exit("JSONL degil\n");
}

// Dosya adı İSTEMCİDEN gelir ama temizlenir: cihaz kimliği önek olarak eklenir,
// böylece iki telefonun aynı saniyede başlattığı oturum birbirini ezmez.
$device = preg_replace('/[^A-Za-z0-9_-]/', '', isset($_GET['d']) ? $_GET['d'] : 'anon');
$name   = preg_replace('/[^A-Za-z0-9._-]/', '', isset($_GET['f']) ? $_GET['f'] : 'session.jsonl');

// str_ends_with DEĞİL: PHP 8.0 öncesinde tanımsız fonksiyon = ölümcül hata.
if ($name === '' || substr($name, -6) !== '.jsonl') $name = 'session.jsonl';
if ($device === '') $device = 'anon';

$path = LOG_DIR . '/' . substr($device, 0, 16) . '-' . $name;

// Üzerine yaz, ekleme YAPMA: istemci her seferinde dosyanın tamamını gönderiyor.
// Ekleme yapsaydık her gönderimde kayıt katlanarak büyürdü.
if (file_put_contents($path, $body, LOCK_EX) === false) {
    http_response_code(500);
    exit("yazilamadi\n");
}

http_response_code(200);
echo "ok " . strlen($body) . "\n";
