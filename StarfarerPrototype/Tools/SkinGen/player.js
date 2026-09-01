// Oyuncu tarafi. Ana gemi SABIT durur ve donmez; dusmanlar SAGDAN gelir.
// Bu yuzden gemi yandan gorunumdur: iticiler solda, kopru/burun sagda.
// Tuval bodyWidth x bodyHeight'in 4 kati (PPU 400).

const { pal, u } = require("./palette");

// ── Cizim yardimcilari ─────────────────────────────────────────────────────
const box = (x, y, w, h, color) =>
  ({ pts: [x, y, x + w, y, x + w, y + h, x, y + h], color });

/** x0'dan x1'e adim adim ayni kutudan dizer - pencere ve isik siralari icin. */
const strip = (y, x0, x1, step, w, h, color) => {
  const out = [];
  for (let x = x0; x <= x1; x += step) out.push(box(x, y, w, h, color));
  return out;
};

// PlayerShip - 400x240 birim -> 1600x960 tuval, PPU 400.
//
// ── Neden bu boyut ───────────────────────────────────────────────────────
// Gemi eskiden 4x1 birimdi ve slot izgarasi govdenin TAMAMEN DISINDA,
// boslukta asili duruyordu: slotlar dunya y = +-0.8'de, komponent ikonu ise
// 0.35 birim capinda (ShipComponentBase.k_ringSize), yani 0.975'e kadar
// uzaniyor - govde 0.5'te bitiyordu.
//
// Ust sinir kalkan kuresidir: ShieldEffect.ShieldRadius = 2.5, govdenin
// yari-kosegeni sqrt(2.0^2 + 1.2^2) = 2.33. Daha buyuk bir gemi kalkanin
// disina tasardi.
//
// ── Neden dikdortgen DEGIL ───────────────────────────────────────────────
// Slotlari eski izgara konumlarinda birakip govdeyi buyutmek denendi: dort
// kosede de tam yukseklik gerektiginden siluet zorunlu olarak tuglaya donuyor,
// pruva kamasi / kic basamagi / yukselen guverte hatti hicbiri yapilamiyordu.
// Cozum ters yonden geldi - SLOTLAR govdeyi takip ediyor:
//
//   kic makine blogu (kalin, tam yukseklik)  -> slot 0, 3, 7
//   sirt kulesi (govdeden yukselen yapi)     -> slot 1 (ana silah), 4
//   bel govdesi (dar, iki omuzla baglanir)   -> slot 5, 8
//   karin hangar modulu (bindirme agizlari)  -> slot 6
//   bas kesimi (kopru + kamali pruva)        -> slot 2, 9
//
// Konumlar PlayerShip.slotPositions ile BIREBIR eslesir; biri degisirse
// digeri de degismeli. Tuval <-> dunya: canvas = (800 + 400x, 480 + 400y).
//
// ── Neden yan profil ─────────────────────────────────────────────────────
// Ilk surum iki ucu da sivrilen bir mercekti ve ustunde bastan basa genis bir
// acik band vardi; ikisi de bir govdenin PLAN gorunumunun isaretidir ve gemi
// deniz gemisi gibi okunuyordu. Yandan bakan bir gemi dikeyde simetrik
// degildir: ustte kule, altta hangar modulu, kicta iticiler, burunda mahmuz.
//
// Denenip BIRAKILAN: guvertede dikilen ayri radyator kanatciklari, dik sensor
// diregi ve tum govdeyi kaplayan dikey cerceve izgarasi. Ilk ikisi BACA gibi
// okunuyordu, ucuncusu silueti konteynere ceviriyordu.
const playerBody = () => {
  const p = pal(u(0.30, 0.30, 0.40), u(0.24, 0.24, 0.33));

  // Kucuk isiklar. Statik sprite oldugu icin "yaniyor" hissi renkten gelir:
  // camgobegi pencere, kehribar seyir isigi, uclarda kirmizi/yesil borda.
  const AMBER  = [255, 198, 104];
  const NAVR   = [255,  96,  96];
  const NAVG   = [116, 255, 158];
  const PLASMA = [140, 225, 255];
  const HOT    = [228, 248, 255];
  const VOID   = [ 18,  20,  30];   // hangar agzinin ici

  // Iticiler: bogaz kic duvarinda (x=150), agiz solda acilir. Dort tane -
  // 810 piksellik kic duvari tek buyuk lule ile bos kalirdi.
  const bell  = cy => [150, cy + 70, 90, cy + 86, 20, cy + 94,
                        20, cy - 94, 90, cy - 86, 150, cy - 70];
  const flame = cy => [26, cy + 80, 76, cy + 68, 76, cy - 68, 26, cy - 80];
  const core  = cy => [30, cy + 56, 60, cy + 46, 60, cy - 46, 30, cy - 56];
  const THRUSTERS = [190, 385, 580, 775];

  // Hangar bindirme agzi: govdenin altinda oyuk. Karanlik ic + tavan isigi +
  // agiz dudaginda yaklasma isiklari. Slot 6'nin ikonu solda durdugu icin
  // agizlar sagda toplandi, yoksa ikonun altinda kalirlardi.
  const bay = x => [
    box(x,      54, 126, 88, p.dark),
    box(x + 10, 62, 106, 72, VOID),
    box(x + 10, 116, 106, 12, PLASMA),
    box(x +  4, 44,  14, 10, AMBER),
    box(x + 108, 44, 14, 10, AMBER),
  ];

  return {
    name: "PlayerBody", dir: "Player", w: 1600, h: 960, ppu: 400,
    shapes: [
      // ── Iticiler - govdenin ARKASINDA kalsin diye once cizilir ──────────
      ...THRUSTERS.map(cy => ({ pts: bell(cy),  color: p.dark })),
      ...THRUSTERS.map(cy => ({ pts: flame(cy), color: PLASMA })),
      ...THRUSTERS.map(cy => ({ pts: core(cy),  color: HOT })),

      // ── Karin hangar modulu - bel govdesinin altina asilir ──────────────
      { pts: [500, 158, 1060, 158, 1026, 46, 534, 46], color: p.wing },

      // ── Ana govde ───────────────────────────────────────────────────────
      // Kalin kic + iki omuzla daralan bel + kamali pruva.
      { pts: [1590, 502,                        // mahmuz ucu (ust)
              1478, 640, 1320, 700, 1150, 706,  // burun kamasi (ust)
              1010, 698,  620, 690,             // bel guvertesi
               470, 890,  150, 890,             // kic omzu + kic blogu tepesi
               150,  80,  470,  80,             // kic duvari + blok tabani
               620, 156, 1010, 156,             // kic omzu + bel omurgasi
              1150, 160, 1320, 202, 1478, 306,  // burun kamasi (alt)
              1590, 434], color: p.hull },      // mahmuz ucu (alt)

      // Itici montaj plakasi
      box(112, 90, 42, 790, p.dark),

      // ── Kic makine blogu - ayri bir kutle olarak okunsun diye acik ton ──
      { pts: [150, 890, 470, 890, 620, 690, 620, 156, 470, 80, 150, 80],
        color: p.trim },
      ...strip(600, 176, 424, 84, 50, 28, p.eye),   // ust sira: bel ile AYNI y
      ...strip(300, 176, 424, 84, 50, 28, p.eye),
      ...strip(864, 176, 440, 62, 14, 10, AMBER),
      box(166, 852, 20, 20, NAVR),
      box(166,  92, 20, 20, NAVR),

      // ── Sirt kulesi - ana silah (slot 1) ve slot 4 burada ───────────────
      // Ic panel govdeden ACIK tutulur. Koyu denendi ve govdede delik gibi
      // okundu: yukselen bir yapi degil, oyuk gibi.
      { pts: [520, 686, 520, 880, 570, 928, 910, 928, 1010, 862, 1030, 686],
        color: p.trim },
      // Ic panel govde tonunda: acik cerceve icinde ICERI CEKILMIS yuzey.
      // p.light denendi, kule gemideki en parlak seye donup kopruyle
      // yarisiyordu - parlak olan tek yer kopru olmali.
      { pts: [556, 692, 556, 872, 592, 914, 900, 914, 988, 856, 1000, 692],
        color: p.hull },
      ...strip(700, 580, 960, 76, 50, 30, p.eye),   // kule tabani pencereleri
      ...strip(906, 612, 880, 68, 46, 14, AMBER),
      box(586, 932, 18, 18, NAVR),

      // ── Bel govdesi ─────────────────────────────────────────────────────
      // Yatay dikis + uzerine dizilmis isik sirasi denendi ve CIT gibi
      // okundu; pencere siralari tek basina uzunlugu zaten veriyor.
      ...strip(600, 660, 1120, 92, 50, 28, p.eye),
      ...strip(210, 470, 1120, 92, 50, 28, p.eye),

      // ── Hangar bindirme agizlari ────────────────────────────────────────
      ...bay(740),
      ...bay(886),

      // ── Bas kesimi: kopru ───────────────────────────────────────────────
      // Guverteye oturan bir boyunla baglanir; havada duran bir blok
      // govdeye ait gorunmuyordu.
      { pts: [1046, 700, 1046, 776, 1094, 824, 1236, 824, 1330, 742, 1340, 700],
        color: p.trim },
      { pts: [1074, 704, 1074, 770, 1108, 810, 1228, 810, 1306, 742, 1312, 704],
        color: p.light },
      { pts: [1232, 816, 1322, 738, 1314, 716, 1224, 792], color: p.eye },
      { pts: [1238, 804, 1310, 740, 1306, 728, 1234, 786], color: p.eyeIn },
      ...strip(756, 1096, 1196, 52, 40, 26, p.eye),
      box(1160, 828, 18, 16, NAVG),
      ...strip(676, 1350, 1420, 68, 14, 10, AMBER),
      ...strip(420, 1180, 1400, 80, 44, 26, p.eye),

      // ── Zirhli mahmuz + on sensor ───────────────────────────────────────
      // Govdeden KOYU: dovulmus zirh plakasi. Acik denendi, burun govdenin
      // devami gibi okunup kama etkisini yiyordu.
      { pts: [1590, 494, 1478, 634, 1400, 664,
              1400, 266, 1478, 314, 1590, 442], color: p.wing },
      box(1398, 266, 14, 398, p.dark),
      { pts: [1560, 500, 1490, 480, 1490, 424, 1560, 404], color: p.eye },
      { pts: [1546, 490, 1502, 476, 1502, 434, 1546, 420], color: p.eyeIn },
    ],
    // Olculen dikdortgen ~4x2.2 birime cikiyor (eskiden 4x0.63). Gemi artik
    // gercekten o kadar buyuk cizildigi icin hitbox'in silueti izlemesi
    // gerekiyor: gorunur govdesine isabet eden merminin saymamasi daha kotu
    // olurdu. 0.90 ile hitbox siluetin bir tik ICINDE kalir - CLAUDE.md'deki
    // "oyuncuya gelen hedefte kil payi oyuncunun lehine" kurali.
    //
    // DIKKAT: bu bir DENGE degisikligidir. Kalkan kapaliyken govdenin kesit
    // alani ~3x buyudu. Kalkan acikken fark yok - EnemyBullet once 2.5 birimlik
    // kalkan kuresine carpiyor, govde collider'ina hic ulasmiyor.
    skin: { id: "player.body", colliderMode: "Box", hitboxScale: 0.90 },
  };
};

// PlayerBarrel - 20x80 birim -> 80x320 tuval.
// DIKKAT: pivot alt-merkez (0.5, 0), namlu YUKARI uzanir. Diger tum
// spritelarin aksine bu dikeydir - WeaponMount mouse'a dogru dondurur.
const playerBarrel = () => {
  const p = pal(u(1.0, 0.92, 0.0), u(0.80, 0.72, 0.0));

  return {
    name: "PlayerBarrel", dir: "Player", w: 80, h: 320, ppu: 400,
    shapes: [
      // Kaide - mount noktasinda genis, yukari daralir
      { pts: [12, 0, 68, 0, 62, 60, 18, 60], color: p.wing },
      // Namlu govdesi
      { pts: [22, 55, 58, 55, 54, 280, 26, 280], color: p.hull },
      // Sol kenar golgesi - silindir hissi
      { pts: [22, 55, 32, 55, 30, 280, 26, 280], color: p.dark },
      // Sag kenar isigi
      { pts: [50, 55, 58, 55, 54, 280, 50, 280], color: p.light },
      // Agiz freni
      { pts: [18, 280, 62, 280, 60, 312, 20, 312], color: p.wing },
      { pts: [28, 292, 52, 292, 51, 306, 29, 306], color: p.dark },
    ],
    skin: { id: "player.barrel", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

module.exports = { playerBody, playerBarrel };
