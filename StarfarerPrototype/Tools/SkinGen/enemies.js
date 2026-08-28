// 12 dusman tipi. Koordinat: y YUKARI, origin sol-alt, BURUN SAGA (+X).
// Tuval = bodyWidth x bodyHeight'in 4 kati (import'ta PPU 400).
//
// Siluet tasarim kurali: her tip kendi MEKANIGINI okutur. Oyuncu gemiyi
// tanimadan once ne yapacagini siluetinden tahmin edebilmeli.

const { mirrorY } = require("./raster");
const { pal, u }  = require("./palette");

// Armored - zirhli tugla, onde kalin plaka
const armored = () => {
  const p = pal(u(0.42, 0.45, 0.50), u(0.40, 0.40, 0.45));
  const fin = [110, 158, 98, 192, 132, 192, 152, 160];

  return {
    name: "Armored", w: 320, h: 220, ppu: 400,
    shapes: [
      { pts: fin,               color: p.wing },
      { pts: mirrorY(fin, 110), color: p.wing },
      { pts: [296, 84, 268, 70, 96, 60, 44, 78, 30, 110,
              44, 142, 96, 160, 268, 150, 296, 136], color: p.hull },
      { pts: [304, 78, 262, 62, 248, 70, 248, 150, 262, 158, 304, 142], color: p.trim },
      { pts: [300, 84, 268, 71, 258, 76, 258, 144, 268, 149, 300, 136], color: p.dark },
      { pts: [30, 110, 44, 142, 72, 146, 72, 74, 44, 78], color: p.dark },
      { pts: [296, 136, 268, 150, 96, 160, 44, 142, 44, 135,
              96, 152, 268, 142], color: p.light },
      { pts: [84, 113, 240, 113, 246, 110, 240, 107, 84, 107], color: p.dark },
      { pts: [244, 110, 228, 98, 208, 101, 208, 119, 228, 122], color: p.eye },
      { pts: [238, 110, 227, 102, 214, 104, 214, 116, 227, 118], color: p.eyeIn },
    ],
    skin: { id: "enemy.armored", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Shield - onde enerji emitor yayi
const shield = () => {
  const p = pal(u(0.25, 0.35, 0.85), u(0.15, 0.20, 0.70));
  const fin = [120, 146, 104, 184, 142, 184, 164, 148];

  return {
    name: "Shield", w: 280, h: 200, ppu: 400,
    shapes: [
      { pts: fin,               color: p.wing },
      { pts: mirrorY(fin, 100), color: p.wing },
      { pts: [272, 100, 262, 62, 242, 34, 222, 42, 240, 68, 248, 100,
              240, 132, 222, 158, 242, 166, 262, 138], color: p.light },
      { pts: [244, 100, 232, 74, 198, 58, 118, 52, 58, 66, 30, 88, 24, 100,
              30, 112, 58, 134, 118, 148, 198, 142, 232, 126], color: p.hull },
      { pts: [24, 100, 30, 112, 58, 134, 76, 136, 76, 64, 58, 66, 30, 88], color: p.dark },
      { pts: [232, 126, 198, 142, 118, 148, 58, 134, 58, 128,
              118, 141, 198, 135], color: p.light },
      { pts: [70, 103, 200, 103, 206, 100, 200, 97, 70, 97], color: p.dark },
      { pts: [214, 100, 198, 88, 178, 91, 178, 109, 198, 112], color: p.eye },
      { pts: [208, 100, 197, 92, 184, 94, 184, 106, 197, 108], color: p.eyeIn },
    ],
    skin: { id: "enemy.shield", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Bomber - ince igne, altinda bomba yuvasi
const bomber = () => {
  const p = pal(u(0.90, 0.50, 0.10), u(0.70, 0.32, 0.06));
  const fin = [56, 34, 44, 46, 74, 46, 84, 35];

  return {
    name: "Bomber", w: 176, h: 48, ppu: 400,
    shapes: [
      { pts: fin,              color: p.wing },
      { pts: mirrorY(fin, 24), color: p.wing },
      { pts: [72, 14, 116, 14, 116, 5, 108, 2, 80, 2, 72, 5], color: p.dark },
      { pts: [172, 24, 148, 16, 90, 12, 40, 14, 10, 22,
              10, 26, 40, 34, 90, 36, 148, 32], color: p.hull },
      { pts: [10, 22, 10, 26, 30, 32, 30, 16], color: p.dark },
      { pts: [172, 24, 148, 32, 90, 36, 40, 34, 40, 31,
              90, 33, 148, 29], color: p.light },
      { pts: [150, 24, 138, 18, 124, 20, 124, 28, 138, 30], color: p.eye },
    ],
    skin: { id: "enemy.bomber", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// BombRunner - agir tasiyici, altinda bomba rafi
const bombRunner = () => {
  const p = pal(u(0.85, 0.45, 0.05), u(0.70, 0.35, 0.05));
  const fin = [96, 136, 82, 172, 118, 172, 138, 138];

  return {
    name: "BombRunner", w: 260, h: 180, ppu: 400,
    shapes: [
      { pts: fin,              color: p.wing },
      { pts: mirrorY(fin, 90), color: p.wing },
      { pts: [236, 90, 222, 60, 188, 44, 88, 40, 38, 56, 22, 90,
              38, 124, 88, 140, 188, 136, 222, 120], color: p.hull },
      { pts: [78, 44, 108, 43, 108, 24, 100, 20, 86, 20, 78, 24], color: p.dark },
      { pts: [118, 42, 148, 42, 148, 22, 140, 18, 126, 18, 118, 22], color: p.dark },
      { pts: [158, 43, 188, 45, 188, 26, 180, 22, 166, 22, 158, 26], color: p.dark },
      { pts: [22, 90, 38, 124, 66, 128, 66, 52, 38, 56], color: p.dark },
      { pts: [222, 120, 188, 136, 88, 140, 38, 124, 38, 117,
              88, 133, 188, 129], color: p.light },
      { pts: [76, 93, 196, 93, 202, 90, 196, 87, 76, 87], color: p.dark },
      { pts: [210, 90, 194, 78, 174, 81, 174, 99, 194, 102], color: p.eye },
      { pts: [204, 90, 193, 82, 180, 84, 180, 96, 193, 98], color: p.eyeIn },
    ],
    skin: { id: "enemy.bombrunner", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Interceptor - ok basi, geriye supurulmus kanat, cift kuyruk
const interceptor = () => {
  const p = pal(u(0.95, 0.75, 0.20), u(0.75, 0.55, 0.10));
  const wing = [176, 40, 44, 66, 12, 66, 12, 56, 34, 50, 132, 38];
  const tail = [40, 48, 22, 70, 34, 70, 54, 50];

  return {
    name: "Interceptor", w: 208, h: 72, ppu: 400,
    shapes: [
      { pts: wing,              color: p.wing },
      { pts: mirrorY(wing, 36), color: p.wing },
      { pts: tail,              color: p.dark },
      { pts: mirrorY(tail, 36), color: p.dark },
      { pts: [202, 36, 170, 27, 104, 23, 44, 25, 12, 34,
              12, 38, 44, 47, 104, 49, 170, 45], color: p.hull },
      { pts: [12, 34, 12, 38, 32, 44, 32, 28], color: p.dark },
      { pts: [202, 36, 170, 45, 104, 49, 44, 47, 44, 44,
              104, 46, 170, 42], color: p.light },
      { pts: [180, 36, 166, 29, 150, 31, 150, 41, 166, 43], color: p.eye },
      { pts: [174, 36, 164, 32, 154, 33, 154, 39, 164, 40], color: p.eyeIn },
    ],
    skin: { id: "enemy.interceptor", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Artillery - one uzanan uzun namlu, agir kic
const artillery = () => {
  const p = pal(u(0.35, 0.42, 0.30), u(0.28, 0.34, 0.24));
  const fin = [92, 146, 78, 180, 114, 180, 134, 148];

  return {
    name: "Artillery", w: 344, h: 184, ppu: 400,
    shapes: [
      { pts: fin,              color: p.wing },
      { pts: mirrorY(fin, 92), color: p.wing },
      { pts: [340, 84, 340, 100, 246, 104, 246, 80], color: p.wing },
      { pts: [340, 88, 340, 96, 262, 98, 262, 86], color: p.dark },
      { pts: [268, 74, 268, 110, 240, 112, 240, 72], color: p.trim },
      { pts: [252, 92, 240, 58, 200, 40, 88, 36, 36, 54, 18, 92,
              36, 130, 88, 148, 200, 144, 240, 126], color: p.hull },
      { pts: [18, 92, 36, 130, 64, 134, 64, 50, 36, 54], color: p.dark },
      { pts: [240, 126, 200, 144, 88, 148, 36, 130, 36, 123,
              88, 141, 200, 137], color: p.light },
      { pts: [74, 95, 210, 95, 216, 92, 210, 89, 74, 89], color: p.dark },
      { pts: [206, 92, 190, 80, 170, 83, 170, 101, 190, 104], color: p.eye },
      { pts: [200, 92, 189, 84, 176, 86, 176, 98, 189, 100], color: p.eyeIn },
    ],
    skin: { id: "enemy.artillery", colliderMode: "Box", hitboxScale: 1.0 },
  };
};


// Yardimci: cokgen daire. Tam halka tek cokgenle cizilemez (even-odd dolgu
// delik acmaz); once buyuk daire, ustune govde renginde kucuk daire konur.
const circle = (cx, cy, rx, ry, n = 28) => {
  const pts = [];
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2;
    pts.push(cx + Math.cos(a) * rx, cy + Math.sin(a) * ry);
  }
  return pts;
};

// Jammer - buyuk parabolik canak + anten dizisi
const jammer = () => {
  const p = pal(u(0.55, 0.25, 0.75), u(0.40, 0.18, 0.60));
  const ant  = [150, 150, 146, 196, 154, 196, 162, 152];
  const ant2 = [110, 148, 106, 186, 114, 186, 122, 150];

  return {
    name: "Jammer", w: 264, h: 208, ppu: 400,
    shapes: [
      { pts: ant,                color: p.wing },
      { pts: mirrorY(ant, 104),  color: p.wing },
      { pts: ant2,               color: p.wing },
      { pts: mirrorY(ant2, 104), color: p.wing },
      { pts: [256, 104, 246, 50, 224, 14, 202, 24, 224, 60, 232, 104,
              224, 148, 202, 184, 224, 194, 246, 158], color: p.trim },
      { pts: [250, 104, 242, 62, 226, 34, 216, 38, 230, 66, 238, 104,
              230, 142, 216, 170, 226, 174, 242, 146], color: p.light },
      { pts: [228, 104, 216, 76, 184, 58, 96, 52, 44, 70, 22, 104,
              44, 138, 96, 156, 184, 150, 216, 132], color: p.hull },
      { pts: [22, 104, 44, 138, 70, 142, 70, 66, 44, 70], color: p.dark },
      { pts: [216, 132, 184, 150, 96, 156, 44, 138, 44, 131,
              96, 149, 184, 143], color: p.light },
      { pts: [80, 107, 190, 107, 196, 104, 190, 101, 80, 101], color: p.dark },
      { pts: [196, 104, 180, 92, 160, 95, 160, 113, 180, 116], color: p.eye },
      { pts: [190, 104, 179, 96, 166, 98, 166, 110, 179, 112], color: p.eyeIn },
    ],
    skin: { id: "enemy.jammer", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Phantom - kesikli yuzeyli hayalet kama, keskin ve dusuk kontrast
const phantom = () => {
  const p = pal(u(0.45, 0.80, 0.78), u(0.30, 0.60, 0.58));
  const wing = [150, 66, 46, 96, 16, 96, 16, 86, 40, 78, 116, 62];

  return {
    name: "Phantom", w: 232, h: 120, ppu: 400,
    shapes: [
      { pts: wing,              color: p.wing },
      { pts: mirrorY(wing, 60), color: p.wing },
      { pts: [226, 60, 190, 42, 120, 32, 50, 36, 14, 54,
              14, 66, 50, 84, 120, 88, 190, 78], color: p.hull },
      { pts: [226, 60, 190, 42, 120, 32, 120, 60], color: p.light },
      { pts: [226, 60, 190, 78, 120, 88, 120, 60], color: p.dark },
      { pts: [14, 54, 14, 66, 40, 74, 40, 46], color: p.dark },
      { pts: [204, 60, 188, 50, 168, 53, 168, 67, 188, 70], color: p.eye },
      { pts: [198, 60, 187, 54, 174, 56, 174, 64, 187, 66], color: p.eyeIn },
    ],
    skin: { id: "enemy.phantom", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Regenerator - sisman govde + onde emitor halkasi (aurayi okutur)
const regenerator = () => {
  const p = pal(u(0.25, 0.70, 0.40), u(0.18, 0.52, 0.30));
  const fin = [110, 172, 94, 214, 134, 214, 156, 176];

  return {
    name: "Regenerator", w: 312, h: 232, ppu: 400,
    shapes: [
      { pts: fin,               color: p.wing },
      { pts: mirrorY(fin, 116), color: p.wing },
      { pts: circle(248, 116, 56, 56), color: p.light },
      { pts: circle(248, 116, 38, 38), color: p.wing },
      { pts: circle(248, 116, 22, 22), color: p.light },
      { pts: [236, 116, 224, 76, 186, 52, 92, 46, 40, 68, 20, 116,
              40, 164, 92, 186, 186, 180, 224, 156], color: p.hull },
      { pts: [20, 116, 40, 164, 68, 168, 68, 64, 40, 68], color: p.dark },
      { pts: [224, 156, 186, 180, 92, 186, 40, 164, 40, 156,
              92, 178, 186, 172], color: p.light },
      { pts: [80, 119, 196, 119, 202, 116, 196, 113, 80, 113], color: p.dark },
      { pts: [200, 116, 184, 104, 164, 107, 164, 125, 184, 128], color: p.eye },
      { pts: [194, 116, 183, 108, 170, 110, 170, 122, 183, 124], color: p.eyeIn },
    ],
    skin: { id: "enemy.regenerator", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Leech - bocegimsi dar govde, onde iki kiskac
const leech = () => {
  const p = pal(u(0.60, 0.85, 0.25), u(0.45, 0.65, 0.18));
  const claw = [156, 44, 132, 26, 112, 30, 128, 38, 118, 44];
  const leg  = [70, 56, 58, 76, 68, 78, 82, 58];
  const leg2 = [110, 60, 98, 80, 108, 82, 122, 62];

  return {
    name: "Leech", w: 160, h: 88, ppu: 400,
    shapes: [
      { pts: claw,              color: p.wing },
      { pts: mirrorY(claw, 44), color: p.wing },
      { pts: leg,               color: p.dark },
      { pts: mirrorY(leg, 44),  color: p.dark },
      { pts: leg2,              color: p.dark },
      { pts: mirrorY(leg2, 44), color: p.dark },
      { pts: [126, 44, 116, 30, 92, 24, 46, 26, 16, 36, 12, 44,
              16, 52, 46, 62, 92, 64, 116, 58], color: p.hull },
      { pts: [12, 44, 16, 52, 34, 58, 34, 30, 16, 36], color: p.dark },
      { pts: [116, 58, 92, 64, 46, 62, 46, 58, 92, 60], color: p.light },
      { pts: [106, 44, 94, 36, 76, 38, 76, 50, 94, 52], color: p.eye },
      { pts: [100, 44, 92, 39, 82, 40, 82, 48, 92, 49], color: p.eyeIn },
    ],
    skin: { id: "enemy.leech", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Splitter - govde ortadan ikiye ayrilmis, dikis gorunur
const splitter = () => {
  const p = pal(u(0.85, 0.35, 0.55), u(0.65, 0.25, 0.42));
  const half = [252, 116, 238, 84, 200, 62, 96, 56, 42, 74, 24, 108,
                24, 116, 60, 116, 120, 116, 200, 116];
  const fin  = [104, 156, 90, 194, 128, 194, 148, 158];

  return {
    name: "Splitter", w: 288, h: 216, ppu: 400,
    shapes: [
      { pts: fin,                color: p.wing },
      { pts: mirrorY(fin, 108),  color: p.wing },
      { pts: half,               color: p.hull },
      { pts: mirrorY(half, 108), color: p.hull },
      { pts: [24, 108, 24, 116, 60, 130, 60, 86, 42, 74], color: p.dark },
      { pts: [238, 148, 200, 170, 96, 176, 42, 158, 42, 150,
              96, 168, 200, 162], color: p.light },
      { pts: [250, 112, 200, 112, 120, 112, 60, 112, 26, 112,
              26, 104, 60, 104, 120, 104, 200, 104, 250, 104], color: p.dark },
      { pts: [216, 100, 200, 88, 180, 91, 180, 100], color: p.eye },
      { pts: [216, 116, 200, 128, 180, 125, 180, 116], color: p.eye },
    ],
    skin: { id: "enemy.splitter", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Juggernaut - devasa katmanli zirh blogu, oyunun en agir silueti
const juggernaut = () => {
  const p = pal(u(0.30, 0.32, 0.36), u(0.24, 0.26, 0.30));
  const fin = [150, 208, 130, 262, 180, 262, 208, 212];

  return {
    name: "Juggernaut", w: 440, h: 288, ppu: 400,
    shapes: [
      { pts: fin,               color: p.wing },
      { pts: mirrorY(fin, 144), color: p.wing },
      { pts: [400, 108, 368, 88, 130, 76, 56, 100, 36, 144,
              56, 188, 130, 212, 368, 200, 400, 180], color: p.hull },
      { pts: [418, 98, 356, 74, 336, 84, 336, 204, 356, 214, 418, 190], color: p.trim },
      { pts: [412, 106, 362, 86, 350, 92, 350, 196, 362, 202, 412, 182], color: p.dark },
      { pts: [330, 82, 330, 206, 302, 210, 302, 78], color: p.trim },
      { pts: [296, 80, 296, 208, 268, 210, 268, 78], color: p.dark },
      { pts: [36, 144, 56, 188, 96, 194, 96, 94, 56, 100], color: p.dark },
      { pts: [400, 180, 368, 200, 130, 212, 56, 188, 56, 178,
              130, 202, 368, 190], color: p.light },
      { pts: [110, 148, 256, 148, 262, 144, 256, 140, 110, 140], color: p.dark },
      { pts: [250, 144, 232, 128, 206, 132, 206, 156, 232, 160], color: p.eye },
      { pts: [242, 144, 230, 134, 214, 136, 214, 152, 230, 154], color: p.eyeIn },
    ],
    skin: { id: "enemy.juggernaut", colliderMode: "Box", hitboxScale: 1.0 },
  };
};

module.exports = {
  armored, shield, bomber, bombRunner, interceptor, artillery,
  jammer, phantom, regenerator, leech, splitter, juggernaut,
};
