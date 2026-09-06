// Sunucudaki denge kayıtlarını yerele indirir.
//
//   node Tools/Balance/pull.js
//
// Yapılandırma: Tools/Balance/pull.config.json (git'e girmez, token içerir)
//   { "endpoint": "https://akinayan.de/starfarer/log/log.php", "readToken": "..." }
//
// readToken, log.php'deki READ_TOKEN'dır ve YALNIZCA burada yaşar — hiçbir
// build'in içine girmez. Build'lerdeki yazma token'larıyla liste ve indirme
// uçlarına erişilemez; sebebi log.php'nin başında yazılı. Eski tek-token
// düzeninden gelen "token" alanı hâlâ okunur ama uyarı basar.
//
// Neden ayrı bir indirici var: Claude'un çalıştığı ortamda dışarı HTTP kapalı
// (DNS çözülüyor ama bağlantı kurulmuyor). Kayıtlar diskte olursa okunabiliyor,
// yani indirme adımı senin terminalinde bir kez çalışıyor, analiz bende.
//
// Yalnızca YENİ veya BOYUTU DEĞİŞMİŞ dosyalar indirilir: aynı oturum oyun
// devam ettikçe büyür ve her seferinde tamamı yeniden gönderilir.

const fs    = require("fs");
const path  = require("path");
const https = require("https");
const http  = require("http");

const here    = __dirname;
const cfgPath = path.join(here, "pull.config.json");
const outDir  = path.join(here, "logs");

if (!fs.existsSync(cfgPath)) {
  console.error(`Yapılandırma yok: ${cfgPath}`);
  console.error(`Örnek:\n{\n  "endpoint": "https://akinayan.de/starfarer/log/log.php",\n  "readToken": "..."\n}`);
  process.exit(1);
}
const cfg = JSON.parse(fs.readFileSync(cfgPath, "utf8"));

// Okuma token'ı ayrıldıktan sonra eski yapılandırmalar sessizce 403 alırdı;
// "nope" dönen bir sunucu ile yanlış alan adı taşıyan bir dosyayı ayırt etmek
// zordur, o yüzden burada söyleniyor.
const readToken = cfg.readToken || cfg.token;
if (!readToken) {
  console.error(`Yapılandırmada readToken yok: ${cfgPath}`);
  process.exit(1);
}
if (!cfg.readToken) {
  console.warn("uyarı: pull.config.json'daki 'token' alanı 'readToken' olarak yeniden adlandırılmalı " +
               "— değeri de log.php'deki READ_TOKEN olmalı, yazma token'ı buraya girmez.");
}
fs.mkdirSync(outDir, { recursive: true });

function get(url) {
  return new Promise((resolve, reject) => {
    const lib = url.startsWith("https") ? https : http;
    lib.get(url, res => {
      if (res.statusCode !== 200) {
        res.resume();
        return reject(new Error(`HTTP ${res.statusCode} — ${url.replace(/t=[^&]*/, "t=***")}`));
      }
      const chunks = [];
      res.on("data", c => chunks.push(c));
      res.on("end", () => resolve(Buffer.concat(chunks)));
    }).on("error", reject);
  });
}

const base = `${cfg.endpoint}?t=${encodeURIComponent(readToken)}`;

(async () => {
  const list = JSON.parse((await get(`${base}&list=1`)).toString());
  console.log(`sunucuda ${list.length} kayıt`);

  let indirilen = 0;
  for (const f of list) {
    const local = path.join(outDir, f.file);
    // Boyut aynıysa dokunma: oturum sürerken dosya büyür, bittiğinde sabitlenir
    if (fs.existsSync(local) && fs.statSync(local).size === f.bytes) continue;

    const data = await get(`${base}&get=${encodeURIComponent(f.file)}`);
    fs.writeFileSync(local, data);
    console.log(`  ↓ ${f.file}  ${(f.bytes / 1024).toFixed(0)} KB`);
    indirilen++;
  }

  console.log(indirilen ? `${indirilen} dosya indirildi → Tools/Balance/logs/`
                        : "yeni kayıt yok");
})().catch(e => { console.error("hata:", e.message); process.exit(1); });
