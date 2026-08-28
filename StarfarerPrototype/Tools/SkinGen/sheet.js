// Kullanim: node Tools/SkinGen/sheet.js [olcek] [cikti.png]
// Tum gemileri tek koyu sayfada yan yana dizer - siluetleri ve olcek
// iliskisini birlikte gormek icin. Gemiler oyundaki gercek boy oraninda
// yerlesir, buyutme yalnizca okunurluk icindir.
const fs   = require("fs");
const path = require("path");
const { encodePNG } = require("./png");
const { render }    = require("./raster");
const { ships }     = require("./ships");

const scale = Number(process.argv[2] || 2);
const out   = process.argv[3] || path.join(__dirname, "sheet.png");

const PAD  = 24;
const COLS = 4;

// Hucre boyutu en buyuk gemiye gore - oranlar korunur
const cellW = Math.max(...ships.map(s => s.w)) + PAD * 2;
const cellH = Math.max(...ships.map(s => s.h)) + PAD * 2;
const rows  = Math.ceil(ships.length / COLS);

const W = cellW * COLS * scale;
const H = cellH * rows * scale;
const buf = new Uint8Array(W * H * 4);

// Zemin: oyunun uzay siyahi + hafif izgara
for (let y = 0; y < H; y++) {
  for (let x = 0; x < W; x++) {
    const o = (y * W + x) * 4;
    const cx = Math.floor(x / (cellW * scale));
    const cy = Math.floor(y / (cellH * scale));
    const alt = (cx + cy) % 2 === 0;
    const v = alt ? 16 : 22;
    buf[o] = v; buf[o + 1] = v; buf[o + 2] = v + 4; buf[o + 3] = 255;
  }
}

ships.forEach((s, i) => {
  const rgba = render(s.w, s.h, s.shapes);
  const col  = i % COLS;
  const row  = Math.floor(i / COLS);

  // Hucre icinde ortala
  const ox = (col * cellW + Math.floor((cellW - s.w) / 2)) * scale;
  const oy = (row * cellH + Math.floor((cellH - s.h) / 2)) * scale;

  for (let y = 0; y < s.h * scale; y++) {
    for (let x = 0; x < s.w * scale; x++) {
      const src = (Math.floor(y / scale) * s.w + Math.floor(x / scale)) * 4;
      const a   = rgba[src + 3] / 255;
      if (a <= 0.002) continue;

      const dx = ox + x, dy = oy + y;
      if (dx < 0 || dy < 0 || dx >= W || dy >= H) continue;
      const d = (dy * W + dx) * 4;
      for (let c = 0; c < 3; c++)
        buf[d + c] = Math.round(rgba[src + c] * a + buf[d + c] * (1 - a));
    }
  }
});

fs.writeFileSync(out, encodePNG(buf, W, H));
console.log(`${out}  ${W}x${H}  ${ships.length} gemi, ${COLS} sutun`);
ships.forEach((s, i) => {
  const col = i % COLS, row = Math.floor(i / COLS);
  console.log(`  satir ${row + 1}, sutun ${col + 1}: ${s.name}  (${s.w / 4}x${s.h / 4} birim)`);
});
