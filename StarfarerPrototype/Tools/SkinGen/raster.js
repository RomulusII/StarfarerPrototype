// Poligon rasterleştirici — 4x supersampling ile kenar yumuşatma.
// Koordinat sistemi: y YUKARI, origin sol-alt. PNG'ye yazarken çevrilir.

const SS = 4;   // her eksende alt-örnek sayısı (16 örnek/piksel)

function pointInPoly(px, py, pts) {
  let inside = false;
  for (let i = 0, j = pts.length - 2; i < pts.length; j = i, i += 2) {
    const xi = pts[i],     yi = pts[i + 1];
    const xj = pts[j],     yj = pts[j + 1];
    if ((yi > py) !== (yj > py) &&
        px < ((xj - xi) * (py - yi)) / (yj - yi) + xi) inside = !inside;
  }
  return inside;
}

/**
 * shapes: [{ pts: [x0,y0,x1,y1,...], color: [r,g,b,a] }]
 * Sırayla üst üste "source-over" ile bindirilir.
 */
function render(w, h, shapes) {
  const W = w * SS, H = h * SS;
  const acc = new Float32Array(W * H * 4);   // premultiplied RGBA, 0..255

  for (const s of shapes) {
    // Palet RGB de olabilir; alfa verilmemişse opak sayılır
    const [sr, sg, sb] = s.color;
    const sa    = s.color.length > 3 ? s.color[3] : 255;
    const alpha = sa / 255;

    // Şeklin sınırlayıcı kutusu — tüm tuvali taramaya gerek yok
    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    for (let i = 0; i < s.pts.length; i += 2) {
      minX = Math.min(minX, s.pts[i]);     maxX = Math.max(maxX, s.pts[i]);
      minY = Math.min(minY, s.pts[i + 1]); maxY = Math.max(maxY, s.pts[i + 1]);
    }
    const x0 = Math.max(0, Math.floor(minX * SS) - 1);
    const x1 = Math.min(W - 1, Math.ceil(maxX * SS) + 1);
    const y0 = Math.max(0, Math.floor(minY * SS) - 1);
    const y1 = Math.min(H - 1, Math.ceil(maxY * SS) + 1);

    for (let sy = y0; sy <= y1; sy++) {
      const py = (sy + 0.5) / SS;
      for (let sx = x0; sx <= x1; sx++) {
        const px = (sx + 0.5) / SS;
        if (!pointInPoly(px, py, s.pts)) continue;

        const o = (sy * W + sx) * 4;

        // Silme modu: alfa dahil her seyi sifirlar, yani gercek delik acar.
        // Halka gibi sekiller icin gerekli - even-odd dolgu delik acamiyor ve
        // dis/ic halkayi tek yolda birlestirmek dikis yerinde centik biraikiyor.
        if (s.mode === "erase") {
          acc[o] = acc[o + 1] = acc[o + 2] = acc[o + 3] = 0;
          continue;
        }

        const inv = 1 - alpha;
        acc[o    ] = sr * alpha + acc[o    ] * inv;
        acc[o + 1] = sg * alpha + acc[o + 1] * inv;
        acc[o + 2] = sb * alpha + acc[o + 2] * inv;
        acc[o + 3] = sa * alpha + acc[o + 3] * inv;
      }
    }
  }

  // Kutu filtresi ile indir + y eksenini çevir (PNG satırları yukarıdan aşağı)
  const out = new Uint8Array(w * h * 4);
  const n = SS * SS;
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      let r = 0, g = 0, b = 0, a = 0;
      for (let j = 0; j < SS; j++) {
        for (let i = 0; i < SS; i++) {
          const o = (((y * SS + j) * W) + (x * SS + i)) * 4;
          r += acc[o]; g += acc[o + 1]; b += acc[o + 2]; a += acc[o + 3];
        }
      }
      r /= n; g /= n; b /= n; a /= n;

      // Premultiplied -> straight alpha (kenarlarda renk kararmasın)
      const af = a / 255;
      const o2 = ((h - 1 - y) * w + x) * 4;
      out[o2    ] = af > 0.001 ? Math.min(255, Math.round(r / af)) : 0;
      out[o2 + 1] = af > 0.001 ? Math.min(255, Math.round(g / af)) : 0;
      out[o2 + 2] = af > 0.001 ? Math.min(255, Math.round(b / af)) : 0;
      out[o2 + 3] = Math.round(a);
    }
  }
  return out;
}

/** Bir şekli merkez ekseninde aynalar (y = cy etrafında). */
function mirrorY(pts, cy) {
  const out = new Array(pts.length);
  for (let i = 0; i < pts.length; i += 2) {
    out[i]     = pts[i];
    out[i + 1] = 2 * cy - pts[i + 1];
  }
  return out;
}

module.exports = { render, mirrorY, SS };
