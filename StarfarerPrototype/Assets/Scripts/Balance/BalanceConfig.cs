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
    [Tooltip("Level 1'in tehdit puanı bütçesi.")]
    public float baseThreatBudget = 7f;

    [Tooltip("Level başına bütçe büyümesi.\n\n" +
             "OYUNCU GÜCÜNDEN TÜRER: oyuncu kampanya boyunca ~13.8 kat güçleniyor " +
             "(LevelCurve). Bütçe de aynı oranda büyürse level SÜRESİ sabit kalır " +
             "ve büyüme tamamen dalga BOYUTUNA gider — istenen buydu. " +
             "13.8^(1/99) = 1.0267.\n\n" +
             "Neden %10-15 değil: 100 level bileşik faizdir. %10 ile Lv100 bütçesi " +
             "87.700 tehdit puanı eder, yani tek levelde 87.700 Swarm. Level " +
             "başına anlamlı olan oran %2.7; hissedilen birim BÖLÜMDÜR ve orada " +
             "artış ×1.31 olur.")]
    public float budgetGrowth = 1.027f;

    [Header("Düşman Değeri")]
    [Tooltip("Tehdit puanı başına düşen kaynak (level 1). Eskiden sabit 4'tü.")]
    public float baseDropPerThreat = 2.1f;

    [Tooltip("Level başına drop büyümesi. Bütçeden AYRI tutulur: kalabalık ve " +
             "birim değeri farklı hızlarda büyür.\n\n" +
             "budgetGrowth 1.018 -> 1.027 çıkarken bu 1.031 -> 1.022'ye indirildi. " +
             "Kampanya geliri = Σ(bütçe × drop) olduğu için çarpımları sabit " +
             "tutulmalıydı (1.018×1.031 ≈ 1.027×1.022); yoksa toplam gelir iki " +
             "katına çıkar ve yükseltme fiyatlarının tamamı geçersizleşirdi. " +
             "Artık düşman daha çok ama tanesi daha ucuz.")]
    public float dropGrowth = 1.022f;

    [Header("Asteroit")]
    [Tooltip("Level başına asteroit kaynak bütçesi. Asteroit geliri eskiden süre " +
             "bazlıydı ve düşman gelirinin 3 katına çıkıyordu — bölümü uzatarak " +
             "sınırsız farm edilebiliyordu. Artık levelle birlikte büyür.")]
    public float asteroidBase   = 10f;
    public float asteroidGrowth = 1.035f;

    [Tooltip("Bir level içinde her dalganın bir öncekine göre büyüme oranı. " +
             "Level kendi zirvesiyle bitsin diye: ilk dalga ısınma, son dalga " +
             "levelin en ağır anı.")]
    public float waveBudgetGrowth = 1.25f;

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
             "veriyordu, yani oyundaki en zayıf yükseltme oluyordu. 1.5'te her " +
             "seviye tamponu tam yarı yarıya büyütür.")]
    public float capacitorStatStep = 1.5f;

    [Tooltip("Zırh (gövde HP) statının maliyet çarpanı. Onarım biriminin diğer " +
             "izleriyle aynı tabandan başlasaydı, doğrudan hayatta kalma satın " +
             "alan bir iz olarak açık ara en verimli yükseltme olurdu.")]
    public float armorStatCostFactor = 3f;

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
    /// Statın kendi maliyet çarpanı. Çoğu iz 1.0'dır; ayrıcalıklı izler (zırh)
    /// aynı komponentin tabanını paylaşmak yerine burada pahalılaşır.
    /// </summary>
    public float StatCostFactor(string key) => key == "armor" ? armorStatCostFactor : 1f;

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
