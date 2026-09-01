// Sunucudaki denge kayıtlarını yerele indirir.
//
//   node Tools/Balance/pull.js
//
// Yapılandırma: Tools/Balance/pull.config.json (git'e girmez, token içerir)
//   { "endpoint": "https://akinayan.de/starfarer/log.php", "token": "..." }
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
  console.error(`Örnek:\n{\n  "endpoint": "https://akinayan.de/starfarer/log/log.php",\n  "token": "..."\n}`);
  process.exit(1);
}
const cfg = JSON.parse(fs.readFileSync(cfgPath, "utf8"));
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

const base = `${cfg.endpoint}?t=${encodeURIComponent(cfg.token)}`;

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
