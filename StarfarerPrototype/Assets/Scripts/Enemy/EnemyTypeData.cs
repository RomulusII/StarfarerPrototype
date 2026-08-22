using UnityEngine;

[System.Serializable]
public struct DamageModifier
{
    public WeaponType weaponType;
    public float      multiplier;
}

/// <summary>
/// Bir düşman tipini tamamen tanımlar.
/// Yeni tip eklemek = yeni asset oluşturmak; kod değişikliği gerekmez.
/// CreateAssetMenu ile Assets/Enemy Types/ altında asset oluşturulabilir.
/// Editörde asset yokken CreateXxx() factory metodları runtime'da varsayılan veri üretir.
/// </summary>
[CreateAssetMenu(fileName = "EnemyType_New", menuName = "Starfarer/Enemy Type Data")]
public class EnemyTypeData : ScriptableObject
{
    [Header("Kimlik")]
    public string      displayName;
    public EnemyRole   role;
    public int         threatScore = 1;

    [Header("Temel İstatistikler")]
    public float maxHP         = 30f;
    public float maxShield     = 0f;
    public float mass          = 1f;
    public float enginePower   = 3f;
    public float contactDamage = 20f;

    [Header("Görsel")]
    public int   bodyWidth   = 60;
    public int   bodyHeight  = 40;
    public Color bodyColor   = Color.red;
    public Color barrelColor = new Color(0.5f, 0.1f, 0.1f);
    [Tooltip("Render sırası: küçük sayı = büyük nesne = arkada. Sprite gelince bu değeri güncelle.")]
    public int   sizeOrder   = 5;

    [Header("Hareket")]
    public EnemyMovementKind movementKind    = EnemyMovementKind.Charge;
    public float             engageRange     = 6f;
    public float             fireRange       = 5.5f;
    public float             orbitRadius     = 4.5f;
    public float             engageDuration  = 5f;

    [Header("Uçuş Karakteri")]
    [Tooltip("Dönüş hızı çarpanı. Yüksek = kıvrak, dar kavis. Küçük gemiler 1.2–1.6, " +
             "ağır gemiler 0.5–0.8. Kavis yarıçapı ile ters orantılıdır.")]
    public float agility = 1f;

    [Tooltip("Hareket vektörünün burnu takip etme oranı. 1 = savrulma yok, " +
             "düşük değer = kavislerde dışa savrulan hantal gemi.")]
    [Range(0f, 1f)] public float grip = 0.9f;

    [Tooltip("Nişan almadığı anlarda burnun rastgele saptığı max açı (derece). " +
             "Yüksek = tahmin edilmesi zor, düzensiz uçuş. 0 = kaçamak manevra yok.")]
    public float evasionAngle = 15f;

    [Tooltip("Kaçarken uzaklaşma vektöründen sapma açısı (derece). 0 = radyal " +
             "(tam ters) kaçış, yüksek = çapraz kaçış. ChapterData bölüme göre ölçekler.")]
    public float escapeAngle = 40f;

    [Header("Silah")]
    public EnemyWeaponKind weaponKind   = EnemyWeaponKind.Laser;
    public float           fireDamage   = 8f;
    public float           fireRate     = 4f;
    public float           bulletSpeed  = 3f;

    [Header("Enkaz Kaynağı")]
    [Tooltip("Ölünce bırakılan kaynak tipi. threatScore × 4 metal/kristal düşer.")]
    public ResourceType debrisResourceType = ResourceType.RawMaterial;

    [Header("Hull Dirençleri")]
    public DamageModifier[] hullResistances;

    [Header("Kalkan Dirençleri (maxShield > 0 ise geçerli)")]
    public DamageModifier[] shieldResistances;

    // ── Runtime factory metodları ─────────────────────────────────────────────
    // Editor'da SO asset oluşturulmadan önce oyunun çalışmasını sağlar.

    public static EnemyTypeData CreateSwarm()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Swarm";
        d.displayName   = "Swarm";
        d.role          = EnemyRole.Vanguard;
        d.threatScore   = 1;
        d.maxHP         = 20f;
        d.mass          = 1f;   d.enginePower   = 3f;
        d.contactDamage = 20f;
        d.bodyWidth     = 60;   d.bodyHeight    = 20;   d.sizeOrder = 8;
        d.bodyColor     = new Color(0.9f, 0.20f, 0.20f);
        d.barrelColor   = new Color(0.7f, 0.15f, 0.15f);
        d.movementKind  = EnemyMovementKind.Strafe;
        d.engageRange   = 7f;   d.fireRange     = 6.5f;
        d.orbitRadius   = 3.5f; d.engageDuration = 4f;
        d.agility       = 1.5f; d.grip          = 0.95f;  // küçük ve kıvrak: dar kavis, savrulmaz
        d.evasionAngle  = 18f;  // sinek gibi düzensiz — en oynak tip
        d.escapeAngle   = 40f;
        d.weaponKind    = EnemyWeaponKind.Kinetic;
        d.fireDamage    = 3f;   d.fireRate      = 5f;   d.bulletSpeed = 6f;
        d.hullResistances = new[]
        {
            new DamageModifier { weaponType = WeaponType.Laser, multiplier = 1.5f },
        };
        return d;
    }

    public static EnemyTypeData CreateArmored()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Armored";
        d.displayName   = "Armored";
        d.role          = EnemyRole.Rear;
        d.threatScore   = 4;
        d.maxHP         = 80f;
        d.mass          = 5f;   d.enginePower   = 7.5f;
        d.contactDamage = 25f;
        d.bodyWidth     = 80;   d.bodyHeight    = 55;   d.sizeOrder = 3;
        d.bodyColor     = new Color(0.42f, 0.45f, 0.50f);
        d.barrelColor   = new Color(0.40f, 0.40f, 0.45f);
        d.movementKind  = EnemyMovementKind.HoverFire;
        d.engageRange   = 5f;   d.fireRange     = 5f;
        d.orbitRadius   = 4f;   d.engageDuration = 8f;
        d.agility       = 0.55f; d.grip         = 0.72f;  // hantal: geniş kavis, belirgin savrulma
        d.evasionAngle  = 6f;   // ağır gemi kaçamak yapmaz, rotasını korur
        d.escapeAngle   = 30f;  // hantal — çaprazı da az
        d.weaponKind    = EnemyWeaponKind.Cannon;
        d.fireDamage    = 15f;  d.fireRate      = 6f;   d.bulletSpeed = 2f;
        d.hullResistances = new[]
        {
            new DamageModifier { weaponType = WeaponType.Kinetic, multiplier = 0.30f },
            new DamageModifier { weaponType = WeaponType.Plasma,  multiplier = 1.80f },
        };
        return d;
    }

    public static EnemyTypeData CreateShield()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Shield";
        d.displayName   = "Shield";
        d.role          = EnemyRole.Center;
        d.threatScore   = 5;
        d.maxHP         = 50f;  d.maxShield     = 40f;
        d.mass          = 3f;   d.enginePower   = 6f;
        d.contactDamage = 20f;
        d.bodyWidth     = 70;   d.bodyHeight    = 50;   d.sizeOrder = 5;
        d.bodyColor     = new Color(0.25f, 0.35f, 0.85f);
        d.barrelColor   = new Color(0.15f, 0.20f, 0.70f);
        d.movementKind  = EnemyMovementKind.Charge;
        d.engageRange   = 5.5f; d.fireRange     = 4.5f;
        d.orbitRadius   = 3.5f; d.engageDuration = 5f;
        d.agility       = 0.85f; d.grip         = 0.85f;  // orta sınıf: dengeli kavis
        d.evasionAngle  = 12f;
        d.escapeAngle   = 40f;
        d.weaponKind    = EnemyWeaponKind.Laser;
        d.fireDamage    = 6f;   d.fireRate      = 3f;   d.bulletSpeed = 3.5f;
        d.shieldResistances = new[]
        {
            new DamageModifier { weaponType = WeaponType.Kinetic, multiplier = 1.5f  },
            new DamageModifier { weaponType = WeaponType.Laser,   multiplier = 0.25f },
        };
        return d;
    }

    public static EnemyTypeData CreateBomber()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Bomber";
        d.displayName   = "Bomber";
        d.role          = EnemyRole.Flank;
        d.threatScore   = 10;
        d.maxHP         = 10f;
        d.mass          = 2f;   d.enginePower   = 8f;
        d.contactDamage = 0f;
        d.bodyWidth     = 44;   d.bodyHeight    = 12;   d.sizeOrder = 6;
        d.bodyColor     = new Color(0.9f, 0.50f, 0.10f);
        d.movementKind  = EnemyMovementKind.AttackRun;
        d.engageRange   = 8f;   d.fireRange     = 2.5f;
        d.agility       = 1.15f; d.grip         = 0.93f;  // hızlı avcı: dalışta geniş, frende dar kavis
        d.evasionAngle  = 15f;
        d.escapeAngle   = 40f;
        d.weaponKind    = EnemyWeaponKind.ComponentBurst;
        d.fireDamage    = 2f;   d.fireRate      = 1.8f; d.bulletSpeed = 2.25f;
        return d;
    }

    public static EnemyTypeData CreateBombRunner()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "BombRunner";
        d.displayName   = "Bomb Runner";
        d.role          = EnemyRole.Flank;
        d.threatScore   = 12;
        d.maxHP         = 35f;
        d.mass          = 2f;   d.enginePower   = 5f;
        d.contactDamage = 0f;
        d.bodyWidth     = 65;   d.bodyHeight    = 45;   d.sizeOrder = 6;
        d.bodyColor     = new Color(0.85f, 0.45f, 0.05f);
        d.barrelColor   = new Color(0.70f, 0.35f, 0.05f);
        d.movementKind  = EnemyMovementKind.BombRun;
        d.agility       = 0.6f; d.grip          = 0.8f;
        d.evasionAngle  = 0f;   // düz hat bomba koşusu — salınım ve kaçış yok
        d.escapeAngle   = 0f;
        d.weaponKind    = EnemyWeaponKind.None;
        d.fireDamage    = 30f;
        d.fireRate      = 2.5f;
        d.bulletSpeed   = 2.5f;
        return d;
    }
}
