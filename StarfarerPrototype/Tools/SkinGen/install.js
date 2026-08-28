// Kullanim: node Tools/SkinGen/install.js
//
// Unity Editor acikken bile calisir: import ayarlarini .meta dosyasina, skin
// kayitlarini SkinSet.asset'e DOGRUDAN yazar. Unity odaklaninca ikisini de okur.
//
// MEVCUT GUID'LERE DOKUNMAZ. Bir sprite'in .meta'si varsa GUID'i korunur -
// aksi halde SkinSet'teki referanslar kopar ve sprite alanlari None'a duser.

const fs     = require("fs");
const path   = require("path");
const crypto = require("crypto");
const { render }     = require("./raster");
const { hitboxRect } = require("./measure");
const { ships }      = require("./ships");

const root       = path.join(__dirname, "..", "..");
const artRoot    = path.join(root, "Assets", "Art");
const skinSetPath = path.join(root, "Assets", "Resources", "SkinSet.asset");

const newGuid = () => crypto.randomBytes(16).toString("hex");

// ── Klasor meta'lari ────────────────────────────────────────────────────────

function ensureFolderMeta(dirPath) {
  const meta = dirPath + ".meta";
  if (fs.existsSync(meta)) return;
  fs.writeFileSync(meta,
    "fileFormatVersion: 2\n" +
    `guid: ${newGuid()}\n` +
    "folderAsset: yes\n" +
    "DefaultImporter:\n" +
    "  externalObjects: {}\n" +
    "  userData: \n" +
    "  assetBundleName: \n" +
    "  assetBundleVariant: \n");
  console.log("  + klasor meta: " + path.relative(root, meta));
}

// ── Texture meta ────────────────────────────────────────────────────────────

function textureMeta(guid, ppu) {
  return `fileFormatVersion: 2
guid: ${guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: ${ppu}
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

// Pivotu ozel olan spritelar (varsayilan merkez degil)
const PIVOTS = {
  "player.barrel": { x: 0.5, y: 0 },   // namlu mount noktasindan YUKARI uzanir
};

// SkinSet'e KAYDEDILMEYECEK spritelar: kendi govde gorseli olan komponentler.
// Halkalari sade kalmali, yoksa ikon ile govde ust uste biner.
const SKIP_REGISTER = new Set(["component.hangar", "component.turretcontroller"]);

ensureFolderMeta(artRoot);

const entries = [];

for (const s of ships) {
  const dir = path.join(artRoot, s.dir || "Enemies");
  ensureFolderMeta(dir);

  const png  = path.join(dir, s.name + ".png");
  const meta = png + ".meta";

  // Import ayarlarinin sahibi BU BETIKTIR, Unity degil. Unity bir PNG'yi kendi
  // varsayilanlariyla import etmisse (PPU 100, Sprite Mode Multiple, Mesh Tight)
  // ayarlar yanlis olur - govde 4 kat buyuk cikar ve hitbox ofseti kayar.
  // O yuzden meta her calistirmada yeniden yazilir; GUID yalnizca KORUNUR.
  let guid, existed = fs.existsSync(meta);
  if (existed) {
    const txt = fs.readFileSync(meta, "utf8");
    const gi  = txt.indexOf("guid: ");
    guid = gi >= 0 ? txt.substr(gi + 6, 32) : null;
    if (!guid) throw new Error("meta okunamadi: " + meta);
  } else {
    guid = newGuid();
  }

  let body = textureMeta(guid, s.ppu);
  const pv = PIVOTS[s.skin.id];
  if (pv) {
    body = body.replace("spritePivot: {x: 0.5, y: 0.5}",
                        "spritePivot: {x: " + pv.x + ", y: " + pv.y + "}")
               .replace("alignment: 0", "alignment: 9");   // 9 = Custom
  }
  fs.writeFileSync(meta, body);
  console.log("  " + (existed ? "~" : "+") + " " + s.name +
              "  PPU " + s.ppu + (existed ? "  (guid korundu, ayarlar yazildi)" : "  (yeni guid)"));

  if (SKIP_REGISTER.has(s.skin.id)) {
    console.log(`    (SkinSet'e yazilmadi - kendi govde gorseli var)`);
    continue;
  }

  const rgba = render(s.w, s.h, s.shapes);
  const r    = hitboxRect(rgba, s.w, s.h);

  entries.push(
    `  - id: ${s.skin.id}\n` +
    `    sprite: {fileID: 21300000, guid: ${guid}, type: 3}\n` +
    `    colliderMode: ${s.skin.colliderMode === "Polygon" ? 2 : 1}\n` +
    `    hitboxRect:\n` +
    `      serializedVersion: 2\n` +
    `      x: ${r.x}\n      y: ${r.y}\n` +
    `      width: ${r.width}\n      height: ${r.height}\n` +
    `    hitboxScale: ${s.skin.hitboxScale}`);
}

// ── SkinSet.asset ───────────────────────────────────────────────────────────

const prev = fs.readFileSync(skinSetPath, "utf8");
const head = prev.slice(0, prev.indexOf("  entries:"));
fs.writeFileSync(skinSetPath, head + "  entries:\n" + entries.join("\n") + "\n");

console.log(`\nSkinSet.asset guncellendi: ${entries.length} girdi`);
