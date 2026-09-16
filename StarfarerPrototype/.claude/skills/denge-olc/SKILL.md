---
name: denge-olc
description: Bir denge sayısı sorgulandığında veya değiştirilmek üzereyken (tehdit puanı, statStep, statCostGrowth, fiyatlar, HP/zırh eğrisi, isabet oranı, level süresi, gelir, dalga bütçesi, drop, silah/turret hasarı) kağıttaki tablodan cevap vermek yerine headless simülasyonla ÖLÇ. "şu değer doğru mu", "X'i Y yapsak ne olur", "dengeyi ölç", "A/B koş", "duyarlılık analizi" gibi istekleri kapsar.
---

# Denge ölçümü

CLAUDE.md'deki denge tabloları **kağıt modelden** gelir; hiçbiri oyunda
ölçülmedi ve düzeltilmek istenen şey tam olarak budur. O tablolara bakıp
"bu sayı doğru/yanlış" demek, ölçülmemiş bir varsayımı ikinci kez tekrarlamaktır.

**Kural 0 — ölçülebilen bir soruya tablodan cevap verme.** Simülasyon hazır ve
10 levellik bir koşu ~30 saniye sürüyor. Önce koş.

## Akış

1. **Koşucu var mı:** `Builds/Sim/Starfarer-sim.exe`. Yoksa bir kez alınır
   (Unity batchmode, `SimBuild.Player`) — komut `Tools/Sim/README.md`'de.
   Yalnızca **C# değiştiyse** yeniden alınır; denge sayısı için gerekmez,
   parametreler `--set` ile ezilir.

2. **Taban ve varyantı ayrı ayrı koş.** Aralarındaki tek fark ölçülen
   parametre olmalı; tohum, koşu sayısı, profil ve level aralığı AYNI:

   ```bash
   node Tools/Sim/run.js --kosu 6 --etiket taban
   node Tools/Sim/run.js --kosu 6 --set statStep=1.5 --etiket statStep-yuksek
   ```

   **Taraf başına en az 4 koşu.** Altında yayılım ölçülemez, araç `?` basar ve
   ortada karşılaştırma değil iki sayı kalır.

3. **Yan yana oku:**

   ```bash
   node Tools/Balance/analyze.js --karsilastir <taban-klasoru> <varyant-klasoru>
   ```

   Tek bir koşunun ayrıntısı gerekirse: `node Tools/Balance/analyze.js <dosya.jsonl>`.

## Sonucu okurken

- **`≈` bir sonuçtur, sonuçsuzluk değil.** "Fark yayılımın içinde" demek,
  o parametrenin ölçülen aralıkta bu metriği oynatmadığı demektir. Böyle
  çıktıysa böyle rapor et; tabloya dönüp "ama teorik olarak" deme.
- **`↑`/`↓` bir p-değeri değil**, kaba bir kapı (n = 4–8). "Konuşmaya değer"
  demektir, "kanıtlandı" değil.
- **Tip kırılımına bak.** Toplam metrik "hangi tip zorlaştı" sorusunu yutar.
- Araç tohum kümesi uyuşmazlığı ve kesilmiş koşu (`sebep=sure/duvar`) için
  uyarı basar — uyarı varsa sonucu ona göre çerçevele, sessizce geçme.

## Simülasyonun görmediği şeyler

Bunlar hakkında ölçüme dayanarak sonuç ÇIKARMA (bkz. `Tools/Sim/README.md`
"Bilinen sınırlar"): boost hiç kullanılmıyor, turret uzmanlaşması hiç satın
alınmıyor, ölüm sonrası devam yok, sahte oyuncunun hedef seçimi turretlerin
formülü. İsabet modeli insandan ölçülmüş tek bir orana (%52) kalibre edildi —
dokunmatik veya farklı oyuncu için geçerli değil.

## Ölçümden sonra

**Ölçümle denge değişikliğini aynı adımda yapma.** Ölçüm aracı ile ona
dayanan karar aynı hamlede verilirse, ölçümün kendisi doğrulanmadan
uygulanmış olur. Önce sayıları raporla; değişiklik ayrı bir karar.

Değişiklik yapılacaksa CLAUDE.md'deki ilgili bölüme **ölçülen sayıyı ve koşu
etiketini** yaz — projedeki her denge kararı gerekçesiyle birlikte duruyor,
ölçülmüş olanlar bunu daha çok hak ediyor.
