# Starfarer — Claude Project Instructions

## Genel Bakış
Bu proje Unity 6.3 LTS ile geliştirilen 2D uzay oyunudur. Oyun hem PC hem mobil (Android) hedeflidir. Geliştirici C# konusunda uzmandır, Unity'yi orta seviyede bilmektedir. Claude Code (terminal tabanlı) ile birlikte geliştirme yapılmaktadır.

**GitHub:** https://github.com/RomulusII/StarfarerPrototype

---

## Hikaye
Uzak bir gezegenden dönen araştırma gemisi, FTL bubble'dan erken çıkmak zorunda kalır ve Oort bulutu bölgesinde, Dünya'ya 1 ışık yılı uzakta mahsur kalır. Yola devam etmek için kaynak toplamak gerekir. Kaynak toplarken küçük botların saldırısına uğrarlar — botlar ilerledikçe büyür ve tehlikeli hale gelir. Ekip Dünya ile iletişim kurmaya çalışırken botların kaynağını araştırır.

**Gizem:** Botların kökeni kasıtlı olarak belirsiz bırakılır. Her bölümden sonra çelişkili ipuçları ortaya çıkar — tanıdık semboller, hiç görülmemiş teknolojiler, hibrit yapılar. Araştırma gemisinin giderken bir şeyi tetiklemiş olabileceği ima edilir. Hikaye kesin bir cevap vermez.

**Anlatım:** Bölüm aralarında pilotların kısa diyalogları ile aktarılır. Hikayeye dayanmak yerine oynanış ön plandadır.

---

## Oyun Tasarımı

### Tür
Incremental Space Shooter with Base Management. Referans: FTL, Into the Breach ruhu.

### Temel Mekanikler
- **Ana gemi:** Devasa, sabit pozisyonda, mermilerden kaçamaz
- **Silah:** Mouse/touch ile nişan alınır, sol tık/dokunma ile ateş edilir
- **Silah tipleri:** Lazer (kalkana etkili, sürekli enerji tüketir), Plazma (şarj edip burst atar), Kinetik (mermili, enerji yemez)
- **Enerji sistemi:** Jeneratör ürettiği enerjiyi kalkan/silah arasında dağıtır; Boost sistemi anlık yönlendirme sağlar
- **Toplayıcı gemiler:** Tuşa basarak asteroid/enkaz bölgesine gönderilir, geri çağrılabilir
- **Kaynak:** 2 tip — Ham madde (fiziksel sistemler için), Enerji kristali (enerji sistemleri için)
- **Upgrade sistemi:** Anlık, bölüm içinde. Komponent satılıp başka şey alınabilir
- **Point defence:** Otomatik kısa menzilli sistemler, küçük hızlı hedeflere karşı

### Gemi Upgrade Slotları
Motor, Enerji Jeneratörü, Kalkan, Ana Silah (slot 5), Otomatik Turretler, İkincil Silahlar (point defence dahil)

### Ana Silah (Slot 5) — Tasarım Kararları
- Oyun başında **Lazer Mk1** ücretsiz kurulu gelir
- Her silah tipi (**Lazer / Kinetik / Plazma**) bağımsız Mk1→Mk2→Mk3 zincirine sahip
- Tüm tipler aynı anda farklı tier'larda olabilir; switch butonu ile aktif tip seçilir
- Yeni tip satın alınınca `_unlockedWeapons` dict'e eklenir — normal slot mekanizması bypass edilir
- Upgrade UI'da slot 5 seçilince her tip için ayrı satır: kilitliyse **Satın Al**, açıksa **Seç** + **Upgrade** butonu
- Upgrade, aktif olmayan tipler için de yapılabilir (bağımsız zincir)

### Boost Sistemi — Tasarım Kararları
- İki boost modu var: **Kalkan Boost** ve **Silah Boost** — birbirini iptal eder, toggle çalışır
- **Kalkan Boost aktif:**
  - Kalkan şarj hızı ×3, enerji maliyeti ×5
  - Silah hasarı ×1/3, mermi boyutu ×0.6
  - Lazer enerji maliyeti ×1/3
- **Silah Boost aktif:**
  - Silah hasarı ×2, mermi boyutu ×1.5
  - Lazer enerji maliyeti ×3
  - Kalkan şarjı durur
- Boost HUD upgrade ekranı açıkken gizlenir

### Düşman Çeşitliliği — Tasarım Kararları

**Uzak saldırganlar** (hareket ederken periyodik ateş, mermileri kalkan üzerinden hasar verir):
- **Swarm:** HP 30, hız 3, lazer'e kırılgan (×1.5). Ateş hızı ~4sn.
- **Armored:** HP 80, hız 1.5, kinetike dirençli (×0.3), plazmaya zayıf (×1.8). Ateş hızı ~6sn, hasar 15.
- **Shield:** HP 50 + 40 kalkan, hız 2. Kalkana karşı kinetik (×1.5) kırar, lazer etkisiz (×0.25). Ateş hızı ~3sn.

**Yakın saldırganlar** (geminin kalkan çemberine kadar girer, komponentlere doğrudan ateş eder):
- **Bomber:** Gemiye yaklaşır (x≈2), hover ederken 3 mermi atar, her mermi rastgele bir operasyonel komponenti hedefler. Kalkan bypass eder. Sonra hızla geri çekilir.
- Bomber mermileri doğrudan `ShipComponentBase.TakeDamage()` çağırır — kalkan sistemi araya girmez
- İleride **Fighter** tipi de eklenebilir (tasarım henüz netleşmedi)

**Gelecek:** Büyük düşman gemilerinin attığı **area-effect bombalar** komponentlere de hasar verebilir (tasarım kararı bekliyor).

### Kaynak Ekonomisi — Tasarım Kararları

**İki kaynak:** Ham madde (metal) fiziksel sistemler için, Enerji kristali enerji
sistemleri için.

**Toplama zinciri:** Düşman/asteroit yok olur → `Debris` düşer → `CollectorShip`
toplar → hangara döner → `ResourceInventory`'ye boşaltır.

**Enkaz (`Debris`) kuralları:**
- Kısa bir sürüklenmeden sonra **durur** (Drag = 0.9). Sahadan kaçıp gitmez, toplayıcının
  yetişebileceği yerde kalır.
- Tipe göre renklenir: ham madde kahverengi, kristal **mavimsi gri**.
- Ömrü 180 sn. Son 60 saniyede kademeli olarak %30 saydamlığa solar, son 10 saniyede
  yanıp söner. Sürüklenme durduğu için bu sayaç artık gerçekten işliyor —
  eskiden enkaz zaten çoktan ekran dışına kaymış oluyordu.

**Toplayıcı kuralları:**
- Tip ayrımı yapmaz, ne bulursa toplar. Kargo tip başına ayrı sayılır (`_cargo[]`),
  kapasite toplam üzerinden işler, hangarda hepsi kendi envanterine boşaltılır.
- Toplanacak enkaz kalmadıysa ve kargoda bir şey varsa boşta beklemez — ana gemiye
  dönüp boşaltır. Sadece kargo boşken hangar etrafında bekler.
- Enkaz 180 saniye sonra kaybolur; toplanamayan kaynak yanar.

**Kristal kaynakları:**

| Kaynak | Kristal | Not |
|--------|---------|-----|
| Kalkanlı düşman | `maxShield × 0.1` | Kalkan teknolojisi kristal tabanlı. Bölüm HP çarpanı kalkanı da büyüttüğü için getiri bölümle artar. |
| Asteroit (tam parçalanmış büyük) | ~3.7 | `Asteroid.CrystalChance` = %12, küçük parça başına 5 birim |
| Boss (komuta gemisi) | `maxShield × 0.1` ≈ 30 | Kalkanlı gemiyle aynı kural, 2–3 parçaya bölünür. Miktar henüz dengelenmedi. |

Kalkanlı düşman ≈ 4 kristal (bölüm 5'te), ≈ 7 kristal (bölüm 9'da). Büyük asteroit
kabaca bir kalkanlı düşmana denk — ama 205 HP'ye karşılık, yani HP başına daha az
verimli (0.030 vs 0.044). Asteroit pasif/opsiyonel kaynak olarak tasarlandı.

**Kristal talebi şimdilik arzın altında** (canlı katalogda kristal isteyen tek şey lazer
zinciri: oyun boyu 80). Bilinçli olarak ertelendi — ileride eklenecek yetenekler kristal
harcayacak ve fazlalık orada erirken denge kurulacak. Erken müdahale edilmeyecek.

**Metal sıfırdan başlar.** `ResourceInventory.metal = 0`. Önceki 500 değeri test içindi.

### Asteroitler — Tasarım Kararları

`Asteroid` düşman değildir; ateş edilmezse sürüklenip gemiye çarpar.

- Üç boyut: **Large → Medium → Small**. Her boyut yok edilince 2–3 parçaya bölünür.
- **Hız iki bileşenlidir:** `_drift` (sürüklenme, korunur) + `_separation` (ayrılma itmesi,
  sönümlenir). Parça, ana parçanın sürüklenmesinin %50'sini miras alır ve küçük bir
  ayrılma itmesi alır; itme ~0.5 sn'de söner. Sonuç: parçalar dağılıp uzaklaşmaz,
  ana parçanın yanıbaşında kümelenir.
- **HP barı var** — düşmanlarla aynı `HealthBar`. HP tamken görünmez, hasar alınca çıkar.
- **Silah dirençleri:** kinetik **×2.0**, lazer **×0.25**, plazma nötr. Kayaya karşı
  raylı top; lazer neredeyse işe yaramaz.
- Yalnızca **Small** enkaz bırakır. Büyük parçalar sadece bölünür — kaynak için
  zinciri sonuna kadar götürmek gerekir.
- Gemiye çarparsa hasar verir ve **dağılır** (bölünmez, kaynak bırakmaz). Kalkan
  aktifse kalkanda dağılır, değilse gövdeye vurur.
- Hasar `DamageUtil` üzerinden gelir; tüm silahlar ve turretler otomatik işler.

| Boyut | HP | Sprite | Çarpma hasarı | Ölünce |
|-------|-----|--------|---------------|--------|
| Large | 60 | 68px | 30 | 2–3 Medium |
| Medium | 28 | 40px | 18 | 2–3 Small |
| Small | 12 | 22px | 8 | 1 enkaz (5 birim) |

Bir büyük asteroidin tam zinciri: **205 HP → ~6.2 küçük parça → ~25 metal + ~3.7 kristal**.
Bu HP kinetik ile ~103'e düşer, lazer ile ~820'ye çıkar.

**Spawn:** `ChapterManager.UpdateAsteroidField()` bölümün `asteroidCount` yoğunluğunu
korur — sayım parçaları da kapsar, yani bölünen bir asteroit alanı doldurur.
Asteroitler wave ilerlemesini engellemez (`UpdateWaitClear` onları saymaz).
Yoğunluk `ChapterData.AsteroidsForChapter()`: bölüm 1–2 → 2, 3–6 → 3, 7+ → 4.

### Gemi Hareket Modeli — Tasarım Kararları

Tüm AI gemileri (`EnemyBot`, `FighterShip`, `CollectorShip`) `ShipMovement` üzerindeki
**roket-itkili uçuş modelini** kullanır. Amaç: gemiler kaymak yerine uçsun.

**Kurallar:**

1. **İtki yalnız burun doğrultusunda.** Gemi yana doğru hızlanamaz — arkasında roket
   varmış gibi kuyruktan buruna doğru ivmelenir.
2. **Dönüş hızı ankı hıza bağlıdır.** Dururken kendi ekseninde dönebilir, tam hızda
   yavaş döner. Kavis yarıçapı = hız / dönüş hızı → hızlıyken geniş çember,
   yavaşken dar çember.
3. **Hareket vektörü burnu takip eder** (`grip`). Klasik fizikte böyle olmaz; bilinçli
   bir tercihtir, uzay gemisi hissini doğal kılar. `grip < 1` ise kavislerde dışa
   savrulma kalır. Dönmek hız kaybettirmez — sadece yön değiştirir.
4. **Kalan yanal kayma sönümlenir**, drift her zaman toparlanır.
5. **Burun saptıkça gaz kesilir** (90°+ sapmada tamamen). 70°'den sonra retro devreye
   girer. Gemi önce yavaşlar, döner, sonra ivmelenir — U dönüşü bir manevradır.
6. **Varışta fren.** `MoveToward` fren mesafesini v²/(2a) ile hesaplar. Hedef geminin
   kavis çemberinin içinde kalıyorsa (`IsInsideTurnCircle`) dönerek ulaşılamaz —
   gemi yavaşlar, kavis daralır, hedef erişilebilir olur. Bu olmadan ağır gemiler
   hedefin etrafında sonsuz çember çizer.
7. **Komut Update'te, entegrasyon LateUpdate'te.** Aynı karede çift ilerleme olmaz.
   Komut verilmeyen kare = süzülme (coast).
8. **Nişan alırken net, diğer zamanlarda kaçamak.** Ateş menzilindeyken gemi burnunu
   hedefe net çevirir. Diğer tüm anlarda burun 1–2 saniyede bir yenilenen küçük
   rastgele açılarla salınır (`evasive: true`). Sapma anî değildir, yeni hedefine
   süzülür — rota da burnu takip ettiği için gemi yılankavi bir iz çizer.
9. **Kaçış radyal değil çapraz.** Hedeften tam ters yönde kaçmak tahmin edilebilir
   ve nişan almayı kolaylaştırır. Kaçış yönü, uzaklaşma vektörünün `escapeAngle`
   kadar (±%40 dağılımla) sağa veya sola döndürülmüş hâlidir; üstüne kaçamak
   salınım biner.
10. **Kaçamak davranış bölüme göre ölçeklenir.** `ChapterData.enemyEvasionMultiplier`
   hem `evasionAngle` hem `escapeAngle` değerlerini çarpar; `ChapterManager`
   düşmanın runtime kopyasına uygular — `enemyHpMultiplier` ile aynı mekanizma.

**Uçuş karakteri parametreleri** (`EnemyTypeData` içinde, tip başına):

| Tip | agility | grip | evasionAngle | escapeAngle | Karakter |
|-----|---------|------|--------------|-------------|----------|
| Swarm | 1.5 | 0.95 | 18° | 40° | Küçük, kıvrak — dar kavis, en oynak uçuş |
| Shield | 0.85 | 0.85 | 12° | 40° | Orta sınıf, dengeli |
| Armored | 0.55 | 0.72 | 6° | 30° | Hantal — geniş kavis, rotasını korur |
| Bomber | 1.15 | 0.93 | 15° | 40° | Hızlı avcı — dalışta geniş, frende dar kavis |
| BombRunner | 0.6 | 0.8 | 0° | 0° | Düz hat bomba koşusu — salınım ve kaçış yok |
| Fighter (oyuncu) | 1.4 | 0.94 | 12° | 40° | Kıvrak avcı |
| Collector (oyuncu) | 1.6 | 0.97 | 0° | — | İş gemisi — düz ve verimli gider |

`evasionAngle` burnun nişan dışı anlardaki max rastgele sapması, `escapeAngle` ise
kaçarken radyal yönden sapmasıdır. Tablodaki değerler **tam** seviyedir (bölüm 3+).

**Zorluk eğrisi** — `ChapterData.EvasionForChapter()` = `Mathf.InverseLerp(1f, 8f, bölüm)`.
1. bölümde kaçamak davranış **tamamen kapalı**, 8. bölümde tam açık, arası doğrusal.

| Bölüm | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8+ |
|-------|---|---|---|---|---|---|---|----|
| Çarpan | 0.00 | 0.14 | 0.29 | 0.43 | 0.57 | 0.71 | 0.86 | 1.00 |

Tam açıkken Swarm'da ~27° burun genliği, 29 birimlik yolda ~1.6 birim rota salınımı,
kaçışta radyal yönden ~26° sapma.

`agility` dönüş hızı çarpanıdır, kavis yarıçapı ile ters orantılıdır.
Referans dönüş hızı = `enginePower / mass × 30 × agility` derece/sn.

**İstisna — `omniThrust`:** Boss/taşıyıcı gibi devasa gemiler manevra iticileriyle
her yöne itebilir, burun yönü hareketten bağımsızdır. Bu gemiler kavis çizmez,
mevki tutar. 1–5, 8 ve 9 numaralı kurallar onlar için geçerli değildir.

**Taktik AI (`ShipBrain`) ile ilişki:** Brain'in verdiği her yön komutu artık
"burnunu şuraya çevir ve it" anlamına gelir, anlık hız değişimi değil. Bu yüzden
`Orbit` yörünge yarıçapı `MinTurnRadius`'un altına inemez, `HoverFire` geri
çekilirken 180° dönmek yerine burnu hedefte tutup retro ile uzaklaşır.

Kaçamak manevra kararı da Brain'e aittir: `Approaching` ve `Disengaging` her zaman
kaçamaklıdır, `Engaging` sırasında ise yalnızca ateş menzili dışındayken. Bomber'ın
`AttackRun`'ında salınım, hedefe `fireRange × 2` mesafesine girene kadar açıktır —
nişan hattına girdikten sonra kesilir.

### Komponent HP Sistemi — Tasarım Kararları
- Her komponent kendi `currentHP / maxHP`'sini `ShipComponentBase`'de tutar
- Oyuncu komponentlerin HP'sini göremez (UI şimdilik yok; ileride eklenebilir)
- HP sıfırlandığında zorluk ayarına göre davranış:
  - **Easy:** Komponent deaktif kalır (`_deactivated = true`), GO yok edilmez. RepairUnit maxHP'ye tamir edince otomatik yeniden açılır.
  - **Normal / Hard:** `ShipLoadout` slot'u temizler, GO yok edilir. Yeniden kurulum gerekir.
- `DifficultyManager.Current` statik; oyun başında ayarlanır (default: Normal)
- RepairUnit en düşük HP oranlı komponenti önceliklendirir; Easy modda deaktif komponentleri de tamir eder

### Otomatik Turretler — Tasarım Kararları

Slot'a kurulunca otomatik ateş açar, oyuncu müdahalesi gerekmez. Enerji tüketir.

**Fire Rate skalası:** Düşük = 20sn'de 1 atış (`fireRate=20f`) · Orta = 5sn'de 1 (`fireRate=5f`) · Hızlı = saniyede 1 (`fireRate=1f`)

**Mermi ömrü** saniye cinsinden tanımlanır (`Destroy(go, lifeTime)`). Ömür × hız = efektif menzil.

| Tip | Fire Rate | Hasar | Mermi Hızı | Enerji | Ömür | Özellik |
|-----|-----------|-------|------------|--------|------|---------|
| **Gatling** | Hızlı (1f) | Düşük | Orta | Düşük | 3s | Şarjör + reload mekaniği |
| **Plazma** | Düşük (20f) | Orta | Düşük | Yüksek | 4s | — |
| **Lazer** | Orta (5f) | Orta | Yüksek | Yüksek | 4s | — |
| **Roket** | Çok düşük (30f+) | Yüksek | Orta | Düşük | 10s | Güdümlü, hedefi izler |
| **Point Defence** | Hızlı (1f) | Düşük | Orta | Orta | 0.8s | Gelen roketleri + Bomber/Fighter/Drone hedefler, kısa menzil |

**Hedefleme kuralları:**
- Gatling / Plazma / Lazer / Roket: en yakın düşman gemisini hedefler
- Point Defence: önce gelen roketleri, sonra kalkan içine girmiş küçük gemileri (Bomber, Fighter, Drone) hedefler
- İnce ayarlar (kesin değerler) sonraya bırakıldı

### Bölüm Yapısı
8-10 bölüm, ortalama 2-3 dakika. Çeşitlilik:
- Asteroid/enkaz bölgesi (kaynak + küçük botlar)
- Sürü bölümü (çok sayıda hızlı düşman)
- Pusu bölümü (toplayıcılar tuzağa düşer)
- Taşıyıcı boss (uzun, aşamalı)
- Karanlık bölüm (sensörler çalışmıyor)

---

## Teknik Kararlar

### Ortam
- **Unity 6.3 LTS** (6000.3.11f1)
- **Template:** Universal 2D
- **Input:** New Input System (UnityEngine.InputSystem) — eski Input sistemi KULLANILMAZ
- **Render:** URP 2D
- **Platform:** PC + Android + WebGL

### Kod Kuralları
- `FindObjectOfType` yerine `FindFirstObjectByType` kullan (Unity 6 uyumu)
- Mouse input: `Mouse.current.position.ReadValue()`
- Touch input: `Touchscreen.current.primaryTouch.position.ReadValue()` — null check ile
- Tüm objelerin scale'i **uniform** olmalı (X=Y=Z). Şekil ve proporsiyon bilgisi sprite texture boyutundan gelir
- Prefablar: `Assets/Prefabs/` klasöründe
- Scriptler: `Assets/Scripts/` klasöründe

### Sahne Hiyerarşisi Kuralları
- Health/Shield barları her zaman ilgili objenin **child'ı** olmalı
- WeaponMount, PlayerShip'in child'ı
- Body (gövde sprite), PlayerShip'in child'ı
- Barlar world space'de her zaman dik ve yatay kalır (`rotation = Quaternion.identity`)

---

## Mevcut Scriptler

| Script | Görev |
|--------|-------|
| PlayerShip.cs | Ana gemi, sabit pozisyon, `TakeDamage(amount, bypassShields)` |
| WeaponMount.cs | Mouse'a dönen silah noktası |
| WeaponController.cs | Kinetic/Laser/Plasma ateş mantığı, Boost çarpanları |
| Bullet.cs | Oyuncu mermisi — hareket, trigger collision, 3sn sonra yok |
| EnemyBot.cs | Swarm/Armored/Shield/Bomber — hareket, ateş, hasar direnci, Bomber state machine |
| Asteroid.cs | Parçalanabilir asteroit — Large→Medium→Small, çarpma hasarı, enkaz bırakır |
| Debris.cs | Enkaz — sürüklenip durur, tipe göre renk, ömür sonunda solup yanıp söner |
| ShipMovement.cs | Roket-itkili uçuş modeli — burun itkisi, hıza bağlı dönüş, grip, fren |
| ShipBrain.cs | Taktik AI — Orbit/Strafe/HoverFire pattern'ları, ShipMovement'e komut verir |
| EnemyBullet.cs | Düşman mermisi — hull modu (kalkan üzerinden) veya komponent modu (doğrudan) |
| EnemySpawner.cs | Ağırlıklı rastgele tip seçimi ile düşman spawn eder |
| StarField.cs | 400 yıldız, -15/+15 birim arası random pozisyon |
| CameraController.cs | Parallax kayma + zoom, power curve (t²) |
| HealthBar.cs | Can/kalkan barı, SpriteRenderer tabanlı, child olarak eklenir |
| GameManager.cs | HP sıfırlanınca Game Over, Restart, TimeScale yönetimi |
| ShipComponentBase.cs | Tüm komponentlerin base class'ı — HP, TakeDamage, zorluk-aware yıkım |
| ShipLoadout.cs | 10 slot yönetimi, silah zinciri + switch sistemi |
| ComponentDefinition.cs | ScriptableObject — komponent istatistikleri ve upgrade zinciri |
| DifficultyManager.cs | Easy/Normal/Hard statik seçim |
| BoostController.cs | Shield/Weapon boost toggle (static) |
| BoostHUD.cs | Boost butonları Canvas, upgrade açıkken gizlenir |
| ShieldGeneratorComponent.cs | Kalkan üretimi, Boost çarpanları |
| GeneratorComponent.cs | Enerji üretimi |
| RepairUnitComponent.cs | En hasarlı komponenti otomatik tamir eder |
| EnergyBus.cs | Enerji dağıtım sistemi |
| ResourceInventory.cs | Ham madde + kristal envanteri |
| UpgradeUI.cs | Tab ile açılan upgrade ekranı, 4 panel layout |
| SlotVisual.cs | World-space slot göstergesi, tıklama ile UpgradeUI tetiklenir |

---

## Kamera Sistemi
- **Temel pozisyon:** (0, 0, -10), Orthographic Size: 5
- **Kayma:** Mouse ekranın %80'inden sonra başlar, maksimum 8 birim, power curve t²
- **Zoom:** Mouse ekranın %90'ından sonra başlar, Size 5→7
- **Formül:**
```csharp
float t = Mathf.Clamp01(delta.magnitude);
float moveT = Mathf.Clamp01((t - 0.8f) / 0.2f);
float curvedMoveT = Mathf.Pow(moveT, 2f);
float zoomT = Mathf.Clamp01((t - 0.9f) / 0.1f);
```

---

## HealthBar Sistemi
- SpriteRenderer tabanlı, Canvas kullanılmaz
- **Kalkan barı:** Mavi, maxShield > 0 ise her zaman görünür
- **Can barı:** Hasar alınca görünür, 3sn sonra gizlenir, timer hasar alınca sıfırlanır
- `TakeDamage(float amount)` metodu ile hasar verilir
- Barlar parent döndüğünde `rotation = Quaternion.identity` ile dik kalır

---

# Starfarer — Güncel Yapılacaklar Listesi

## Tamamlananlar

- [x] Enerji sistemi — EnergyBus, CoreGenerator
- [x] Kalkan sistemi — recharge delay, ShieldGeneratorComponent
- [x] Upgrade sistemi — ShipLoadout, ComponentDefinition (hardcoded)
- [x] Upgrade UI — layout, slot tıklama, hover detay, 4 panel
- [x] Kamera zoom — upgrade açılınca gemiye zoom, Tab'la geri
- [x] World slot daireleri — SlotVisual, renk göstergesi
- [x] Temel silah, mermi, düşman, spawner, GameManager
- [x] [Kur] / [Sat] / [Upgrade] butonları — kaynak yetersizse inaktif, maliyet kırmızı
- [x] ResourceInventory HUD — ham madde + kristal GeneralPanel'de
- [x] Silah tipleri — Kinetic/Laser/Plasma, her biri bağımsız Mk1→Mk2→Mk3 zinciri
- [x] Ana silah switch mekanizması — per-tip Satın Al / Seç / Upgrade, bağımsız upgrade
- [x] Boost sistemi — Kalkan/Silah boost toggle, BoostHUD, çarpan efektleri
- [x] Düşman çeşitleri — Swarm/Armored/Shield/Bomber, ateş sistemi, Bomber komponent hedefleme
- [x] Komponent HP sistemi — Easy deaktivasyon / Normal+Hard yıkım, DifficultyManager
- [x] Gemi hareket modeli — roket-itkili uçuş, hıza bağlı kavis, grip, kavis-içi hedef freni
- [x] Kaçamak manevra — nişan dışı burun salınımı + çapraz kaçış, bölüme göre ölçekli
- [x] Toplayıcı karışık kargo — tip başına sayım, hangarda tipe göre boşaltma
- [x] Asteroitler — Large→Medium→Small parçalanma, çarpma hasarı, enkaz bırakma
- [x] Asteroit cilası — HP barı, kinetik ×2 / lazer ×0.25 direnç, parçalar kümelenir
- [x] Enkaz cilası — sürüklenip durur, kristal mavimsi gri, ömür sonunda solup yanıp söner
- [x] Boss kristal düşürür; metal sıfırdan başlar; kaçamak eğrisi bölüm 1→8 doğrusal

---

## Sıradaki Adımlar (öncelik sırasıyla)

- [x] Otomatik turretler — Gatling/Plazma/Lazer/Roket/Point Defence, slot pozisyonuna kurulur
- [x] Toplayıcı gemiler + kaynak toplama sistemi — Debris → CollectorShip → ResourceInventory
- [ ] Stat upgrade sistemi — komponent başına %'lik stat artışları (damage, HP, fire rate vb.)
- [ ] Bölüm sistemi (8–10 bölüm, wave yapısı, bölüm arası geçiş)
- [ ] Boss taşıyıcı gemi
- [ ] Point defence turretleri — küçük/hızlı hedeflere odaklı otomatik turret
- [ ] Mobil UI
- [ ] Ses efektleri
- [ ] Gerçek sprite'lar — görsel iyileştirme

---

## Açık İşler — Kaynak Ekonomisi

Kristal çalışması sırasında çıkan, henüz kapatılmamış maddeler.

**Ertelendi (bilinçli karar):**
- Kristal talebinin arzın altında kalması. İleride eklenecek yetenekler kristal harcayacak;
  denge orada kurulacak, şimdi müdahale edilmeyecek.

**Teknik borç:**
- [ ] **İki ayrı komponent kataloğu var — konuşulacak.** Aynı komponentin (kalkan, jeneratör,
  hangar) iki yerde birbirinden farklı tanımı duruyor: `ShipLoadout.MakeShieldDef()` vb.
  kristal fiyatlı sürümü, `UpgradeUI.GetComponentDefs()` ham madde fiyatlı sürümü üretiyor.
  Oyunda satın alma UI'dan geçtiği için `ShipLoadout` sürümleri hiç kullanılmıyor —
  ama duruyorlar. Fiyat değiştirmek isteyen kişi yanlış dosyayı düzenleyebilir.
  Karar: hangisi tek kaynak olacak, diğeri silinecek.
- [ ] **EnemySpawner bölüm çarpanlarını uygulamıyor — testle netleşecek.**
  `ChapterManager.SpawnEnemy()` düşmanın kopyasını alıp bölümün HP/hasar/kaçamak
  çarpanlarını uyguluyor; `EnemySpawner.SpawnEnemy()` ise ham veriyi doğrudan kullanıyor.
  Normal oyunda `ChapterManager` onu kapattığı için etkisi yok. Sorun sadece
  EnemySpawner ile test edilirse çıkar: düşmanlar bölüm 1'de bile tam güçte gelir.

---

## Açık İşler — Hareket & Zorluk

- [ ] **Deterministik kaçış manevrası — konuşulacak.** Mevcut kaçamak davranış rastgele;
  ustalaşmayı ödüllendirmiyor. Öğrenilebilir, tekrarlanabilir kaçış desenleri (tip başına
  imza manevrası) daha zevkli olabilir. Rastgelelik tamamen kalkmalı mı, yoksa desen
  seçimi mi rastgele olmalı?
- [ ] **Kamera kontrolü iyileştirmesi.** Daha uzağa, özellikle yukarı/aşağı bakabilmeli.
  Mevcut: taban (0,0,-10), size 5, mouse %80'den sonra max 8 birim kayma, %90'dan sonra
  size 5→7 zoom.
- [ ] **Enkaz ömrü vs toplayıcı kapasitesi.** Enkaz artık sürüklenip durduğu için 180 sn
  gerçekten sahada duruyor — bölümün neredeyse tamamı. Asteroitlerle birlikte sahada
  çok daha fazla enkaz olacak; toplayıcılar yetişmezse kaynak yanar ve ekran kalabalıklaşır.
  Süre test sonrası kısaltılabilir.
- [ ] **Sistematik denge çalışması.** Düşman tehdit katsayıları (`threatScore`) deneme
  sonuçlarına göre belirlenmeli. Teknik borçlar ve eksik mekanikler bittikten sonra.

---

## Bekleyen Tasarım Kararları

- [ ] Fighter tipi düşman — Bomber'dan nasıl ayrışır? (tasarım netleşmedi)
- [ ] Area-effect bombalar — büyük düşman gemilerinden, komponentlere hasar verir mi?
- [ ] Komponent HP göstergesi — oyuncuya nasıl gösterilecek? (UI tasarımı yok)
- [ ] Zorluk seçim ekranı — oyun başında mı, menüde mi?
- [ ] Stat upgrade sistemi detayı — hangi statlar, kaç seviye, maliyet eğrisi
