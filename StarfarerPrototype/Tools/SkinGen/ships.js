// Gemi siluetleri. Koordinat: y YUKARI, origin sol-alt, BURUN SAĞA (+X).
// Oyunda gemi Euler(0,0,facingAngle) ile döner ve varsayılan facing 180° —
// yani sağa bakan sprite oyunda sola, oyuncuya döner.
//
// Tuval = bodyWidth x bodyHeight'ın 4 katı; import'ta PPU 400 verilir, böylece
// dünya boyutu birebir aynı kalır ama piksel yoğunluğu 4x olur.

const { mirrorY } = require("./raster");

// EnemyTypeData renkleri (0..1 -> 0..255)
const C = {
  hull:  [230,  51,  51],   // bodyColor   (0.9, 0.20, 0.20)
  wing:  [179,  38,  38],   // barrelColor (0.7, 0.15, 0.15)
  dark:  [122,  26,  26],
  light: [255, 120, 105],
  eye:   [102, 224, 255],
  eyeIn: [200, 245, 255],
};

const swarm = () => {
  const CY = 40;

  // Tam boy delta kanat. Kanadı arka yarıya sıkıştırmak siluetin ön yarısını
  // bomboş bırakıyordu; hücum kenarı buruna kadar geldiğinde hem daha hızlı
  // bir gemi okunuyor hem de kutunun doluluğu artıyor.
  const wingUpper = [208,43, 36,77, 20,77, 20,68, 28,43];

  return {
    name: "Swarm",
    w: 240, h: 80, ppu: 400,
    shapes: [
      { pts: wingUpper,              color: C.wing },
      { pts: mirrorY(wingUpper, CY), color: C.wing },

      // Gövde: burnu sivri iğ
      { pts: [238,40, 198,31, 140,26, 70,27, 24,33, 10,40,
              24,47, 70,53, 140,54, 198,49], color: C.hull },

      // Motor bloğu — gövdenin İÇİNDE kalır, dışarı nub olarak taşmaz
      { pts: [12,40, 26,46.5, 42,48, 42,32, 26,33.5], color: C.dark },

      // Üst kenar aydınlatması — hacim hissi
      { pts: [238,40, 198,49, 140,54, 70,53, 24,47,
              24,43.5, 70,49.5, 140,50.5, 198,45.5], color: C.light },

      // Omurga panel çizgisi
      { pts: [40,41, 180,41, 186,40, 180,39, 40,39], color: C.dark },

      // Sensör lensi — bot olduğunu okutan tek parça, kırmızıya karşı kontrast
      { pts: [216,40, 200,35.5, 178,37.5, 178,42.5, 200,44.5], color: C.eye },
      { pts: [210,40, 199,37.5, 184,38.8, 184,41.2, 199,42.5], color: C.eyeIn },
    ],

    skin: {
      id: "enemy.swarm",
      colliderMode: "Box",
      // hitboxRect ölçülüp basılıyor; oraya oturunca ek daraltmaya gerek yok.
      hitboxScale: 1.0,
    },
  };
};

const E = require("./enemies");
const P = require("./player");
const K = require("./components");
const PR = require("./props");
const B  = require("./boss");

module.exports = {
  ships: [
    swarm(),
    E.armored(), E.shield(), E.barrier(), E.bomber(), E.bombRunner(),
    E.interceptor(), E.artillery(), E.jammer(), E.phantom(),
    E.regenerator(), E.leech(), E.splitter(), E.juggernaut(),

    P.playerBody(), P.playerBarrel(),

    K.generator(), K.shieldGen(), K.repair(), K.storage(),
    K.capacitor(), K.hangar(), K.turretRing(),

    // Gemi olmayan her sey: mermiler, bombalar, fuzeler, enkaz, turret parcalari
    ...PR.all(),

    // Boss govdesi (10 boss icin PAYLASILAN) + 5 hardpoint tipi
    ...B.all(),
  ],
  C,
};
