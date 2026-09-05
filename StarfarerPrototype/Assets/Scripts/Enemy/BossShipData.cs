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
    [Tooltip("Metin tablosu anahtarı (boss.type.*), ekrana yazılacak ad değil — " +
             "bkz. EnemyTypeData.displayName.")]
    public string displayName;
    public int    threatScore = 20;

    [Header("Temel İstatistikler")]
    public float maxHP       = 800f;
    public float mass        = 20f;
    public float enginePower = 8f;

    [Tooltip("Atış başına sabit hasar düşürür. Geç bölüm boss'larının düşük " +
             "seviye silahları elemesini sağlayan mekanizma — bir bölüm boss'u " +
             "ancak o bölümün beklediği seviyedeki silahla makul sürede iner.")]
    public float armor = 0f;

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

    // ── Bölüm boss'ları ───────────────────────────────────────────────────────

    /// <summary>
    /// Bir bölümün kapanış boss'u. HP ve zırh <see cref="LevelCurve"/>'den
    /// türer; elle yazılan tek şey mekanik ve isimdir. On boss'u tek tek
    /// elle dengelemek 100 levellik bir kampanyada sürdürülemez.
    ///
    /// Her boss bir sistemi sınar: kalkan katmanı, Point Defence, zırh eşiği,
    /// menzil, enerji bütçesi, DPS eşiği.
    /// </summary>
    public static BossShipData CreateForChapter(int chapter)
    {
        chapter = Mathf.Clamp(chapter, 1, 10);

        var curve = LevelCurve.Instance;
        int level = chapter * curve.levelsPerChapter;

        var b = CreateInstance<BossShipData>();
        b.maxHP       = curve.BossHullHP(level);
        b.threatScore = Mathf.RoundToInt(BalanceConfig.Instance.bossThreatValue);
        b.mass        = 20f + chapter;
        b.enginePower = 8f;
        b.bodyWidth   = 200 + chapter * 6;
        b.bodyHeight  = 100 + chapter * 3;
        b.preferredX  = 8f;
        b.verticalRange = 2f;

        float hpHP = curve.BossHardpointHP(level);
        int   hpN  = curve.BossHardpointCount(chapter);

        var swarm   = EnemyTypeData.CreateSwarm();
        var armored = EnemyTypeData.CreateArmored();
        var bomber  = EnemyTypeData.CreateBomber();

        // Bölümün tanıttığı mekanik — boss o mekaniğin sınavıdır
        switch (chapter)
        {
            case 1:
                b.name = "Sentinel";       b.displayName = "boss.type.sentinel";
                b.bodyColor = new Color(0.30f, 0.32f, 0.36f);
                b.hardpoints = Cannons(hpN, hpHP, dmg: 18f, rate: 5f, speed: 2.5f);
                break;

            case 2:
                b.name = "PatrolLeader";   b.displayName = "boss.type.patrolLeader";
                b.bodyColor = new Color(0.42f, 0.45f, 0.50f);
                b.hardpoints = Combine(
                    Cannons(hpN - 1, hpHP, 20f, 5f, 2.5f),
                    DroneBay(hpHP, 9f, armored));
                break;

            case 3:
                b.name = "ShieldMatrix";   b.displayName = "boss.type.shieldMatrix";
                b.bodyColor = new Color(0.25f, 0.35f, 0.85f);
                // Kalkan jeneratörü yaşadığı sürece gövde vurulamaz
                b.hardpoints = Combine(
                    Cannons(hpN - 1, hpHP, 18f, 4.5f, 3f),
                    ShieldGen(hpHP * 1.3f));
                break;

            case 4:
                b.name = "BombPlatform";   b.displayName = "boss.type.bombPlatform";
                b.bodyColor = new Color(0.85f, 0.45f, 0.05f);
                b.hardpoints = Combine(
                    Cannons(hpN - 1, hpHP, 24f, 4f, 2.2f),
                    DroneBay(hpHP, 7f, bomber));
                break;

            case 5:
                b.name = "ArmoredKeep";    b.displayName = "boss.type.armoredKeep";
                b.bodyColor = new Color(0.34f, 0.30f, 0.26f);
                b.armor     = 12f;   // ilk gerçek zırh duvarı
                b.hardpoints = Cannons(hpN, hpHP, 26f, 4.5f, 2.4f);
                break;

            case 6:
                b.name = "HowitzerLine";   b.displayName = "boss.type.howitzerLine";
                b.bodyColor = new Color(0.35f, 0.42f, 0.30f);
                b.armor     = 6f;
                b.preferredX = 11f;  // menzil dışından döver, oyuncu yaklaşmalı
                b.hardpoints = Cannons(hpN, hpHP, 34f, 6f, 1.6f);
                break;

            case 7:
                b.name = "Disruptor";      b.displayName = "boss.type.disruptor";
                b.bodyColor = new Color(0.55f, 0.25f, 0.75f);
                b.armor     = 8f;
                // Jammer droneları enerji üretimini kısar — bütçe sınavı
                b.hardpoints = Combine(
                    Cannons(hpN - 1, hpHP, 22f, 4f, 2.6f),
                    DroneBay(hpHP, 6f, EnemyTypeData.CreateJammer()));
                break;

            case 8:
                b.name = "HiveMother";     b.displayName = "boss.type.hiveMother";
                b.bodyColor = new Color(0.25f, 0.70f, 0.40f);
                b.armor     = 10f;
                // Onarım yatağı hardpoint'leri geri getirir — DPS eşiği sınavı
                b.hardpoints = Combine(
                    Cannons(hpN - 2, hpHP, 24f, 4f, 2.5f),
                    RepairBay(hpHP * 1.2f),
                    DroneBay(hpHP, 7f, EnemyTypeData.CreateRegenerator()));
                break;

            case 9:
                b.name = "Dreadnought";    b.displayName = "boss.type.dreadnought";
                b.bodyColor = new Color(0.28f, 0.24f, 0.32f);
                b.armor     = 14f;
                // ChapterManager bu bölümde İKİ tane spawn eder — hedef bölme sınavı
                b.hardpoints = Combine(
                    Cannons(hpN - 1, hpHP, 26f, 4f, 2.5f),
                    ShieldGen(hpHP));
                break;

            default:
                b.name = "HiveMind";       b.displayName = "boss.type.hiveMind";
                b.bodyColor = new Color(0.55f, 0.10f, 0.18f);
                b.armor     = curve.maxArmor;   // 20 — her mekanik burada toplanır
                b.hardpoints = Combine(
                    Cannons(hpN - 3, hpHP, 30f, 3.5f, 2.8f),
                    ShieldGen(hpHP * 1.4f),
                    RepairBay(hpHP * 1.2f),
                    DroneBay(hpHP, 5f, EnemyTypeData.CreateJuggernaut()));
                break;
        }

        b.phases = StandardPhases(chapter, swarm, armored, bomber);
        return b;
    }

    // ── Hardpoint kurucuları ──────────────────────────────────────────────────

    static BossHardpointDef[] Cannons(int count, float hp, float dmg, float rate, float speed)
    {
        count = Mathf.Max(1, count);
        var list = new BossHardpointDef[count];
        for (int i = 0; i < count; i++)
        {
            // Toplar gövde boyunca dikeyde dağılır
            float t = count == 1 ? 0f : (i / (float)(count - 1)) * 2f - 1f;
            list[i] = new BossHardpointDef
            {
                label       = count == 1 ? "Top" : $"Top {i + 1}",
                type        = HardpointType.Cannon,
                localOffset = new Vector2(0.55f, t * 0.38f),
                hp = hp, width = 28, height = 20,
                color       = new Color(0.35f, 0.38f, 0.42f),
                fireDamage  = dmg, fireRate = rate, bulletSpeed = speed,
            };
        }
        return list;
    }

    static BossHardpointDef[] ShieldGen(float hp) => new[]
    {
        new BossHardpointDef
        {
            label       = "Kalkan Jeneratörü",
            type        = HardpointType.ShieldGenerator,
            localOffset = new Vector2(-0.2f, 0f),
            hp = hp, width = 36, height = 36,
            color       = new Color(0.20f, 0.40f, 0.90f),
        }
    };

    static BossHardpointDef[] RepairBay(float hp) => new[]
    {
        new BossHardpointDef
        {
            label       = "Onarım Yatağı",
            type        = HardpointType.RepairBay,
            localOffset = new Vector2(-0.2f, -0.35f),
            hp = hp, width = 34, height = 28,
            color       = new Color(0.25f, 0.70f, 0.40f),
        }
    };

    static BossHardpointDef[] DroneBay(float hp, float interval, EnemyTypeData drone) => new[]
    {
        new BossHardpointDef
        {
            label         = "Drone Hangarı",
            type          = HardpointType.DroneBay,
            localOffset   = new Vector2(-0.55f, 0f),
            hp = hp, width = 40, height = 30,
            color         = new Color(0.55f, 0.35f, 0.10f),
            spawnInterval = interval,
            dronePool     = new[] { drone },
        }
    };

    static BossHardpointDef[] Combine(params BossHardpointDef[][] groups)
    {
        int n = 0;
        foreach (var g in groups) n += g.Length;
        var all = new BossHardpointDef[n];
        int k = 0;
        foreach (var g in groups)
            foreach (var d in g) all[k++] = d;
        return all;
    }

    /// <summary>
    /// Üç fazlı standart dövüş eğrisi. Geç bölümlerde fazlar daha sert:
    /// escort havuzu genişler, ateş hızı çarpanı büyür.
    /// </summary>
    static BossPhase[] StandardPhases(int chapter, EnemyTypeData swarm,
                                      EnemyTypeData armored, EnemyTypeData bomber)
    {
        float ramp = 1f + chapter * 0.04f;
        return new[]
        {
            new BossPhase
            {
                hpThreshold = 1.0f, fireRateMultiplier = 1.0f,
                spawnInterval = 10f, spawnPool = new[] { swarm }, rushPlayer = false,
            },
            new BossPhase
            {
                hpThreshold = 0.6f, fireRateMultiplier = 1.4f * ramp,
                spawnInterval = 6f, spawnPool = new[] { swarm, armored }, rushPlayer = false,
            },
            new BossPhase
            {
                hpThreshold = 0.3f, fireRateMultiplier = 2.0f * ramp,
                spawnInterval = 4f, spawnPool = new[] { swarm, armored, bomber }, rushPlayer = true,
            },
        };
    }

    // ── Built-in factory ──────────────────────────────────────────────────────

    public static BossShipData CreateCarrierCommand()
    {
        var swarm   = EnemyTypeData.CreateSwarm();
        var armored = EnemyTypeData.CreateArmored();
        var bomber  = EnemyTypeData.CreateBomber();

        var b = CreateInstance<BossShipData>();
        b.name        = "CarrierCommand";
        b.displayName = "boss.type.carrierCommand";
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
