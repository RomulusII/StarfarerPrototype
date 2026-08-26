using UnityEngine;

/// <summary>
/// Gelir ve zırh eğrilerinin TEK sahibi.
///
/// Kapsam notu: yükseltme eğrisi buraya AİT DEĞİLDİR. Yükseltme sistemi
/// (tier zincirleri + komponent başına çoklu stat) <see cref="ComponentCatalog"/>
/// ve <see cref="UpgradeUI"/> içinde kendi kurallarıyla yaşar. Bu dosya yalnızca
/// oyuncunun ne kadar kaynak KAZANDIĞINI ve düşmanların ne kadar sert olduğunu
/// belirler.
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
