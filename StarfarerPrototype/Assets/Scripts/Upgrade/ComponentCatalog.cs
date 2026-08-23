using UnityEngine;

/// <summary>
/// Oyundaki tüm komponent tanımlarının TEK sahibi. "Ne var ve kaça" sorusunun
/// cevabı buradadır.
///
/// Katman ayrımı:
///   ComponentCatalog — ne var, kaça, hangi zincirle yükselir
///   ShipLoadout      — oyuncuda ne kurulu (10 slot, kur/sat/upgrade)
///   UpgradeUI        — bunlar nasıl gösteriliyor
///
/// Daha önce tanımlar hem ShipLoadout hem UpgradeUI içinde ayrı ayrı üretiliyordu;
/// aynı kalkanın iki farklı adı, statı ve maliyet kaynağı vardı. Artık tek yer.
///
/// Değerler C# içinde sabit; oturduktan sonra ScriptableObject asset'lerine
/// taşınabilir (ComponentDefinition zaten CreateAssetMenu'ya sahip).
/// </summary>
public static class ComponentCatalog
{
    // ── Satın alınabilirler ───────────────────────────────────────────────────

    static ComponentDefinition[] _purchasable;

    /// <summary>Upgrade ekranında satılan komponentler (her zincirin ilk halkası).</summary>
    public static ComponentDefinition[] Purchasable
    {
        get
        {
            if (_purchasable != null) return _purchasable;
            _purchasable = new[]
            {
                ShieldChain[0],
                GeneratorChain[0],
                RepairChain[0],
                StorageChain[0],
                TurretKinetic,
                TurretEnergy,
                TurretMissile,
            };
            return _purchasable;
        }
    }

    // ── Başlangıç donanımı ────────────────────────────────────────────────────

    /// <summary>
    /// Oyun başında bedava kurulu gelenler. Slot indeksiyle eşleşir.
    /// Bunlar mağazadakilerin ta kendisidir — ayrı bir "başlangıç sürümü" yoktur.
    /// Satılabilirler ve normal sellValue'larıyla iade verirler; oyuncu bedava
    /// geleni satıp yerine başka bir şey kurabilsin diye bilinçli bırakıldı.
    /// </summary>
    public static (ComponentDefinition def, int slot)[] StartingLoadout => new[]
    {
        (Hangar,            6),
        (GeneratorChain[0], 0),
        (ShieldChain[0],    3),
    };

    // ── Kalkan ────────────────────────────────────────────────────────────────
    // Tek tip, üç kademe. Başlangıçtaki kalkan Mk1'dir; ikinci bir kalkan almak
    // demek yine Mk1 almak demektir. Enerji sistemi olduğu için kristalle alınır.

    static ComponentDefinition[] _shieldChain;

    public static ComponentDefinition[] ShieldChain
    {
        get
        {
            if (_shieldChain != null) return _shieldChain;
            var mk3 = Shield("Kalkan Jeneratörü Mk3", 3, cost: 70, sell: 28, maxShield: 170f, recharge: 3.0f, next: null);
            var mk2 = Shield("Kalkan Jeneratörü Mk2", 2, cost: 45, sell: 18, maxShield: 100f, recharge: 1.8f, next: mk3);
            var mk1 = Shield("Kalkan Jeneratörü Mk1", 1, cost: 25, sell: 10, maxShield:  50f, recharge: 0.8f, next: mk2);
            _shieldChain = new[] { mk1, mk2, mk3 };
            return _shieldChain;
        }
    }

    static ComponentDefinition Shield(string name, int tier, int cost, int sell,
                                      float maxShield, float recharge, ComponentDefinition next)
    {
        var d = New(name, ComponentType.Shield, tier, ResourceType.EnergyCrystal, cost, sell);
        d.maxShield    = maxShield;
        d.rechargeRate = recharge;
        d.upgradeTo    = next;
        return d;
    }

    // ── Jeneratör ─────────────────────────────────────────────────────────────

    static ComponentDefinition[] _generatorChain;

    public static ComponentDefinition[] GeneratorChain
    {
        get
        {
            if (_generatorChain != null) return _generatorChain;
            var mk3 = Generator("Enerji Jeneratörü Mk3", 3, cost: 110, sell: 45, production: 28f, next: null);
            var mk2 = Generator("Enerji Jeneratörü Mk2", 2, cost:  65, sell: 28, production: 18f, next: mk3);
            var mk1 = Generator("Enerji Jeneratörü Mk1", 1, cost:  35, sell: 15, production: 10f, next: mk2);
            _generatorChain = new[] { mk1, mk2, mk3 };
            return _generatorChain;
        }
    }

    static ComponentDefinition Generator(string name, int tier, int cost, int sell,
                                         float production, ComponentDefinition next)
    {
        var d = New(name, ComponentType.Generator, tier, ResourceType.RawMaterial, cost, sell);
        d.productionAmount = production;
        d.upgradeTo        = next;
        return d;
    }

    // ── Onarım birimi ─────────────────────────────────────────────────────────
    // Mk1 kasten yavaş: ilk seviyede tamir savaşın gidişatını belirlememeli.

    static ComponentDefinition[] _repairChain;

    public static ComponentDefinition[] RepairChain
    {
        get
        {
            if (_repairChain != null) return _repairChain;
            var mk3 = Repair("Onarım Birimi Mk3", 3, cost: 90, sell: 38, rate: 7.0f, next: null);
            var mk2 = Repair("Onarım Birimi Mk2", 2, cost: 55, sell: 22, rate: 4.0f, next: mk3);
            var mk1 = Repair("Onarım Birimi Mk1", 1, cost: 30, sell: 12, rate: 2.0f, next: mk2);
            _repairChain = new[] { mk1, mk2, mk3 };
            return _repairChain;
        }
    }

    static ComponentDefinition Repair(string name, int tier, int cost, int sell,
                                      float rate, ComponentDefinition next)
    {
        var d = New(name, ComponentType.RepairUnit, tier, ResourceType.RawMaterial, cost, sell);
        d.repairRate = rate;
        d.upgradeTo  = next;
        return d;
    }

    // ── Depo ──────────────────────────────────────────────────────────────────
    // Kaynak tavanını yükseltir. Upgrade'ler "daha kompakt depolama" — aynı
    // slotta çok daha fazla kapasite.

    static ComponentDefinition[] _storageChain;

    public static ComponentDefinition[] StorageChain
    {
        get
        {
            if (_storageChain != null) return _storageChain;
            var mk3 = Storage("Sıkıştırılmış Depo", 3, cost: 150, sell: 60, metal: 1200f, crystal: 250f, next: null);
            var mk2 = Storage("Kompakt Depo",       2, cost:  80, sell: 32, metal:  600f, crystal: 120f, next: mk3);
            var mk1 = Storage("Depo",               1, cost:  40, sell: 16, metal:  250f, crystal:  50f, next: mk2);
            _storageChain = new[] { mk1, mk2, mk3 };
            return _storageChain;
        }
    }

    static ComponentDefinition Storage(string name, int tier, int cost, int sell,
                                       float metal, float crystal, ComponentDefinition next)
    {
        var d = New(name, ComponentType.Storage, tier, ResourceType.RawMaterial, cost, sell);
        d.storageMetal   = metal;
        d.storageCrystal = crystal;
        d.upgradeTo      = next;
        return d;
    }

    // ── Hangar ────────────────────────────────────────────────────────────────

    static ComponentDefinition _hangar;

    public static ComponentDefinition Hangar
    {
        get
        {
            if (_hangar != null) return _hangar;
            _hangar = New("Hangar", ComponentType.Hangar, 1, ResourceType.RawMaterial, cost: 20, sell: 8);
            return _hangar;
        }
    }

    // ── Turretler ─────────────────────────────────────────────────────────────
    // 3 temel tip — uzmanlaşma upgrade ekranından yapılır.
    // Hedef: TEMEL_DPS = 6 (damage/fireRate)

    static ComponentDefinition _tKinetic, _tEnergy, _tMissile;

    public static ComponentDefinition TurretKinetic => _tKinetic ??= TurretBase(
        "Raylı Turret", TurretBaseType.Kinetic, cost: 22,
        fireRate: 2f, damage: 12f, speed: 9f, life: 3f, energy: 0.5f);

    public static ComponentDefinition TurretEnergy => _tEnergy ??= TurretBase(
        "Enerji Turret", TurretBaseType.Energy, cost: 22,
        fireRate: 3f, damage: 18f, speed: 14f, life: 4f, energy: 3f);

    public static ComponentDefinition TurretMissile => _tMissile ??= TurretBase(
        "Füze Turret", TurretBaseType.Missile, cost: 28,
        fireRate: 10f, damage: 60f, speed: 7f, life: 5f, energy: 0.5f);

    static ComponentDefinition TurretBase(string name, TurretBaseType bt, int cost,
        float fireRate, float damage, float speed, float life, float energy,
        int mag = 0, float reload = 0f)
    {
        var d = New(name, ComponentType.Turret, 1, ResourceType.RawMaterial, cost, cost / 2);
        d.turretBaseType       = bt;
        d.turretSpecType       = TurretSpecType.None;
        d.turretFireRate       = fireRate;
        d.turretDamage         = damage;
        d.turretBulletSpeed    = speed;
        d.turretBulletLifeTime = life;
        d.turretEnergyPerShot  = energy;
        d.turretMagazineSize   = mag;
        d.turretReloadTime     = reload;
        return d;
    }

    /// <summary>
    /// Bir temel turretin verilen uzmanlaşmadaki hâli.
    /// EFEKTİF_DPS = TEMEL_DPS × hedefleme_çarpanı ≈ 6 (hepsi)
    ///   Lazer:   TEMEL=2.0, çarpan=3.0 → 6.0  (damage × burnDuration / fireRate = 12×0.5/3)
    ///   Gatling: sustained = 10×8/(10×1+3) = 80/13 ≈ 6.15
    ///   Roket:   TEMEL=4.0, çarpan=1.5 → 6.0  (60/15)
    ///   PD:      TEMEL=9.0 — menzil kısıtlı (5.5u), daha yüksek ham DPS hak ediyor
    /// </summary>
    public static ComponentDefinition TurretSpec(ComponentDefinition baseDef, TurretSpecType spec)
    {
        return spec switch
        {
            TurretSpecType.Gatling      => Spec(baseDef, spec, specCost: 20,
                fireRate: 1f,  damage: 8f,  speed: 9f,   life: 3f,   energy: 0.5f, mag: 10, reload: 3f),
            TurretSpecType.PointDefence => Spec(baseDef, spec, specCost: 25,
                fireRate: 1f,  damage: 9f,  speed: 8f,   life: 0.8f, energy: 1f),
            TurretSpecType.Laser        => Spec(baseDef, spec, specCost: 30,
                fireRate: 3f,  damage: 12f, speed: 14f,  life: 4f,   energy: 3f, burnDuration: 0.5f),
            TurretSpecType.Plasma       => Spec(baseDef, spec, specCost: 40,
                fireRate: 6f,  damage: 36f, speed: 5f,   life: 4f,   energy: 4f),
            TurretSpecType.HomingRocket => Spec(baseDef, spec, specCost: 35,
                fireRate: 15f, damage: 60f, speed: 4.5f, life: 6f,   energy: 0.5f),
            _ => baseDef,
        };
    }

    static ComponentDefinition Spec(ComponentDefinition baseDef, TurretSpecType spec, int specCost,
        float fireRate, float damage, float speed, float life, float energy,
        int mag = 0, float reload = 0f, float burnDuration = 0f)
    {
        var d = New($"{TurretSpecHelper.GetBaseTypeName(baseDef.turretBaseType)} — {TurretSpecHelper.GetSpecName(spec)}",
                    ComponentType.Turret, 1, baseDef.costResource, baseDef.cost, baseDef.sellValue);
        d.turretBaseType       = baseDef.turretBaseType;
        d.turretSpecType       = spec;
        d.specCost             = specCost;
        d.specCostResource     = baseDef.costResource;
        d.turretFireRate       = fireRate;
        d.turretDamage         = damage;
        d.turretBulletSpeed    = speed;
        d.turretBulletLifeTime = life;
        d.turretEnergyPerShot  = energy;
        d.turretMagazineSize   = mag;
        d.turretReloadTime     = reload;
        d.turretBurnDuration   = burnDuration;
        return d;
    }

    // ── Silah zincirleri ──────────────────────────────────────────────────────
    // Ana silah slotu. Her tip bağımsız Mk1→Mk2→Mk3 zincirine sahip;
    // oyun başında Lazer Mk1 ücretsiz kurulu gelir.

    static ComponentDefinition[] _laser, _kinetic, _plasma;

    public static ComponentDefinition[] WeaponChain(WeaponType type)
    {
        InitWeapons();
        return type switch
        {
            WeaponType.Laser   => _laser,
            WeaponType.Kinetic => _kinetic,
            WeaponType.Plasma  => _plasma,
            _                  => null,
        };
    }

    static void InitWeapons()
    {
        if (_laser != null) return;

        // Lazer — sürekli ışın: damage=DPS, fireRate kullanılmaz, energy=enerji/saniye
        var lm3 = Weapon("Lazer Topu Mk3", WeaponType.Laser, 3, ResourceType.EnergyCrystal, 60, 25, null, 60f, 0f, 30f);
        var lm2 = Weapon("Lazer Topu Mk2", WeaponType.Laser, 2, ResourceType.EnergyCrystal, 35, 15, lm3,  40f, 0f, 20f);
        var lm1 = Weapon("Lazer Topu Mk1", WeaponType.Laser, 1, ResourceType.EnergyCrystal,  0,  0, lm2,  25f, 0f, 15f);
        _laser = new[] { lm1, lm2, lm3 };

        var km3 = Weapon("Raylı Top Mk3", WeaponType.Kinetic, 3, ResourceType.RawMaterial, 50, 20, null, 18f, 0.40f);
        var km2 = Weapon("Raylı Top Mk2", WeaponType.Kinetic, 2, ResourceType.RawMaterial, 28, 10, km3,  14f, 0.65f);
        var km1 = Weapon("Raylı Top Mk1", WeaponType.Kinetic, 1, ResourceType.RawMaterial, 12,  5, km2,  10f, 1.00f);
        _kinetic = new[] { km1, km2, km3 };

        // Plazma — şarj + bırak mekaniği: fireRate = atış sonrası bekleme, charge = tam şarj süresi
        var pm3 = Weapon("Plazma Topu Mk3", WeaponType.Plasma, 3, ResourceType.RawMaterial, 65, 25, null, 45f, 1.0f, 0f, 1.4f, 0);
        var pm2 = Weapon("Plazma Topu Mk2", WeaponType.Plasma, 2, ResourceType.RawMaterial, 38, 15, pm3,  36f, 1.2f, 0f, 1.7f, 0);
        var pm1 = Weapon("Plazma Topu Mk1", WeaponType.Plasma, 1, ResourceType.RawMaterial, 20,  8, pm2,  28f, 1.5f, 0f, 2.0f, 0);
        _plasma = new[] { pm1, pm2, pm3 };
    }

    static ComponentDefinition Weapon(string name, WeaponType wt, int tier,
        ResourceType res, int cost, int sell, ComponentDefinition next,
        float dmg = 10f, float rate = 0.15f, float energy = 0f,
        float charge = 0.8f, int burst = 3)
    {
        var d = New(name, ComponentType.Weapon, tier, res, cost, sell);
        d.weaponType              = wt;
        d.weaponDamage            = dmg;
        d.weaponFireRate          = rate;
        d.weaponEnergyCostPerShot = energy;
        d.weaponChargeTime        = charge;
        d.weaponBurstCount        = burst;
        d.upgradeTo               = next;
        return d;
    }

    // ── Ortak kurucu ──────────────────────────────────────────────────────────

    static ComponentDefinition New(string name, ComponentType type, int tier,
                                   ResourceType res, int cost, int sell)
    {
        var d = ScriptableObject.CreateInstance<ComponentDefinition>();
        d.componentName = name;
        d.componentType = type;
        d.tier          = tier;
        d.costResource  = res;
        d.cost          = cost;
        d.sellValue     = sell;
        return d;
    }
}
