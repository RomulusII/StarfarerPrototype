// Gemi olmayan nesneler: mermiler, bombalar, füzeler, enkaz, turret parçaları.
// Koordinat: y YUKARI, origin sol-alt. Tuval = px x 4, PPU 400 — dünya boyutu
// prosedürel yedekle birebir aynı kalır, piksel yoğunluğu 4 kat olur.
//
// RENK KURALI — iki grup var:
//   1. Rengi SABİT olanlar (bomba, düşman mermisi, avcı, toplayıcı, hangar):
//      renk sprite'a gömülür, kod SpriteRenderer.color vermez.
//   2. Rengi KODDAN gelenler (turret tabanı/namlusu/mermisi, enkaz): sprite
//      GRİ TONLAMALI çizilir, kod sr.color ile çarpar. Tek sprite tüm
//      varyantları karşılar — turretin 6 uzmanlığı için 12 ayrı görsel
//      üretmek yerine 2 görsel + çarpım yeter.

const { mirrorY } = require("./raster");

// ── Geometri yardımcıları ───────────────────────────────────────────────────

/** Elips/daire poligonu. sq<1 yassı, >1 uzun. */
const circle = (cx, cy, r, n = 32, sq = 1) => {
  const p = [];
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2;
    p.push(cx + Math.cos(a) * r, cy + Math.sin(a) * r * sq);
  }
  return p;
};

/** Kalınlığı olan doğru parçası (dörtgen). Enkaz çubukları ve çerçeveler için. */
const bar = (x0, y0, x1, y1, t) => {
  const dx = x1 - x0, dy = y1 - y0;
  const L  = Math.hypot(dx, dy) || 1;
  const nx = (-dy / L) * t * 0.5, ny = (dx / L) * t * 0.5;
  return [x0 + nx, y0 + ny, x1 + nx, y1 + ny, x1 - nx, y1 - ny, x0 - nx, y0 - ny];
};

/** Tohumlu rastgele — enkaz varyantları her çalıştırmada AYNI çıksın diye. */
const rnd = seed => {
  let s = (seed * 2654435761) >>> 0;
  return () => (s = (s * 1664525 + 1013904223) >>> 0) / 4294967296;
};

/**
 * Şekilsiz leke: yarıçapı iki sinüs lobuyla dalgalanan kapalı eğri. Asteroit
 * enkazının TEK yapı taşı — orada çizgi/parça değil yalnızca leke isteniyor,
 * o yüzden lob sayısı ve faz tohumdan geliyor; siluet hiçbir varyantta
 * simetrik ya da tanınır çıkmıyor.
 */
const blob = (cx, cy, r, seed, wob = 0.40, n = 26) => {
  const R  = rnd(seed);
  const l1 = 2 + Math.floor(R() * 3), p1 = R() * Math.PI * 2;
  const l2 = 4 + Math.floor(R() * 4), p2 = R() * Math.PI * 2;
  const k  = 0.45 + R() * 0.55;
  const pts = [];
  for (let i = 0; i < n; i++) {
    const a  = (i / n) * Math.PI * 2;
    const rr = r * (1 + wob * (Math.sin(a * l1 + p1) * 0.62 + Math.sin(a * l2 + p2) * 0.38 * k));
    pts.push(cx + Math.cos(a) * rr, cy + Math.sin(a) * rr);
  }
  return pts;
};

// ── Paletler ────────────────────────────────────────────────────────────────

// Gri tonlama: kod rengi bunu ÇARPAR, o yüzden en parlak ton beyaza yakın olmalı
const G = {
  hi:   [246, 248, 252],
  mid:  [188, 192, 202],
  lo:   [126, 130, 142],
  dark: [ 68,  72,  84],
};

// Sabit renkler (rim = dış hat, mid = gövde, hot = iç, core = çekirdek)
const ORANGE = { core: [255, 236, 200], hot: [255, 176,  74], mid: [242, 110,  28], rim: [150,  52,  10] };
const PURPLE = { core: [248, 220, 255], hot: [216, 132, 255], mid: [168,  38, 216], rim: [ 92,  16, 128] };
const KINETC = { core: [255, 255, 255], hot: [206, 226, 255], mid: [138, 168, 224], rim: [ 62,  86, 140] };
const YELLOW = { core: [255, 255, 226], hot: [255, 232, 118], mid: [226, 184,  32], rim: [128,  98,  10] };
const GREY   = { core: G.hi,            hot: G.mid,           mid: G.lo,            rim: G.dark };

const mk = (name, dir, id, w, h, shapes) => ({
  name, dir, w, h, ppu: 400, shapes,
  skin: { id, colliderMode: "Box", hitboxScale: 1.0 },
});

// ── Bomba — vurulabilir mühimmat ────────────────────────────────────────────
// Bomb transform'u DÖNMEZ (Bomb.Update yalnızca Translate yapar), o yüzden
// silueti yöne bağlı olmayan bir deniz mayını: her açıdan aynı okunur.
// Oyundaki en yüksek hasarlı tekil mermi ve en irisi (0.14 birim) — silah
// gücü boyuttan okunmalı.

const bomb = () => {
  const C = 28, shapes = [];

  // 8 diken — "bu şeye dokunma" dilinin tamamı burada
  for (let i = 0; i < 8; i++) {
    const a = (i / 8) * Math.PI * 2 + Math.PI / 8;
    const t = 0.20;
    const at = (r, o) => [C + Math.cos(a + o) * r, C + Math.sin(a + o) * r];
    const p0 = at(25.5, 0), p1 = at(13, -t), p2 = at(13, t);
    shapes.push({ pts: [p0[0], p0[1], p1[0], p1[1], p2[0], p2[1]], color: ORANGE.rim });
  }

  shapes.push(
    { pts: circle(C, C, 17.5), color: ORANGE.rim },
    { pts: circle(C, C, 15.5), color: [96, 40, 22] },     // kabuk
    // Sıcak dikiş — kabuğun ortasındaki çekirdek
    { pts: circle(C, C, 9.2),  color: ORANGE.mid },
    { pts: circle(C, C, 6.4),  color: ORANGE.hot },
    { pts: circle(C, C, 3.2),  color: ORANGE.core },
    // Üst kenar aydınlatması — hacim hissi
    { pts: [C - 11, C + 9, C - 4, C + 14.5, C + 5, C + 14, C + 2, C + 11.5,
            C - 4, C + 12, C - 8.5, C + 7.5], color: [178, 96, 52] });

  return mk("Bomb", "Props", "enemy.bomb", 56, 56, shapes);
};

// ── Düşman mermileri — yönsüz parlak yumru ──────────────────────────────────
// EnemyBullet de dönmez. Hasarı düşük olduğu için bombadan belirgin şekilde
// küçük (0.08'e karşı 0.14).

const orb = (name, id, P) => mk(name, "Props", id, 32, 32, [
  { pts: circle(16, 16, 15),   color: [P.rim[0], P.rim[1], P.rim[2], 90] },   // hale
  { pts: circle(16, 16, 11),   color: P.rim },
  { pts: circle(16, 16,  9),   color: P.mid },
  { pts: circle(16, 16,  5.6), color: P.hot },
  { pts: circle(16, 16,  2.8), color: P.core },
]);

// ── Ana silah mermisi — BURUN YUKARI (+Y) ───────────────────────────────────
// WeaponController mermiyi transform.rotation ile doğurur ve Bullet
// transform.up yönünde ilerler; bu tek sprite +Y'ye bakar.

const kineticBullet = () => mk("BulletKinetic", "Props", "player.bullet.kinetic", 40, 120, [
  { pts: [20, 119, 31, 92, 33, 46, 29, 14, 20, 2, 11, 14, 7, 46, 9, 92],  color: KINETC.rim },
  { pts: [20, 115, 28, 92, 30, 46, 26, 17, 20, 7, 14, 17, 10, 46, 12, 92], color: KINETC.mid },
  { pts: [20, 110, 25, 90, 26, 48, 23, 22, 20, 14, 17, 22, 14, 48, 15, 90], color: KINETC.hot },
  { pts: [20, 104, 22, 86, 22, 50, 20, 30, 18, 50, 18, 86], color: KINETC.core },
  // İz — arkada solan kuyruk
  { pts: [16, 14, 24, 14, 21, 0, 19, 0], color: [KINETC.mid[0], KINETC.mid[1], KINETC.mid[2], 120] },
]);

// ── Namlu mermileri — BURUN SAĞA (+X), pivot sol-orta ───────────────────────

/** İzli mermi. turret.bullet gri tonlamalıdır: rengi TurretController verir. */
const tracer = (name, id, P) => mk(name, "Props", id, 32, 16, [
  { pts: [31, 8, 23, 4.4, 7, 5.2, 0, 8, 7, 10.8, 23, 11.6], color: P.rim },
  { pts: [29, 8, 22, 5.4, 8, 6.1, 3, 8, 8, 9.9, 22, 10.6],  color: P.mid },
  { pts: [26, 8, 20, 6.3, 9, 6.9, 6, 8, 9, 9.1, 20, 9.7],   color: P.hot },
  { pts: [22, 8, 17, 7.1, 11, 7.3, 11, 8.7, 17, 8.9],       color: P.core },
]);

// ── Füze (HomingRocket) — BURUN SAĞA, pivot sol-orta ────────────────────────
// Mermiden üç kat uzun; "bu bir füze" bilgisi siluetten okunsun diye burun
// konisi, gövde tüpü ve kanatçıklar ayrı ayrı seçilir.

const rocket = () => {
  const finU = [16, 12, 6, 21, 2, 21, 5, 12];
  return mk("Rocket", "Props", "turret.bullet.homingrocket", 56, 24, [
    { pts: finU,              color: G.lo },
    { pts: mirrorY(finU, 12), color: G.lo },
    // Gövde tüpü + burun konisi
    { pts: [55, 12, 44, 6.5, 6, 6.5, 6, 17.5, 44, 17.5], color: G.mid },
    // Üst kenar aydınlatması / alt gölge
    { pts: [55, 12, 44, 17.5, 6, 17.5, 6, 15.2, 44, 15.2, 50, 12], color: G.hi },
    { pts: [55, 12, 44, 6.5, 6, 6.5, 6, 8.6, 44, 8.6, 50, 12],     color: G.dark },
    // Gövde bandı
    { pts: bar(30, 6.5, 30, 17.5, 2.6), color: G.dark },
    // Egzoz alevi
    { pts: [6, 9.5, 6, 14.5, 0, 12], color: [255, 255, 255, 150] },
  ]);
};

// ── Turret tabanı ve namlusu — gri tonlamalı, kod renklendirir ──────────────

const turretBase = () => {
  const C = 60;
  const s = [
    { pts: circle(C, C, 58, 8), color: G.dark },
    { pts: circle(C, C, 50, 8), color: G.lo },
    { pts: circle(C, C, 40, 8), color: G.mid },
    // Omuz halkası
    { pts: circle(C, C, 27), color: G.dark },
    { pts: circle(C, C, 23), color: G.hi },
    { pts: circle(C, C, 15), color: G.lo },
  ];
  // Cıvatalar
  for (let i = 0; i < 8; i++) {
    const a = (i / 8) * Math.PI * 2 + Math.PI / 8;
    s.push({ pts: circle(C + Math.cos(a) * 45, C + Math.sin(a) * 45, 4.2, 10), color: G.dark });
  }
  return mk("TurretBase", "Props", "turret.base", 120, 120, s);
};

const turretBarrel = () => mk("TurretBarrel", "Props", "turret.barrel", 80, 32, [
  { pts: [0, 4, 26, 4, 26, 28, 0, 28],       color: G.lo },    // kök bloğu
  { pts: [22, 10, 72, 10, 72, 22, 22, 22],   color: G.mid },   // namlu tüpü
  { pts: [70, 7, 79, 7, 79, 25, 70, 25],     color: G.hi },    // ağız bileziği
  { pts: [22, 20, 72, 20, 72, 22, 22, 22],   color: G.hi },
  { pts: [22, 10, 72, 10, 72, 12, 22, 12],   color: G.dark },
  { pts: [0, 4, 26, 4, 26, 8, 0, 8],         color: G.dark },
]);

// ── Avcı uçağı — BURUN SAĞA ─────────────────────────────────────────────────

const fighter = () => {
  const CY = 20, H = [217, 191, 51], D = [138, 118, 24], L = [255, 240, 150];
  const wing = [58, 22, 22, 38, 10, 38, 12, 30, 30, 22];
  return mk("Fighter", "Player", "player.fighter", 88, 40, [
    { pts: wing,              color: D },
    { pts: mirrorY(wing, CY), color: D },
    { pts: [86, 20, 66, 14, 34, 12, 12, 15, 4, 20, 12, 25, 34, 28, 66, 26], color: H },
    { pts: [86, 20, 66, 26, 34, 28, 12, 25, 12, 22.5, 34, 25, 66, 23],      color: L },
    { pts: [6, 16, 20, 16, 20, 24, 6, 24], color: D },                       // motor
    { pts: [70, 20, 58, 17, 44, 18, 44, 22, 58, 23], color: [102, 224, 255] }, // kokpit
  ]);
};

// ── Toplayıcı — BURUN SAĞA, önde toplama kepçesi ────────────────────────────

const collector = () => {
  const CY = 24, H = [77, 191, 102], D = [42, 112, 58], L = [168, 240, 186];
  const scoopU = [94, 44, 66, 30, 66, 24, 78, 24, 94, 36];
  return mk("Collector", "Player", "player.collector", 96, 48, [
    { pts: scoopU,              color: D },
    { pts: mirrorY(scoopU, CY), color: D },
    { pts: [78, 24, 62, 12, 14, 10, 4, 20, 4, 28, 14, 38, 62, 36], color: H },
    { pts: [78, 24, 62, 36, 14, 38, 4, 28, 4, 25.5, 14, 35, 62, 33], color: L },
    { pts: [6, 17, 22, 17, 22, 31, 6, 31], color: D },                       // kargo ambarı
    { pts: [56, 24, 44, 19, 30, 20, 30, 28, 44, 29], color: [102, 224, 255] },
  ]);
};

// ── Hangar gövdesi ──────────────────────────────────────────────────────────

const hangar = () => {
  const H = [77, 115, 153], D = [40, 62, 88], L = [150, 190, 226];
  return mk("HangarBody", "Player", "player.hangar", 144, 96, [
    { pts: [4, 10, 140, 10, 140, 86, 4, 86], color: D },
    { pts: [8, 14, 136, 14, 136, 82, 8, 82], color: H },
    { pts: [8, 74, 136, 74, 136, 82, 8, 82], color: L },        // üst aydınlatma
    // Fırlatma ağzı — sağa açılır (avcılar +X'e çıkar)
    { pts: [100, 26, 136, 26, 136, 70, 100, 70], color: D },
    { pts: [108, 32, 136, 32, 136, 64, 108, 64], color: [16, 24, 34] },
    { pts: bar(112, 48, 134, 48, 5), color: [102, 224, 255] },  // ışık şeridi
    // Panel çizgileri
    { pts: bar(28, 14, 28, 82, 4), color: D },
    { pts: bar(56, 14, 56, 82, 4), color: D },
    { pts: bar(84, 14, 84, 82, 4), color: D },
  ]);
};

// ── Vurulabilir işareti — köşe parantezleri, beyaz, kod renklendirir ────────
// Bomba gibi vurulabilir mühimmatın etrafında yanıp söner. Halka DEĞİL
// parantez: halka bir kalkan/aura okuması yaratıyor, parantez evrensel
// "nişan alınabilir" dili.

const shootableFrame = () => {
  const S = 120, T = 9, A = 38, I = 5, s = [];
  const corner = (x, y, sx, sy) => {
    s.push({ pts: bar(x, y, x + sx * A, y, T), color: [255, 255, 255] });
    s.push({ pts: bar(x, y, x, y + sy * A, T), color: [255, 255, 255] });
  };
  corner(I + T / 2,     I + T / 2,      1,  1);
  corner(S - I - T / 2, I + T / 2,     -1,  1);
  corner(I + T / 2,     S - I - T / 2,  1, -1);
  corner(S - I - T / 2, S - I - T / 2, -1, -1);
  return mk("ShootableFrame", "Props", "fx.shootable", S, S, s);
};

// ── Enkaz ───────────────────────────────────────────────────────────────────
// Gri tonlamalı: rengi Debris kaynak tipinden verir (ham madde kahverengi,
// kristal camgöbeği). İki AYRI aile var:
//
//   Gemi enkazı  — gemi parçasına benzeyen çizgiler/plakalar + şekilsiz silik
//                  lekeler. Kırılmış bir şeyin geride bıraktığı okunmalı.
//   Kaya enkazı  — YALNIZCA şekilsiz silik lekeler. Kayanın parçası kayadır,
//                  düz kenar ya da perçin taşımaz.
//
// Enkaz ekranda ~9 piksel çizilir; ayırt edici olan detay değil SİLUETTİR,
// o yüzden şekiller iri ve az sayıdadır. Varyant seçimi runtime'da rastgele,
// üretim ise tohumlu — aynı 10 sprite her derlemede aynı çıkar.

// Enkaz tasarim tuvali. Sekiller bu olcude cizilir, sonra DebrisScale ile
// buyutulur — koordinatlari elle yeniden yazmak yerine tek sayidan olceklemek
// oranlari bozmadan boyut denemesi yapmayi mumkun kiliyor.
const DW = 48, DH = 40;

/**
 * Enkaz oyunda cok kucuk kaliyordu: 0.12 birim ~= 9 ekran pikseli, yani
 * silueti okunmadan once toplanip gidiyordu. %50 buyutuldu -> 0.18 birim.
 * PPU 400 sabit kalir (4x piksel yogunlugu kurali), buyume TUVALDEN gelir.
 */
const DebrisScale = 1.5;

/** Bir sekil listesinin tum koordinatlarini olcekler. */
const scaled = (shapes, k) => shapes.map(s =>
  Object.assign({}, s, { pts: s.pts.map(v => v * k) }));

/** Arkaya serpilen silik toz lekeleri — iki ailede de ortak zemin. */
const haze = (seed, n, cx, cy, r) => {
  const R = rnd(seed), out = [];
  for (let i = 0; i < n; i++) {
    const a = R() * Math.PI * 2, d = R() * r * 0.6;
    out.push({
      pts:   blob(cx + Math.cos(a) * d, cy + Math.sin(a) * d,
                  r * (0.45 + R() * 0.45), seed + i * 37, 0.5),
      color: [G.lo[0], G.lo[1], G.lo[2], 46 + Math.floor(R() * 40)],
    });
  }
  return out;
};

const ERASE = { mode: "erase", color: [0, 0, 0, 0] };
const cut   = pts => Object.assign({ pts }, ERASE);

const shipDebris = [
  // 0 — L köşe plakası: bir gövde panelinin kırık köşesi
  () => [...haze(101, 3, 24, 20, 15),
         { pts: [8, 8, 34, 12, 33, 19, 17, 17, 16, 32, 9, 31], color: G.mid },
         { pts: [8, 8, 34, 12, 34, 14, 9, 11],                 color: G.hi },
         { pts: [16, 17, 17, 32, 12, 31, 12, 16],              color: G.dark }],

  // 1 — kirişin kopmuş bölümü, iki ucunda bağlantı topuzu
  () => [...haze(202, 2, 24, 20, 14),
         { pts: bar(9, 13, 39, 27, 7),   color: G.mid },
         { pts: bar(9, 15, 39, 29, 2.6), color: G.hi },
         { pts: circle(10, 13, 5.2, 12), color: G.lo },
         { pts: circle(38, 27, 4.4, 12), color: G.lo }],

  // 2 — kanat/dümen ucu: üçgen bir dilim
  () => [...haze(303, 3, 22, 20, 15),
         { pts: [42, 30, 12, 22, 8, 11, 26, 14],  color: G.mid },
         { pts: [42, 30, 12, 22, 14, 25, 40, 31], color: G.hi },
         { pts: [8, 11, 26, 14, 24, 17, 11, 15],  color: G.dark }],

  // 3 — perçinli bükülmüş panel
  () => [...haze(404, 2, 24, 20, 14),
         { pts: [10, 26, 24, 32, 40, 24, 36, 14, 20, 10, 11, 16], color: G.mid },
         { pts: [10, 26, 24, 32, 40, 24, 38, 21, 24, 28, 12, 23], color: G.hi },
         { pts: circle(19, 21, 2.6, 10), color: G.dark },
         { pts: circle(28, 24, 2.6, 10), color: G.dark },
         { pts: circle(33, 18, 2.4, 10), color: G.dark }],

  // 4 — halka çerçevenin bir yayı (kaportanın kalıntısı). Delik ve kesikler
  //     "erase" ile açılır: even-odd dolgu gerçek delik açamıyor.
  () => [...haze(505, 3, 24, 20, 15),
         { pts: circle(24, 20, 17, 26), color: G.mid },
         cut(circle(24, 20, 12, 26)),
         cut([24, 20, 48, 2, 48, 40, 24, 40]),
         cut([24, 20, 0, 34, 0, 40, 24, 40]),
         { pts: bar(24, 3, 24, 9, 6), color: G.hi }],

  // 5 — çapraz iki kiriş, birinin ucu kopuk
  () => [...haze(606, 2, 24, 20, 14),
         { pts: bar(8, 30, 38, 12, 5.5), color: G.lo },
         { pts: bar(13, 9, 36, 31, 5),   color: G.mid },
         { pts: bar(14, 11, 34, 30, 1.8), color: G.hi },
         { pts: circle(21, 21, 4.6, 12),  color: G.mid }],
];

const rockDebris = [
  // Yalnızca leke — hiçbirinde düz kenar, plaka ya da simetri yok
  () => [...haze(701, 4, 24, 20, 17),
         { pts: blob(23, 20, 13, 711, 0.42), color: [G.mid[0], G.mid[1], G.mid[2], 165] },
         { pts: blob(21, 22,  7, 712, 0.50), color: [G.hi[0],  G.hi[1],  G.hi[2],  115] }],
  () => [...haze(802, 4, 25, 19, 16),
         { pts: blob(26, 19, 12, 811, 0.48), color: [G.mid[0], G.mid[1], G.mid[2], 150] },
         { pts: blob(29, 17,  6, 812, 0.55), color: [G.hi[0],  G.hi[1],  G.hi[2],  100] }],
  () => [...haze(903, 5, 23, 21, 17),
         { pts: blob(22, 21, 14, 911, 0.36), color: [G.mid[0], G.mid[1], G.mid[2], 140] },
         { pts: blob(19, 18,  6, 912, 0.60), color: [G.hi[0],  G.hi[1],  G.hi[2],  108] }],
  () => [...haze(1004, 4, 24, 20, 16),
         { pts: blob(24, 20, 11, 1011, 0.55), color: [G.mid[0], G.mid[1], G.mid[2], 172] },
         { pts: blob(27, 23,  5, 1012, 0.45), color: [G.hi[0],  G.hi[1],  G.hi[2],  120] }],
];

const debris = () => {
  const W = Math.round(DW * DebrisScale), H = Math.round(DH * DebrisScale);
  const out = [];
  shipDebris.forEach((f, i) =>
    out.push(mk("DebrisShip" + i, "Props", "world.debris.ship." + i,
                W, H, scaled(f(), DebrisScale))));
  rockDebris.forEach((f, i) =>
    out.push(mk("DebrisRock" + i, "Props", "world.debris.rock." + i,
                W, H, scaled(f(), DebrisScale))));
  return out;
};

// ── Dışa aktarım ────────────────────────────────────────────────────────────

module.exports = {
  all: () => [
    bomb(),
    orb("BulletEnemy",     "enemy.bullet.hull",      ORANGE),
    orb("BulletComponent", "enemy.bullet.component", PURPLE),
    kineticBullet(),
    tracer("BulletTurret",  "turret.bullet",         GREY),
    tracer("BulletFighter", "player.fighter.bullet", YELLOW),
    rocket(),
    turretBase(), turretBarrel(),
    fighter(), collector(), hangar(),
    shootableFrame(),
    ...debris(),
  ],

  // Enkaz varyant sayıları — SkinId sabitleriyle elle senkron tutulur
  DEBRIS_SHIP: shipDebris.length,
  DEBRIS_ROCK: rockDebris.length,
};
