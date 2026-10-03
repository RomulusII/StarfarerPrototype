using UnityEngine;

/// <summary>
/// Gelir ve zırh eğrilerinin TEK sahibi.
///
/// Kapsam notu: yükseltme SİSTEMİ buraya ait değildir — hangi komponentin hangi
/// statı var sorusu <see cref="ComponentCatalog"/> ve <see cref="UpgradeUI"/>
/// içinde yaşar. Burada yalnızca EĞRİLER durur: oyuncunun ne kadar kaynak
/// kazandığı, bir stat seviyesinin ne kadar güç ve ne kadar para ettiği, ve
/// düşmanların ne kadar sert olduğu.
///
/// Neden ayrı bir dosya: eskiden gelir tek bir sabitti — düşman ölünce
/// <c>threatScore × 4</c>. Gelir yalnızca wave bütçesiyle büyüdüğü için 100.
/// levelde gereken kaynağı üretmek 125× düşman spawn etmeyi gerektirirdi.
/// İki ekseni ayırmak bunu çözer.
///
/// Değerler burada C# varsayılanı olarak durur; Resources/BalanceConfig.asset
/// oluşturulursa o ezer. Asset olmadan da oyun çalışır (EnemyTypeData factory
/// metodlarıyla aynı desen).
/// </summary>
[CreateAssetMenu(fileName = "BalanceConfig", menuName = "Starfarer/Balance Config")]
public class BalanceConfig : ScriptableObject
{
    // ── Gelir eğrisi ──────────────────────────────────────────────────────────

    [Header("Wave Bütçesi")]
    [Tooltip("Level 1'in tehdit puanı bütçesi (formülün tabanı; level 1'in " +
             "kendisi elle yazılır — ChapterManager.OpeningWaveBudgets).\n\n" +
             "7 -> 9 (denge r8): oyuncu kampanyayı 'çok kolay' buldu. Taban " +
             "bütün levelleri AYNI oranda (+%29) kaydırır; geç levellerin ayrıca " +
             "sertleşmesi budgetGrowth'tan gelir.")]
    public float baseThreatBudget = 9f;

    [Tooltip("Level başına bütçe büyümesi.\n\n" +
             "İKİ EĞRİNİN ÇARPIMIDIR: 1.027 × 1.0233 = 1.05093.\n\n" +
             "  1.027  — oyuncunun güç eğrisi. Oyuncu kampanya boyunca ~13.8 kat " +
             "güçleniyor; bütçe aynı oranda büyürse level SÜRESİ sabit kalır ve " +
             "büyümenin tamamı dalga BOYUTUNA gider. 13.8^(1/99) = 1.0267.\n\n" +
             "  1.0233 — eskiden düşmanın HP eğrisiydi (LevelCurve.hpGrowth). " +
             "Çöp gemiler artık levelle büyümediği için o büyüme BURAYA devredildi. " +
             "Yer değiştirme TAMDIR, yaklaşık değil: bir levele gelen toplam " +
             "efektif HP birebir korunur — Lv100'de 959 -> 955. Değişen tek şey, " +
             "aynı işin uzun HP barları yerine daha çok gemi olarak gelmesi.\n\n" +
             "DİKKAT: bu sayı bir ALT SINIRDIR. Zırhın da düzleşmesi (levelden " +
             "gelen +20 zırhın kalkması) ayrıca bir kolaylaştırmadır ve etkisi " +
             "silaha göre ×0.78 ile ×0.10 arasında değişir, yani oyuncunun " +
             "build'ine bağlıdır ve kağıtta fiyatlanamaz. Gerçek telafi " +
             "katsayısını simülasyon söyleyecek.\n\n" +
             "Neden %10-15 değil: 100 level bileşik faizdir. %10 ile Lv100 bütçesi " +
             "87.700 tehdit puanı eder, yani tek levelde 87.700 Swarm. " +
             "Hissedilen birim BÖLÜMDÜR ve orada artış ×1.64 olur.\n\n" +
             "1.05093 -> 1.053 (denge r8): kampanya kolay bulundu. Tabanla " +
             "birlikte eski eğriye göre Lv10 ×1.33, Lv50 ×1.42, Lv100 ×1.56. " +
             "Drop tehdit başına sabit kaldığı için gelir de aynı oranda artar — " +
             "daha çok iş, daha çok kaynak; zorluğun bir kısmını oyuncu geri alır.")]
    public float budgetGrowth = 1.053f;

    [Header("Düşman Değeri")]
    [Tooltip("Tehdit puanı başına düşen kaynak. dropGrowth 1.0 olduğu için bu " +
             "artık bir LEVEL 1 DEĞERİ değil, kampanyanın tamamında geçerli tek " +
             "sayıdır: bir Swarm her levelde bunu düşürür.\n\n" +
             "2.1 -> 1.8972: bütçe eğrisi dikleşince (1.027 -> 1.05093) toplam " +
             "gelir %11.3 şişiyordu. 'budgetGrowth × dropGrowth sabit kalsın' " +
             "kuralı BÜYÜME katsayılarıyla tam tutturulamıyor — 1.027×1.022 = " +
             "1.04959 ile 1.05093×1.0 arasındaki binde 1.3'lük fark 99 levelde " +
             "bileşik olarak %14'e çıkıyor. Düzeltme büyümeye değil TABANA " +
             "yazıldı: dropGrowth'u 0.9989 gibi bir sayıya çekmek geliri " +
             "düzeltirdi ama 'tehdit başına drop bir sabittir' ifadesini de " +
             "bozardı. Sabit kaldı, değeri değişti.\n\n" +
             "Sonuç: kampanya geliri değişmiyor (ölçülen fark %0.0).\n\n" +
             "Eskiden 4'tü ve hiç ölçeklenmiyordu.")]
    public float baseDropPerThreat = 1.8972f;

    [Tooltip("Level başına drop büyümesi. ARTIK 1.0 — yani tehdit puanı başına " +
             "düşen kaynak bir SABİTTİR. Bir Swarm 1. levelde ne düşürüyorsa " +
             "100. levelde de onu düşürür.\n\n" +
             "İki gerekçe aynı yere çıkıyor:\n\n" +
             "  1. Muhasebe: kampanya geliri = Σ(bütçe × drop) ve bu toplam " +
             "korunmalıydı, yoksa yükseltme fiyatlarının tamamı geçersizleşirdi. " +
             "Büyümeyi 1.0'a sabitlemek tek başına geliri %11.3 şişiriyor; " +
             "telafi baseDropPerThreat'e yazıldı (2.1 -> 1.8972), çünkü " +
             "düzeltmeyi büyümeye yazmak sabitliği bozardı.\n\n" +
             "  2. Anlam: düşman statı düzleştiği için tehdit puanı artık DÜRÜST " +
             "bir sabit. Sabit bir şeyin birim fiyatının levelle büyümesi için " +
             "hiçbir sebep yok. Gelirin tamamı artık tek bir şeyden gelir: kaç " +
             "gemi öldürdüğünden.\n\n" +
             "1.0 olduğu için matematiksel olarak gereksiz bir alan; ayarlanabilir " +
             "kalması bilinçli — ölçüm gelirin eğilmesi gerektiğini söylerse " +
             "dokunulacak yer burasıdır.")]
    public float dropGrowth = 1.0f;

    [Header("Asteroit")]
    [Tooltip("Level başına asteroit kaynak bütçesi. Asteroit geliri eskiden süre " +
             "bazlıydı ve düşman gelirinin 3 katına çıkıyordu — bölümü uzatarak " +
             "sınırsız farm edilebiliyordu. Artık levelle birlikte büyür.")]
    public float asteroidBase   = 10f;
    public float asteroidGrowth = 1.035f;

    [Tooltip("Bir level içinde her dalganın bir öncekine göre büyüme oranı. " +
             "Level kendi zirvesiyle bitsin diye: ilk dalga ısınma, son dalga " +
             "levelin en ağır anı. " +
             "BİR ARA 1.6 YAPILIP GERİ ALINDI. Amaç oyunun ilk dalgasını tek " +
             "Swarm'a indirmekti (7 bütçe / 1.25 büyüme bölüşümü 2/2/3 veriyor). " +
             "İşe yarıyordu ama bedeli yüz levelin tamamında daha sivri son " +
             "dalgalardı — level 100'ün son dalgası 33'ten 43'e çıkıyordu. Açılış " +
             "artık ChapterManager.OpeningWaveBudgets ile ELLE yazıldığı için bu " +
             "katsayının o gerekçesi kalmadı; ölçülmemiş bir kaydırmayı taşımak " +
             "için sebep yok.")]
    public float waveBudgetGrowth = 1.25f;

    [Tooltip("Bir dalganın tehdidinin bu oranı yok edilince sonraki dalga gelir. " +
             "Geride kalanlar KAÇMAZ, savaşmaya devam eder — yeni dalga onların " +
             "üstüne biner.\n\n" +
             "Neden: dalgalar düz statla onlarca gemiye çıktı; son üç gemiyi " +
             "kovalamak leveli dakikalarca, hiçbir şeyin olmadığı bir bekleyişte " +
             "uzatıyordu. Küçük dalgalarda kural kendiliğinden devre dışı: 3 " +
             "Swarm'lık bir dalgada %10 = 0.3 tehdit, yani hepsi ölmeli.\n\n" +
             "LEVELİN SON DALGASINA UYGULANMAZ — orası tam temizlenme bekler. " +
             "Level sınırında kayıt alınıyor, bant çıkıyor ve bölüm sonunda " +
             "diyalog ekranı açılıyor; arkada ateş eden gemiler kalmamalı. " +
             "Üstelik level başı kaydı gemileri tutmuyor: sınırda sağ kalan bir " +
             "gemi, kapatıp açarak silinebilen bir kaçış yolu olurdu.")]
    [Range(0.5f, 1f)] public float waveClearRatio = 0.9f;

    [Header("Dalga Kompozisyonu")]
    [Tooltip("Dalga kadrosu kurulurken bir tipin seçilme ağırlığı: " +
             "tehdit^(−alfa).\n\n" +
             "Eskiden bütçeye SIĞAN tipler arasından düzgün rastgele seçiliyordu, " +
             "yani Swarm ile Kaleci eşit sıklıkta geliyordu. Düşman statı " +
             "düzleşince sayı tek para birimi hâline geldi ve bu seçim artık " +
             "levelin ŞEKLİNİ belirliyor — tek tip dalga yerine dokusu olan " +
             "dalgalar isteniyor.\n\n" +
             "Kuvvet yasasının iki sonucu var ve ikisi birbirinden bağımsız:\n" +
             "    adet payı  ∝ tehdit^(−alfa)\n" +
             "    bütçe payı ∝ tehdit^(1−alfa)\n\n" +
             "'Ucuzlar kalabalık olsun ama bütçenin azını yesin' ancak " +
             "0 < alfa < 1 aralığında sağlanır: alfa >= 1 bütçeyi de ucuzlara " +
             "kaydırır, alfa <= 0 ağırları hem pahalı hem kalabalık yapar.\n\n" +
             "0.5 özel bir nokta — adet oranı ile bütçe oranı TAM AYNA olur " +
             "(c^-0.5 ile c^+0.5 birbirinin tersi). Swarm, Kaleci'den 5.2 kat " +
             "kalabalık gelir; Kaleci bütçenin 5.2 katını yer. Ortalama gemi " +
             "maliyeti 9.14 -> 7.38 düşer, yani aynı bütçe ~%24 daha çok gemi.\n\n" +
             "İLERİDE: bu alan dalga başına ezilebilir hâle gelince levelin " +
             "karakteri olur — yüksek alfa (~2) aynı bütçeyi ~450 gemiye " +
             "çevirir (cümbüş), negatif alfa az ve ağır bir duvar kurar. " +
             "Negatif ucun şu anda karşılığı yok: havuzun en pahalısı Kaleci " +
             "(27) olduğu için 'seyrek ve ağır' bir dalga kurulamıyor.")]
    public float compositionAlpha = 0.5f;

    /// <summary>
    /// Bir tipin dalga kadrosunda seçilme ağırlığı — <see cref="compositionAlpha"/>
    /// ile. Tehdit puanı en az 1 sayılır; 0 veya negatif bir puan sonsuz ağırlık
    /// üretir ve dalga tek tipten oluşurdu.
    /// </summary>
    public float CompositionWeight(int threatScore)
        => CompositionWeight(threatScore, compositionAlpha);

    /// <summary>Alfa'yı açıkça vererek — cümbüş dalgası kendi alfasını taşır.</summary>
    public float CompositionWeight(int threatScore, float alpha)
        => Mathf.Pow(Mathf.Max(1, threatScore), -alpha);

    [Header("Cümbüş")]
    [Tooltip("Her bölümün kaçıncı levelinde cümbüş dalgası gelir. DETERMİNİSTİK: " +
             "oyuncu kalıbı öğrenip ona göre hazırlanabilmeli (kaçamak manevra " +
             "desenleriyle aynı gerekçe). 5 — bölümün ortası; 1. level tanıtım, " +
             "10. level boss.")]
    public int surgeLevelInChapter = 5;

    [Tooltip("Cümbüş dalgasının kompozisyon alfası. 2'de ağırlık tehdit^-2: " +
             "Swarm, Kaleci'den 729 kat sık seçilir ve ortalama gemi maliyeti " +
             "~7.2'den ~2.1'e düşer — aynı bütçe ~3.4 kat gemi.")]
    public float surgeAlpha = 2f;

    [Tooltip("Cümbüş levelinde orta dalganın levelin bütçesinden aldığı pay. " +
             "Diğer dalgalar kalanı eski oranlarıyla paylaşır.\n\n" +
             "CÜMBÜŞ BÜTÇE EKLEMEZ, YENİDEN DAĞITIR. Levelin toplamı değişmediği " +
             "için gelir eğrisi ve kampanya ekonomisi etkilenmez; değişen yalnızca " +
             "levelin ŞEKLİ. Eklenen bütçe hem zorluğu hem geliri sessizce " +
             "kaydırırdı.")]
    [Range(0.3f, 0.8f)] public float surgeBudgetShare = 0.55f;

    [Header("Boss")]
    [Tooltip("Bölümü kapatan boss'un tehdit değeri ve kapanış primi çarpanı.")]
    public float bossThreatValue      = 25f;
    public float bossRewardMultiplier = 3f;

    // ── Yükseltme eğrisi ──────────────────────────────────────────────────────

    [Header("Stat Upgrade")]
    [Tooltip("Stat seviyesi başına güç çarpanı. 1.5 iken oyuncu üstünlüğü kampanya " +
             "boyunca 4.5× → 26.5×'e kayıyordu: turret ve silahta hasar VE ateş " +
             "hızı ikisi de DPS'e çarpımsal giriyor, Lv5/Lv6 demek 1.5^11 = 86× " +
             "demekti. 1.25'te üstünlük 4.5 → 4.3 arasında düz kalıyor.")]
    public float statStep = 1.25f;

    [Tooltip("Stat seviyesi başına maliyet çarpanı. Tier'lar kaldırılıp tavan 8'den " +
             "10'a çıkınca 2.5 tutulamazdı: taban 60 ile 10. seviye tek başına " +
             "230.000 kaynak eder, kampanyanın TOPLAM geliri ise ~45.700. Yani son " +
             "seviyeler var ama alınamaz olurdu. 1.65'te bir izi sonuna kadar " +
             "yükseltmek ~9.100 tutuyor (kampanya gelirinin ~%20'si) ve son " +
             "seviye ~3.600, yani geç bir levelin iki katı gelir. Fayda seviye " +
             "başına sabit ×1.25 olduğu için maliyet faydadan hâlâ çok daha " +
             "hızlı büyür — istenen buydu.")]
    public float statCostGrowth = 1.65f;

    [Tooltip("Kapasitör izinin seviye başına büyümesi. Diğer statlar statStep " +
             "(1.25) kullanır; kapasitör bilerek AYRIKTIR.\n\n" +
             "Sebep: kapasitör bir AKIŞ değil STOK. Üretim, tüketimle yarışır " +
             "(statStep 1.25'e karşı energyGrowth 1.30) ve o yarış dengelidir. " +
             "Tampon o yarışa hiç girmez — yalnızca ne kadar süre burst " +
             "yapabildiğini belirler. 1.25 ile ilk seviye 98 metala +12 enerji " +
             "veriyordu, yani oyundaki en zayıf yükseltme oluyordu. 1.5 de az " +
             "bulundu; 2.0'da her seviye tamponu İKİYE katlar (denge r15).")]
    public float capacitorStatStep = 2f;

    [Tooltip("Zırh (gövde HP) statının maliyet çarpanı. Onarım biriminin diğer " +
             "izleriyle aynı tabandan başlasaydı, doğrudan hayatta kalma satın " +
             "alan bir iz olarak açık ara en verimli yükseltme olurdu.")]
    public float armorStatCostFactor = 3f;

    [Tooltip("Zırh (isabet başına hasar düşümü, onarım biriminin 'plating' izi) " +
             "maliyet çarpanı. Gövde izinden ucuz: kalabalığa karşı güçlü ama ağır " +
             "toplara (15–30 hasar) neredeyse dokunmuyor, yani gövde HP'si kadar " +
             "genel bir hayatta kalma satmıyor.")]
    public float platingStatCostFactor = 2f;

    [Tooltip("Kapasitör (enerji tamponu) statının maliyet çarpanı. Jeneratörün " +
             "üretim iziyle aynı tabanı paylaşıyordu, oysa ikisi aynı şeyi " +
             "satmıyor: üretim her saniyeye, tampon yalnızca BURST anlarına " +
             "dokunur. Aynı fiyata üretim almak neredeyse her zaman daha " +
             "doğruydu — yani tampon bir seçenek değil, tuzaktı.")]
    public float capacitorStatCostFactor = 0.5f;

    [Tooltip("Satışta iade oranı — kurulum + stat harcamalarının toplamına uygulanır.")]
    public float sellRefundRatio = 0.40f;

    [Header("Enerji Bütçesi")]
    [Tooltip("Komponent enerji tüketiminin stat seviyesi başına büyümesi. " +
             "Jeneratörün üretim adımından (statStep) YÜKSEK tutulur — böylece " +
             "jeneratör hep geriden gelir ve kaç komponenti besleyebileceğin " +
             "ona ne kadar yatırdığına bağlı olur.")]
    public float energyGrowth = 1.30f;

    public float StatMultiplier(int level) => Mathf.Pow(statStep, Mathf.Max(0, level));

    /// <summary>
    /// Statın kendi maliyet çarpanı. Çoğu iz 1.0'dır; komponentin tabanını
    /// paylaşmanın yanlış olduğu izler burada ayrışır — zırh pahalılaşır
    /// (doğrudan hayatta kalma satıyor), kapasitör ucuzlar (yalnızca burst).
    /// </summary>
    public float StatCostFactor(string key) => key switch
    {
        "armor"     => armorStatCostFactor,
        "plating"   => platingStatCostFactor,
        "capacitor" => capacitorStatCostFactor,
        _           => 1f,
    };

    public int StatUpgradeCost(int baseCost, int currentLevel, string key = null)
        => Mathf.RoundToInt(Mathf.Max(5, baseCost) * StatCostFactor(key)
                            * Mathf.Pow(statCostGrowth, Mathf.Max(0, currentLevel)));

    /// <summary>Bir stat izine o seviyeye kadar harcanan toplam.</summary>
    public int StatTotalSpent(int baseCost, int level, string key = null)
    {
        int total = 0;
        for (int L = 0; L < level; L++) total += StatUpgradeCost(baseCost, L, key);
        return total;
    }

    public float EnergyMultiplier(int level) => Mathf.Pow(energyGrowth, Mathf.Max(0, level));

    // ── Zırh ──────────────────────────────────────────────────────────────────

    [Header("Zırh Eşiği")]
    [Tooltip("Zırh hasarı tamamen emse bile geçen minimum oran. Eşik atış BAŞINA " +
             "hasarı ödüllendirir: aynı zırh, güçlü tek atışı biraz, zayıf çok " +
             "atışı tamamen yer.")]
    public float armorMinDamageRatio = 0.10f;

    [Tooltip("Zırhın SÜREKLİ kaynakları (ışınlar) saniyede kaç kez ısırdığı.\n\n" +
             "Işının atışı yoktur; zırh eşiğinin ona uygulanabilmesi için bir " +
             "referans sıklık gerekir. Bu sayı tamamen bir DENGE kolu, fiziksel " +
             "bir gerçek değil: yüksek değer ışını zırha karşı zayıflatır.\n\n" +
             "2 seçildi çünkü lazer turretinin 0.5 sn'lik yanmasını tam bir " +
             "'atış' sayar. O ayarla turret kinetik turretle aynı ligde kalıyor " +
             "(Lv50 zırhında 2.13'e karşı 2.70 DPS).")]
    public float beamArmorBitesPerSecond = 2f;

    // ── Nişan (Bilgisayar) ────────────────────────────────────────────────────

    [Header("Nişan — Bilgisayar")]
    [Tooltip("Bilgisayar YOKKEN öngörü: 0 = turret hedefin şu anki konumuna " +
             "ateş eder. Yana uçan hedefi pek vuramaz, kendisine doğru geleni vurur.")]
    [Range(0f, 1f)] public float aimLeadNoComputer = 0f;

    [Tooltip("Bilgisayar kurulu, Öngörü Sv0.")]
    [Range(0f, 1f)] public float aimLeadBase = 0.3f;

    [Tooltip("Öngörü izinin seviye başına katkısı. 0.3 + 10 × 0.07 = 1.0: " +
             "Sv10'da nişan tam buluşma noktasındadır (eski davranış).")]
    public float aimLeadPerLevel = 0.07f;

    [Tooltip("Bilgisayar YOKKEN atış başına sapma tavanı (derece).")]
    public float aimSpreadNoComputer = 4f;

    [Tooltip("Bilgisayar kurulu, Hassasiyet Sv0 — sapma tavanı (derece).")]
    public float aimSpreadBase = 3f;

    [Tooltip("Hassasiyet izinin seviye başına sapma çarpanı. 3 × 0.8^10 = 0.32°.")]
    public float aimSpreadDecay = 0.8f;

    [Tooltip("Sapmanın inebileceği en düşük değer (derece).")]
    public float aimSpreadMin = 0.4f;

    [Tooltip("Bilgisayar YOKKEN füzelerin dönüş hızı çarpanı.")]
    public float guidanceNoComputer = 0.6f;

    [Tooltip("Bilgisayar kurulu, Güdüm Sv0.")]
    public float guidanceBase = 0.8f;

    [Tooltip("Güdüm izinin seviye başına çarpanı. 0.8 × 1.07^10 = 1.57. " +
             "statStep (1.25) kullanılmaz: dönüş hızı 7 katına çıkınca füze " +
             "hiçbir hedefi kaçırmaz, güdüm bir karar olmaktan çıkar.")]
    public float guidanceStep = 1.07f;

    // ── Zorluk ────────────────────────────────────────────────────────────────

    [Header("Zorluk")]
    [Tooltip("Kolay modda düşman HP'si (kalkan ve şarjı dahil) ve hasarı bu " +
             "oranla çarpılır.\n\n" +
             "Gelir DEĞİŞMEZ: drop tehdit puanından gelir, HP'den değil. Kolay " +
             "mod aynı kaynağı daha az emekle verir — bilinçli, Kolay'ın vaadi " +
             "budur.")]
    public float easyEnemyMultiplier = 0.8f;

    [Tooltip("Zor modda düşman HP'si (kalkan ve şarjı dahil) ve hasarı bu " +
             "oranla çarpılır. Normal ×1 — dengenin kalibre edildiği mod.")]
    public float hardEnemyMultiplier = 1.2f;

    public float EnemyDifficultyMultiplier(Difficulty d) => d switch
    {
        Difficulty.Easy => easyEnemyMultiplier,
        Difficulty.Hard => hardEnemyMultiplier,
        _               => 1f,
    };

    // ── Singleton ─────────────────────────────────────────────────────────────

    static BalanceConfig _instance;

    public static BalanceConfig Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<BalanceConfig>("BalanceConfig");
            if (_instance == null) _instance = CreateInstance<BalanceConfig>();
            return _instance;
        }
    }

    /// <summary>
    /// Singleton'ı koşuya özgü bir KOPYAYA çevirir.
    ///
    /// Simülasyon parametre ezmesi (--set) uygularken asıl asset'e yazamaz:
    /// editörde koşulduğunda Resources/BalanceConfig.asset diske kirlenir ve bir
    /// duyarlılık koşusunun ±%20'si projenin kalıcı dengesi hâline gelirdi.
    /// Kopya yalnızca bellekte yaşar.
    /// </summary>
    public static void UseRuntimeCopy()
    {
        _instance = Instantiate(Instance);
    }

    // ── Hesaplar ──────────────────────────────────────────────────────────────

    public float ThreatBudget(int gameLevel)
        => baseThreatBudget * Mathf.Pow(budgetGrowth, Mathf.Max(0, gameLevel - 1));

    public float DropPerThreat(int gameLevel)
        => baseDropPerThreat * Mathf.Pow(dropGrowth, Mathf.Max(0, gameLevel - 1));

    public float AsteroidYieldPerLevel(int gameLevel)
        => asteroidBase * Mathf.Pow(asteroidGrowth, Mathf.Max(0, gameLevel - 1));

    /// <summary>
    /// Levelin bütçesini dalgalara böler. Her dalga bir öncekinden
    /// <see cref="waveBudgetGrowth"/> kadar büyüktür; toplam levelin bütçesine
    /// eşittir. Eşit bölüşüm + son dalgaya sabit bir zam yerine geometrik
    /// bölüşüm: level baştan sona TIRMANIR, sonunda tek bir sıçrama yapmaz.
    /// </summary>
    public int[] SplitWaveBudget(float levelBudget, int waveCount)
    {
        waveCount = Mathf.Max(1, waveCount);

        var weights = new float[waveCount];
        float sum = 0f;
        for (int i = 0; i < waveCount; i++)
        {
            weights[i] = Mathf.Pow(waveBudgetGrowth, i);
            sum += weights[i];
        }

        var result = new int[waveCount];
        for (int i = 0; i < waveCount; i++)
            result[i] = Mathf.Max(1, Mathf.RoundToInt(levelBudget * weights[i] / sum));
        return result;
    }

    /// <summary>Zırh eşiği — atış başına hasarı ödüllendiren tek formül.</summary>
    public float ApplyArmor(float damage, float armor)
    {
        if (armor <= 0f) return damage;
        return Mathf.Max(damage - armor, damage * armorMinDamageRatio);
    }

    /// <summary>
    /// SÜREKLİ kaynaklar (ışınlar) için zırhın etkisi — 0..1 arası bir ORAN.
    ///
    /// Zırh eşiği atış BAŞINA sabit bir miktar düşürür; ışının atışı yoktur.
    /// Işını "saniyede bir atış yapan silah" saymak tek tutarlı çözüm:
    ///
    ///     efektif_dps = max(dps − zırh, dps × armorMinDamageRatio)
    ///
    /// Kritik nokta: sonuç bir ORANDIR ve hasarın hangi SIKLIKTA uygulandığından
    /// bağımsızdır. Böylece ışın her karede minik hasar verebilir — oyuncu hedefin
    /// barının akıcı düştüğünü görür — ama zırh yine de saniyede bir kez ısırır.
    ///
    /// Bunun olmadığı hâlde iki kötü seçenekten birini seçmek zorundaydık:
    /// ya hasarı sık uygula (zırh 60 kez ısırır, ışın gücünün %90'ını kaybeder,
    /// üstelik sonuç kare hızına bağlanır) ya da seyrek uygula (zırh doğru
    /// ısırır ama hedef 0.5 saniye hiç hasar almamış gibi durur).
    /// </summary>
    public float BeamArmorEfficiency(float dps, float armor)
    {
        if (armor <= 0f || dps <= 0.001f) return 1f;

        // Işını "saniyede N atış yapan silah" say: her atış dps/N taşır, zırh
        // her birinden armor kadar keser. Saniyeye indirgenince N × armor olur.
        float bite = armor * Mathf.Max(0.1f, beamArmorBitesPerSecond);
        return Mathf.Max(dps - bite, dps * armorMinDamageRatio) / dps;
    }
}
