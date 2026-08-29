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

**Bölüm 5+ tipleri** — her biri oyuncunun bir sistemine baskı yapar, süs değildir:

| Tip | Tehdit | Davranış | Neyi zorlar |
|---|---|---|---|
| **Interceptor** (Avcı) | 6 | Çok hızlı, kırılgan, yüksek kaçamak | Turret hedeflemesi ve isabet |
| **Artillery** (Obüs) | 9 | Ekran kenarından uzun menzilli yavaş mermi | Menzil; oyuncuyu ilerlemeye zorlar |
| **Jammer** (Karıştırıcı) | 11 | Menzilindeyken jeneratör üretimini %40 kısar | Enerji — öncelik hedefleme |
| **Phantom** (Hayalet) | 10 | 4.5 sn'de bir 2 sn vurulamaz | Sürekli DPS yerine burst |
| **Regenerator** (Onarıcı) | 13 | Çevresini saniyede 6 HP onarır | DPS eşiği — yavaş build duvara çarpar |
| **Leech** (Sülük) | 8 | Komponentlere yapışır | Point Defence talebi |
| **Splitter** (Bölünen) | 12 | Ölünce ikiye ayrılır (%50 HP) | Alan hasarı talebi |
| **Juggernaut** (Kaleci) | 20 | Zırh +12, çok yavaş, 200 HP | Zırh eşiğinin doruk testi |

Jammer `EnergyBus.JamFactor` üzerinden üretimi kısar; Phantom faz sırasında
`IsValidTarget = false` döner (turretler boşa mermi harcamasın); Splitter
`EnemySpawner.Spawn` ile iki parça üretir; Regenerator aurası 0.25 sn'de bir tarar.

### Zırh Eşiği — Tasarım Kararları

```
efektif = max(hasar − zırh, hasar × 0.10)
```

Zırh **atış başına** ve **dirençlerden önce** uygulanır. Sıra önemli: ters olsaydı
dirençli düşmanlarda zırh iki kez cezalandırırdı.

Neden var: gemi DPS'i silah ve turretlerin **toplamı**, maliyet ise dışbükey —
zırh olmadan çok sayıda zayıf silah, az sayıda güçlü silahı her zaman yener.
Zırh bu eşitliği bozar ve atış başına hasarı ödüllendirir. Zırh 18'e karşı
10 hasarlı bir atış 1.0, 60 hasarlı bir atış 42 geçirir.

**Turret hedeflemesi zırhı bilir** (`ITurretTarget.ArmorValue`). Zırh
`RawDamageToKill`'e gömülemez çünkü etkisi turretin atış hasarına bağlıdır;
turret `EffectiveShotDamage`'ını bildirir, `TurretTargeting` cezayı hesaplar.
Bu olmadan turret asla vuramayacağı hedefe kilitlenip mermi harcardı.

Zırh iki kaynaktan gelir ve **toplanır**: levelin taban zırhı (`LevelCurve.Armor`)
+ tipin kendi bonusu (`EnemyTypeData.armor`). Çarpılsaydı zırhsız tipler sonsuza
dek zırhsız kalırdı.

**Gelecek:** Büyük düşman gemilerinin attığı **area-effect bombalar** komponentlere de hasar verebilir (tasarım kararı bekliyor).

### Komponent Kataloğu — Tasarım Kararları

Tüm komponent tanımlarının tek sahibi **`ComponentCatalog`**. Önceden tanımlar hem
`ShipLoadout` hem `UpgradeUI` içinde ayrı ayrı üretiliyordu; aynı kalkanın iki farklı
adı, statı ve maliyet kaynağı vardı.

**Katman ayrımı:** `ComponentCatalog` ne var → `ShipLoadout` ne kurulu → `UpgradeUI`
nasıl gösteriliyor.

**Tek kalkan tipi.** Başlangıçta kurulu gelen kalkan mağazadakinin ta kendisidir (Mk1);
ayrı bir "başlangıç sürümü" yoktur. İkinci bir kalkan almak yine Mk1 almaktır.
Enerji sistemi olduğu için kristalle alınır.

**Bedava başlangıç komponentleri normal `sellValue` taşır** — oyuncu bedava geleni
satıp yerine başka bir şey kurabilsin diye bilinçli. Stratejik esneklik.

| Zincir | Kaynak | Mk1 | Mk2 | Mk3 |
|---|---|---|---|---|
| Kalkan Jeneratörü | Kristal | 25 → 50 kalkan, 0.8 şarj | 45 → 100, 1.8 | 70 → 170, 3.0 |
| Enerji Jeneratörü | Metal | 35 → 10 üretim | 65 → 18 | 110 → 28 |
| Onarım Birimi | Metal | 30 → 2.0 tamir | 55 → 4.0 | 90 → 7.0 |
| Depo | Metal | 40 → +250 metal / +50 kristal | 80 → +600/+120 | 150 → +1200/+250 |

Onarım Mk1 kasten yavaş (eskiden 8.0'dı) — ilk seviyede tamir savaşın gidişatını
belirlememeli.

**Depo komponenti.** Kaynak tavanı artık sabit değil: `ResourceInventory` taban
kapasiteyi (150 metal / 50 kristal) kurulu depoların toplamıyla topluyor. Yıkılan
veya deaktif olan depo kapasite vermez — hasar almak biriktirdiğin kaynağı da yakar.

Kapasite kilidi bilinçli: kalkan Mk1→Mk2 (35 kristal) taban tavanla yapılabilir,
Mk2→Mk3 (52 kristal) için önce depo kurmak gerekir.

### Yükseltme Sistemi — Tasarım Kararları

**Yapı korundu, sayılar ayarlandı.** Bir oturumda tier zincirleri kaldırılıp
komponent başına tek stat eksenine geçildi; sistem dengelenmişti ama upgrade
ekranı ~22 yükseltme kararından 9 tane tek-butonluk "Yükselt"e inmişti. Geri
alındı: **derinlik dengeye tercih edildi.** Sonra ölçüldü ve o refactor'ın
gereksiz olduğu görüldü — bir sabit yeterliymiş.

**İki eksen:** tier zinciri (Mk1→Mk3, mağazadan) + komponent başına stat
seviyeleri (8 seviye). Sayıların sahibi `BalanceConfig`.

| Ayar | Değer | Not |
|---|---|---|
| `statStep` | **1.25** | seviye başına güç; eskiden 1.5 idi |
| `statCostGrowth` | 2.5 | seviye başına maliyet |
| `sellRefundRatio` | 0.40 | kurulum + stat harcamasının iadesi |
| `energyGrowth` | 1.30 | seviye başına enerji tüketimi |

**Neden 1.5 değil 1.25:** turret ve ana silahta hasar **ve** ateş hızı ikisi de
DPS'e çarpımsal giriyor. 1.5 ile Lv5/Lv6 demek `1.5^11 = 86×` demekti; ölçülen
oyuncu üstünlüğü kampanya boyunca 4.5× → 26.5×'e kayıyordu. İlk 25 level
sağlamdı, kırılma 50'den sonra başlıyordu.

| Level | 1 | 25 | 50 | 75 | 100 |
|---|---|---|---|---|---|
| `statStep = 1.5` (eski) | 4.5 | 3.9 | 11.1 | 21.0 | **26.5** |
| `statStep = 1.25` (şimdi) | 4.5 | 3.2 | 4.4 | 4.9 | **4.3** |

Simülasyon "en ucuz statı al" davranışı ve %100 isabet varsayar; mutlak
değerler değil, **eğrinin şekli** güvenilirdir.

**Düzeltilen üç hata:**

- **Stat seviyeleri tier upgrade'de siliniyordu.** `UpgradeComponent` komponenti
  yok edip yenisini kuruyor, `StatLevels` sıfırlanıyordu — oyuncu binlerce
  kaynak harcadığı yatırımı Mk2'ye geçerken sessizce kaybediyordu. Artık taşınır.
- **Mk1'de ucuz stat basma istismarı.** Stat fiyatı o anki tier'ın fiyatından
  hesaplanıyordu; Mk1'de statları maxlayıp sonra tier atlamak 4× ucuza geliyordu,
  yani optimal oyun tier'ları geciktirmekti. Fiyat artık zincirin SON tier'ına
  sabitlenmiştir (`ComponentDefinition.statCostBase`).
- **UI `Lv x/5` diyordu ama tavan 8'di.** Etiket `MaxStatLevel`'den okunuyor.

**Satış iadesi stat harcamasını da kapsar.** Eskiden yalnızca tier'ın
`sellValue`'su dönüyordu: Mk3 kalkana binlerce kristal stat basıp satan oyuncu
28 kristal alıyordu. Artık `(kurulum + stat harcaması) × 0.40`. Sat butonu
tutarı gösterir.

### Enerji Bütçesi — Tasarım Kararları

`EnergyBus`ın üretim/tüketim muhasebesi uzun süre yazılıydı ama **kapalıydı**:
her komponent `Awake`'de `energyConsumption = 0f` yapıyordu, dolayısıyla
`TotalConsumption` her zaman sıfırdı. Artık besleniyor.

```
tüketim = baseEnergyCost × 1.30^(en yüksek stat seviyesi)
```

**En yüksek seviye kullanılır, toplam değil.** Toplama bağlansaydı yedi izli
Hangar, iki izli turretten katbekat fazla enerji yerdi; oysa ikisi de "aynı
derecede yükseltilmiş". Yan etkisi bilinçli: geride kalan bir izi yükseltmek
bedavadır, en yüksek izi zorlamak enerji ister.

| Aşama | Üretim | Tüketim | Net |
|---|---|---|---|
| Başlangıç (Jen Mk1 + Kalkan + Hangar) | 10.0 | 3.5 | +6.5 |
| +1 turret | 10.0 | 4.5 | +5.5 |
| Erken orta (Jen Mk2, 3 turret, onarım) | 18.0 | 17.7 | **+0.3** |
| Geç (Jen Mk3 + üretim Sv6, hepsi Mk3 Sv6) | 106.8 | 68.5 | +38.3 |

Erken ortada gerçekten sıkar — jeneratöre yatırım yapmak zorunludur ve o yatırım
silahtan çıkar. Geç oyunda gevşer; sıkmaya devam etmesi isteniyorsa ayar noktası
`energyGrowth`. **Test edilecek.**

**Enerji yetersizliği kaynak yetersizliğinden AYRI gösterilir** — ikisi aynı gri
butonla anlatılsaydı oyuncu jeneratörü suçlamayı akıl edemezdi. Yükseltme satırı
turuncuya döner, ek enerji yükünü (⚡) ve ne kadar eksik olduğunu yazar.

Kapı hem stat hem tier yükseltmesinde, hem de kurulumda uygulanır; kaynak
kontrolünden ÖNCE — kaynağı harcayıp enerjiye takılmak kötü bir sürpriz olurdu.

### Kayıt ve Level Seçimi — Tasarım Kararları

100 level tek oturumda oynanamaz; kayıt olmadan eğrinin ikinci yarısı test bile
edilemez.

- **Kayıt yalnızca level sınırlarında alınır** (`ChapterManager.CompleteLevel`).
  Savaş ortasında kaydetmek yarım kalmış bir dalgayı geri yüklemeye çalışmak
  demek olurdu. Ölünce o levelin başına değil, son tamamlanan levele dönülür.
- **PlayerPrefs + JsonUtility, tek slot.** Prototip için yeterli; "kayıt slotu
  seçme" akışı oynanışa bir şey katmıyor.
- **Komponent tanımları runtime'da üretildiği için referansları kaydedilemez.**
  Tip + tier + (turret ise) uzmanlaşma yazılır, `ComponentCatalog.Resolve` ile
  geri bulunur.
- **Kaynaklar slotlar kurulduktan SONRA yazılır** — tavan depo komponentlerine
  bağlı; önce yazılsaydı taban tavana kırpılırdı.
- **Level seçimi ulaşılmış en yüksek levelle sınırlı** ve bölüm başlarına atlar
  (1, 11, 21 …). İstenen her levele atlamak testi kolaylaştırırdı ama ilerlemeyi
  anlamsız kılardı; bölüm ortasından başlamak da o bölümün yeni düşman tipini
  tanıtan leveli atlamak demek olurdu.

### Kaynak Ekonomisi — Tasarım Kararları

**İki kaynak:** Ham madde (metal) fiziksel sistemler için, Enerji kristali enerji
sistemleri için.

**Toplama zinciri:** Düşman/asteroit yok olur → `Debris` düşer → `CollectorShip`
toplar → hangara döner → `ResourceInventory`'ye boşaltır.

**Enkaz (`Debris`) kuralları:**
- Hız iki bileşenli: **saçılma** (patlama itmesi, ~1 sn'de söner) + **sabit sola
  kayma** (0.3 birim/sn, kalıcı). Enkaz asla durmaz; vaktinde toplanmazsa soldan
  çıkıp kaybolur. Tamamen dursaydı ekranın sağında kalan enkaz toplayıcının
  menzili (hangardan 12 birim) dışında sonsuza dek asılı kalırdı.
- Tipe göre renklenir: ham madde kahverengi, kristal **mavimsi gri**.
- İki şekilde kaybolur: soldan çıkarak (asıl yol, sahnenin genişliğine göre
  ~50–107 sn) veya 180 sn'lik ömrü dolarak (emniyet). Görsel uyarı hangisi önce
  gelecekse ona göre işler: kaybolmasına 25 sn kala solmaya başlar, 8 sn kala
  yanıp söner.
- Toplayıcı, topladığı enkaz sola kayarken onunla birlikte sürüklenir; enkaz
  menzil dışına çıkarsa bırakır — yoksa toplayıcı sahneden dışarı çekilirdi.

**Toplayıcı kuralları:**
- Tip ayrımı yapmaz, ne bulursa toplar. Kargo tip başına ayrı sayılır (`_cargo[]`),
  kapasite toplam üzerinden işler, hangarda hepsi kendi envanterine boşaltılır.
- Toplanacak enkaz kalmadıysa ve kargoda bir şey varsa boşta beklemez — ana gemiye
  dönüp boşaltır. Sadece kargo boşken hangar etrafında bekler.
- Enkaz 180 saniye sonra kaybolur; toplanamayan kaynak yanar.
- **Kargo KESİRLİ tutulur.** Eskiden kargo tam birime yuvarlanıyor, artan kesir
  ayrı bir birikeçte bekliyor ve hangarda boşaltılırken sıfırlanıyordu. Level 1'de
  bir asteroit parçası 0.5 kaynak düşürür — yani asteroitten gelen kristalin
  TAMAMI, metalin de her seferin artığı sessizce yanıyordu. Kristal ayrıca
  parça başına %12 olasılıkla düştüğü için neredeyse hep tek başına kalıyor
  ve tam birime hiç ulaşamıyordu: oyuncu asteroitlerden hiç kristal alamıyordu.
  `ResourceInventory.Add` de artık `float` alır.

**Kristal kaynakları:**

**Kristal parçası KENDİ tabanını taşır.** Metal ve kristal aynı miktarı
paylaşıyordu ama bu hiç gerekçelendirilmemişti — tek bir kod yolunu
paylaşmalarından düşmüştü. Level 1'de parça başına 0.5 kaynak × %12 düşme
olasılığı, yani tam parçalanmış bir büyük asteroit **0.37 kristal** veriyordu;
sayaç kıpırdamıyordu. Nadir düşen şey İRİ düşmeli: `CrystalMinAmount = 3`.
Enkaz da parlak camgöbeği ve daha iri çizilir (eski mavimsi gri ~7 pikselde
kahverengi metalden ayırt edilemiyordu).

| Kaynak | Kristal | Not |
|--------|---------|-----|
| Kalkanlı düşman | `maxShield × 0.1` | Kalkan teknolojisi kristal tabanlı. Bölüm HP çarpanı kalkanı da büyüttüğü için getiri bölümle artar. |
| Asteroit (tam parçalanmış büyük) | ~2.3 | `CrystalChance` = %12, kristal parça başına en az 3 birim |
| Boss (komuta gemisi) | `maxShield × 0.1` ≈ 30 | Kalkanlı gemiyle aynı kural, 2–3 parçaya bölünür. Miktar henüz dengelenmedi. |

Kalkanlı düşman ≈ 4 kristal (bölüm 5'te), ≈ 7 kristal (bölüm 9'da). Büyük asteroit
kabaca bir kalkanlı düşmana denk — ama 205 HP'ye karşılık, yani HP başına daha az
verimli (0.030 vs 0.044). Asteroit pasif/opsiyonel kaynak olarak tasarlandı.

**Kristal talebi şimdilik arzın altında** (canlı katalogda kristal isteyen tek şey lazer
zinciri: oyun boyu 80). Bilinçli olarak ertelendi — ileride eklenecek yetenekler kristal
harcayacak ve fazlalık orada erirken denge kurulacak. Erken müdahale edilmeyecek.

**Metal sıfırdan başlar.** `ResourceInventory.metal = 0`. Önceki 500 değeri test içindi.

### Gelir Eğrisi — `BalanceConfig`

Gelir artık sabit değil, levelden türer. Sayıların tek sahibi `BalanceConfig`
(ScriptableObject; `Resources/BalanceConfig.asset` yoksa C# varsayılanları
kullanılır — `EnemyTypeData` factory'leriyle aynı desen).

Düşman geliri `threatScore × dropPerThreat(level)`. Eskiden çarpan sabit **×4**'tü
ve gelir yalnızca wave bütçesiyle büyüyordu; 100. levelde gereken kaynağı üretmek
**125× düşman** spawn etmeyi gerektirirdi. İki eksene ayrıldı:

| Bileşen | Formül | Lv1 → Lv100 |
|---|---|---|
| Wave tehdit bütçesi | `7 × 1.018^(n−1)` | 7 → 41 (5.8× daha çok düşman) |
| Tehdit başına drop | `2.1 × 1.031^(n−1)` | 2.1 → 43 (21× daha değerli düşman) |
| Asteroit bütçesi | `10 × 1.035^(n−1)` | 10 → 301 |
| Boss primi | `25 × drop × 3` | bölüm kapanışı |

Kalabalık yavaş, birim değeri hızlı büyür. Kampanya toplam geliri **≈45.700**.

**Asteroit geliri artık süre bazlı bir kaçak değil.** `Asteroid.SmallResourceAmount`
sabit 5 iken asteroit geliri düşman gelirinin **3 katıydı** ve bölümü uzatarak
sınırsız farm edilebiliyordu. Artık levelin asteroit bütçesinden türer
(≈14 büyük asteroit × 6.25 parça varsayımıyla) ve düşman geliriyle birlikte büyür.
Bu varsayım level süresinin ~3.5 dakika olmasına dayanır — gerçek süre saparsa
asteroit payı da sapar, ölçülecek.

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
8. **Nişan alırken net, diğer zamanlarda kaçamak — ve kaçamak DETERMİNİSTİK.**
   Ateş menzilindeyken gemi burnunu hedefe net çevirir. Diğer tüm anlarda burun
   sabit bir desende salınır (`evasive: true`). Desen rastgele değil:

       sapma = evasionAngle × (sin θ + 0.35 · sin 2.5θ) / 1.35

   Tek sinüs ilk bakışta çözülür; ikinci harmonik deseni okunması zor ama
   **öğrenilebilir** kılar — rastgelelikte olmayan bir ustalaşma alanı açar.
   Harmonik 2.5 = 5/2 olduğu için desenin gerçek periyodu `evasionPeriod`'un
   **iki katıdır** (4π); faz bu yüzden 4π'de sarılır, 2π'de sarmak dalgada
   kırılma yaratır.

   Tek rastgele öğe spawn'daki başlangıç fazıdır — aynı desendeki gemiler senkron
   uçmasın diye. Davranışın kendisi öngörülebilir kalır.
9. **Kaçış radyal değil çapraz, ve imzalı.** Hedeften tam ters yönde kaçmak tahmin
   edilebilir. Kaçış yönü, uzaklaşma vektörünün `escapeAngle` kadar döndürülmüş
   hâlidir — **açı sabittir, taraf her kaçışta değişir** (sağ, sol, sağ...).
   Oyuncu "bu tip önce sağa, sonra sola kırar" kalıbını öğrenebilir. Yalnızca ilk
   taraf spawn'da rastgelelenir.
10. **Kaçamak davranış bölüme göre ölçeklenir.** `ChapterData.enemyEvasionMultiplier`
   hem `evasionAngle` hem `escapeAngle` değerlerini çarpar; `ChapterManager`
   düşmanın runtime kopyasına uygular — `enemyHpMultiplier` ile aynı mekanizma.

**Uçuş karakteri parametreleri** (`EnemyTypeData` içinde, tip başına):

| Tip | agility | grip | evasionAngle | evasionPeriod | escapeAngle | Karakter |
|-----|---------|------|--------------|---------------|-------------|----------|
| Swarm | 1.5 | 0.95 | 18° | 1.4 sn | 40° | Küçük, kıvrak — hızlı titrek imza |
| Shield | 0.85 | 0.85 | 12° | 2.2 sn | 40° | Orta sınıf, dengeli |
| Armored | 0.55 | 0.72 | 6° | 3.4 sn | 30° | Hantal — yayvan ve az salınım |
| Bomber | 1.15 | 0.93 | 15° | 1.7 sn | 40° | Hızlı avcı |
| BombRunner | 0.6 | 0.8 | 0° | — | 0° | Düz hat bomba koşusu — salınım ve kaçış yok |
| Fighter (oyuncu) | 1.4 | 0.94 | 12° | 1.6 sn | 40° | Kıvrak avcı |
| Collector (oyuncu) | 1.6 | 0.97 | 0° | — | — | İş gemisi — düz ve verimli gider |

`evasionAngle` salınımın tepe açısı, `evasionPeriod` tipin **uçuş imzası** — oyuncunun
öğreneceği şey budur; desenin tam tekrarı bunun iki katında olur (Swarm: 2.8 sn).
`escapeAngle` kaçarken radyal yönden sapma. Değerler **tam** seviyedir (bölüm 8+).

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

**Hedefleme — puanlama formülü** (`TurretTargeting`):

    puan = tehdit × aciliyet / (öldürme süresi + mermi uçuş süresi)

- **öldürme süresi** = (bu silahla öldürmek için gereken ham hasar) / (turretin DPS'i).
  Dirençler burada devrededir — raylı top Armored'ı 44 saniyede bitirir, o yüzden ona
  yönelmez; asteroidi ×2 ile yarı sürede bitirir.
- **uçuş süresi** = mesafe / mermi hızı. Uzak hedef hem geç vurulur hem kaçırılır.
- **aciliyet** = ana gemiye yaklaştıkça 1 → 2.5. Kalkana dayanmış bomber acil,
  uzaktaki swarm değil.

"En yakın", "en çok zarar vereceğim" ve "en çabuk öldüreceğim" böylece tek bir orana
iner: *dikkatimin saniyesi başına ne kadar tehdit ortadan kalkar*.

**Kilitlenme:**
- Hedef her karede değil, `ReevaluateInterval` (0.35 sn) aralıklarla değerlendirilir.
- Kilitli hedef geçerli ve **menzildeyken** kilit korunur. Menzil =
  `bulletLifeTime × bulletSpeed` (PD için 5.5 birim).
- Rakip hedef ancak puanı kilitli hedefin **1.6 katı** olursa kiliti kırar.

Ölçülen davranış (raylı top, DPS 6, tam HP yakın Swarm'a kilitli):

| Rakip hedef | Puan | Kiliti kırar mı? |
|---|---|---|
| Kalkana dayanmış Bomber (10 HP) | 9.81 | **kırar** |
| 3 HP kalmış yakın Swarm | 2.23 | **kırar** |
| Başka bir tam HP Swarm | 0.51 | kıramaz |
| Kalkanlı gemi | 0.54 | kıramaz |
| Büyük asteroit | 0.32 | kıramaz |
| Armored (kinetiğe dirençli) | 0.13 | kıramaz |

**Hedef tipleri** `ITurretTarget` arayüzünü uygular: `EnemyBot`, `BossShip`,
`Asteroid`, `Bomb`. Yeni bir hedef tipi eklemek için `TurretController`'a
dokunmak gerekmez — arayüzü uygulamak yeterlidir.

Point Defence yalnızca `IsPointDefencePriority` olan hedefleri alır: bombalar,
yakın saldırgan gemiler (Approach / BombRun / AttackRun) ve küçük asteroit parçaları.

### Açılış Menüsü — Tasarım Kararları

Oyun `StartMenuUI` ile başlar; menü kapanana kadar bölüm sistemi **kurulmaz**,
dolayısıyla arkada düşman spawn olmaz.

| Seçim | Sonuç |
|---|---|
| **BAŞLA** | `ChapterManager` kurulur, normal dalga akışı |
| **SERBEST MOD** | `ChapterManager` kurulmaz, `EnemySpawner.debugFreeSpawn` açılır |
| **ZORLUK** | `DifficultyManager.Current` — Kolay / Normal / Zor |

Zorluk seçimi buraya taşındı; daha önce yalnızca Game Over panelindeydi ve oyuncu
zorluğu ancak öldükten sonra değiştirebiliyordu. Game Over'daki butonlar duruyor.

**Serbest modun kendi zorluk rampası vardır** — baştan her tipi boca etmez.
Geçen süreden bir *seviye* hesaplanır (`levelDuration` = 40 sn) ve dört şey
birlikte artar:

| | Formül | 0:00 | 2:00 | 4:00 | 5:20+ |
|---|---|---|---|---|---|
| Açık tipler | `threatScore ≤ 1 + seviye × 1.5` | Swarm | +Armored, Shield | +Bomber | +BombRunner |
| Spawn aralığı | 4.5 → 1.0 sn (seviye 8'de) | 4.5 sn | 3.2 sn | 1.9 sn | 1.0 sn |
| Aynı anda sahada | `3 + seviye × 1.2` (tavan 14) | 3 | 5 | 9 | 11 → 14 |
| HP / hasar / kaçamak | `1+0.10L` / `1+0.07L` / `L/7` | 1.00 / 1.00 / 0 | 1.30 / 1.21 / 0.43 | 1.60 / 1.42 / 0.86 | 1.80+ / 1.56+ / 1.00 |

Tipler tehdit puanına göre açılır: Armored 1:20, Shield 1:47, Bomber 4:00,
BombRunner 4:53. Tam zorluğa ~5.5 dakikada varılır.

**"Aynı anda sahada" tavanı** rampanın en önemli parçası: oyuncu temizleyemezse
spawn durur, yığılma olmaz.

Çarpanlar sentetik bir `ChapterData` üzerinden uygulanır — serbest mod da
kampanyayla **aynı ölçekleme yolunu** kullanır, ayrı bir formül yoktur.
`EnemySpawner.debugChapter` doldurulursa rampa devre dışı kalır ve o bölümün
sabit zorluğu kullanılır.

Serbest modda küçük bir asteroit alanı da kurulur (3 asteroit) — yoksa hiç
kaynak akmaz ve oyuncu hiçbir şey inşa edemez.

Menü açıkken oyun `SpeedController.Pause()` ile durdurulur — `Time.timeScale`
doğrudan ezilmez, hız sistemiyle çakışmasın diye. `StartMenuUI.IsOpen` açıkken
Tab / upgrade ekranı kilitlidir. Canvas runtime'da kurulur (GameManager deseni),
ayrı sahne gerekmez.

### Spawn Mimarisi — Tasarım Kararları

Sorumluluk ayrımı:

| | Karar | Uygulama |
|---|---|---|
| `ChapterManager` | **NE** spawn edilecek — bütçe, dalga, formasyon, hangi bölüm | — |
| `EnemySpawner` | — | **NASIL** kurulacak: GameObject, HealthBar, bölüm çarpanları |
| `AsteroidSpawner` | — | Alan yoğunluğunu korur |

**Tek inşa yolu.** Oyunda `AddComponent<EnemyBot>()` çağıran tek yer
`EnemySpawner.Spawn()`. Bölüm çarpanları (HP / hasar / kaçamak) orada uygulanır,
dolayısıyla ikinci bir yol sessizce ondan sapamaz. Boss'un drone üretimi de bu
yoldan geçer.

Çağıran bölümü bilmek zorunda değildir: `Spawn(data, pos)` çarpanları
`ChapterManager.CurrentChapter`'dan okur. Açıkça `Spawn(data, pos, chapter)`
denirse o bölüm, `null` denirse çarpansız düşman kurulur.

**Base class yok.** `EnemySpawner` ve `AsteroidSpawner` ortak bir soyutlamadan
türemiyor; paylaştıkları tek şey "sahne kenarından bir şey üretmek" ve o üç satır.
Ortaklık gerçekten belirginleşirse sonra çıkarılır.

**Serbest mod** (`EnemySpawner.debugFreeSpawn`) dalga sistemini beklemeden düşman
akıtır; test içindir, varsayılan kapalıdır ve `ChapterManager` sahnedeyken
otomatik kapatılır. Aynı `Spawn()` metodunu çağırdığı için gerçek oyundan farklı
bir düşman üretmesi mümkün değildir — eskiden ayrı bir kod yolu olduğu için
çarpansız düşman üretiyor ve testi yanıltıyordu.

### Bölüm Yapısı — 100 Level, 10 Bölüm, 10 Boss

**Bölüm = 10 level. Her bölümün 10. leveli boss levelidir.** Tek gerçek sayı
`GameProgress.CurrentLevel`'dır (1–100); bölüm ondan türer.

**Zorluk bölümden değil LEVELDEN gelir** (`LevelCurve`). Bölüm sınırı yalnızca
tema ve yeni bir düşman tipi getirir — zorluk orada sıçramaz, sürekli akar.
Eskiden her bölümde elle yazılmış `enemyHpMultiplier` ve wave dizileri vardı;
10 bölüm için idare edilebilirdi, 100 level için edilemez.

| Formül | Değer | Lv100 |
|---|---|---|
| `HpMultiplier(n)` | `1.0233^(n−1)` | 9.8× |
| `DamageMultiplier(n)` | `1.0141^(n−1)` | 4.0× (eskiden sabit 1.0 idi) |
| `Armor(n)` | `20 × (n/100)^1.6` | 20 |
| `EvasionMultiplier(n)` | `n = 1..25 arası doğrusal` | 1.0 |

**Wave'ler elle yazılmaz.** `ChapterManager` levelin tehdit bütçesini 2–4 dalgaya
böler; geç leveller daha çok dalga görür (tek seferde 40 tehdit puanı boca etmek
yığılma yaratır). Son dalga %25 daha ağırdır — level kendi zirvesiyle bitsin.

**İki özel level tipi:**
- **Bölümün 1. leveli** yalnızca o bölümün yeni tipini getirir. Oyuncu bir tipin
  davranışını kalabalık içinde öğrenemez.
- **Bölümün 10. leveli** boss: önce escort dalgası, sonra boss + refakat.

| Bölüm | Level | Sektör | Yeni tip | Boss | Sınadığı şey |
|---|---|---|---|---|---|
| 1 | 1–10 | İlk Temas | Swarm | Nöbetçi | temel mekanik |
| 2 | 11–20 | Devriye Hattı | Armored | Devriye Lideri | silah tipi seçimi |
| 3 | 21–30 | Kalkan Duvarı | Shield | Kalkan Matriksi | kalkan katmanı |
| 4 | 31–40 | Bomba Yağmuru | Bomber | Bombardıman Platformu | Point Defence |
| 5 | 41–50 | Avcı Sürüsü | Interceptor | Zırhlı Kale | zırh eşiği (zırh 12) |
| 6 | 51–60 | Uzun Menzil | Artillery | Obüs Hattı | menzil |
| 7 | 61–70 | Karartma | Jammer, Phantom | Karıştırıcı | enerji |
| 8 | 71–80 | Onarım Kovanı | Regenerator, Leech | Kovan Anası | DPS eşiği |
| 9 | 81–90 | Bölünen Sürü | Splitter | İkiz Dreadnought (×2) | hedef bölme |
| 10 | 91–100 | Kovan Zihni | Juggernaut | Kovan Zihni | hepsi (zırh 20) |

**Boss'lar formülden türer** (`BossShipData.CreateForChapter`): gövde
`500 × HpMultiplier(n)`, hardpoint sayısı `2 + bölüm/2`, her biri
`120 × HpMultiplier(n)`. Elle yazılan tek şey mekanik ve isimdir.

Eski `CreateCarrierCommand()` duruyor ama artık çağrılmıyor — referans olarak
bırakıldı.

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
| EnemyBot.cs | Data-driven düşman — hareket, ateş, direnç, zırh eşiği, faz/bölünme/onarım aurası |
| Asteroid.cs | Parçalanabilir asteroit — Large→Medium→Small, çarpma hasarı, enkaz bırakır |
| Debris.cs | Enkaz — sürüklenip durur, tipe göre renk, ömür sonunda solup yanıp söner |
| ComponentCatalog.cs | Tüm komponent tanımlarının tek sahibi — ne var, kaça, hangi zincirle |
| BalanceConfig.cs | Gelir ve zırh eğrilerinin tek sahibi (SO; asset yoksa varsayılan) |
| LevelCurve.cs | Düşman ölçeklemesi: HP, hasar, zırh, kaçamak — levelden türer |
| GameProgress.cs | Kampanyadaki yer: 100 level, 10 bölüm, bölüm başına 1 boss |
| SaveSystem.cs | Kampanya kaydı (PlayerPrefs), level sınırlarında yazılır |
| StorageComponent.cs | Depo — kurulu olduğu sürece kaynak tavanını yükseltir |
| ITurretTarget.cs | Turretlerin nişan alabileceği her şeyin ortak arayüzü |
| CombatArea.cs | Dogfight sınırları — savaşçılar ekrandan çıkmasın |
| TurretTargeting.cs | Hedef puanlama formülü + kilit histerezisi |
| ShipMovement.cs | Roket-itkili uçuş modeli — burun itkisi, hıza bağlı dönüş, grip, fren |
| ShipBrain.cs | Taktik AI — Orbit/Strafe/HoverFire pattern'ları, ShipMovement'e komut verir |
| EnemyBullet.cs | Düşman mermisi — hull modu (kalkan üzerinden) veya komponent modu (doğrudan) |
| EnemySpawner.cs | Düşmanın TEK inşa yolu — GameObject, HealthBar, level ölçeklemesi. Serbest test modu içerir |
| AsteroidSpawner.cs | Asteroit alanının yoğunluğunu korur |
| StartMenuUI.cs | Açılış ekranı — kampanya / devam / serbest mod / zorluk / level seçimi |
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
| EnergyBus.cs | Enerji dağıtım sistemi — üretim/tüketim muhasebesi, Jammer kısar (JamFactor) |
| ResourceInventory.cs | Ham madde + kristal envanteri |
| UpgradeUI.cs | Tab ile açılan upgrade ekranı, 4 panel layout |
| SlotVisual.cs | World-space slot göstergesi, tıklama ile UpgradeUI tetiklenir |
| SkinLibrary.cs | TÜM görsel üretiminin tek giriş noktası — skin varsa sprite, yoksa prosedürel dikdörtgen |
| SkinSet.cs | Skin'lerin tek sahibi (SO; Resources/SkinSet.asset). Ana aç/kapa anahtarı burada |
| SkinId.cs | Skin anahtarları. Düşman/boss anahtarları tip adından türer |
| HitboxOverlay.cs | Teşhis aracı — collider sınırlarını sprite üstüne çizer |

---

## Kamera Sistemi
- **Kadraj:** Ana gemi (0, -2)'de sabit. Kamera gemiye göre konumlanır ve gemiyi
  ekranda **soldan %29**, **üstten %52** oranında tutar — yani solda ve HUD şeridinden
  kalan dikey bandın ortasında. İleri (sağ) tarafta böylece daha çok alan kalır.
- Kayma dünya birimiyle değil **ekran oranıyla** verilir (`shipScreenX/Y`); Free Aspect'te
  pencere en-boy oranı ve zoom değişince kadraj bozulmasın diye. Gerekli dünya kayması
  her karede kameranın o anki yarı genişlik/yüksekliğinden türetilir.
- **Temel pozisyon:** Orthographic Size: 5
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

## Skin Sistemi

Tüm görsel üretimi `SkinLibrary` üzerinden geçer. Eskiden 12 dosyaya dağılmış
53 ayrı `MakeTex`/`Sprite.Create` çağrısı vardı; hepsi tek imzada toplandı.

### Aç / kapa

`Assets/Resources/SkinSet.asset` içindeki `enabled` alanı. **Bu bir asset
alanıdır — değiştirmek derleme TETİKLEMEZ**, Play sırasında bile kapatılabilir.
Asset hiç yoksa da sistem kapalı sayılır ve oyun prosedürel dikdörtgenlere döner.

Aç/kapa anahtar bazında da çalışır: SkinSet'te yalnızca `enemy.swarm` doluysa
sadece Swarm gerçek görselle çıkar, gerisi dikdörtgen kalır. Tip tip göç edilebilir.

### Hitbox — sprite'tan TÜRER

Sprite şeklin tek kaynağıdır. Hitbox onun `SkinEntry.hitboxScale` ile
daraltılmış halidir (`SkinLibrary.TryApplyCollider`):

    collider = sprite'ın physics shape'i (veya sınırları) × hitboxScale

Bağımsız değil, **türev**. Neden daraltma var: itki alevi, anten, glow gibi
dekoratif parçalar bounding box'ı şişirir ama vurulabilir olmamalı.

Yön asimetrisi kasıtlıdır:
- **Oyuncunun VURDUĞU** hedeflerde (düşman, boss, hardpoint) hitbox siluete
  yakın olmalı — `hitboxScale` 0.85'in altına inmemeli, yoksa "vurdum ama
  saymadı" hissi oluşur.
- **Oyuncuya GELEN** hedeflerde (PlayerShip, savaşçı) hitbox daha küçük
  olabilir — kıl payı kurtuluş becerikli hissettirir.

Skin yokken `EnemyTypeData.EffectiveHitboxWidth/Height` kutusuna düşülür, yani
mevcut denge sayıları aynen korunur. Skin ile hitbox **birlikte** açılıp kapanır.

### Sanat spec'i

1. Opak gövde kütlesi `bodyWidth × bodyHeight` kutusunun içinde kalacak
2. Burun **sağa (+X)** bakacak. Oyunda `Euler(0,0,facingAngle)` uygulanır ve
   düşmanların varsayılan facing'i 180°'dir — sağa bakan sprite oyuncuya döner
3. Alev / anten / glow kutunun dışına taşabilir, vurulmaz sayılır
4. Siluet dışbükeye yakın olacak. İçbükey siluet (X, halka, çatal kanat) box
   collider'da boşluğu doldurur — poligon moduna geçilmeli
5. Export `bodyWidth × bodyHeight`'ın **4 katı** piksel, import'ta **PPU 400**.
   Dünya boyutu birebir aynı kalır, piksel yoğunluğu 4× olur (zoom ve mobil için)
6. Import'ta **Mesh Type = Full Rect**. Tight, saydam kenarları kırpar ve
   `sprite.bounds` küçülür — hitbox ofset hesabı kayar

### Doluluk: hangi sayıya bakılır

Önceki not "siluet kutuyu %70 doldurmalı" diyordu; **bu kural yanlıştı**.
Sivri burunlu hiçbir gemi bunu tutturamaz — Swarm'ın sınırlayıcı kutu doluluğu
%44 ve bu normaldir. Doğru ölçü, kutunun değil **hitbox'ın içindeki** doluluktur:

> Hitbox dikdörtgeninin içindeki opak piksel oranı **%60'ın altına düşmemeli.**

Altına düşüyorsa hitbox kütlenin olmadığı yeri kaplıyor demektir ve o boşluğa
atılan mermi hiçbir şeye çarpmaz. `SkinEntry.hitboxRect` bu yüzden var: hitbox
sınırlayıcı kutuya değil, siluetin gövdesine oturur. Değerleri üreteç ölçer.

### Üreteç — `Tools/SkinGen/`

Sprite'lar poligon tanımlarından üretilir (Node, dış bağımlılık yok):

```
node Tools/SkinGen/gen.js
```

| Dosya | İçerik |
|---|---|
| `raster.js` | Poligon rasterleştirici — 4×4 alt-örnekleme, `mode: "erase"` ile delik |
| `palette.js` | Gövde/kanat renginden gölge-ışık-lens tonlarını türetir |
| `enemies.js` | 12 düşman tipi |
| `player.js` | Ana gemi gövdesi + namlu |
| `components.js` | Komponent slot ikonları |
| `ships.js` | Swarm + hepsini toplayan liste |
| `gen.js` | PNG'leri yazar, `hitboxRect` ölçer, doluluk basar |
| `install.js` | `.meta` ve `SkinSet.asset`'i yazar |
| `sheet.js` | Tüm gemileri tek kontakt sayfasında dizer |

**Import ayarlarının sahibi `install.js`'tir, Unity değil.** Unity bir PNG'yi
önce görürse kendi varsayılanlarıyla import eder — PPU 100, Sprite Mode
**Multiple**, Mesh Type **Tight**. Üçü de yanlıştır: gövde 4 kat büyük çıkar,
`fileID: 21300000` çözülmeyebilir ve Tight saydam kenarları kırptığı için
`sprite.bounds` küçülüp hitbox ofseti kayar. `install.js` her çalıştırmada
meta'yı yeniden yazar ama **GUID'i korur** — GUID değişseydi SkinSet
referansları kopar ve sprite alanları None'a düşerdi.

Akış: `node Tools/SkinGen/gen.js` (çiz) → `node Tools/SkinGen/install.js`
(meta + SkinSet). İkincisi `hitboxRect`'i yeniden ölçtüğü için siluet
değiştiğinde elle bir şey güncellemek gerekmez.

**Çalıştırınca doğan iki gürültü — commit'e alınmamalı:**
- `gen.js` TÜM PNG'leri yeniden yazar. Pikseller birebir aynıdır; değişen
  yalnızca zlib çıktısıdır (Node sürümü). Dokunulmayan sprite'lar geri alınmalı.
- `install.js`'in meta şablonu Unity 6.3'ün gerisinde: Unity'nin eklediği
  Standalone/Android/WebGL override bloklarını siler ve `ignoreMipmapLimit`
  alanını eski adına (`ignoreMasterTextureLimit`) döndürür. Unity import'ta
  hepsini yeniden yazar, yani zararsız ama her seferinde 22 dosya kirlenir.
  Şablon güncellenene kadar meta'lar `git checkout` ile geri alınıyor.

`Tools/` klasörü `Assets/` dışında olduğu için Unity onu derlemez.

Göz kontrolü (dama zemin + hitbox çerçevesi):

```
node Tools/SkinGen/preview.js Assets/Art/Enemies/Swarm.png 5 out.png --rect=23,8,188,64
```

### Doğrulama

`SkinSet.showHitboxOverlay` açıldığında collider sınırları sprite'ın üstüne
çizilir (yeşil = kutu, sarı = poligon). Örtüşmeyi tablodan hesaplamak yerine
gözle görmek için. Çağrı noktalarına dokunmaz, skin sistemine henüz girmemiş
nesnelerde de çalışır.

### Performans notu

Prosedürel dikdörtgenler artık önbelleğe alınıyor — aynı (boyut, renk, pivot)
tek sprite paylaşıyor. Eskiden her düşman/mermi doğuşunda yeni bir `Texture2D`
ayrılıyordu. Renk animasyonu `SpriteRenderer.color` üzerinden yapıldığı için
paylaşım güvenli.

`PolygonCollider2D`, `BoxCollider2D`'den pahalıdır. Basit siluetlerde
`SkinEntry.colliderMode = Box` tercih edilmeli.

---

## Ana Gemi Siluet ve Slot Düzeni — Tasarım Kararları

Gemi **4.0 × 2.4 birim** (tuval 1600×960, PPU 400). Eskiden 4×1'di.

**Neden büyüdü:** slot ızgarası dünya `y = ±0.8`'deydi ve komponent ikonu
0.35 birim çapında (`ShipComponentBase.k_ringSize`), yani `0.975`'e kadar
uzanıyordu. Gövde `0.5`'te bittiği için komponentler geminin **tamamen dışında,
boşlukta asılı** duruyordu.

**Üst sınır kalkan küresi:** `ShieldEffect.ShieldRadius = 2.5`, gövdenin
yarı-köşegeni `√(2.0² + 1.2²) = 2.33`. Daha büyük bir gemi kalkanın dışına taşar.

**Slotlar gövdeyi takip eder, ızgara değildir.** Izgarayı koruyup gemiyi
büyütmek denendi: slotları kapsamak gövdenin dört köşede de tam yükseklikte
olmasını gerektiriyor, yani siluet zorunlu olarak tuğlaya dönüyor — pruva
kaması, kıç basamağı, yükselen güverte hattı hiçbiri yapılamıyor. Çözüm ters
yönden geldi.

| Yapı | Slotlar | Not |
|---|---|---|
| Kıç makine bloğu (kalın, tam yükseklik) | 0, 3, 7 | 4 itici arkasında; jeneratör ve kalkan burada başlar |
| Sırt kulesi (gövdeden yükselir) | 1, 4 | Slot 1 **ana silah** — namlu kuleden yukarı uzanır |
| Bel gövdesi (dar, iki omuzla bağlanır) | 5, 8 | |
| Karın hangar modülü | 6 | Bindirme ağızları — hangar burada başlar |
| Baş kesimi (köprü + kamalı pruva) | 2, 9 | |

`PlayerShip.slotPositions` ile `Tools/SkinGen/player.js` **birebir eşleşir**;
biri değişirse diğeri de değişmeli. Dönüşüm: `canvas = (800 + 400x, 480 + 400y)`.
`ComponentCatalog.StartingLoadout`'un slot numaraları (0, 3, 6) bilinçli olarak
korundu — makine bloğu ve hangar modülü zaten doğru yerler.

**HealthBar geometrisi gövdeden türer.** Sahnede `barOffsetY = 0.7` yazıyordu
ve bu 4×1'lik eski gövdeye göreydi; yeni gövdede bar hull'un içinde kalırdı.
`PlayerShip.Start` artık sprite bounds'undan hesaplıyor.

**Hitbox bir denge değişikliğidir.** Ölçülen dikdörtgen ~4×2.2 birime çıktı
(eskiden 4×0.63); `hitboxScale = 0.90` ile siluetin bir tık içinde kalıyor.
Görünür gövdesine isabet eden merminin saymaması daha kötü olurdu. Kalkan
açıkken fark yok — `EnemyBullet` önce 2.5 birimlik kalkan küresine çarpıyor,
gövde collider'ına hiç ulaşmıyor. **Kalkan kapalıyken gövdenin kesit alanı ~3×
büyüdü; test edilecek.**

### Yan profil — denenip bırakılanlar

İlk sürüm iki ucu da sivrilen bir mercekti ve üstünde baştan başa geniş bir açık
band vardı; ikisi de bir gövdenin **plan görünümünün** işaretidir ve gemi deniz
gemisi gibi okunuyordu. Yandan bakan bir gemi dikeyde simetrik değildir.

- **Güvertede dikilen radyatör kanatçıkları ve dik sensör direği** → BACA gibi
  okunup tam kaçınılmak istenen deniz gemisi işaretini geri getiriyordu.
- **Tüm gövdeyi kaplayan dikey çerçeve ızgarası + yatay dikiş çizgileri** →
  silueti konteynere/çite çeviriyordu. Pencere sıraları uzunluğu zaten veriyor.
- **Kulenin koyu iç paneli** → yükselen bir yapı değil, gövdede delik gibi
  okunuyordu. İç panel gövde tonunda: açık çerçeve içinde içeri çekilmiş yüzey.
- **Kulenin `p.light` iç paneli** → kule gemideki en parlak şeye dönüp köprüyle
  yarışıyordu. Parlak olan tek yer köprü olmalı.
- **Açık tonlu burun plakası** → gövdenin devamı gibi okunup kama etkisini
  yiyordu. Mahmuz gövdeden **koyu**: dövülmüş zırh plakası.

Pencere ve ışık sıraları slot bantlarının **arasına** konur (tuvalde
`y = 90..250 / 350..550 / 730..900` şeritleri komponent ikonlarıyla dolar);
aksi halde ikonların altında kaybolurlar.

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
- [x] Turret hedefleme — puanlama formülü, kilit histerezisi, menzil kapısı
- [x] Kadraj (oran tabanlı) + savaşçılara dogfight sınırı
- [x] Komponent kataloğu birleştirildi — ComponentCatalog tek sahip, tek kalkan tipi
- [x] Depo komponenti — kaynak tavanı kurulu depolardan türetiliyor
- [x] Onarım birimi yavaşlatıldı — Mk1 8.0 → 2.0
- [x] Deterministik kaçamak manevra — iki harmonikli desen + imza kaçışı
- [x] Spawn mimarisi ayrıştırıldı — tek inşa yolu, AsteroidSpawner, serbest test modu
- [x] Açılış menüsü — kampanya / serbest mod / zorluk seçimi

---

## Sıradaki Adımlar (öncelik sırasıyla)

- [x] Otomatik turretler — Gatling/Plazma/Lazer/Roket/Point Defence, slot pozisyonuna kurulur
- [x] Toplayıcı gemiler + kaynak toplama sistemi — Debris → CollectorShip → ResourceInventory
- [x] Stat upgrade sistemi — komponent başına çoklu stat, `1.5^seviye`, 8 seviye
- [x] Bölüm sistemi — 100 level / 10 bölüm / 10 boss, wave'ler bütçeden üretilir
- [x] Boss taşıyıcı gemi — 10 boss, `BossShipData.CreateForChapter` formülünden türer
- [x] Gelir eğrisi — `BalanceConfig`, asteroit farm kaçağı kapandı
- [x] Zırh eşiği — atış başına hasar ödüllendirilir, turret hedeflemesi zırhı bilir
- [x] Hitbox görselden ayrıldı — skin'ler dengeyi kaydırmayacak
- [x] 8 yeni düşman tipi — Interceptor / Artillery / Jammer / Phantom /
      Regenerator / Leech / Splitter / Juggernaut
- [x] Stat adımı 1.5 → 1.25 — oyuncu üstünlüğü kampanya boyunca düz kaldı
- [x] Enerji bütçesi açıldı — tüketim stat seviyesiyle büyür, jeneratör gerçek bir kapı
- [x] Satış iadesi stat harcamasını kapsıyor
- [x] **Level seçimi** — başlangıç menüsünde, ulaşılmış en yüksek levele kadar
- [x] **Kayıt/yükleme** — SaveSystem, level sınırlarında kaydeder
- [ ] **Denge testleri** — aşağıdaki listeye bak; sayıların hiçbiri oyunda denenmedi
- [ ] Point defence turretleri — küçük/hızlı hedeflere odaklı otomatik turret
- [ ] Mobil UI
- [ ] Ses efektleri
- [x] **Skin altyapısı** — SkinLibrary/SkinSet/SkinId, 53 çağrı tek imzada toplandı,
      hitbox sprite siluetinden türer, tek bool ile aç/kapa
- [~] Gerçek sprite'lar — 13 düşman + ana gemi + namlu + 5 komponent ikonu çizildi.
      Kalan: boss + hardpoint, asteroit, enkaz, mermiler, savaşçı, toplayıcı, turret

### Yeniden ele alınabilecekler

- **Enerji geç oyunda gevşiyor.** Erken ortada net akış +0.3'e kadar iniyor ama
  geç oyunda +38'e çıkıyor; jeneratöre yatırım yapan oyuncu için enerji bir
  kapı olmaktan çıkıyor. Ayar noktası `BalanceConfig.energyGrowth` (1.30).
  Önce oynanıp ölçülecek.
- **Puan havuzu alternatifi.** Denge yeniden ele alınırsa: seviye bir *puan
  havuzu* verir, oyuncu puanı statlar arasında dağıtır. Çarpımsal statlarda
  toplam güç dağılımdan bağımsız olur (`1.3¹⁰ = 1.3⁵ × 1.3⁵`), yani seçenekler
  durur ama tavan sabitlenir. Şu anki ayar yeterliyse gerekmez.

---

## Açık İşler — Kaynak Ekonomisi

Kristal çalışması sırasında çıkan, henüz kapatılmamış maddeler.

**Ertelendi (bilinçli karar):**
- Kristal talebinin arzın altında kalması. İleride eklenecek yetenekler kristal harcayacak;
  denge orada kurulacak, şimdi müdahale edilmeyecek.

**Teknik borç:**
- [ ] **Menüde bölüm seçimi yok.** Test için "şu bölümden başla" faydalı olur;
  `StartMenuUI`'ya eklenecek doğal yer hazır.

---

## Açık İşler — Sıradaki Oturum

Sıra kararlaştırıldı: **hitbox ayrımı → denge testleri → skin'ler.**

- [x] **Hitbox'ları görselden ayır.** `EnemyTypeData.hitboxWidth/hitboxHeight`
  eklendi (0 = gövde boyutu kullanılır). `EnemyBot.ApplyStats` artık collider'ı
  buradan türetiyor. Skin'ler gelip `bodyWidth/bodyHeight` değiştiğinde vurma
  zorluğu kaymayacak. Avcı, Obüs, Onarıcı, Sülük ve Kaleci görselden küçük
  hitbox taşıyor.

  **Kalan:** `Asteroid`'de aynı bağ hâlâ var (`RadiusFor` → `PxFor`).

- [ ] **Denge testleri.** Ölçülecekler:

  - **Level süresi.** Asteroit geliri ~3.5 dakikalık level varsayımına dayanır;
    gerçek süre saparsa asteroit payı da sapar.
  - **İsabet oranı.** Düşman HP eğrisi %100 isabet varsayımıyla kalibre edildi.
    Gerçek oran %60 ise tüm TTK'lar 1.7× uzar.
  - **Zırh eşiğinin hissi.** Düşük seviye silahla zırhlı düşman "zor" mu
    hissettiriyor yoksa "silahım hiç işlemiyor" mu? `armorMinDamageRatio` (0.10)
    ve `armorExponent` (1.6) ayar noktaları.
  - **Yükseltme dengesi.** `statStep = 1.25` ile üstünlük düz kalıyor (4.5 → 4.3)
    ama bu simülasyon; gerçekte oyuncu daha odaklı oynar ve üstünlük artar.
    Hangi bölümde oyun kolaylaşıyor?
  - **Enerji baskısı.** Erken ortada jeneratöre yatırım zorunluluğu doğru mu
    hissettiriyor, yoksa "hiçbir şey yükseltemiyorum" duvarı mı?
  - **Kayıt akışı.** Ölünce son tamamlanan levele dönmek doğru ceza mı?
  - Kristal arz/talep, bölüm temposu, serbest mod rampası, uçuş hissi,
    onarım hızları, enkaz ömrü.

- [x] **Skin altyapısı kuruldu.** `SkinLibrary` tek giriş noktası; skin yoksa
  bugünkü prosedürel dikdörtgenlere düşer, görüntü birebir korunur. Hitbox artık
  sprite siluetinden türer (`hitboxScale`), skin ile birlikte açılıp kapanır.
  Ayrıntı: "Skin Sistemi" bölümü.

  `Assets/Resources/SkinSet.asset` oluşturuldu ve `enemy.swarm` girdisi işlendi.

- [~] **Skin'ler — çizim.** 20 sprite çizildi ve SkinSet'e işlendi: 13 düşman tipi,
  ana gemi gövdesi, ana silah namlusu, 5 komponent ikonu (jeneratör, kalkan,
  onarım, depo, kapasitör).

  Swarm oyunda doğrulandı. **Kalan 18'i oyunda görülmedi** — bakılacaklar:
  boyut (PPU 400/128 tuttu mu), namlu pivotu (0.5, 0 — namlu mount noktasından
  yukarı uzanmalı), komponent ikonlarının slot ızgarasında okunurluğu.

  **Ana gemi yeniden çizildi** — ayrıntı: "Ana Gemi Siluet ve Slot Düzeni".

  **Bilerek skin verilmeyenler:** Hangar ve Turret halkaları. İkisinin de kendi
  gövde görseli var; halkaya da ikon konsaydı üst üste binerdi.

  **Hâlâ prosedürel dikdörtgen:** boss + hardpoint, asteroit, enkaz, tüm
  mermiler, savaşçı, toplayıcı, hangar gövdesi, turret taban/namlu, kalkan kabuğu.

---

## Açık İşler — Hareket & Zorluk

- [ ] **Deterministik kaçış — testte doğrulanacak.** Desen ve imza kaçışı yazıldı;
  oyuncunun kalıbı gerçekten öğrenip öğrenemediği ancak oynayarak anlaşılır.
  Ayar noktaları: `EnemyTypeData.evasionPeriod` (imza), `WanderHarmonic` /
  `WanderHarmonicGain` (desenin karmaşıklığı).
- [ ] **Kamera dikey kaydırma yok.** `CameraController` yalnızca yatay kayıyor
  (`direction.x`), dikey kayma hiç uygulanmıyor. Yukarı/aşağı bakabilmek için eklenmeli.
- [ ] **Otomatik zoom rahatsız edici.** Mouse ekranın %90'ından sonra size 5→7.
  Alternatif: zoom miktarını azaltıp yatay kaydırmayı artırmak. Test edilecek.
- [ ] **Yıldız alanı kadrajı karşılamıyor.** `StarField` 36×14 birim (-18..18, -7..7).
  Yeni kadrajda kamera sağa kaydığı için görünür alan x ekseninde +24'e, y ekseninde
  -8.7'ye kadar gidiyor — kenarlarda yıldızsız boşluk kalır. Alan büyütülmeli
  (ve yoğunluk korunacaksa `starCount` da).
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
- [ ] Bölüm 9 İkiz Dreadnought: iki boss aynı anda spawn oluyor ama ikisi de aynı
      `preferredX`'i hedefliyor — üst üste binebilirler, konumlandırma test edilmeli
- [ ] Yörünge üssü — LEO'da modüler bir üs (docking, kontrol, yaşam birimi, yaşam
      desteği, depolar, iticiler...). Hikâyeyle çelişiyor (gemi Oort bulutunda,
      Dünya'ya 1 ışık yılı) — prolog olarak kurgulanabilir. Komponent listesi ve
      inşa şekli (ızgara/bitişiklik vs sabit slot) henüz kararlaştırılmadı.
