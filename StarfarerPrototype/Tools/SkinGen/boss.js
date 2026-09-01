// Boss gövdesi ve hardpoint'leri. Koordinat: y YUKARI, origin sol-alt,
// BURUN SAĞA (+X). Tuval = boyutun 4 katı, PPU 400.
//
// TEK GÖVDE, 10 BOSS. BossShip zaten iki kademeli anahtar kullanıyor:
// önce "boss.<ad>", bulamazsa "boss.body". Her bölüm için ayrı bir kapital
// gemi çizmek yerine paylaşılan gövde kaydediliyor ve rengi koddan geliyor
// (sprite GRİ TONLAMALI, sr.color çarpar) — turret parçalarındaki mantığın
// aynısı. Bir bölümün boss'u gerçekten farklı görünmeliyse "boss.<ad>"
// eklemek yeterli, bu dosyaya dokunmadan üstüne yazar.
//
// Oran her bölümde AYNI: bodyWidth = 200 + bölüm×6, bodyHeight = 100 + bölüm×3,
// yani tam 2:1. Bu yüzden tek bir 2:1 tuval bütün bosslara UNIFORM ölçekle
// oturuyor (FitToSize).
//
// Hitbox DEĞİŞMEZ: BossShip collider'ı "boss.<ad>" anahtarıyla arıyor, o da
// kayıtlı değil — veri kaynaklı kutu yerinde kalıyor. Görsel geldi diye
// vurma zorluğu kaymasın.

const { mirrorY } = require("./raster");

const G = {
  hi:   [246, 248, 252],
  mid:  [188, 192, 202],
  lo:   [126, 130, 142],
  dark: [ 68,  72,  84],
  void_:[ 26,  28,  36],
};

const CYAN = [102, 224, 255];

const bar = (x0, y0, x1, y1, t) => {
  const dx = x1 - x0, dy = y1 - y0;
  const L  = Math.hypot(dx, dy) || 1;
  const nx = (-dy / L) * t * 0.5, ny = (dx / L) * t * 0.5;
  return [x0 + nx, y0 + ny, x1 + nx, y1 + ny, x1 - nx, y1 - ny, x0 - nx, y0 - ny];
};

const box = (x0, y0, x1, y1) => [x0, y0, x1, y0, x1, y1, x0, y1];

const circle = (cx, cy, r, n = 28) => {
  const p = [];
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2;
    p.push(cx + Math.cos(a) * r, cy + Math.sin(a) * r);
  }
  return p;
};

const mk = (name, id, w, h, shapes) => ({
  name, dir: "Boss", w, h, ppu: 400, shapes,
  skin: { id, colliderMode: "Box", hitboxScale: 1.0 },
});

/**
 * Sekilleri baska bir tuvale tasir. Hardpoint tuvali verideki olcunun TAM
 * 4 kati olmak ZORUNDA: BossHardpoint FitToSize cagiriyor ve o transform
 * collider'i da tasiyor - oran tutmazsa gorsel duzelirken HITBOX kayar.
 */
const fit = (shapes, fromW, fromH, toW, toH) =>
  shapes.map(s => Object.assign({}, s, {
    pts: s.pts.map((v, i) => i % 2 === 0 ? v * toW / fromW : v * toH / fromH),
  }));

// ── Gövde ───────────────────────────────────────────────────────────────────
// Siluet KADEMELİ: ana zırh gövdesi + sırt üstyapısı + köprü kulesi + karın
// hangar ağzı + yan top sponsonları. Düz bir kutu ya da yumuşak bir mercek
// istemiyoruz — ikisi de küçük ölçekte "dikdörtgen" okuyor. Kademeler
// siluetin dış hattını kırıyor ve gemiyi devasa gösteren tek şey o.
//
// Hardpoint'ler localOffset × (yarıGenişlik, yarıYükseklik) ile yerleşiyor:
// toplar x = +0.55 (tuvalde 768), kalkan jeneratörü x = -0.20 (tuvalde 397).
// Gövde tam o bantlarda dolu olacak şekilde çizildi, yoksa hardpoint boşlukta
// asılı kalırdı.

const W = 992, H = 496, CY = 248;

const bossBody = () => {
  const s = [];

  // Sponson platformlari (toplarin oturdugu yan kanatlar) - govdeden ONCE
  const sponsonU = box(690, 320, 830, 372);
  s.push({ pts: sponsonU,          color: G.lo });
  s.push({ pts: mirrorY(sponsonU, CY), color: G.lo });

  // Motor nozullari - kuyruktan sola tasar
  for (let i = 0; i < 4; i++) {
    const y = 176 + i * 48;
    s.push({ pts: box(4, y, 44, y + 34), color: G.dark });
    s.push({ pts: box(8, y + 6, 30, y + 28), color: G.void_ });
  }

  // Karin hangar agzi
  s.push({ pts: box(300, 108, 660, 178), color: G.lo });
  s.push({ pts: box(340, 116, 620, 160), color: G.void_ });
  s.push({ pts: bar(360, 138, 600, 138, 10), color: CYAN });

  // Sirt ustyapisi ve kopru kulesi
  // Ustyapi 660de biter: 700e kadar uzatilinca ust sponsonu ortuyor ve
  // gemi ust/alt asimetrik gorunuyordu.
  s.push({ pts: box(250, 318, 660, 384), color: G.lo });
  s.push({ pts: box(250, 372, 660, 384), color: G.mid });
  s.push({ pts: [390, 384, 530, 384, 508, 442, 412, 442], color: G.mid });
  s.push({ pts: [404, 396, 516, 396, 500, 430, 420, 430], color: G.void_ });
  s.push({ pts: bar(420, 413, 500, 413, 14), color: CYAN });

  // ANA ZIRH GOVDESI - pruva kamasi sagda
  s.push({ pts: [984, 248, 900, 190, 830, 172, 120, 176,
                  60, 200,  44, 248,  60, 296, 120, 320,
                 830, 324, 900, 306], color: G.mid });

  // Ust kenar isigi / alt golge - hacim
  s.push({ pts: [984, 248, 900, 306, 830, 324, 120, 320, 120, 306,
                 830, 310, 900, 292], color: G.hi });
  s.push({ pts: [984, 248, 900, 190, 830, 172, 120, 176, 120, 190,
                 830, 186, 900, 204], color: G.dark });

  // Motor blogu - govdenin ICINDE
  s.push({ pts: box(44, 196, 150, 300), color: G.dark });

  // Zirh dikis cizgileri. YALNIZCA ust yarida ve UC tane: bes dikey cizgi
  // + boydan boya omurga bir IZGARA olusturuyor ve siluet gemi degil
  // konteyner okuyordu (ana gemide de aynisi denenip birakilmisti).
  for (const x of [300, 480, 660]) {
    s.push({ pts: bar(x, 256, x, 316, 8), color: G.dark });
  }
  // Omurga
  s.push({ pts: [150, 244, 880, 244, 900, 248, 880, 252, 150, 252], color: G.dark });

  // Pruva mahmuzu - govdeden KOYU: dovulmus zirh plakasi
  s.push({ pts: [984, 248, 906, 208, 862, 214, 862, 282, 906, 288], color: G.dark });
  s.push({ pts: [962, 248, 908, 224, 884, 228, 884, 268, 908, 272], color: G.lo });

  // Sensor lensi - "bot" okumasi butun gemilerde ayni yerden gelir
  s.push({ pts: [846, 248, 812, 226, 762, 232, 762, 264, 812, 270], color: CYAN });

  return mk("BossBody", "boss.body", W, H, s);
};

// ── Hardpoint'ler ───────────────────────────────────────────────────────────
// Hepsi GRİ TONLAMALI: rengi BossHardpoint definition.color ile verir, yani
// aynı tip farklı bosslarda o boss'un paletinde çıkar.
//
// Tuval her tipin kendi ölçüsünün tam 4 katı — FitToSize böylece 1.0 ölçek
// üretir ve hardpoint verideki boyutunda kalır.

// Top: taban + one bakan cift namlu (28x20)
const hpCannon = () => mk("HpCannon", "boss.hardpoint.cannon", 112, 80, [
  { pts: box(4, 14, 74, 66),   color: G.lo },
  { pts: box(4, 56, 74, 66),   color: G.mid },
  { pts: box(60, 46, 108, 60), color: G.mid },
  { pts: box(60, 20, 108, 34), color: G.mid },
  { pts: box(100, 44, 110, 62), color: G.hi },
  { pts: box(100, 18, 110, 36), color: G.hi },
  { pts: box(12, 24, 34, 56),  color: G.dark },
]);

// Lazer: emitor blogu + lens (28x20)
const hpLaser = () => mk("HpLaser", "boss.hardpoint.laser", 112, 80, [
  { pts: box(4, 12, 68, 68),  color: G.lo },
  { pts: box(4, 58, 68, 68),  color: G.mid },
  { pts: [68, 22, 96, 32, 96, 48, 68, 58], color: G.mid },
  { pts: circle(98, 40, 12),  color: G.hi },
  { pts: circle(98, 40, 6),   color: CYAN },
  { pts: bar(16, 20, 16, 60, 10), color: G.dark },
]);

// Kalkan jeneratoru: halka + cekirdek (36x36)
const hpShieldGen = () => mk("HpShieldGen", "boss.hardpoint.shieldgenerator", 144, 144, [
  { pts: box(10, 10, 134, 134), color: G.dark },
  { pts: box(18, 18, 126, 126), color: G.lo },
  { pts: circle(72, 72, 50),    color: G.dark },
  { pts: circle(72, 72, 42),    color: G.mid },
  { pts: circle(72, 72, 30),    color: G.hi },
  { pts: circle(72, 72, 20),    color: CYAN },
  { pts: bar(72, 22, 72, 122, 8), color: G.dark },
  { pts: bar(22, 72, 122, 72, 8), color: G.dark },
]);

// Drone hangari: acik kapaklar + icinde kucuk gemi (veride 40x30)
const hpDroneBay = () => mk("HpDroneBay", "boss.hardpoint.dronebay", 160, 120,
  fit([
  { pts: box(6, 8, 130, 104),   color: G.lo },
  { pts: box(6, 92, 130, 104),  color: G.mid },
  { pts: box(22, 22, 124, 90),  color: G.void_ },
  { pts: box(6, 22, 24, 90),    color: G.dark },
  // Ic hangardaki kucuk gemi
  { pts: [110, 56, 88, 44, 56, 42, 40, 50, 40, 62, 56, 70, 88, 68], color: G.mid },
  { pts: [110, 56, 88, 68, 56, 70, 40, 62, 40, 59, 56, 66, 88, 64], color: G.hi },
  { pts: bar(28, 26, 28, 86, 8), color: CYAN },
], 136, 112, 160, 120));

// Onarim hangari: kollar + carpi isareti (veride 34x28)
const hpRepairBay = () => mk("HpRepairBay", "boss.hardpoint.repairbay", 136, 112,
  fit([
  { pts: box(6, 10, 154, 110),  color: G.lo },
  { pts: box(6, 98, 154, 110),  color: G.mid },
  { pts: box(20, 24, 140, 94),  color: G.dark },
  // Onarim kolu
  { pts: bar(34, 40, 96, 78, 12), color: G.mid },
  { pts: bar(96, 78, 132, 46, 12), color: G.mid },
  { pts: circle(96, 78, 11),      color: G.hi },
  { pts: circle(132, 46, 9),      color: CYAN },
  { pts: bar(34, 40, 34, 40, 18), color: G.lo },
], 160, 120, 136, 112));

module.exports = {
  all: () => [bossBody(), hpCannon(), hpLaser(), hpShieldGen(), hpDroneBay(), hpRepairBay()],
};
