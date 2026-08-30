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

    [Tooltip("Level başına bütçe büyümesi. Lv100'de 5.8× daha çok düşman — " +
             "sahneye sığacak kadar.")]
    public float budgetGrowth = 1.018f;

    [Header("Düşman Değeri")]
    [Tooltip("Tehdit puanı başına düşen kaynak (level 1). Eskiden sabit 4'tü.")]
    public float baseDropPerThreat = 2.1f;

    [Tooltip("Level başına drop büyümesi. Lv100'de 21× daha değerli düşman. " +
             "Bütçeden AYRI tutulur: kalabalık yavaş, değer hızlı büyür.")]
    public float dropGrowth = 1.031f;

    [Header("Asteroit")]
    [Tooltip("Level başına asteroit kaynak bütçesi. Asteroit geliri eskiden süre " +
             "bazlıydı ve düşman gelirinin 3 katına çıkıyordu — bölümü uzatarak " +
             "sınırsız farm edilebiliyordu. Artık levelle birlikte büyür.")]
    public float asteroidBase   = 10f;
    public float asteroidGrowth = 1.035f;

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

    /// <summary>Zırh eşiği — atış başına hasarı ödüllendiren tek formül.</summary>
    public float ApplyArmor(float damage, float armor)
    {
        if (armor <= 0f) return damage;
        return Mathf.Max(damage - armor, damage * armorMinDamageRatio);
    }
}
