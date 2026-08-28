// Oyuncu tarafi. Ana gemi SABIT durur ve donmez; dusmanlar SAGDAN gelir.
// Bu yuzden gemi yandan gorunumdur: motorlar solda, kopru/burun sagda.
// Tuval yine bodyWidth x bodyHeight'in 4 kati (PPU 400).

const { mirrorY } = require("./raster");
const { pal, u }  = require("./palette");

// PlayerShip - 400x100 birim -> 1600x400 tuval. Oyunun en buyuk silueti.
const playerBody = () => {
  const p = pal(u(0.30, 0.30, 0.40), u(0.24, 0.24, 0.33));
  const CY = 200;

  // Ust sirt: slot izgarasi bunun uzerine oturur, o yuzden duz birakildi
  const spine = [1180, 316, 900, 340, 520, 336, 300, 318, 300, 300, 520, 316, 900, 320, 1180, 296];

  return {
    name: "PlayerBody", dir: "Player", w: 1600, h: 400, ppu: 400,
    shapes: [
      // Ana govde - uzun, agir, sivri olmayan. Kacamayan bir gemi kutle okutmali
      { pts: [1560, 200, 1500, 150, 1380, 112, 1120, 86, 700, 76, 340, 88,
              150, 120, 60, 170, 40, 200,
              60, 230, 150, 280, 340, 312, 700, 324, 1120, 314, 1380, 288,
              1500, 250], color: p.hull },

      // Kopru bloku - burnun hemen gerisinde, yukseltilmis.
      // Ic dolgu KOYU DEGIL: koyu birakilinca govdede delik gibi okunuyordu,
      // yukseltilmis bir yapi degil.
      { pts: [1420, 118, 1240, 100, 1180, 116, 1180, 284, 1240, 300, 1420, 282], color: p.trim },
      { pts: [1400, 128, 1252, 112, 1204, 124, 1204, 276, 1252, 288, 1400, 272], color: p.wing },
      { pts: [1400, 272, 1252, 288, 1204, 276, 1204, 200, 1400, 200], color: p.light },
      { pts: [1300, 196, 1380, 196, 1380, 204, 1300, 204], color: p.dark },

      // Motor blogu - solda. Gemi boyuna gore buyutuldu, yoksa 400 birimlik
      // govdenin ucunda kaybolan bir cikinti olarak okunuyordu.
      { pts: [50, 200, 78, 252, 190, 268, 190, 132, 78, 148], color: p.dark },
      { pts: [26, 126, 26, 274, 62, 264, 62, 136], color: p.wing },
      { pts: [12, 152, 12, 248, 32, 240, 32, 160], color: p.trim },
      { pts: [34, 172, 34, 228, 56, 220, 56, 180], color: p.dark },

      // Zirh kusagi - govde boyunca alt kenar
      { pts: [1380, 112, 1120, 86, 700, 76, 340, 88, 150, 120, 150, 138,
              340, 106, 700, 94, 1120, 104, 1380, 130], color: p.dark },

      // Ust kenar aydinlatmasi
      { pts: spine, color: p.light },

      // Panel bolmeleri - uzunlugu okutur, bos govde alani birakmaz
      { pts: [420, 300, 440, 300, 440, 100, 420, 100], color: p.dark },
      { pts: [660, 314, 680, 314, 680, 90, 660, 90], color: p.dark },
      { pts: [900, 318, 920, 318, 920, 82, 900, 82], color: p.dark },
      { pts: [1140, 312, 1160, 312, 1160, 88, 1140, 88], color: p.dark },

      // Kopru penceresi - geminin "yasadigi" tek nokta
      { pts: [1500, 200, 1460, 160, 1400, 168, 1400, 232, 1460, 240], color: p.eye },
      { pts: [1478, 200, 1452, 176, 1416, 181, 1416, 219, 1452, 224], color: p.eyeIn },
    ],
    skin: { id: "player.body", colliderMode: "Box", hitboxScale: 1.0 },
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
