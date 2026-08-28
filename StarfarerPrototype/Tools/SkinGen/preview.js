// Kullanım: node Tools/SkinGen/preview.js <png> <ölçek> [çıktı]
// Nearest-neighbour büyütme + şeffaflık için dama zemini. Sadece göz kontrolü için.
const fs = require("fs");
const zlib = require("zlib");
const { encodePNG } = require("./png");

function decodePNG(buf) {
  let p = 8, w = 0, h = 0, idat = [];
  while (p < buf.length) {
    const len = buf.readUInt32BE(p);
    const type = buf.toString("ascii", p + 4, p + 8);
    const data = buf.subarray(p + 8, p + 8 + len);
    if (type === "IHDR") { w = data.readUInt32BE(0); h = data.readUInt32BE(4); }
    if (type === "IDAT") idat.push(data);
    p += 12 + len;
  }
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const stride = w * 4, out = new Uint8Array(w * h * 4);
  let prev = new Uint8Array(stride);
  for (let y = 0; y < h; y++) {
    const f = raw[y * (stride + 1)];
    const line = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    const cur = new Uint8Array(stride);
    for (let i = 0; i < stride; i++) {
      const a = i >= 4 ? cur[i - 4] : 0, b = prev[i], c = i >= 4 ? prev[i - 4] : 0;
      let v = line[i];
      if (f === 1) v += a; else if (f === 2) v += b;
      else if (f === 3) v += (a + b) >> 1;
      else if (f === 4) {
        const pp = a + b - c, pa = Math.abs(pp - a), pb = Math.abs(pp - b), pc = Math.abs(pp - c);
        v += (pa <= pb && pa <= pc) ? a : (pb <= pc ? b : c);
      }
      cur[i] = v & 255;
    }
    out.set(cur, y * stride); prev = cur;
  }
  return { w, h, rgba: out };
}

const [file, scaleArg, outFile] = process.argv.slice(2);
const scale = parseInt(scaleArg || "4", 10);
const { w, h, rgba } = decodePNG(fs.readFileSync(file));
const W = w * scale, H = h * scale;
const out = new Uint8Array(W * H * 4);

for (let y = 0; y < H; y++) {
  for (let x = 0; x < W; x++) {
    const s = ((y / scale | 0) * w + (x / scale | 0)) * 4;
    const d = (y * W + x) * 4;
    // Dama zemini — şeffaf bölgeler göz kontrolünde belli olsun
    const chk = (((x >> 3) + (y >> 3)) & 1) ? 60 : 40;
    const a = rgba[s + 3] / 255;
    out[d]     = rgba[s]     * a + chk * (1 - a);
    out[d + 1] = rgba[s + 1] * a + chk * (1 - a);
    out[d + 2] = rgba[s + 2] * a + chk * (1 - a);
    out[d + 3] = 255;
  }
}
fs.writeFileSync(outFile || file.replace(/\.png$/, "_preview.png"), encodePNG(out, W, H));
console.log("önizleme:", outFile || file.replace(/\.png$/, "_preview.png"), W + "x" + H);

// İsteğe bağlı: hitbox dikdörtgenini çiz.  --rect x,y,w,h  (sol-alt orijin)
const rectArg = process.argv.find(a => a.startsWith("--rect="));
if (rectArg) {
  const [rx, ry, rw, rh] = rectArg.slice(7).split(",").map(Number);
  const px = (x, y, c) => {
    if (x < 0 || y < 0 || x >= W || y >= H) return;
    const d = (y * W + x) * 4;
    out[d] = c[0]; out[d + 1] = c[1]; out[d + 2] = c[2]; out[d + 3] = 255;
  };
  const green = [0, 255, 90];
  const X0 = rx * scale, X1 = (rx + rw) * scale - 1;
  const Y1 = H - 1 - ry * scale, Y0 = H - (ry + rh) * scale;
  for (let x = X0; x <= X1; x++) { px(x, Y0, green); px(x, Y1, green); }
  for (let y = Y0; y <= Y1; y++) { px(X0, y, green); px(X1, y, green); }
  fs.writeFileSync(outFile || file.replace(/\.png$/, "_preview.png"), encodePNG(out, W, H));
  console.log("hitbox çizildi:", rx, ry, rw, rh);
}
