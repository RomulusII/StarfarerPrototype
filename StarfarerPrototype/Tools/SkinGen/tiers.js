// Düşman TIER varyantları — aynı siluet, zırh tonu farklı.
//
// Her düşman sprite'ı poligonlardan bir PALETLE çiziliyor (palette.js). Tier
// varyantı aynı poligonları paletin ZIRH rollerini değiştirerek yeniden çizer:
//
//   değişen  — wing (kanat/plaka), trim (kenar), dark (motor bloğu, panel çizgisi)
//   korunan  — hull (gövde: tipin kimlik rengi), light (üst ışık), eye (sensör)
//
// Böylece Swarm hâlâ kırmızı bir Swarm'dır ama plakaları bronz/çelik/altındır:
// tip gövdeden, tier plakalardan okunur. Rol, parçanın RENK NESNESİ ile tanınır
// (aynı referans) — her gemi paletini `pal` alanında taşır.
//
// Çıktı: <Ad>_T2.png … <Ad>_T4.png, skin anahtarı "<id>.t2" … "<id>.t4".
// Oyun tier anahtarını bulamazsa tabana düşer (EnemyBot.BuildBody).

const { mul, mix, WHITE } = require("./palette");

// Renkler EnemyTier.cs'deki TierColor ile aynı olmalı (chevron işareti oradan).
const TIERS = [
  { tier: 2, name: "Zırhlı", armor: [196, 128,  62] },   // bronz
  { tier: 3, name: "Ağır",   armor: [110, 150, 205] },   // çelik mavisi
  { tier: 4, name: "Elit",   armor: [240, 200,  80] },   // altın
];

function recolor(ship, t) {
  const p     = ship.pal;
  const wing  = t.armor;
  const trim  = mix(t.armor, WHITE, 0.25);
  const dark  = mul(t.armor, 0.45);

  const shapes = ship.shapes.map(s => {
    let color = s.color;
    if (s.color === p.wing)      color = wing;
    else if (s.color === p.trim) color = trim;
    else if (s.color === p.dark) color = dark;
    return Object.assign({}, s, { color });
  });

  return Object.assign({}, ship, {
    name:   `${ship.name}_T${t.tier}`,
    shapes,
    skin:   Object.assign({}, ship.skin, { id: `${ship.skin.id}.t${t.tier}` }),
    tierOf: ship.skin.id,
  });
}

/** Paleti olan her düşman sprite'ı için üç tier varyantı. */
function tierVariants(ships) {
  const out = [];
  for (const s of ships) {
    if (!s.pal || !s.skin || !s.skin.id.startsWith("enemy.")) continue;
    for (const t of TIERS) out.push(recolor(s, t));
  }
  return out;
}

module.exports = { TIERS, tierVariants };
