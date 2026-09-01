// Kullanım: node Tools/SkinGen/gen.js
// Çıktı: Assets/Art/Enemies/<Ad>.png + SkinSet'e girilecek değerler
const fs   = require("fs");
const path = require("path");
const { encodePNG }  = require("./png");
const { render }     = require("./raster");
const { hitboxRect } = require("./measure");
const { ships }      = require("./ships");

const artRoot = path.join(__dirname, "..", "..", "Assets", "Art");

for (const s of ships) {
  // Her gemi kendi klasorune yazar (varsayilan: Enemies)
  const outDir = path.join(artRoot, s.dir || "Enemies");
  fs.mkdirSync(outDir, { recursive: true });

  const rgba = render(s.w, s.h, s.shapes);
  fs.writeFileSync(path.join(outDir, s.name + ".png"), encodePNG(rgba, s.w, s.h));

  let opaque = 0;
  for (let i = 3; i < rgba.length; i += 4) if (rgba[i] > 127) opaque++;
  const boxFill = opaque / (s.w * s.h);

  const r = hitboxRect(rgba, s.w, s.h);

  console.log(`\n${s.name}.png  ${s.w}x${s.h}  PPU ${s.ppu}`);
  console.log(`  sınırlayıcı kutu doluluğu : %${(boxFill * 100).toFixed(1)}  (sivri gemide düşük olması normal)`);
  console.log(`  hitboxRect                : x=${r.x} y=${r.y} w=${r.width} h=${r.height}`);
  console.log(`  hitbox içi doluluk        : %${(r.fill * 100).toFixed(1)}  <- ASIL SAYI, %60 altına düşmemeli`);
  console.log(`  SkinSet girdisi           : id="${s.skin.id}"  colliderMode=${s.skin.colliderMode}  hitboxScale=${s.skin.hitboxScale}`);
}
