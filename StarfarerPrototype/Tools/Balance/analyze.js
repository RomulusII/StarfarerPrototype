// Denge kaydı analizi — BalanceLog'un ürettiği JSONL dosyasını okur.
//
//   node Tools/Balance/analyze.js <dosya.jsonl>
//   node Tools/Balance/analyze.js            (en yeni kaydı otomatik bulur)
//   node Tools/Balance/analyze.js --karsilastir <taban> <varyant>
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

// Biçimlendirme yardımcıları. Her iki kip de kullandığı için dosya
// çözümlemesinden ÖNCE tanımlanırlar.
const pad = (s, n) => String(s).padStart(n);
const h   = t => console.log("\n\x1b[1m── " + t + " " + "─".repeat(Math.max(0, 60 - t.length)) + "\x1b[0m");

// ── Karşılaştırma kipi ──────────────────────────────────────────────────────
//
//   node Tools/Balance/analyze.js --karsilastir <taban> <varyant>
//
// Taraf bir KLASÖR (koşu kümesi) ya da tek bir dosya olabilir.
//
// Neden ayrı bir kip: tek dosyalık rapor bir A/B sorusunu cevaplayamaz.
// Asıl mesele şu — **gürültü bandı olmayan bir fark, fark değildir.**
// Bir koşu tohuma bağlıdır; AYNI parametreyle koşulan iki koşu arasında bile
// fark çıkar. Bu yüzden her metrik KOŞU BAŞINA hesaplanır; ortalaması ve
// yayılımı birlikte basılır ve fark ancak koşular arası yayılımın dışına
// çıkıyorsa işaretlenir.
//
// Kapı kaba: n küçük (4–8 koşu), yani bu bir p-değeri değil. Cevapladığı soru
// "bu fark istatistiksel olarak anlamlı mı" değil, "bu farkı konuşmaya değer
// mi, yoksa tohum gürültüsü mü". Tek koşuyu tek koşuyla kıyaslamak — kipin
// yerine geçen yöntem buydu — bu soruyu hiç sormuyordu.

function jsonlFiles(target) {
  if (!fs.existsSync(target)) return [];
  if (fs.statSync(target).isFile()) return [target];
  const out = [];
  for (const e of fs.readdirSync(target, { withFileTypes: true })) {
    const p = path.join(target, e.name);
    if (e.isDirectory())                out.push(...jsonlFiles(p));
    else if (e.name.endsWith(".jsonl")) out.push(p);
  }
  return out.sort();
}

function parseRows(file) {
  const out = [];
  for (const l of fs.readFileSync(file, "utf8").trim().split("\n")) {
    try { out.push(JSON.parse(l)); } catch (e) { /* yarım kalmış son satır */ }
  }
  return out;
}

// Karşılaştırılan metrikler. `d` basamak sayısı, `hedef` CLAUDE.md'de yazılı
// hedef eğri (varsa) — fark anlamlı çıktığında hangi yöne gittiğini okumak için.
const KARSI_METRIK = [
  { k: "level_dk",      ad: "level süresi (dk)",      d: 2, hedef: "3–4" },
  { k: "ulasilan",      ad: "ulaşılan level",         d: 1 },
  { k: "olum",          ad: "ölümle biten koşu %",    d: 0 },
  { k: "isabet_ana",    ad: "isabet, ana silah %",    d: 1 },
  { k: "isabet_turret", ad: "isabet, turret %",       d: 1 },
  { k: "dovus",         ad: "ort. dövüş süresi (sn)", d: 2 },
  { k: "govde_pay",     ad: "gövdeye geçen hasar %",  d: 1 },
  { k: "hasar_level",   ad: "alınan hasar / level",   d: 0 },
  { k: "yukseltme",     ad: "yükseltme + kurulum",    d: 1 },
  { k: "metal",         ad: "toplanan metal",         d: 0 },
  { k: "kristal",       ad: "toplanan kristal",       d: 0 },
  { k: "metal_kayip",   ad: "toplanamayan metal %",   d: 0, hedef: "<15" },
  { k: "kristal_kayip", ad: "toplanamayan kristal %", d: 0, hedef: "<15" },
];

/// Tek bir koşudan skaler metrikler. Her biri KOŞU BAŞINA bir sayıdır —
/// bütün koşuları havuzlamak yayılımı görünmez kılardı.
function kosuMetrikleri(file) {
  const R    = parseRows(file);
  const by   = t => R.filter(r => r.ev === t);
  const topl = (a, f) => a.reduce((x, y) => x + (y[f] || 0), 0);
  const oran = (p, q) => q ? 100 * p / q : NaN;

  const ends   = by("level_end");
  const son    = by("sim_end")[0];
  const bas    = by("sim_start")[0];
  const fired  = by("shot_fired"), hit = by("shot_hit");
  const deaths = by("enemy_death");
  const pd     = by("player_damage");
  const res    = by("resource");

  const isabet = k => oran(hit.filter(r => r.kaynak === k).length,
                           fired.filter(r => r.kaynak === k).length);

  const dustu    = t => topl(res.filter(r => r.olay === "dustu"    && r.tip === t), "miktar");
  const toplandi = t => topl(res.filter(r => r.olay === "toplandi" && r.tip === t), "miktar");
  const kayip    = t => { const D = dustu(t); return D ? oran(D - toplandi(t), D) : NaN; };

  const dovusler = deaths.filter(d => d.dovus >= 0).map(d => d.dovus);
  const gelen    = topl(pd, "gelen");

  // Tip kırılımı koşu içinde havuzlanır: bir koşuda bir tipten 0–3 ölüm olur,
  // koşu başına ortalamanın yayılımı ölümün kendisinden çok tipin o koşuda
  // sahneye çıkıp çıkmadığını ölçerdi.
  const tipler = {};
  for (const d of deaths) {
    const a = tipler[d.tip] ||
              (tipler[d.tip] = { n: 0, dovus: 0, dovusN: 0, yenen: 0, tehdit: d.tehdit });
    a.n++; a.yenen += d.yenen || 0;
    if (d.dovus >= 0) { a.dovus += d.dovus; a.dovusN++; }
  }

  return {
    file, tipler,
    seed:  bas ? bas.seed : null,
    sebep: son ? son.sebep : null,
    olay:  R.length,
    m: {
      level_dk:      ends.length ? topl(ends, "sure") / ends.length / 60 : NaN,
      ulasilan:      son ? son.level : (R.length ? Math.max(...R.map(r => r.lvl || 0)) : NaN),
      olum:          son ? (son.sebep === "oldu" ? 100 : 0) : NaN,
      isabet_ana:    isabet("ana"),
      isabet_turret: isabet("turret"),
      dovus:         dovusler.length ? dovusler.reduce((a, b) => a + b, 0) / dovusler.length : NaN,
      govde_pay:     oran(topl(pd, "govde"), gelen),
      hasar_level:   ends.length ? gelen / ends.length : NaN,
      yukseltme:     by("upgrade").length + by("kurulum").length,
      metal:         toplandi("RawMaterial"),
      kristal:       toplandi("EnergyCrystal"),
      metal_kayip:   kayip("RawMaterial"),
      kristal_kayip: kayip("EnergyCrystal"),
    },
  };
}

/// Ortalama + örneklem standart sapması. Geçersiz (NaN) koşular SAYILMAZ:
/// hiç turret kurmamış bir koşu turret isabetini sıfıra çekmemeli — o koşuda
/// o metrik ölçülmemiştir, sıfır değildir.
function ozet(degerler) {
  const v = degerler.filter(x => typeof x === "number" && isFinite(x));
  if (!v.length) return { n: 0, ort: NaN, sd: NaN };
  const ort = v.reduce((a, b) => a + b, 0) / v.length;
  const sd  = v.length < 2 ? 0
            : Math.sqrt(v.reduce((a, b) => a + (b - ort) * (b - ort), 0) / (v.length - 1));
  return { n: v.length, ort, sd };
}

function karsilastir(aYol, bYol) {
  const yukle = (yol, etiket) => {
    const dosyalar = jsonlFiles(yol);
    if (!dosyalar.length) {
      console.error(`${etiket}: kayıt bulunamadı — ${yol}`);
      process.exit(1);
    }
    return {
      // Koşu klasörleri "20260902-163137-nisan-sifir" gibi adlanır; sütun
      // başlığında işe yarayan kısım ETİKETTİR, zaman damgası değil.
      ad: path.basename(yol.replace(/[\\/]+$/, "")).replace(/^\d{8}-\d{6}-/, ""),
      kosular: dosyalar.map(kosuMetrikleri),
    };
  };

  const A = yukle(aYol, "taban"), B = yukle(bYol, "varyant");

  const sag = (s, n) => String(s).padStart(n);
  const sol = (s, n) => String(s).padEnd(n);
  const say = (v, d) => (typeof v === "number" && isFinite(v)) ? v.toFixed(d) : "—";

  for (const [etiket, S] of [["taban  ", A], ["varyant", B]]) {
    const olay = S.kosular.reduce((a, k) => a + k.olay, 0);
    console.log(`\x1b[1m${etiket}\x1b[0m : ${S.ad}  —  ${S.kosular.length} koşu, ${olay} olay`);
  }

  // Aynı tohumlarla koşulmamış iki küme, parametre farkını tohum farkından
  // ayıramaz. Koşuyu durdurmaz — insan oturumlarının tohumu yoktur — ama
  // sessizce geçilirse yanlış bir sonuç çıkarılır.
  const tohum = S => S.kosular.map(k => k.seed).filter(s => s != null)
                              .sort((x, y) => x - y).join(",");
  const tA = tohum(A), tB = tohum(B);
  if (tA && tB && tA !== tB)
    console.log(`\x1b[33m  ⚠ tohum kümeleri farklı (${tA} vs ${tB}) — ` +
                `fark parametreden mi tohumdan mı geldi, ayrılamaz\x1b[0m`);

  for (const [etiket, S] of [["taban", A], ["varyant", B]]) {
    const n = S.kosular.filter(k => k.sebep === "sure" || k.sebep === "duvar").length;
    if (n) console.log(`\x1b[33m  ⚠ ${etiket}: ${n} koşu sınıra takılıp kesildi ` +
                       `(sebep=sure/duvar) — ortalamaları aşağı çeker\x1b[0m`);
  }

  h("METRİKLER  (ortalama ±sapma, koşu başına)");
  console.log(`  ${sol("", 26)}${sag(A.ad.slice(0, 15), 17)}${sag(B.ad.slice(0, 15), 17)}${sag("fark", 10)}`);

  for (const M of KARSI_METRIK) {
    const a = ozet(A.kosular.map(k => k.m[M.k]));
    const b = ozet(B.kosular.map(k => k.m[M.k]));
    if (!a.n && !b.n) continue;

    const fark  = (isFinite(a.ort) && isFinite(b.ort)) ? b.ort - a.ort : NaN;
    const yuzde = (isFinite(fark) && a.ort) ? 100 * fark / Math.abs(a.ort) : NaN;

    // Gürültü bandı: iki ortalamanın standart hatası. n < 2 ise yayılım
    // ölçülemez — "?" basılır, sıfır sayılmaz.
    let isaret = " ?", renk = "\x1b[90m";
    if (a.n >= 2 && b.n >= 2 && isFinite(fark)) {
      const se = Math.sqrt(a.sd * a.sd / a.n + b.sd * b.sd / b.n);
      if (Math.abs(fark) > 2 * se) { isaret = " ↑"; renk = "\x1b[33m"; if (fark < 0) isaret = " ↓"; }
      else                         { isaret = " ≈"; renk = "\x1b[90m"; }
    }

    const hucre = s => `${say(s.ort, M.d)} ±${say(s.sd, M.d)}`;
    console.log(`  ${sol(M.ad, 26)}${sag(hucre(a), 17)}${sag(hucre(b), 17)}` +
                `${sag(isFinite(fark) ? (fark > 0 ? "+" : "") + say(fark, M.d) : "—", 10)}` +
                `${renk}${sag(isFinite(yuzde) ? (yuzde > 0 ? "+" : "") + yuzde.toFixed(0) + "%" : "", 7)}` +
                `${isaret}\x1b[0m` + (M.hedef ? `  \x1b[90mhedef ${M.hedef}\x1b[0m` : ""));
  }
  console.log(`  \x1b[90m↑↓ fark yayılımın dışında · ≈ gürültünün içinde · ` +
              `? yayılım ölçülemedi (n<2)\x1b[0m`);

  // ── Tip kırılımı ──────────────────────────────────────────────────────────
  //
  // Toplam bir metrik "hangi TİP zorlaştı" sorusunu yutar: Armored'ın dövüş
  // süresi ikiye katlanırken Swarm'ınki yarılanırsa ortalama kıpırdamaz.
  h("DÜŞMAN TİPİ  (ölüm/koşu · ort.dövüş sn · ort.yenen hasar)");
  const birlestir = S => {
    const t = {};
    for (const k of S.kosular)
      for (const [ad, a] of Object.entries(k.tipler)) {
        const x = t[ad] || (t[ad] = { n: 0, dovus: 0, dovusN: 0, yenen: 0, tehdit: a.tehdit });
        x.n += a.n; x.dovus += a.dovus; x.dovusN += a.dovusN; x.yenen += a.yenen;
      }
    return t;
  };
  const tipA = birlestir(A), tipB = birlestir(B);
  const adlar = [...new Set([...Object.keys(tipA), ...Object.keys(tipB)])]
    .sort((x, y) => ((tipB[y] || tipA[y]).tehdit || 0) - ((tipB[x] || tipA[x]).tehdit || 0));

  if (adlar.length) {
    console.log(`  ${sol("tip", 14)}${sag("tehdit", 7)}` +
                `${sag(A.ad.slice(0, 18), 24)}${sag(B.ad.slice(0, 18), 24)}`);
    const hucre = (t, kosu) => t
      ? `${(t.n / kosu).toFixed(1)} · ${t.dovusN ? (t.dovus / t.dovusN).toFixed(1) : "—"} · ` +
        `${(t.yenen / t.n).toFixed(0)}`
      : "—";
    for (const ad of adlar)
      console.log(`  ${sol(ad, 14)}${sag(say((tipB[ad] || tipA[ad]).tehdit, 0), 7)}` +
                  `${sag(hucre(tipA[ad], A.kosular.length), 24)}` +
                  `${sag(hucre(tipB[ad], B.kosular.length), 24)}`);
  }
  console.log();
}

// Karşılaştırma kipi tek dosyalık raporun YERİNE geçer, yanına değil.
const _argv = process.argv.slice(2);
const _cmp  = _argv.indexOf("--karsilastir");
if (_cmp >= 0) {
  if (!_argv[_cmp + 1] || !_argv[_cmp + 2]) {
    console.error("Kullanım: node Tools/Balance/analyze.js --karsilastir <taban> <varyant>");
    process.exit(1);
  }
  karsilastir(_argv[_cmp + 1], _argv[_cmp + 2]);
  process.exit(0);
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

console.log(`\x1b[1m${path.basename(file)}\x1b[0m — ${R.length} olay` +
            (dropped ? `, ${dropped} bozuk satır atlandı` : ""));

// ── Oturum künyesi ──────────────────────────────────────────────────────────
//
// Dağıtılan build'lerden veri gelmeye başlayınca "bu kayıt nereden geldi"
// sorusu ilk soru oldu: aynı sayı telefonda ve PC'de aynı şeyi anlatmıyor.
//
// DENGE REVİZYONU ayrı basılır çünkü karşılaştırmayı asıl o böler: mağaza
// sürümü aynı kalırken formül değişir. Farklı revizyonların kayıtları aynı
// havuzda toplanırsa ortalama iki ayarın ortasını gösterir ve hiçbirini
// anlatmaz. Alanın hiç olmaması da bir cevaptır: bütçenin saatte büyüdüğü
// eski build.

const oturum = by("session")[0];
if (oturum) {
  const ekran = oturum.ekran_en && oturum.ekran_boy
    ? `${oturum.ekran_en}×${oturum.ekran_boy}` + (oturum.dpi ? ` @${Math.round(oturum.dpi)}dpi` : "")
    : "?";
  console.log(`  ${oturum.platform || "?"}  ${oturum.cihaz || "?"}  ${ekran}  ` +
              `${oturum.ram_mb ? oturum.ram_mb + "MB" : "?"}  ` +
              `${oturum.cekirdek ? oturum.cekirdek + " çekirdek" : ""}`);
  console.log(`  ${oturum.isletim || "?"}  ·  ${oturum.gpu || "?"}`);
  const denge = oturum.denge != null ? `denge r${oturum.denge}` : "denge YOK (2026-09-05 öncesi)";
  console.log(`  sürüm ${oturum.surum || "?"} · ${denge} (Unity ${oturum.unity || "?"})` +
              `  ·  dil ${oturum.dil || "?"}`);
}

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

// ── Performans ──────────────────────────────────────────────────────────────
//
// Ortalama FPS tek başına yanıltıcıdır: saniyede bir gelen 200 ms'lik takılma
// ortalamayı 60'tan ancak 55'e indirir, oyuncunun şikâyet ettiği şey ise odur.
// Bu yüzden asıl bakılan sayı p95 — kötü kareler burada görünür.
//
// Yük sütunu (sahadaki gemi sayısı) olmadan sayılar karşılaştırılamaz: aynı
// cihazda 5 ve 40 gemiyle ölçülen kare süresi aynı şeyi anlatmaz.

h("PERFORMANS");
const perf = by("perf");
if (!perf.length) {
  console.log("  \x1b[33mperf örneği yok\x1b[0m — PerfSampler kurulmamış ya da oturum çok kısa");
} else {
  const kare  = sum(perf, "kare");
  const agir  = perf.filter(p => (p.ms_p95 || 0) > 33.3);   // p95 30 fps'in altında
  const enKot = perf.reduce((a, b) => ((b.ms_max || 0) > (a.ms_max || 0) ? b : a));

  console.log(`  ${perf.length} pencere, ${kare} kare  ·  ` +
              `ortalama \x1b[1m${avg(perf, "fps_ort").toFixed(1)} fps\x1b[0m ` +
              `(${avg(perf, "ms_ort").toFixed(1)} ms)`);
  console.log(`  p50 ${avg(perf, "ms_p50").toFixed(1)} ms  ·  ` +
              `p95 \x1b[1m${avg(perf, "ms_p95").toFixed(1)} ms\x1b[0m  ·  ` +
              `en kötü kare ${(enKot.ms_max || 0).toFixed(0)} ms (${enKot.dusman || 0} gemi varken)`);

  if (agir.length)
    console.log(`  \x1b[33m${agir.length}/${perf.length} pencerede p95 > 33 ms\x1b[0m ` +
                `(30 fps altı) — ortalama ${avg(agir, "dusman").toFixed(0)} gemi sahadayken`);

  // Yüke göre kırılım: hangi gemi sayısından sonra bozuluyor?
  const kova = { "0-9": [], "10-19": [], "20-39": [], "40+": [] };
  for (const p of perf) {
    const d = p.dusman || 0;
    kova[d < 10 ? "0-9" : d < 20 ? "10-19" : d < 40 ? "20-39" : "40+"].push(p);
  }
  for (const [ad, a] of Object.entries(kova)) {
    if (!a.length) continue;
    console.log(`    ${ad.padEnd(6)} ${pad(a.length, 3)} pencere  ` +
                `ort ${pad(avg(a, "fps_ort").toFixed(0), 3)} fps  ` +
                `p95 ${pad(avg(a, "ms_p95").toFixed(1), 5)} ms`);
  }

  const gc = perf.filter(p => p.gc_mb);
  if (gc.length) {
    const ilk = gc[0].gc_mb, son = gc[gc.length - 1].gc_mb;
    console.log(`  yönetilen yığın ${ilk.toFixed(1)} → ${son.toFixed(1)} MB` +
                (son > ilk * 1.5 ? "  \x1b[33m(sürekli büyüyor — sızıntı olabilir)\x1b[0m" : ""));
  }
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
