using UnityEngine;

public enum HardpointType
{
    Cannon,           // Top ateşi — yok olunca ateş kapasitesi azalır
    Laser,            // Lazer ateşi
    ShieldGenerator,  // Yaşıyorsa boss kalkanı aktif
    DroneBay,         // Destek gemi üretir — yok olunca spawn durur
    RepairBay,        // Hardpoint'leri onarır (ileri level boss)
}

[System.Serializable]
public struct BossHardpointDef
{
    public string        label;
    public HardpointType type;
    public Vector2       localOffset;  // gemi merkezine göre
    public float         hp;
    public int           width, height;
    public Color         color;

    // Silah hardpoint'leri için
    public float fireDamage;
    public float fireRate;
    public float bulletSpeed;

    // DroneBay için
    public float          spawnInterval;
    public EnemyTypeData[] dronePool;
}

[System.Serializable]
public struct BossPhase
{
    [Range(0f, 1f)]
    [Tooltip("Bu faza geçiş için HP eşiği (0–1 arası yüzde). Örn: 0.6 = %60 HP'de giriş.")]
    public float hpThreshold;

    public float fireRateMultiplier;
    public float spawnInterval;

    [Tooltip("Bu fazda DroneBay'in spawn edeceği tipler. Boşsa önceki faz devam eder.")]
    public EnemyTypeData[] spawnPool;

    [Tooltip("Bu faza girilince boss hedefe yaklaşmaya çalışır.")]
    public bool rushPlayer;
}

/// <summary>
/// Bir boss gemi tipini tamamen tanımlar.
/// Yeni boss = yeni asset. Herhangi bir WaveData'da kullanılabilir:
///   - Boss bölümü için tek başına
///   - Normal wave'de elite olarak (allowedTypes ile birlikte)
///   - İlerideki boss'un yancısı olarak
/// </summary>
[CreateAssetMenu(fileName = "Boss_New", menuName = "Starfarer/Boss Ship Data")]
public class BossShipData : ScriptableObject
{
    [Header("Kimlik")]
    public string displayName;
    public int    threatScore = 20;

    [Header("Temel İstatistikler")]
    public float maxHP       = 800f;
    public float mass        = 20f;
    public float enginePower = 8f;
    public float contactDamage = 0f;

    [Header("Görsel")]
    public int   bodyWidth  = 220;
    public int   bodyHeight = 110;
    public Color bodyColor  = new Color(0.22f, 0.25f, 0.30f);

    [Header("Hareket")]
    [Tooltip("Boss'un tercih ettiği X pozisyonu (ekranın sağ tarafı ≈ 7–9).")]
    public float preferredX   = 8f;
    [Tooltip("Boss'un dikey salınım aralığı.")]
    public float verticalRange = 2f;

    [Header("Hardpoint'ler")]
    public BossHardpointDef[] hardpoints;

    [Header("Fazlar (hpThreshold'a göre küçükten büyüğe sıralayın)")]
    public BossPhase[] phases;

    // ── Built-in factory ──────────────────────────────────────────────────────

    public static BossShipData CreateCarrierCommand()
    {
        var swarm   = EnemyTypeData.CreateSwarm();
        var armored = EnemyTypeData.CreateArmored();
        var bomber  = EnemyTypeData.CreateBomber();

        var b = CreateInstance<BossShipData>();
        b.name        = "CarrierCommand";
        b.displayName = "Komuta Taşıyıcısı";
        b.threatScore = 20;
        b.maxHP       = 800f;
        b.mass        = 20f; b.enginePower = 8f;
        b.bodyWidth   = 220; b.bodyHeight  = 110;
        b.bodyColor   = new Color(0.22f, 0.25f, 0.30f);
        b.preferredX  = 8f;  b.verticalRange = 2f;

        b.hardpoints = new BossHardpointDef[]
        {
            new BossHardpointDef
            {
                label       = "Sol Top",
                type        = HardpointType.Cannon,
                localOffset = new Vector2( 0.6f,  0.35f),
                hp = 120f, width = 28, height = 20,
                color       = new Color(0.35f, 0.38f, 0.42f),
                fireDamage  = 20f, fireRate = 5f, bulletSpeed = 2.5f,
            },
            new BossHardpointDef
            {
                label       = "Sağ Top",
                type        = HardpointType.Cannon,
                localOffset = new Vector2( 0.6f, -0.35f),
                hp = 120f, width = 28, height = 20,
                color       = new Color(0.35f, 0.38f, 0.42f),
                fireDamage  = 20f, fireRate = 5f, bulletSpeed = 2.5f,
            },
            new BossHardpointDef
            {
                label       = "Kalkan Jeneratörü",
                type        = HardpointType.ShieldGenerator,
                localOffset = new Vector2(-0.2f,  0f),
                hp = 160f, width = 36, height = 36,
                color       = new Color(0.20f, 0.40f, 0.90f),
            },
            new BossHardpointDef
            {
                label         = "Drone Hangarı",
                type          = HardpointType.DroneBay,
                localOffset   = new Vector2(-0.55f, 0f),
                hp            = 140f, width = 40, height = 30,
                color         = new Color(0.55f, 0.35f, 0.10f),
                spawnInterval = 10f,
                dronePool     = new[] { swarm },
            },
        };

        b.phases = new BossPhase[]
        {
            new BossPhase   // Faz 1: başlangıç
            {
                hpThreshold        = 1.0f,
                fireRateMultiplier = 1.0f,
                spawnInterval      = 10f,
                spawnPool          = new[] { swarm },
                rushPlayer         = false,
            },
            new BossPhase   // Faz 2: %60 HP
            {
                hpThreshold        = 0.6f,
                fireRateMultiplier = 1.4f,
                spawnInterval      = 6f,
                spawnPool          = new[] { swarm, armored },
                rushPlayer         = false,
            },
            new BossPhase   // Faz 3: %30 HP — çılgınlaşır
            {
                hpThreshold        = 0.3f,
                fireRateMultiplier = 2.0f,
                spawnInterval      = 4f,
                spawnPool          = new[] { swarm, armored, bomber },
                rushPlayer         = true,
            },
        };

        return b;
    }
}
