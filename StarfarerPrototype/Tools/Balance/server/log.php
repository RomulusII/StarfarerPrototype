<?php
/**
 * Starfarer denge kaydı toplayıcı — tek dosya, bağımlılık yok.
 *
 * KURULUM
 *   1. Bu dosyayı web köküne kopyala:  /var/www/akinayan.de/starfarer/log.php
 *   2. Kayıt klasörünü oluştur ve Apache'ye yazma izni ver:
 *        sudo mkdir -p /var/starfarer-logs
 *        sudo chown www-data:www-data /var/starfarer-logs
 *   3. Aşağıdaki TOKEN'ı değiştir (aynısını Unity'deki BalanceUploader'a yaz).
 *
 * UÇLAR
 *   POST /starfarer/log.php?t=TOKEN&d=CIHAZ&f=DOSYA   gövde = JSONL
 *   GET  /starfarer/log.php?t=TOKEN&list=1            → JSON dosya listesi
 *   GET  /starfarer/log.php?t=TOKEN&get=DOSYA         → dosyanın kendisi
 *
 * TOKEN gizlilik için değil: içerik oyun olayları, kimseyi ilgilendirmez.
 * Açık bir POST ucu ise birkaç gün içinde tarayıcı botlarınca bulunur ve
 * diski doldurur. Üç satır, o sorunu kökten keser.
 */

const TOKEN     = 'BUNU_DEGISTIR';
const LOG_DIR   = '/var/starfarer-logs';
const MAX_BYTES = 5 * 1024 * 1024;   // 5 MB — normal oturum ~100 KB

// ── Yetki ────────────────────────────────────────────────────────────────────

if (!hash_equals(TOKEN, $_GET['t'] ?? '')) {
    http_response_code(403);
    exit("nope\n");
}

if (!is_dir(LOG_DIR)) {
    http_response_code(500);
    exit("log dizini yok: " . LOG_DIR . "\n");
}

// ── Okuma uçları ─────────────────────────────────────────────────────────────

if (isset($_GET['list'])) {
    $out = [];
    foreach (glob(LOG_DIR . '/*.jsonl') as $p) {
        $out[] = [
            'file'  => basename($p),
            'bytes' => filesize($p),
            'mtime' => date('c', filemtime($p)),
        ];
    }
    // En yeni önce — indirici hangilerinin yeni olduğunu böyle görür
    usort($out, fn($a, $b) => strcmp($b['mtime'], $a['mtime']));
    header('Content-Type: application/json');
    exit(json_encode($out, JSON_PRETTY_PRINT));
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
    exit("boş gövde\n");
}
if (strlen($body) > MAX_BYTES) {
    http_response_code(413);
    exit("çok büyük\n");
}

// İlk satır JSON olmalı — yanlışlıkla gelen HTML/çöp diske yazılmasın
$first = strtok($body, "\n");
if ($first === false || json_decode($first) === null) {
    http_response_code(400);
    exit("JSONL değil\n");
}

// Dosya adı İSTEMCİDEN gelir ama temizlenir: cihaz kimliği önek olarak eklenir,
// böylece iki telefonun aynı saniyede başlattığı oturum birbirini ezmez.
$device = preg_replace('/[^A-Za-z0-9_-]/', '', $_GET['d'] ?? 'anon');
$name   = preg_replace('/[^A-Za-z0-9._-]/', '', $_GET['f'] ?? 'session.jsonl');
if ($name === '' || !str_ends_with($name, '.jsonl')) $name = 'session.jsonl';

$path = LOG_DIR . '/' . substr($device, 0, 16) . '-' . $name;

// Üzerine yaz, ekleme YAPMA: istemci her seferinde dosyanın tamamını gönderiyor.
// Ekleme yapsaydık her gönderimde kayıt katlanarak büyürdü.
if (file_put_contents($path, $body, LOCK_EX) === false) {
    http_response_code(500);
    exit("yazılamadı\n");
}

http_response_code(200);
echo "ok " . strlen($body) . "\n";
