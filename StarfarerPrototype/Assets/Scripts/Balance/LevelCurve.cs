using UnityEngine;

/// <summary>
/// Düşman ölçeklemesinin tek sahibi. 100 level elle ayarlanamaz; zorluk sürekli
/// bir formülden gelir, bölüm sınırları yalnızca tema ve yeni düşman tipi getirir.
///
/// Ölçek hedefi: düşman efektif HP büyümesi oyuncu güç büyümesinin ~%87'si.
/// Oyuncu Lv1→Lv100 arası ~13.8× güce çıkar; düşman 9.8×'e. Aradaki fark
/// oyuncunun ilerleme hissidir — ama kapanmaz, çünkü zırh eşiği geride kalan
/// her seviyeyi ayrıca cezalandırır.
/// </summary>
[CreateAssetMenu(fileName = "LevelCurve", menuName = "Starfarer/Level Curve")]
public class LevelCurve : ScriptableObject
{
    [Header("Kapsam")]
    public int totalLevels     = 100;
    public int levelsPerChapter = 10;

    [Header("Ölçekleme")]
    [Tooltip("Level başına HP büyümesi. Lv100 = 9.8×.")]
    public float hpGrowth = 1.0233f;

    [Tooltip("Level başına hasar büyümesi. Lv100 = 4.0×. " +
             "Eskiden bu çarpan hiç ölçeklenmiyordu (sabit 1.0).")]
    public float damageGrowth = 1.0141f;

    [Header("Zırh")]
    [Tooltip("Son leveldeki taban zırh. Tip bonusları bunun üstüne eklenir.")]
    public float maxArmor = 20f;

    [Tooltip("Zırh eğrisinin üssü. 1'den büyük = erken leveller neredeyse zırhsız.")]
    public float armorExponent = 1.6f;

    [Header("Kaçamak Manevra")]
    [Tooltip("Kaçamak davranışın tam açıldığı level. Öncesinde doğrusal artar — " +
             "oyuncu nişan almayı öğrenirken düz uçan hedeflerle başlar.")]
    public int evasionFullLevel = 25;

    // ── Singleton ─────────────────────────────────────────────────────────────

    static LevelCurve _instance;

    public static LevelCurve Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<LevelCurve>("LevelCurve");
            if (_instance == null) _instance = CreateInstance<LevelCurve>();
            return _instance;
        }
    }

    // ── Formüller ─────────────────────────────────────────────────────────────

    public float HpMultiplier(int n)     => Mathf.Pow(hpGrowth,     Mathf.Max(0, n - 1));
    public float DamageMultiplier(int n) => Mathf.Pow(damageGrowth, Mathf.Max(0, n - 1));

    public float Armor(int n)
        => maxArmor * Mathf.Pow(Mathf.Clamp01((float)n / totalLevels), armorExponent);

    public float EvasionMultiplier(int n)
        => Mathf.InverseLerp(1f, Mathf.Max(2, evasionFullLevel), n);

    /// <summary>Boss gövde HP'si — bölüm kapanış dövüşü.</summary>
    public float BossHullHP(int n) => 500f * HpMultiplier(n);

    /// <summary>Boss hardpoint HP'si.</summary>
    public float BossHardpointHP(int n) => 120f * HpMultiplier(n);

    /// <summary>Bölüm numarasına göre hardpoint adedi — boss'lar giderek karmaşıklaşır.</summary>
    public int BossHardpointCount(int chapter) => 2 + chapter / 2;
}

/// <summary>
/// Bir düşmana uygulanacak ölçekleme katsayıları. Kampanya bunları
/// <see cref="LevelCurve"/>'den, serbest mod kendi rampasından üretir —
/// ama ikisi de aynı yoldan geçer, ayrı formül yoktur.
/// </summary>
public struct EnemyScaling
{
    public float hp;
    public float damage;
    public float evasion;
    public float armor;

    public static EnemyScaling None => new EnemyScaling
    {
        hp = 1f, damage = 1f, evasion = 1f, armor = 0f,
    };

    public static EnemyScaling ForLevel(int gameLevel)
    {
        var c = LevelCurve.Instance;
        return new EnemyScaling
        {
            hp      = c.HpMultiplier(gameLevel),
            damage  = c.DamageMultiplier(gameLevel),
            evasion = c.EvasionMultiplier(gameLevel),
            armor   = c.Armor(gameLevel),
        };
    }
}
