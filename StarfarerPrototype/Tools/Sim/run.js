// Headless denge simülasyonunun koşucusu — N koşuyu paralel çalıştırır.
//
//   node Tools/Sim/run.js --kosu 8 --profil ucuz --level 1-10
//   node Tools/Sim/run.js --kosu 4 --set statStep=1.5 --etiket statStep-yuksek
//
// Dış bağımlılık yok (SkinGen ve analyze.js ile aynı desen).
//
// NEDEN AYRI SÜREÇLER: koşular birbirinden TAM yalıtılmalı. Aynı süreçte
// arka arkaya koşsaydık statik durum (BalanceLog, ChapterManager.CampaignFinished,
// kalkan yetim havuzu…) koşudan koşuya sızardı — ve bu proje o hatayı bir kez
// yaşadı (bkz. CLAUDE.md "Kalkan Yetim Havuzu"). Süreç sınırı, sıfırlamayı
// unutmanın mümkün olmadığı tek sınırdır.
//
// NEDEN HER SÜRECE AYRI -logFile: Unity'nin Player.log'u sabit bir yoldadır;
// paralel koşan süreçler aynı dosyaya yazmak için birbirini bekler ya da
// birbirinin üstüne yazar.

const fs    = require("fs");
const os    = require("os");
const path  = require("path");
const { spawn } = require("child_process");

const ROOT = path.resolve(__dirname, "..", "..");
const EXE  = path.join(ROOT, "Builds", "Sim", "Starfarer-sim.exe");

// ── Argümanlar ───────────────────────────────────────────────────────────────

function parseArgs(argv) {
  const a = {
    kosu:    4,
    profil:  "ucuz",
    level:   "1-10",
    zorluk:  "normal",
    nisan:   null,
    nisanHiz: null,
    is:      Math.max(1, Math.min(os.cpus().length - 1, 12)),
    etiket:  null,
    sure:    null,
    duvar:   null,
    tohum:   1,
    set:     [],
  };
  for (let i = 2; i < argv.length; i++) {
    const k = argv[i], v = () => argv[++i];
    switch (k) {
      case "--kosu":     a.kosu    = parseInt(v(), 10); break;
      case "--profil":   a.profil  = v(); break;
      case "--level":    a.level   = v(); break;
      case "--zorluk":   a.zorluk  = v(); break;
      case "--nisan":    a.nisan   = v(); break;
      case "--nisan-hiz": a.nisanHiz = v(); break;
      case "--is":       a.is      = parseInt(v(), 10); break;
      case "--etiket":   a.etiket  = v(); break;
      case "--sure":     a.sure    = v(); break;
      case "--duvar":    a.duvar   = v(); break;
      case "--tohum":    a.tohum   = parseInt(v(), 10); break;
      case "--set":      a.set.push(v()); break;
      default:
        console.error("bilinmeyen argüman: " + k);
        process.exit(2);
    }
  }
  return a;
}

const args = parseArgs(process.argv);

if (!fs.existsSync(EXE)) {
  console.error("Koşucu bulunamadı: " + EXE);
  console.error("Önce build al:");
  console.error('  "C:\\Program Files\\Unity\\Hub\\Editor\\<sürüm>\\Editor\\Unity.exe" ' +
                "-batchmode -projectPath . -executeMethod SimBuild.Player");
  process.exit(1);
}

// ── Çıktı klasörü ────────────────────────────────────────────────────────────

function stamp() {
  const d = new Date(), p = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}-${p(d.getHours())}${p(d.getMinutes())}${p(d.getSeconds())}`;
}

const label  = args.etiket || `${args.profil}-${args.level}`;
const outDir = path.join(ROOT, "Tools", "Balance", "logs", "sim", `${stamp()}-${label}`);
fs.mkdirSync(outDir, { recursive: true });

// ── Koşu ─────────────────────────────────────────────────────────────────────

function runArgs(seed, outFile, logFile) {
  const a = [
    "-batchmode", "-nographics",
    // Çözünürlük SABİTLENİR: kadraj (ViewBounds) kameradan türer ve doğum
    // sınırları oradan gelir. Ekran oranı koşudan koşuya değişseydi düşmanlar
    // farklı yerlerde doğar, iki koşu kıyaslanamazdı.
    "-screen-width", "1920", "-screen-height", "1080",
    "-logFile", logFile,
    "--sim",
    "--seed", String(seed),
    "--profil", args.profil,
    "--level", args.level,
    "--zorluk", args.zorluk,
    "--cikti", outFile,
  ];
  if (args.nisan    !== null) a.push("--nisan", args.nisan);
  if (args.nisanHiz !== null) a.push("--nisan-hiz", args.nisanHiz);
  if (args.sure     !== null) a.push("--sure", args.sure);
  if (args.duvar    !== null) a.push("--duvar", args.duvar);
  for (const s of args.set) a.push("--set", s);
  return a;
}

function runOne(seed) {
  return new Promise((resolve) => {
    const outFile = path.join(outDir, `s${seed}.jsonl`);
    const logFile = path.join(outDir, `s${seed}.unity.log`);
    const started = Date.now();

    const p = spawn(EXE, runArgs(seed, outFile, logFile), { stdio: "ignore" });
    p.on("exit", (code) => {
      const sec = (Date.now() - started) / 1000;
      resolve({ seed, code, sec, outFile, logFile });
    });
    p.on("error", (err) => {
      resolve({ seed, code: -1, sec: 0, outFile, logFile, err: err.message });
    });
  });
}

// Havuz: aynı anda en fazla args.is süreç. Hepsini birden başlatmak 12
// çekirdekli bir makinede koşuları birbirine yavaşlatır ve duvar saati
// sınırının (--duvar) anlamını bozardı.
async function runAll() {
  const seeds = [];
  for (let i = 0; i < args.kosu; i++) seeds.push(args.tohum + i);

  const results = [];
  let next = 0;

  async function worker() {
    while (next < seeds.length) {
      const seed = seeds[next++];
      const r = await runOne(seed);
      results.push(r);
      console.log(`  tohum ${r.seed}: cikis=${r.code} sure=${r.sec.toFixed(1)}sn ` +
                  `${summarize(r.outFile)}`);
    }
  }

  const workers = [];
  for (let i = 0; i < Math.min(args.is, seeds.length); i++) workers.push(worker());
  await Promise.all(workers);
  return results.sort((a, b) => a.seed - b.seed);
}

// ── Özet ─────────────────────────────────────────────────────────────────────

function readEvents(file) {
  if (!fs.existsSync(file)) return [];
  const out = [];
  for (const line of fs.readFileSync(file, "utf8").split("\n")) {
    const s = line.trim();
    if (!s) continue;
    try { out.push(JSON.parse(s)); } catch (e) { /* yarım son satır */ }
  }
  return out;
}

function summarize(file) {
  const end = readEvents(file).filter((e) => e.ev === "sim_end").pop();
  if (!end) return "(sim_end yok)";
  return `sebep=${end.sebep} level=${end.level} oyun=${Math.round(end.oyunSn)}sn`;
}

function report(results) {
  console.log("");
  console.log(`Koşu klasörü: ${outDir}`);

  const ends   = [];
  const levelTimes = [];
  // İsabet oranı KAYNAĞA göre ayrılır: ana silahı insan nişanlıyor, turret
  // kendi nişan alıyor. Tek bir ortalama, nişan modelini kalibre etmeye
  // yaramaz — insandan ölçülen sayı da ayrıydı (ana %52, turret %86).
  const shots = {}, hits = {};

  for (const r of results) {
    for (const e of readEvents(r.outFile)) {
      if (e.ev === "sim_end")    ends.push(e);
      if (e.ev === "level_end")  levelTimes.push(e.sure);
      if (e.ev === "shot_fired") shots[e.kaynak] = (shots[e.kaynak] || 0) + 1;
      if (e.ev === "shot_hit")   hits[e.kaynak]  = (hits[e.kaynak]  || 0) + 1;
    }
  }

  const bySebep = {};
  for (const e of ends) bySebep[e.sebep] = (bySebep[e.sebep] || 0) + 1;

  const avg = (xs) => xs.length ? xs.reduce((a, b) => a + b, 0) / xs.length : 0;

  console.log(`Koşu sayısı  : ${results.length}`);
  console.log(`Bitiş sebebi : ${JSON.stringify(bySebep)}`);
  console.log(`Ulaşılan lvl : ort ${avg(ends.map((e) => e.level)).toFixed(1)}`);
  console.log(`Level süresi : ort ${(avg(levelTimes) / 60).toFixed(2)} dk  (hedef 3–4 dk)`);
  const insan = { ana: 52, turret: 86 };
  for (const k of Object.keys(shots).sort()) {
    const oran = (hits[k] || 0) / shots[k] * 100;
    const ref  = insan[k] !== undefined ? `  (insan %${insan[k]})` : "";
    console.log(`İsabet ${k.padEnd(7)}: ${String(hits[k] || 0).padStart(6)}/${String(shots[k]).padEnd(6)}` +
                ` = %${oran.toFixed(1)}${ref}`);
  }
  console.log("");
  console.log("Ayrıntılı metrikler için:  node Tools/Balance/analyze.js " +
              path.join(outDir, "s" + args.tohum + ".jsonl"));
}

(async () => {
  console.log(`Koşucu : ${EXE}`);
  console.log(`Koşu   : ${args.kosu} adet, ${args.is} paralel, profil=${args.profil}, ` +
              `level=${args.level}${args.set.length ? ", ezme=" + args.set.join(",") : ""}`);
  console.log("");
  const results = await runAll();
  report(results);
})();
