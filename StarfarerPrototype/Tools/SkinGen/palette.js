// Renk yardımcıları. Gövde ve kanat renkleri EnemyTypeData'dan birebir gelir
// (0..1 -> 0..255); gölge/ışık/lens tonları onlardan türetilir, böylece her
// gemi kendi renginde kalır ama hepsi aynı görsel dili konuşur.

const clamp = v => Math.max(0, Math.min(255, Math.round(v)));
const mul   = (c, k) => c.map(v => clamp(v * k));
const mix   = (c, t, k) => c.map((v, i) => clamp(v + (t[i] - v) * k));

const WHITE = [255, 255, 255];

/** EnemyTypeData'daki 0..1 Color'ı 0..255'e çevirir. */
const u = (r, g, b) => [clamp(r * 255), clamp(g * 255), clamp(b * 255)];

/**
 * Bir gemi paleti. hull = bodyColor, wing = barrelColor.
 *   dark  — motor bloğu ve panel çizgileri (gövdenin koyusu)
 *   light — üst kenar aydınlatması (hacim hissi)
 *   eye   — sensör lensi; TÜM tiplerde aynı cyan, "bot" okuması buradan gelir
 */
const pal = (hull, wing) => ({
  hull,
  wing,
  dark:  mul(hull, 0.55),
  light: mix(hull, WHITE, 0.38),
  trim:  mix(wing, WHITE, 0.20),
  eye:   [102, 224, 255],
  eyeIn: [200, 245, 255],
});

module.exports = { pal, u, mul, mix, WHITE };
