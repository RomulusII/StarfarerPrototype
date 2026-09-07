// Web build'ini sunucuya yükler ve yayına alır.
//
//   node Tools/Deploy/upload.js                 → Builds/Web klasörünü dağıtır
//   node Tools/Deploy/upload.js --no-swap       → yükler ama yayına ALMAZ
//   node Tools/Deploy/upload.js --dir Builds/X  → başka bir klasörü dağıtır
//
// Yapılandırma: Tools/Deploy/deploy.config.json (git'e girmez, TOKEN içerir)
//   { "deployEndpoint": "https://akinayan.de/starfarer/deploy.php",
//     "deployToken": "...", "publicUrl": "https://akinayan.de/starfarer/game/" }
//
// NEDEN FTP DEĞİL: kurumsal ağ FTP'yi proxy'liyor ve şifrelemeden önce giriş
// bilgisi istiyor (Zscaler; ayrıntı deploy.php başlığında). Bu yol 443'ten
// geçer ve parola değil, yalnızca bu dizine yazabilen bir token taşır.
//
// AKIŞ: ping (sınırları öğren) → reset (game-next temizle) → dosyalar →
// swap (tek rename ile yayına al). Yükleme sürerken kimse yarım build'e
// düşmez; takas anına kadar yayındaki sürüm eskisidir.

const fs    = require("fs");
const path  = require("path");
const https = require("https");
const http  = require("http");

const here    = __dirname;
const proje   = path.resolve(here, "..", "..");

// --config: varsayilan disinda bir yapilandirma. Sinama icin var — gercek
// token'lara dokunmadan sahte bir sunucuya karsi kosturulabilsin.
const cfgArg  = process.argv.indexOf("--config");
const cfgPath = cfgArg >= 0 ? path.resolve(process.argv[cfgArg + 1])
                            : path.join(here, "deploy.config.json");

if (!fs.existsSync(cfgPath)) {
  console.error(`Yapılandırma yok: ${cfgPath}`);
  console.error(`Örnek:\n{\n  "deployEndpoint": "https://akinayan.de/starfarer/deploy.php",\n` +
                `  "deployToken": "...",\n  "publicUrl": "https://akinayan.de/starfarer/game/"\n}`);
  process.exit(1);
}
const cfg = JSON.parse(fs.readFileSync(cfgPath, "utf8"));
for (const alan of ["deployEndpoint", "deployToken"]) {
  if (!cfg[alan]) { console.error(`Yapılandırmada ${alan} yok: ${cfgPath}`); process.exit(1); }
}

const args   = process.argv.slice(2);
const swapVar = !args.includes("--no-swap");
const dirArg = args.indexOf("--dir");
const kaynak = path.resolve(proje, dirArg >= 0 ? args[dirArg + 1] : "Builds/Web");

const base = `${cfg.deployEndpoint}?t=${encodeURIComponent(cfg.deployToken)}`;

/** Tek istek. Gövde varsa POST, yoksa GET. */
function istek(url, body) {
  return new Promise((resolve, reject) => {
    const u   = new URL(url);
    const lib = u.protocol === "https:" ? https : http;
    const req = lib.request(u, {
      method: body ? "POST" : "GET",
      headers: body
        ? { "Content-Type": "application/octet-stream", "Content-Length": body.length }
        : {},
      timeout: 120000,
    }, res => {
      const parcalar = [];
      res.on("data", c => parcalar.push(c));
      res.on("end", () => {
        const metin = Buffer.concat(parcalar).toString();
        if (res.statusCode !== 200) {
          // Token URL'de: hata mesajında maskele, terminal kaydına düşmesin.
          return reject(new Error(`HTTP ${res.statusCode} — ${metin.trim()} ` +
                                  `(${url.replace(/t=[^&]*/, "t=***")})`));
        }
        resolve(metin);
      });
    });
    req.on("timeout", () => req.destroy(new Error("zaman aşımı")));
    req.on("error", reject);
    if (body) req.write(body);
    req.end();
  });
}

const uyu = ms => new Promise(r => setTimeout(r, ms));

/**
 * İsteği yeniden dener. Kurumsal ağ dalgalı: aynı dosya bir gün 20 saniyede
 * giderken ertesi gün 1 MB'lık bir parça 60 saniyede tamamlanmayıp bağlantıyı
 * kopardı (ölçüldü: küçük bir ping 6 sn). Tek bir aksaklıkta 49 MB'lık
 * dağıtımı baştan almak gereksiz.
 *
 * YALNIZCA AĞ hataları tekrarlanır. Sunucunun kural gereği verdiği cevaplar
 * (403 token, 400 yol, 409 ofset) tekrarlansaydı yanlış bir isteği ısrarla
 * yollardık; onlar ilk seferde yukarı fırlatılır.
 */
async function istekTekrarli(url, body, etiket) {
  const bekle = [2000, 5000, 12000];
  for (let i = 0; ; i++) {
    try {
      return await istek(url, body);
    } catch (e) {
      const agHatasi = /zaman aşımı|ECONNRESET|ECONNREFUSED|ETIMEDOUT|EPIPE|socket hang up/i.test(e.message);
      if (!agHatasi || i >= bekle.length) throw e;
      console.log(`    ↻ ${etiket}: ${e.message} — ${bekle[i] / 1000} sn sonra tekrar (${i + 1}/${bekle.length})`);
      await uyu(bekle[i]);
    }
  }
}

/** "8M", "128K", "-1" → bayt. Sınırsızsa Infinity. */
function boyut(s) {
  if (!s) return Infinity;
  const m = String(s).trim().match(/^(-?\d+)\s*([KMG])?$/i);
  if (!m) return Infinity;
  const n = parseInt(m[1], 10);
  if (n < 0) return Infinity;
  const kat = { K: 1024, M: 1048576, G: 1073741824 }[(m[2] || "").toUpperCase()] || 1;
  return n * kat;
}

function dosyalar(dir, kok = dir, acc = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) dosyalar(p, kok, acc);
    else acc.push(path.relative(kok, p).split(path.sep).join("/"));
  }
  return acc;
}

async function main() {
  if (!fs.existsSync(kaynak)) {
    console.error(`Klasör yok: ${kaynak}\nÖnce build al: Tools\\Build\\deploy-web.cmd`);
    process.exit(1);
  }
  const liste = dosyalar(kaynak);
  if (!liste.includes("index.html")) {
    console.error(`${kaynak} içinde index.html yok — bu bir web build'i değil.`);
    process.exit(1);
  }

  const durum = JSON.parse(await istekTekrarli(`${base}&ping=1`, undefined, "ping"));
  // Parça boyutu sunucunun sınırına göre seçilir. post_max_size'ı bilmeden
  // yüklemek, "boş gövde" diye görünen sessiz bir kesilme üretiyordu.
  let parcaBoyu = Math.max(65536, Math.min(
    durum.max_chunk,
    Math.floor(boyut(durum.post_max_size) * 0.8)
  ));

  const toplam = liste.reduce((t, f) => t + fs.statSync(path.join(kaynak, f)).size, 0);
  console.log(`sunucu: PHP ${durum.php} · post_max_size ${durum.post_max_size} · ` +
              `bos alan ${durum.bos_alan_mb} MB`);
  console.log(`yuklenecek: ${liste.length} dosya, ${(toplam / 1048576).toFixed(1)} MB, ` +
              `parca ${(parcaBoyu / 1048576).toFixed(1)} MB`);

  await istekTekrarli(`${base}&reset=1`, Buffer.alloc(0), "reset");

  const basladi = Date.now();
  for (const rel of liste) {
    const tam = path.join(kaynak, rel);
    const boy = fs.statSync(tam).size;
    const fd  = fs.openSync(tam, "r");
    try {
      let ofset = 0;
      do {
        const uzunluk = Math.min(parcaBoyu, boy - ofset);
        const tampon  = Buffer.alloc(uzunluk);
        fs.readSync(fd, tampon, 0, uzunluk, ofset);
        try {
          await istekTekrarli(`${base}&p=${encodeURIComponent(rel)}&o=${ofset}`, tampon, rel);
        } catch (e) {
          // Tekrarlar da tükendiyse parçayı KÜÇÜLT ve devam et. Yavaş bir
          // hatta 6 MB'lık bir parça zaman aşımına uğrarken 1 MB geçebiliyor;
          // dağıtımı tamamen bırakmadan önce denenecek şey bu.
          if (parcaBoyu <= 262144) throw e;
          parcaBoyu = Math.max(262144, Math.floor(parcaBoyu / 4));
          console.log(`    ↓ parça küçültüldü: ${(parcaBoyu / 1024).toFixed(0)} KB — kaldığı yerden devam`);
          continue;   // aynı ofsetten, daha küçük parçayla
        }
        ofset += uzunluk;
      } while (ofset < boy);   // boş dosya da bir kez gönderilsin
    } finally {
      fs.closeSync(fd);
    }
    console.log(`  ↑ ${rel}  ${(boy / 1024).toFixed(0)} KB`);
  }

  const saniye = ((Date.now() - basladi) / 1000).toFixed(1);

  if (!swapVar) {
    console.log(`\n${liste.length} dosya yuklendi (${saniye} sn) — takas YAPILMADI (--no-swap).`);
    console.log(`Yayina almak icin: node Tools/Deploy/upload.js --swap-only`);
    return;
  }

  const sonuc = (await istekTekrarli(`${base}&swap=1`, Buffer.alloc(0), "swap")).trim();
  console.log(`\n${liste.length} dosya, ${(toplam / 1048576).toFixed(1)} MB, ${saniye} sn — ${sonuc}`);
  if (cfg.publicUrl) console.log(`yayinda: ${cfg.publicUrl}`);
}

// Yalnızca takas: --no-swap ile yüklenmiş bir build'i sonradan yayına alır.
async function sadeceTakas() {
  const sonuc = (await istekTekrarli(`${base}&swap=1`, Buffer.alloc(0), "swap")).trim();
  console.log(sonuc);
  if (cfg.publicUrl) console.log(`yayinda: ${cfg.publicUrl}`);
}

(args.includes("--swap-only") ? sadeceTakas() : main())
  .catch(e => { console.error("hata:", e.message); process.exit(1); });
