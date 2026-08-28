// Hitbox dikdörtgeni ölçümü.
//
// Sivri burunlu bir gemi, sınırlayıcı kutusunun ancak yarısını doldurur.
// Kutu collider'ı tüm sınırlara oturtmak, kutunun boş köşelerine atılan
// mermilerin hiçbir şeye çarpmamasına yol açar ("kurşun boşluğa çarptı").
// Bu yüzden hitbox, kütlenin gerçekten bulunduğu bölgeye oturtulur:
// her kenardan, o dilimdeki toplam alfa kütlesi eşiğin altında kaldığı sürece
// kırpılır. Kalan dikdörtgen siluetin gövdesidir; kırpılan kısım seyrek
// kanat ucu ve burun sivrisidir.

const TRIM = 0.02;   // her kenardan atılabilecek toplam kütle oranı

/** rgba satırları YUKARIDAN aşağı. Dönen rect sol-alt orijinli piksel. */
function hitboxRect(rgba, w, h) {
  const col = new Float64Array(w);
  const row = new Float64Array(h);
  let total = 0;

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const a = rgba[(y * w + x) * 4 + 3];
      if (a === 0) continue;
      col[x] += a; row[y] += a; total += a;
    }
  }
  if (total === 0) return { x: 0, y: 0, width: w, height: h, fill: 0 };

  const budget = total * TRIM;
  const trim = (arr) => {
    let lo = 0, hi = arr.length - 1, acc = 0;
    while (lo < hi && acc + arr[lo] <= budget) { acc += arr[lo]; lo++; }
    acc = 0;
    while (hi > lo && acc + arr[hi] <= budget) { acc += arr[hi]; hi--; }
    return [lo, hi];
  };

  const [x0, x1] = trim(col);
  let   [r0, r1] = trim(row);

  // Gemiler yatay eksende simetrik — hitbox da simetrik kalmalı, yoksa
  // yukarı ve aşağı vurma zorluğu farklılaşır.
  const cy = (h - 1) / 2;
  const half = Math.max(cy - r0, r1 - cy);
  r0 = Math.max(0, Math.round(cy - half));
  r1 = Math.min(h - 1, Math.round(cy + half));

  // Satır indeksi (yukarıdan) -> sol-alt orijinli y
  const yBottom = h - 1 - r1;
  const rect = { x: x0, y: yBottom, width: x1 - x0 + 1, height: r1 - r0 + 1 };

  // Dikdörtgenin içindeki doluluk — kutu collider'ın ne kadarının gerçek
  // gövde olduğunu söyler. Asıl bakılması gereken sayı budur.
  let inside = 0;
  for (let y = r0; y <= r1; y++)
    for (let x = x0; x <= x1; x++)
      if (rgba[(y * w + x) * 4 + 3] > 127) inside++;

  rect.fill = inside / (rect.width * rect.height);
  return rect;
}

module.exports = { hitboxRect };
