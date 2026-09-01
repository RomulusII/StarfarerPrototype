// Denge kaydı analizi — BalanceLog'un ürettiği JSONL dosyasını okur.
//
//   node Tools/Balance/analyze.js <dosya.jsonl>
//   node Tools/Balance/analyze.js            (en yeni kaydı otomatik bulur)
//
// Dış bağımlılık yok (SkinGen ile aynı desen). Ham olaydan özet türetir;
// hangi özeti isteyeceğimizi önceden bilmediğimiz için kayıt ham tutuluyor.

const fs   = require("fs");
const path = require("path");
const os   = require("os");

// ── Dosyayı bul ─────────────────────────────────────────────────────────────

// İki kaynak var ve ikisine de bakılmalı:
//   1. Kendi editör oturumların — Unity persistentDataPath
//   2. pull.js ile sunucudan indirilenler (arkadaşların telefonları)
// En YENİ dosya hangisiyse o seçilir; hangi klasörden geldiği önemli değil.
function logDirs() {
  return [
    path.join(__dirname, "logs"),
    path.join(os.homedir(), "AppData", "LocalLow",
              "DefaultCompany", "StarfarerPrototype", "balance"),
  ];
}

function latestLog() {
  let best = null;
  for (const dir of logDirs()) {
    if (!fs.existsSync(dir)) continue;
    for (const f of fs.readdirSync(dir).filter(x => x.endsWith(".jsonl"))) {
      const p = path.join(dir, f);
      const t = fs.statSync(p).mtimeMs;
      if (!best || t > best.t) best = { p, t };
    }
  }
  return best ? best.p : null;
}

const file = process.argv[2] || latestLog();
if (!file || !fs.existsSync(file)) {
  console.error("Kayıt bulunamadı. Kullanım: node Tools/Balance/analyze.js <dosya.jsonl>");
  process.exit(1);
}

// Son satır yarım kalmış olabilir (Play aniden durdurulursa) — atla, sayısını bildir.
const lines = fs.readFileSync(file, "utf8").trim().split("\n");
let dropped = 0;
const R = lines.map(l => { try { return JSON.parse(l); } catch (e) { dropped++; return null; } })
               .filter(Boolean);

const by  = t => R.filter(r => r.ev === t);
const sum = (a, f) => a.reduce((x, y) => x + (y[f] || 0), 0);
const avg = (a, f) => a.length ? sum(a, f) / a.length : 0;
const pad = (s, n) => String(s).padStart(n);
const h   = t => console.log("\n\x1b[1m── " + t + " " + "─".repeat(Math.max(0, 60 - t.length)) + "\x1b[0m");

console.log(`\x1b[1m${path.basename(file)}\x1b[0m — ${R.length} olay` +
            (dropped ? `, ${dropped} bozuk satır atlandı` : ""));

// ── İsabet oranı ────────────────────────────────────────────────────────────
//
// Ölçülmemiş tek kritik bilinmeyen buydu: oyunun bütün TTK ve tehdit hesabı
// %100 isabet varsayımıyla kalibre edilmişti. Gerçek oran her süreyi böler.
// Işınlar paydaya girmez — ıskalamazlar.

h("İSABET ORANI");
const fired = by("shot_fired"), hit = by("shot_hit");
for (const k of ["ana", "turret"]) {
  const f = fired.filter(r => r.kaynak === k).length;
  const t = hit.filter(r => r.kaynak === k).length;
  if (!f) continue;
  const oran = 100 * t / f;
  console.log(`  ${k.padEnd(8)} atılan ${pad(f, 5)}  isabet ${pad(t, 5)}  ` +
              `\x1b[1m%${oran.toFixed(0)}\x1b[0m   → TTK çarpanı ×${(100 / oran).toFixed(2)}`);
}

const hedefler = {};
for (const x of hit) hedefler[x.hedef] = (hedefler[x.hedef] || 0) + 1;
console.log("  isabet edilen hedefler:",
  Object.entries(hedefler).sort((a, b) => b[1] - a[1]).map(([k, v]) => `${k} ${v}`).join(", "));

// Boost, hasarı ×2 ile ×1/3 arasında oynatıyor VE mermi boyutunu ×1.5 / ×0.6
// yapıyor — yani isabet oranını da değiştiriyor. Etiketsiz toplanan tek bir
// oran, üç ayrı silahın karışımı olurdu.
const anaF = fired.filter(r => r.kaynak === "ana" && r.boost !== undefined);
if (anaF.length) {
  console.log("  ana silah, boost moduna göre:");
  for (const m of ["None", "Weapon", "Shield"]) {
    const f = anaF.filter(r => r.boost === m).length;
    const t = hit.filter(r => r.kaynak === "ana" && r.boost === m).length;
    if (!f) continue;
    const boyut = anaF.find(r => r.boost === m)?.boyut;
    console.log(`    ${m.padEnd(7)} atılan ${pad(f, 5)}  isabet ${pad(t, 5)}  ` +
                `%${(100 * t / f).toFixed(0)}${boyut ? `   (mermi boyutu ×${boyut})` : ""}`);
  }
}

// ── Düşman tipleri: TTK ve tehdit doğrulaması ──────────────────────────────
//
// gözlenen_tehdit ≈ α · (oyuncu-saniyesi) + β · (oyuncuya verilen hasar)
//
// "dovus" = ilk isabetten ölüme. "yasam" = doğumdan ölüme. Tehdit için doğru
// ölçü DÖVÜŞ süresidir: sahnede 18 sn durup son 3 sn'de vurulan bir gemi
// oyuncunun 3 saniyesini yemiştir, 18'ini değil.

h("DÜŞMAN TİPLERİ");
const deaths = by("enemy_death"), spawns = by("enemy_spawn");
const tipler = {};
for (const d of deaths) {
  const a = tipler[d.tip] || (tipler[d.tip] = { n: 0, dovus: 0, yasam: 0, yenen: 0, tehdit: d.tehdit });
  a.n++; a.yasam += d.yasam; a.yenen += d.yenen;
  if (d.dovus >= 0) a.dovus += d.dovus;
}
if (Object.keys(tipler).length) {
  console.log("  tip          tehdit  ölüm  ort.dövüş  ort.yaşam  ort.yenen  fazla vuruş");
  for (const [k, a] of Object.entries(tipler).sort((x, y) => y[1].tehdit - x[1].tehdit)) {
    const spawn = spawns.find(s => s.tip === k);
    const hp    = spawn ? spawn.maxHP + (spawn.kalkan || 0) : 0;
    const fazla = hp ? (100 * (a.yenen / a.n - hp) / hp) : 0;
    console.log(`  ${k.padEnd(12)} ${pad(a.tehdit, 5)} ${pad(a.n, 6)} ` +
                `${pad((a.dovus / a.n).toFixed(1) + "sn", 10)} ${pad((a.yasam / a.n).toFixed(1) + "sn", 10)} ` +
                `${pad((a.yenen / a.n).toFixed(0), 10)} ${pad("%" + fazla.toFixed(0), 12)}`);
  }
}
const kacan = spawns.length - deaths.length;
console.log(`  doğan ${spawns.length}, ölen ${deaths.length}` +
            (kacan > 0 ? ` → ${kacan} tanesi ölmeden kayboldu` : ""));

// ── Level temposu ───────────────────────────────────────────────────────────
//
// Hedef 3–4 dk: asteroit geliri ~3.5 dakikalık level varsayımına dayanıyor
// (bkz. CLAUDE.md "Gelir Eğrisi"). Süre saparsa asteroit payı da sapar.

h("LEVEL TEMPOSU  (hedef 3–4 dk)");
const ends = by("level_end");
for (const e of ends) {
  const dk = e.sure / 60;
  const bayrak = dk < 2 ? " \x1b[33m← kısa\x1b[0m" : dk > 5 ? " \x1b[33m← uzun\x1b[0m" : "";
  console.log(`  lvl ${pad(e.lvl, 3)}  ${pad(dk.toFixed(1), 5)} dk   HP ${pad(e.hp, 4)}   ` +
              `metal ${pad(e.metal.toFixed(0), 5)}  kristal ${pad(e.kristal.toFixed(0), 5)}${bayrak}`);
}
if (ends.length) console.log(`  ortalama: ${(avg(ends, "sure") / 60).toFixed(1)} dk`);

// ── Dalga bütçesi: kağıt vs sahne ──────────────────────────────────────────

h("DALGALAR  (bütçe → sahneye çıkan kadro)");
const waves = by("wave");
const asim  = waves.filter(w => w.tehdit > w.butce).length;
for (const w of waves.slice(0, 12))
  console.log(`  lvl ${pad(w.lvl, 3)} dalga ${w.index}: bütçe ${pad(w.butce, 3)} → ` +
              `${pad(w.kadro, 2)} gemi, tehdit ${pad(w.tehdit, 3)}` +
              (w.tehdit > w.butce ? "  \x1b[33m← bütçe aşıldı (taşma/garanti)\x1b[0m" : ""));
if (waves.length > 12) console.log(`  … ${waves.length - 12} dalga daha`);
if (waves.length) console.log(`  bütçe aşan dalga: ${asim}/${waves.length}`);

// ── Kaynak akışı ────────────────────────────────────────────────────────────

h("KAYNAK  (hedef: yanma < %15)");
const res = by("resource");
for (const t of ["RawMaterial", "EnergyCrystal"]) {
  const D = sum(res.filter(r => r.olay === "dustu"    && r.tip === t), "miktar");
  const T = sum(res.filter(r => r.olay === "toplandi" && r.tip === t), "miktar");
  const Y = sum(res.filter(r => r.olay === "toplandi" && r.tip === t), "yanan");
  if (!D && !T) continue;
  const kayip = D ? 100 * (D - T) / D : 0;
  console.log(`  ${t.padEnd(14)} düştü ${pad(D.toFixed(0), 6)}  toplandı ${pad(T.toFixed(0), 6)}  ` +
              `tavanda yandı ${pad(Y.toFixed(0), 5)}  toplanamayan %${kayip.toFixed(0)}`);
}

// ── Oyuncunun aldığı hasar ──────────────────────────────────────────────────

h("OYUNCUYA GELEN HASAR");
const pd = by("player_damage");
if (pd.length) {
  const g = sum(pd, "gelen"), k = sum(pd, "kalkan"), b = sum(pd, "govde");
  console.log(`  ${pd.length} olay, toplam ${g.toFixed(0)} hasar`);
  console.log(`  kalkan yuttu ${k.toFixed(0)} (%${(100 * k / g).toFixed(0)}), ` +
              `gövdeye geçen ${b.toFixed(0)} (%${(100 * b / g).toFixed(0)})`);
} else {
  console.log("  hiç hasar alınmamış");
}

// ── Boost kullanımı ─────────────────────────────────────────────────────────
//
// İki soru: oyuncu boost'u bir ARAÇ olarak mı kullanıyor (kısa, sık, duruma
// göre), yoksa açıp unutuyor mu (uzun, seyrek)? İkincisi ise mekanik bir
// seçim olmaktan çıkmış, pasif bir moda dönüşmüş demektir.

h("BOOST KULLANIMI");
const boosts = by("boost");
if (!boosts.length) {
  console.log("  hiç boost kullanılmamış");
} else {
  const sureler = {};
  for (const b of boosts) {
    const m = b.onceki;
    if (!sureler[m]) sureler[m] = { n: 0, sure: 0 };
    sureler[m].n++; sureler[m].sure += b.sure || 0;
  }
  console.log(`  ${boosts.length} mod değişimi`);
  for (const [m, a] of Object.entries(sureler)) {
    if (m === "None") continue;
    console.log(`  ${m.padEnd(7)} ${pad(a.n, 3)} kez, toplam ${pad(a.sure.toFixed(0) + "sn", 7)}, ` +
                `ortalama ${(a.sure / a.n).toFixed(1)}sn açık kaldı`);
  }
  const oyun = ends.length ? sum(ends, "sure") : 0;
  const acik = Object.entries(sureler).filter(([m]) => m !== "None")
                     .reduce((s, [, a]) => s + a.sure, 0);
  if (oyun) console.log(`  oyun süresinin %${(100 * acik / oyun).toFixed(0)}'inde bir boost açıktı`);
}

// ── Yükseltme temposu ───────────────────────────────────────────────────────

h("YÜKSELTMELER");
const ups = by("upgrade");
if (!ups.length) {
  const sonSure = ends.length ? sum(ends, "sure") / 60 : 0;
  console.log(`  \x1b[33mhiç yükseltme yok\x1b[0m` +
              (sonSure ? ` (${sonSure.toFixed(1)} dakikada)` : "") +
              " — oyuncu gücü hiç artmamış demektir");
} else {
  for (const u of ups)
    console.log(`  t=${pad(u.t.toFixed(0), 5)}s lvl ${pad(u.lvl, 3)}  ` +
                `${u.komponent} / ${u.iz} → sv${u.seviye}  (${u.maliyet} ${u.kaynak})`);
}
console.log();
