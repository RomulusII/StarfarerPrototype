// Gemi komponentleri. Slot izgarasindaki gostergeler.
//
// Bugun hepsi ayni sonuk cyan daire - hangi slotta ne oldugu ancak upgrade
// ekrani acilinca anlasiliyor. Her tipe kendi ikonu verilince oyuncu gemiye
// bakip "jeneratorum solda, kalkanim sagda" diyebilir.
//
// 128x128, PPU 128 -> sprite 1x1 dunya birimi. ShipComponentBase zaten
// localScale = k_ringSize (0.35) uyguluyor, yani kod tarafi degismiyor.

const { pal, u } = require("./palette");

const CX = 64, CY = 64;

/** Kapali cokgen daire. */
const circle = (cx, cy, r, n = 40) => {
  const pts = [];
  for (let i = 0; i < n; i++) {
    const a = (i / n) * Math.PI * 2;
    pts.push(cx + Math.cos(a) * r, cy + Math.sin(a) * r);
  }
  return pts;
};

/** Merkezden gecen kalin cubuk (aci derece). */
const bar = (cx, cy, len, thick, deg) => {
  const a = (deg * Math.PI) / 180;
  const dx = Math.cos(a) * len * 0.5, dy = Math.sin(a) * len * 0.5;
  const nx = -Math.sin(a) * thick * 0.5, ny = Math.cos(a) * thick * 0.5;
  return [
    cx - dx + nx, cy - dy + ny, cx + dx + nx, cy + dy + ny,
    cx + dx - nx, cy + dy - ny, cx - dx - nx, cy - dy - ny,
  ];
};

/** Her komponent ayni iskeleti paylasir: dis halka + zemin + glif. */
const comp = (name, id, hull, wing, glyph) => {
  const p = pal(hull, wing);
  return {
    name, dir: "Components", w: 128, h: 128, ppu: 128,
    shapes: [
      // Halka: once dolu daire, sonra ici SILINIR (mode erase), sonra zemin
      // geri konur. Tek yolla halka cizmek dikis yerinde centik birakiyordu.
      { pts: circle(CX, CY, 62), color: p.hull },
      { pts: circle(CX, CY, 54), mode: "erase", color: [0, 0, 0, 0] },
      { pts: circle(CX, CY, 54), color: [...p.dark, 150] },
      ...glyph(p),
    ],
    skin: { id, colliderMode: "Box", hitboxScale: 1.0 },
  };
};

// Jenerator - reaktor cekirdegi, disa vuran isin cubuklari
const generator = () => comp(
  "Generator", "component.generator", u(1.0, 0.78, 0.15), u(0.80, 0.58, 0.08),
  p => [
    ...[0, 45, 90, 135].map(d => ({ pts: bar(CX, CY, 92, 9, d), color: p.wing })),
    { pts: circle(CX, CY, 26), color: p.light },
    { pts: [76, 78, 58, 66, 68, 64, 52, 50, 70, 62, 60, 64], color: p.dark },
  ]);

// Kalkan jeneratoru - kubbe, ustte katmanli
const shieldGen = () => comp(
  "ShieldGen", "component.shieldgenerator", u(0.30, 0.62, 1.0), u(0.16, 0.40, 0.85),
  p => [
    { pts: [64, 96, 40, 82, 30, 58, 34, 40, 94, 40, 98, 58, 88, 82], color: p.light },
    { pts: [64, 84, 46, 74, 40, 56, 42, 48, 86, 48, 88, 56, 82, 74], color: p.wing },
    { pts: [30, 40, 98, 40, 98, 32, 30, 32], color: p.dark },
  ]);

// Onarim birimi - hac, tibbi degil teknik okusun diye kollari kertikli
const repair = () => comp(
  "RepairUnit", "component.repairunit", u(0.30, 0.85, 0.45), u(0.16, 0.62, 0.30),
  p => [
    { pts: bar(CX, CY, 76, 22, 0),  color: p.light },
    { pts: bar(CX, CY, 76, 22, 90), color: p.light },
    { pts: circle(CX, CY, 13), color: p.dark },
  ]);

// Depo - ustuste yiginlanmis kasalar
const storage = () => comp(
  "Storage", "component.storage", u(0.72, 0.60, 0.35), u(0.52, 0.42, 0.22),
  p => [
    { pts: [34, 34, 92, 34, 92, 60, 34, 60], color: p.light },
    { pts: [34, 64, 62, 64, 62, 92, 34, 92], color: p.wing },
    { pts: [66, 64, 92, 64, 92, 92, 66, 92], color: p.wing },
    { pts: [34, 44, 92, 44, 92, 48, 34, 48], color: p.dark },
  ]);

// Kapasitor - iki plaka arasinda bosluk, klasik kondansator sembolu
const capacitor = () => comp(
  "Capacitor", "component.capacitor", u(0.55, 0.45, 0.90), u(0.36, 0.28, 0.70),
  p => [
    { pts: [52, 30, 62, 30, 62, 98, 52, 98], color: p.light },
    { pts: [70, 30, 80, 30, 80, 98, 70, 98], color: p.light },
    { pts: bar(CX, CY, 96, 8, 0), color: p.wing },
    { pts: [52, 30, 80, 30, 80, 98, 52, 98], color: [...p.dark, 90] },
  ]);

// Hangar - acilan kapak agzi, icinde kucuk bir gemi
const hangar = () => comp(
  "Hangar", "component.hangar", u(0.35, 0.72, 0.90), u(0.20, 0.48, 0.66),
  p => [
    { pts: [28, 40, 100, 40, 100, 88, 28, 88], color: p.dark },
    { pts: [28, 40, 100, 40, 100, 50, 28, 50], color: p.wing },
    { pts: [28, 78, 100, 78, 100, 88, 28, 88], color: p.wing },
    { pts: [88, 64, 66, 56, 42, 60, 38, 64, 42, 68, 66, 72], color: p.light },
  ]);

// Turret - kendi tabani ve namlusu zaten var; halka sade kalir
const turretRing = () => comp(
  "TurretSlot", "component.turretcontroller", u(0.70, 0.70, 0.76), u(0.48, 0.48, 0.54),
  p => [
    { pts: circle(CX, CY, 22), color: p.wing },
    { pts: bar(CX + 16, CY, 44, 12, 0), color: p.light },
  ]);

module.exports = { generator, shieldGen, repair, storage, capacitor, hangar, turretRing };
