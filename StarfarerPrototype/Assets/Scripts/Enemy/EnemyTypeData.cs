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

    [Tooltip("Atış başına sabit hasar düşürür: efektif = max(hasar − zırh, hasar × 0.10). " +
             "Level zırhının ÜSTÜNE eklenen tip bonusudur. Bu eşik atış BAŞINA " +
             "hasarı ödüllendirir — gemi DPS'i toplamsal olduğu için zırh olmadan " +
             "çok sayıda zayıf silah az sayıda güçlü silahı hep yener.")]
    public float armor = 0f;

    [Header("Görsel")]
    public int   bodyWidth   = 60;
    public int   bodyHeight  = 40;
    public Color bodyColor   = Color.red;
    public Color barrelColor = new Color(0.5f, 0.1f, 0.1f);
    [Tooltip("Render sırası: küçük sayı = büyük nesne = arkada. Sprite gelince bu değeri güncelle.")]
    public int   sizeOrder   = 5;

    [Header("Hitbox (görselden bağımsız)")]
    [Tooltip("Çarpışma kutusu (piksel). 0 = gövde boyutu kullanılır. Skin'ler gelince " +
             "bodyWidth/bodyHeight değişecek; hitbox buna bağlı kalsaydı vurma zorluğu " +
             "kayar ve denge ayarları geçersizleşirdi. Shmup'larda hitbox genellikle " +
             "görselden kasten küçük tutulur.")]
    public int hitboxWidth  = 0;
    public int hitboxHeight = 0;

    public int EffectiveHitboxWidth  => hitboxWidth  > 0 ? hitboxWidth  : bodyWidth;
    public int EffectiveHitboxHeight => hitboxHeight > 0 ? hitboxHeight : bodyHeight;

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

    [Tooltip("Nişan almadığı anlarda salınımın tepe açısı (derece). Desen rastgele " +
             "değil, deterministiktir — bu değer genliği belirler. 0 = salınım yok.")]
    public float evasionAngle = 15f;

    [Tooltip("Kaçarken uzaklaşma vektöründen sapma açısı (derece). 0 = radyal " +
             "(tam ters) kaçış, yüksek = çapraz kaçış. ChapterData bölüme göre ölçekler.")]
    public float escapeAngle = 40f;

    [Tooltip("Salınım deseninin tekrar periyodu (saniye). Küçük = titrek ve hızlı, " +
             "büyük = yayvan. Tipin uçuş imzasını belirleyen değer — oyuncu bunu öğrenir.")]
    public float evasionPeriod = 2f;

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

    // ── Özel davranışlar ──────────────────────────────────────────────────────
    // Her biri oyuncunun bir sistemine baskı yapar; süs değildir.

    [Header("Özel Davranışlar")]
    [Tooltip("Jammer: menzilindeyken oyuncunun jeneratör üretimini bu oranda düşürür " +
             "(0–1). Enerji bütçesini hedef alır — öncelik hedeflemeyi zorunlu kılar.")]
    [Range(0f, 1f)] public float energyDrain = 0f;
    [Tooltip("Jammer etkisinin menzili (birim).")]
    public float energyDrainRange = 7f;

    [Tooltip("Phantom: bu periyotta bir vurulamaz hale gelir (saniye). 0 = kapalı. " +
             "Sürekli DPS yerine burst ödüllendirir.")]
    public float phaseInterval = 0f;
    [Tooltip("Vurulamazlık süresi (saniye).")]
    public float phaseDuration = 2f;

    [Tooltip("Splitter: ölünce bu tipten iki tane çıkar. Alan hasarı talebi yaratır.")]
    public EnemyTypeData splitInto;
    [Tooltip("Bölünen parçaların HP oranı.")]
    public float splitHpRatio = 0.5f;

    [Tooltip("Regenerator: menzilindeki düşmanları saniyede bu kadar onarır. " +
             "DPS eşiği yaratır — yavaş build duvara çarpar.")]
    public float repairAura = 0f;
    public float repairAuraRange = 5f;

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
        d.evasionAngle  = 18f;  d.evasionPeriod = 1.4f;  // hızlı, titrek imza
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
        d.evasionAngle  = 6f;   d.evasionPeriod = 3.4f;  // ağır gemi, yayvan ve az
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
        d.evasionAngle  = 12f;  d.evasionPeriod = 2.2f;
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
        d.evasionAngle  = 15f;  d.evasionPeriod = 1.7f;  // hızlı avcı
        d.escapeAngle   = 40f;
        d.weaponKind    = EnemyWeaponKind.ComponentBurst;
        d.fireDamage    = 2f;   d.fireRate      = 1.8f; d.bulletSpeed = 2.25f;
        return d;
    }

    // ── Bölüm 5+ tipleri ──────────────────────────────────────────────────────
    // Her yeni tip oyuncunun bir sistemine baskı yapar. Sırayla açılırlar;
    // hangi bölümde geldikleri ChapterData'da tanımlıdır.

    /// <summary>Avcı — çok hızlı, kırılgan, kaçamak. Turret hedeflemesini zorlar.</summary>
    public static EnemyTypeData CreateInterceptor()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Interceptor";
        d.displayName   = "Avcı";
        d.role          = EnemyRole.Vanguard;
        d.threatScore   = 6;
        d.maxHP         = 25f;
        d.mass          = 0.8f; d.enginePower   = 5.5f;
        d.contactDamage = 18f;
        d.bodyWidth     = 52;   d.bodyHeight    = 18;   d.sizeOrder = 8;
        d.hitboxWidth   = 40;   d.hitboxHeight  = 14;
        d.bodyColor     = new Color(0.95f, 0.75f, 0.20f);
        d.barrelColor   = new Color(0.75f, 0.55f, 0.10f);
        d.movementKind  = EnemyMovementKind.Strafe;
        d.engageRange   = 8f;   d.fireRange     = 7f;
        d.orbitRadius   = 4f;   d.engageDuration = 3.5f;
        d.agility       = 1.8f; d.grip          = 0.97f;
        d.evasionAngle  = 26f;  d.evasionPeriod = 1.1f;
        d.escapeAngle   = 55f;
        d.weaponKind    = EnemyWeaponKind.Kinetic;
        d.fireDamage    = 4f;   d.fireRate      = 3.5f; d.bulletSpeed = 7f;
        return d;
    }

    /// <summary>Obüs — uzaktan döver, yaklaşmayı zorunlu kılar.</summary>
    public static EnemyTypeData CreateArtillery()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Artillery";
        d.displayName   = "Obüs";
        d.role          = EnemyRole.Rear;
        d.threatScore   = 9;
        d.maxHP         = 60f;
        d.armor         = 3f;
        d.mass          = 6f;   d.enginePower   = 6f;
        d.contactDamage = 20f;
        d.bodyWidth     = 86;   d.bodyHeight    = 46;   d.sizeOrder = 3;
        d.hitboxWidth   = 74;   d.hitboxHeight  = 38;
        d.bodyColor     = new Color(0.35f, 0.42f, 0.30f);
        d.barrelColor   = new Color(0.28f, 0.34f, 0.24f);
        d.movementKind  = EnemyMovementKind.HoverFire;
        d.engageRange   = 11f;  d.fireRange     = 10.5f;
        d.orbitRadius   = 8f;   d.engageDuration = 10f;
        d.agility       = 0.45f; d.grip         = 0.70f;
        d.evasionAngle  = 4f;   d.evasionPeriod = 4f;
        d.escapeAngle   = 20f;
        d.weaponKind    = EnemyWeaponKind.Cannon;
        d.fireDamage    = 26f;  d.fireRate      = 7f;   d.bulletSpeed = 1.8f;
        return d;
    }

    /// <summary>Karıştırıcı — enerji üretimini kısar. Enerji bütçesini hedef alır.</summary>
    public static EnemyTypeData CreateJammer()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Jammer";
        d.displayName   = "Karıştırıcı";
        d.role          = EnemyRole.Center;
        d.threatScore   = 11;
        d.maxHP         = 55f;  d.maxShield     = 30f;
        d.mass          = 3.5f; d.enginePower   = 6f;
        d.contactDamage = 12f;
        d.bodyWidth     = 66;   d.bodyHeight    = 52;   d.sizeOrder = 5;
        d.bodyColor     = new Color(0.55f, 0.25f, 0.75f);
        d.barrelColor   = new Color(0.40f, 0.18f, 0.60f);
        d.movementKind  = EnemyMovementKind.HoverFire;
        d.engageRange   = 7f;   d.fireRange     = 6.5f;
        d.orbitRadius   = 5.5f; d.engageDuration = 9f;
        d.agility       = 0.8f; d.grip          = 0.85f;
        d.evasionAngle  = 10f;  d.evasionPeriod = 2.6f;
        d.escapeAngle   = 35f;
        d.weaponKind    = EnemyWeaponKind.Laser;
        d.fireDamage    = 7f;   d.fireRate      = 4f;   d.bulletSpeed = 4f;
        d.energyDrain      = 0.40f;
        d.energyDrainRange = 7f;
        d.shieldResistances = new[]
        {
            new DamageModifier { weaponType = WeaponType.Kinetic, multiplier = 1.5f  },
            new DamageModifier { weaponType = WeaponType.Laser,   multiplier = 0.25f },
        };
        return d;
    }

    /// <summary>Hayalet — periyodik vurulamazlık. Sürekli DPS yerine burst ister.</summary>
    public static EnemyTypeData CreatePhantom()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Phantom";
        d.displayName   = "Hayalet";
        d.role          = EnemyRole.Flank;
        d.threatScore   = 10;
        d.maxHP         = 45f;
        d.mass          = 1.6f; d.enginePower   = 5f;
        d.contactDamage = 16f;
        d.bodyWidth     = 58;   d.bodyHeight    = 30;   d.sizeOrder = 7;
        d.bodyColor     = new Color(0.45f, 0.80f, 0.78f);
        d.barrelColor   = new Color(0.30f, 0.60f, 0.58f);
        d.movementKind  = EnemyMovementKind.Strafe;
        d.engageRange   = 7f;   d.fireRange     = 6f;
        d.orbitRadius   = 4.5f; d.engageDuration = 5f;
        d.agility       = 1.3f; d.grip          = 0.92f;
        d.evasionAngle  = 20f;  d.evasionPeriod = 1.8f;
        d.escapeAngle   = 45f;
        d.weaponKind    = EnemyWeaponKind.Laser;
        d.fireDamage    = 9f;   d.fireRate      = 3f;   d.bulletSpeed = 4.5f;
        d.phaseInterval = 4.5f; d.phaseDuration = 2f;
        return d;
    }

    /// <summary>Onarıcı — çevresini tamir eder. DPS eşiği yaratır.</summary>
    public static EnemyTypeData CreateRegenerator()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Regenerator";
        d.displayName   = "Onarıcı";
        d.role          = EnemyRole.Rear;
        d.threatScore   = 13;
        d.maxHP         = 90f;
        d.armor         = 4f;
        d.mass          = 5f;   d.enginePower   = 6f;
        d.contactDamage = 15f;
        d.bodyWidth     = 78;   d.bodyHeight    = 58;   d.sizeOrder = 4;
        d.hitboxWidth   = 68;   d.hitboxHeight  = 50;
        d.bodyColor     = new Color(0.25f, 0.70f, 0.40f);
        d.barrelColor   = new Color(0.18f, 0.52f, 0.30f);
        d.movementKind  = EnemyMovementKind.HoverFire;
        d.engageRange   = 6f;   d.fireRange     = 5.5f;
        d.orbitRadius   = 5f;   d.engageDuration = 10f;
        d.agility       = 0.6f; d.grip          = 0.78f;
        d.evasionAngle  = 6f;   d.evasionPeriod = 3.2f;
        d.escapeAngle   = 25f;
        d.weaponKind    = EnemyWeaponKind.Laser;
        d.fireDamage    = 8f;   d.fireRate      = 5f;   d.bulletSpeed = 3.5f;
        d.repairAura      = 6f;
        d.repairAuraRange = 5f;
        return d;
    }

    /// <summary>Sülük — komponentlere yapışır. Point Defence talebi yaratır.</summary>
    public static EnemyTypeData CreateLeech()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Leech";
        d.displayName   = "Sülük";
        d.role          = EnemyRole.Flank;
        d.threatScore   = 8;
        d.maxHP         = 30f;
        d.mass          = 1.2f; d.enginePower   = 6.5f;
        d.contactDamage = 0f;
        d.bodyWidth     = 40;   d.bodyHeight    = 22;   d.sizeOrder = 7;
        d.hitboxWidth   = 32;   d.hitboxHeight  = 18;
        d.bodyColor     = new Color(0.60f, 0.85f, 0.25f);
        d.barrelColor   = new Color(0.45f, 0.65f, 0.18f);
        d.movementKind  = EnemyMovementKind.Approach;
        d.engageRange   = 8f;   d.fireRange     = 2.2f;
        d.agility       = 1.35f; d.grip         = 0.94f;
        d.evasionAngle  = 14f;  d.evasionPeriod = 1.5f;
        d.escapeAngle   = 45f;
        d.weaponKind    = EnemyWeaponKind.ComponentBurst;
        d.fireDamage    = 3f;   d.fireRate      = 1.4f; d.bulletSpeed = 2.5f;
        return d;
    }

    /// <summary>Bölünen — ölünce ikiye ayrılır. Alan hasarı talebi yaratır.</summary>
    public static EnemyTypeData CreateSplitter()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Splitter";
        d.displayName   = "Bölünen";
        d.role          = EnemyRole.Center;
        d.threatScore   = 12;
        d.maxHP         = 70f;
        d.mass          = 3f;   d.enginePower   = 5f;
        d.contactDamage = 20f;
        d.bodyWidth     = 72;   d.bodyHeight    = 54;   d.sizeOrder = 5;
        d.bodyColor     = new Color(0.85f, 0.35f, 0.55f);
        d.barrelColor   = new Color(0.65f, 0.25f, 0.42f);
        d.movementKind  = EnemyMovementKind.Charge;
        d.engageRange   = 6f;   d.fireRange     = 5f;
        d.orbitRadius   = 4f;   d.engageDuration = 6f;
        d.agility       = 0.75f; d.grip         = 0.84f;
        d.evasionAngle  = 10f;  d.evasionPeriod = 2.4f;
        d.escapeAngle   = 35f;
        d.weaponKind    = EnemyWeaponKind.Kinetic;
        d.fireDamage    = 9f;   d.fireRate      = 4f;   d.bulletSpeed = 5f;
        d.splitInto     = CreateSwarm();
        d.splitHpRatio  = 0.5f;
        return d;
    }

    /// <summary>Kaleci — çok yüksek zırh. Zırh eşiğinin doruk testi; Sv9–10 şart.</summary>
    public static EnemyTypeData CreateJuggernaut()
    {
        var d = CreateInstance<EnemyTypeData>();
        d.name          = "Juggernaut";
        d.displayName   = "Kaleci";
        d.role          = EnemyRole.Center;
        d.threatScore   = 20;
        d.maxHP         = 200f;
        d.armor         = 12f;
        d.mass          = 10f;  d.enginePower   = 9f;
        d.contactDamage = 35f;
        d.bodyWidth     = 110;  d.bodyHeight    = 72;   d.sizeOrder = 2;
        d.hitboxWidth   = 98;   d.hitboxHeight  = 64;
        d.bodyColor     = new Color(0.30f, 0.32f, 0.36f);
        d.barrelColor   = new Color(0.24f, 0.26f, 0.30f);
        d.movementKind  = EnemyMovementKind.Charge;
        d.engageRange   = 5.5f; d.fireRange     = 5f;
        d.orbitRadius   = 4f;   d.engageDuration = 12f;
        d.agility       = 0.35f; d.grip         = 0.65f;
        d.evasionAngle  = 3f;   d.evasionPeriod = 4.5f;
        d.escapeAngle   = 15f;
        d.weaponKind    = EnemyWeaponKind.Cannon;
        d.fireDamage    = 22f;  d.fireRate      = 5f;   d.bulletSpeed = 2.2f;
        d.hullResistances = new[]
        {
            new DamageModifier { weaponType = WeaponType.Kinetic, multiplier = 0.60f },
            new DamageModifier { weaponType = WeaponType.Plasma,  multiplier = 1.40f },
        };
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
