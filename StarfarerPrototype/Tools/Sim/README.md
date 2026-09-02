# Headless denge simülasyonu

Oyunu görüntüsüz, gerçek zamandan **~40 kat hızlı** koşturur ve her koşudan
`BalanceLog` formatında bir JSONL üretir. Amaç denge parametrelerini elle
deneyerek değil ölçerek ayarlamak.

**Simülasyon ayrı bir model değil, oyunun kendisidir.** Sahte oyuncu gerçek
girdi yolundan nişan alıp ateş eder, gerçek mağazadan alışveriş yapar, gerçek
dalgalarla dövüşür. Ayrı bir model kurulsaydı ölçtüğümüz şey oyunun dengesi
değil modelin dengesi olurdu — bugüne kadarki bütün sayılar zaten öyle bir
kâğıt modelinden geliyor.

---

## Kurulum

Gereken tek şey Unity ve Node. Koşucu bir Windows player'ıdır; **bir kez** alınır:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" -batchmode -projectPath . -executeMethod SimBuild.Player
```

Çıktı: `Builds/Sim/Starfarer-sim.exe` (gitignore kapsamında).

Denge sayısını değiştirmek için **yeniden derlemek gerekmez** — parametreler
komut satırından ezilir (`--set`). Yalnızca C# değiştiğinde yeniden alınır.

---

## Koşu

```bash
node Tools/Sim/run.js --kosu 8 --profil ucuz --level 1-10
```

| Argüman | Anlamı | Varsayılan |
|---|---|---|
| `--kosu N` | kaç koşu (tohum `--tohum`'dan başlayarak artar) | 4 |
| `--is N` | aynı anda kaç süreç | çekirdek − 1 |
| `--profil ucuz\|odak` | sahte oyuncunun satın alma politikası | ucuz |
| `--level 1-10` | oynanacak level aralığı | 1-10 |
| `--zorluk kolay\|normal\|zor` | zorluk | normal |
| `--nisan D` | nişan hatasının sabit bileşeni (derece) | 3.0 |
| `--nisan-hiz D` | hedef hızına bağlı bileşen (derece / birim/sn) | 1.2 |
| `--set ad=değer` | `BalanceConfig` / `LevelCurve` alanını ez (tekrarlanabilir) | — |
| `--sure S` | oyun saniyesi sınırı | 5400 |
| `--duvar S` | duvar saati sınırı (koşu başına) | 900 |
| `--etiket ad` | çıktı klasörünün adı | profil-level |

Çıktı: `Tools/Balance/logs/sim/<tarih>-<etiket>/s<tohum>.jsonl`
(+ her sürecin kendi `s<tohum>.unity.log`'u).

Ayrıntılı metrikler için insan oturumlarıyla **aynı** analiz kullanılır:

```bash
node Tools/Balance/analyze.js Tools/Balance/logs/sim/<klasor>/s1.jsonl
```

### Duyarlılık analizi

```bash
node Tools/Sim/run.js --kosu 6 --etiket taban
node Tools/Sim/run.js --kosu 6 --set statStep=1.0 --etiket statStep-dusuk
node Tools/Sim/run.js --kosu 6 --set statStep=1.5 --etiket statStep-yuksek
```

Ezme asıl asset'e **yazılmaz**; `BalanceConfig.UseRuntimeCopy()` ile bellekteki
kopyada çalışılır. Bilinmeyen bir alan adı koşuyu hatayla durdurur — sessizce
yok sayılsaydı bütün bir tarama, hiç uygulanmamış bir parametre yüzünden
"duyarsız" damgası yerdi.

---

## Sahte oyuncu

| Parça | Dosya | Ne yapar |
|---|---|---|
| Nişan / ateş | `Assets/Scripts/Sim/SimPilot.cs` | hedef seçer, öngörülü nişan alır, gürültü bindirir |
| Alışveriş | `Assets/Scripts/Sim/SimShopper.cs` | iki profil + çıkmaz kaçınma (depo, jeneratör) |
| Koşu yönetimi | `Assets/Scripts/Sim/SimRuntime.cs` | tohum, hızlandırma, bitiş, `--set` |
| Yapılandırma | `Assets/Scripts/Sim/SimConfig.cs` | komut satırı |

**İsabet modeli:** nişan açısına Gauss gürültüsü binder,
`sigma = nisan + nisan_hiz × hedefHızı`. Işın (lazer) ıskalamaz ve isabet
oranı paydasına girmez. Hız bileşeni şart: tek bir sabit oran hangi TİPİN
ıskalandığı bilgisini yok ederdi.

**İki profil şart:** tek politika kendi tercihini denge sanır. Bugüne kadarki
bütün stat eğrisi "en ucuz statı al" varsayımıyla kalibre edildi ve hiç
sınanmadı.

---

## Bilinen sınırlar

- **Determinizm tam değil.** Aynı tohum aynı olayları aynı sırayla verir;
  ölçülen kayma birkaç enkaz-toplama olayının 1–20 kare oynamasıdır (fizik
  iş parçacıklarının sıralaması). Miktarlar ve dövüş dizisi birebir aynı.
  A/B karşılaştırması için yeterli; bit düzeyinde tekrar isteniyorsa
  `Physics2D.simulationMode = Script` ile elle adımlamak gerekir.
- **Nişan hatası kalibre EDİLMEDİ.** Varsayılan değerlerle ölçülen isabet
  oranı %43; insandan ölçülen %52. `--nisan` / `--nisan-hiz` ile aranmalı.
- **Turret uzmanlaşması satın alınmıyor** (Point Defence, Gatling…).
  Uzmanlaşma güç değil karakter seçimidir; doğru profili ölçülmemiş sayılara
  dayanır. Önce temel eğri ölçülecek.
- **Boost kullanılmıyor.** Sahte oyuncu kalkan/silah boost'una hiç basmıyor.
- **Ölüm sonrası devam yok.** Koşu ölümle biter (`sebep=oldu`); gerçek oyuncu
  son levelden devam eder.
