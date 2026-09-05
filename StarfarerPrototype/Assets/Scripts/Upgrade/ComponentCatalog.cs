using UnityEngine;

/// <summary>
/// Oyundaki tüm komponent tanımlarının TEK sahibi. "Ne var ve kaça" sorusunun
/// cevabı buradadır.
///
/// Katman ayrımı:
///   ComponentCatalog — ne var, kaça
///   ShipLoadout      — oyuncuda ne kurulu (10 slot, kur/sat)
///   UpgradeUI        — bunlar nasıl gösteriliyor
///
/// TIER ZİNCİRLERİ KALDIRILDI. Her komponentin tek sürümü vardır; ilerleme
/// yalnızca stat seviyelerinden gelir (0–10). Değerler eski zincirin ORTA
/// halkasından (Mk2) alınmıştır — böylece tavan neredeyse aynı yerde kalır:
///
///   eski: Mk3 statı × 1.25^8  =  Mk3 × 5.96
///   yeni: Mk2 statı × 1.25^10 =  Mk2 × 9.31
///
/// Kalkanda 1013 → 931, jeneratörde 167 → 168. Tier'ların taşıdığı güç stat
/// eğrisine devredildi, kaybolmadı.
///
/// Silah TİPLERİ ve turret uzmanlaşmaları tier değildir — duruyorlar, çünkü
/// güç değil KARAKTER seçtiriyorlar.
/// </summary>
public static class ComponentCatalog
{
    // ── Satın alınabilirler ───────────────────────────────────────────────────

    static ComponentDefinition[] _purchasable;

    /// <summary>Upgrade ekranında satılan komponentler.</summary>
    public static ComponentDefinition[] Purchasable
    {
        get
        {
            if (_purchasable != null) return _purchasable;
            _purchasable = new[]
            {
                Shield,
                Generator,
                Repair,
                Storage,
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
        (Hangar,    6),
        (Generator, 0),
        (Shield,    3),
    };

    /// <summary>
    /// Stat maliyet tabanı komponentin kendi fiyatından türer. Tier'lar varken
    /// taban zincirin SON halkasına sabitlenmişti (yoksa "Mk1'de statları maxla,
    /// sonra tier atla" 4× ucuza geliyordu). Zincir kalkınca o istismar da
    /// kalktı; taban artık tek bir orandır.
    /// </summary>
    const float StatBaseFactor = 1.5f;

    static int StatBase(int cost) => Mathf.RoundToInt(cost * StatBaseFactor);

    // ── Kalkan ────────────────────────────────────────────────────────────────
    // Enerji sistemi olduğu için kristalle alınır.

    static ComponentDefinition _shield;

    public static ComponentDefinition Shield
    {
        get
        {
            if (_shield != null) return _shield;
            _shield = New("Kalkan Jeneratörü", ComponentType.Shield,
                          ResourceType.EnergyCrystal, cost: 45, sell: 18);
            _shield.maxShield      = 100f;
            _shield.rechargeRate   = 1.8f;
            _shield.baseEnergyCost = 3.5f;
            return _shield;
        }
    }

    // ── Jeneratör ─────────────────────────────────────────────────────────────

    static ComponentDefinition _generator;

    public static ComponentDefinition Generator
    {
        get
        {
            if (_generator != null) return _generator;
            _generator = New("Enerji Jeneratörü", ComponentType.Generator,
                             ResourceType.RawMaterial, cost: 65, sell: 28);
            _generator.productionAmount = 18f;
            _generator.baseEnergyCost   = 0f;   // üreticidir, tüketmez
            return _generator;
        }
    }

    // ── Onarım birimi ─────────────────────────────────────────────────────────

    static ComponentDefinition _repair;

    public static ComponentDefinition Repair
    {
        get
        {
            if (_repair != null) return _repair;
            _repair = New("Onarım Birimi", ComponentType.RepairUnit,
                          ResourceType.RawMaterial, cost: 55, sell: 22);
            _repair.repairRate     = 4f;
            _repair.baseEnergyCost = 2.5f;
            return _repair;
        }
    }

    // ── Depo ──────────────────────────────────────────────────────────────────
    // Kaynak tavanını yükseltir. "Kapasite" statı ZORUNLU bir eksendir: geç
    // seviyelerde tek bir stat yükseltmesi binlerce kaynak tutuyor ve taban
    // tavan (150 metal / 100 kristal) o parayı tutamaz. Tier'lar kalkınca
    // kapasite ilerlemesinin tek yolu bu stat oldu.
    //
    // Fiyat kasten DÜŞÜK (50 metal): depo bir güç yükseltmesi değil, başka
    // yükseltmelerin ön koşulu. Pahalı olsaydı oyuncu ilerlemesini açan şeyi
    // satın alabilmek için ilerlemesi gereken bir kısır döngüye girerdi.
    //
    // Taban kapasiteler zincirin ORTA halkasından DEĞİL, üstünden alındı:
    // tavan en pahalı yükseltmeyi tutabilmek ZORUNDA. Sv10 kalkan yükseltmesi
    // 6.164 kristal; 200 tabanlı bir depo maxlansa bile 1.912 tutuyordu, yani
    // sistem kilitleniyordu — para birikiyor ama tavana çarpıp yanıyordu.
    // 350 tabanla maxlanmış tek depo 3.259, iki depo 6.568 tutar.
    //
    // Depo statı kasten UCUZ: altyapıdır, güç değil. Onu yükseltmek başka bir
    // şeyi satın alabilmenin ön koşulu, kendi başına bir ödül değil.

    static ComponentDefinition _storage;

    public static ComponentDefinition Storage
    {
        get
        {
            if (_storage != null) return _storage;
            _storage = New("Depo", ComponentType.Storage,
                           ResourceType.RawMaterial, cost: 50, sell: 20);
            _storage.storageMetal   = 900f;
            _storage.storageCrystal = 350f;
            _storage.baseEnergyCost = 0.8f;
            return _storage;
        }
    }

    // ── Hangar ────────────────────────────────────────────────────────────────

    static ComponentDefinition _hangar;

    public static ComponentDefinition Hangar
    {
        get
        {
            if (_hangar != null) return _hangar;
            _hangar = New("Hangar", ComponentType.Hangar, ResourceType.RawMaterial,
                          cost: 20, sell: 8);
            _hangar.baseEnergyCost = 1.5f;
            return _hangar;
        }
    }

    // ── Turretler ─────────────────────────────────────────────────────────────
    // 3 temel tip — uzmanlaşma upgrade ekranından yapılır.
    // Hedef: TEMEL_DPS = 6 (damage/fireRate)

    // MERMİ HIZI KURALI: kinetik ve enerji turretlerin mermisi ana silahla AYNI
    // hızda uçar (6 birim/sn, bkz. WeaponController.UpdateKinetic). Turret
    // mermisi ana silahtan hızlı olunca oyuncunun kendi atışı sahnedeki en yavaş
    // mermi oluyor ve nişan alırken öğrendiği önde tutma hissi turretlerinkiyle
    // çelişiyordu. Füzeler kasten DAHA YAVAŞ: ağır ve gecikmeli silahlar.
    //
    // ÖMÜRLER HIZLA BİRLİKTE AYARLANIR. Menzil bulletLifeTime × bulletSpeed'tir
    // (EffectiveRange); yalnızca hızı düşürmek turretlerin menzilini sessizce
    // kırpardı — enerji turretinde 56'dan 24'e. Buradaki her ömür, menzil ESKİSİ
    // GİBİ KALSIN diye yeniden hesaplandı. Değişen tek şey uçuş SÜRESİ.
    // İstisnalar: Point Defence (bombayı kalkana varmadan karşılamak zorunda,
    // 20'de kalır), Lazer (ışın, mermi hızı kavramı yok), Plazma (zaten 5,
    // yani ana silahtan yavaş — yükseltmek "düşsün" isteğine ters olurdu).

    static ComponentDefinition _tKinetic, _tEnergy, _tMissile;

    public static ComponentDefinition TurretKinetic => _tKinetic ??= TurretBase(
        "Raylı Turret", TurretBaseType.Kinetic, cost: 22,
        fireRate: 2f, damage: 12f, speed: 6f, life: 4.5f, energy: 0.5f);   // menzil 27 sabit

    public static ComponentDefinition TurretEnergy => _tEnergy ??= TurretBase(
        "Enerji Turret", TurretBaseType.Energy, cost: 22,
        fireRate: 3f, damage: 18f, speed: 6f, life: 9.33f, energy: 3f);    // menzil 56 sabit

    public static ComponentDefinition TurretMissile => _tMissile ??= TurretBase(
        "Füze Turret", TurretBaseType.Missile, cost: 28,
        fireRate: 10f, damage: 60f, speed: 5f, life: 7f, energy: 0.5f);    // menzil 35 sabit

    static ComponentDefinition TurretBase(string name, TurretBaseType bt, int cost,
        float fireRate, float damage, float speed, float life, float energy,
        int mag = 0, float reload = 0f)
    {
        var d = New(name, ComponentType.Turret, ResourceType.RawMaterial, cost, cost / 2);
        d.baseEnergyCost       = EnergyTurret;
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
    ///   Lazer:   TEMEL=4.33, çarpan=1.35 → 5.8 (damage × burnDuration / fireRate = 26×0.5/3)
    ///   Gatling: sustained = 10×8/(10×1+3) = 80/13 ≈ 6.15
    ///   Roket:   TEMEL=4.0, çarpan=1.5 → 6.0  (60/15)
    ///   PD:      TEMEL=28.6 — YALNIZCA küçük hedef ve en kısa menzil (10.4u);
    ///            bu kadar dar bir rol yüksek ham DPS hak ediyor
    /// </summary>
    public static ComponentDefinition TurretSpec(ComponentDefinition baseDef, TurretSpecType spec)
    {
        return spec switch
        {
            TurretSpecType.Gatling      => Spec(baseDef, spec, specCost: 20,
                fireRate: 1f,  damage: 8f,  speed: 6f,   life: 4.5f, energy: 0.5f, mag: 10, reload: 3f),   // menzil 27 sabit
            // Menzil dar (10.4u) ve hedef listesi dar; karşılığı yüksek ham DPS:
            // 8 hasar / 0.28 sn = 28.6 DPS, diğer turretlerin ~5 katı.
            //
            // MERMİ HIZI 20: bomba 2.5 hızla geliyor ve kalkana varmadan
            // vurulmalı. 8 hızla menzilin ucundaki bir bombaya mermi 0.5 sn'de
            // gidiyordu; bomba o sürede 1.25 birim yol alıyor, yani kalkana
            // çarpmadan durdurmak kıl payına kalıyordu. 20'de uçuş 0.2 sn.
            //
            // Ömür 0.6 sn = 12 birim yol: hedefleme menzilinin (10.4) bir tık
            // ötesi, ıskalayan mermi hemen buharlaşmasın. Ömür menzille BİRLİKTE
            // büyümek ZORUNDA — mermi menzilin ucuna varamazsa menzili artırmak
            // sessizce işlevsiz kalır.
            TurretSpecType.PointDefence => Spec(baseDef, spec, specCost: 25,
                fireRate: 0.28f, damage: 8f, speed: 20f, life: 0.6f, energy: 0.5f),
            // Lazer 12 hasarla 2.0 EFEKTİF DPS veriyordu — diğerlerinin üçte
            // biri. Gerekçe "ışın hiç ıskalamaz, çarpanı 3.0" idi ama bu çarpan
            // hiçbir zaman ölçülmemişti; mermili turretler de çoğu hedefi
            // vuruyor. Isınma hiç ıskalamamanın gerçek değeri ~1.35; hedefi
            // 4.33 efektif DPS.
            TurretSpecType.Laser        => Spec(baseDef, spec, specCost: 30,
                fireRate: 3f,  damage: 26f, speed: 14f,  life: 4f,   energy: 3f, burnDuration: 0.5f),
            TurretSpecType.Plasma       => Spec(baseDef, spec, specCost: 40,
                fireRate: 6f,  damage: 36f, speed: 5f,   life: 4f,   energy: 4f),
            TurretSpecType.HomingRocket => Spec(baseDef, spec, specCost: 35,
                fireRate: 15f, damage: 60f, speed: 3.5f, life: 7.71f, energy: 0.5f),   // menzil 27 sabit
            _ => baseDef,
        };
    }

    static ComponentDefinition Spec(ComponentDefinition baseDef, TurretSpecType spec, int specCost,
        float fireRate, float damage, float speed, float life, float energy,
        int mag = 0, float reload = 0f, float burnDuration = 0f)
    {
        var d = New($"{TurretSpecHelper.GetBaseTypeName(baseDef.turretBaseType)} — {TurretSpecHelper.GetSpecName(spec)}",
                    ComponentType.Turret, baseDef.costResource, baseDef.cost, baseDef.sellValue);
        d.statCostBase         = baseDef.statCostBase;
        d.baseEnergyCost       = baseDef.baseEnergyCost;
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

    /// <summary>Bir turret taban tipinin uzmanlaşmasız tanımı.</summary>
    public static ComponentDefinition TurretBaseOf(TurretBaseType bt) => bt switch
    {
        TurretBaseType.Energy  => TurretEnergy,
        TurretBaseType.Missile => TurretMissile,
        _                      => TurretKinetic,
    };

    // ── Silahlar ──────────────────────────────────────────────────────────────
    // Ana silah slotu. Her TİP ayrı bir silahtır; tier zinciri yoktur, güç stat
    // seviyelerinden gelir. Kinetik oyun başında ücretsiz kurulu gelir; diğer
    // ikisi karakter satın alır — daha güçlü değil, FARKLI.

    static ComponentDefinition _laser, _kinetic, _plasma;

    public static ComponentDefinition Weapon(WeaponType type)
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

        // Kinetik başlangıç silahı: bedava geldiği için kasten en zayıf taban.
        // dmg=10 @ 1.0s → 10 DPS. Hem hasar hem ateş hızı statı DPS'e çarpımsal
        // girdiği için stat yatırımını en çok ödüllendiren silah da budur.
        _kinetic = MakeWeapon("Raylı Top", WeaponType.Kinetic, ResourceType.RawMaterial,
                              cost: 12, sell: 5, dmg: 10f, rate: 1.00f);

        // Lazer — sürekli ışın: damage = DPS, fireRate KULLANILMAZ,
        // energy = enerji/saniye.
        _laser = MakeWeapon("Lazer Topu", WeaponType.Laser, ResourceType.EnergyCrystal,
                            cost: 35, sell: 15, dmg: 46f, rate: 0f, energy: 20f);

        // Plazma — şarj + bırak: fireRate = atış sonrası bekleme,
        // charge = tam şarj süresi.
        _plasma = MakeWeapon("Plazma Topu", WeaponType.Plasma, ResourceType.RawMaterial,
                             cost: 38, sell: 15, dmg: 36f, rate: 1.2f, charge: 1.7f, burst: 0);
    }

    static ComponentDefinition MakeWeapon(string name, WeaponType wt, ResourceType res,
        int cost, int sell, float dmg, float rate,
        float energy = 0f, float charge = 0.8f, int burst = 3)
    {
        var d = New(name, ComponentType.Weapon, res, cost, sell);
        // Silah statı gemi DPS'inin ana ekseni; tabanı komponentin kendi
        // fiyatından DEĞİL sabit bir değerden alır — yoksa bedava kinetik
        // sonsuza dek en ucuz yükseltme olurdu.
        d.statCostBase            = WeaponStatBase(wt);
        d.baseEnergyCost          = 0f;   // ana silah atış başına enerji yer, pasif değil
        d.weaponType              = wt;
        d.weaponDamage            = dmg;
        d.weaponFireRate          = rate;
        d.weaponEnergyCostPerShot = energy;
        d.weaponChargeTime        = charge;
        d.weaponBurstCount        = burst;
        return d;
    }

    static int WeaponStatBase(WeaponType wt) => wt switch
    {
        WeaponType.Laser   => 55,
        WeaponType.Kinetic => 45,
        WeaponType.Plasma  => 60,
        _                  => 45,
    };

    const float EnergyTurret = 1f;

    // ── Kayıttan çözümleme ────────────────────────────────────────────────────

    /// <summary>
    /// Kaydedilmiş bir komponenti tanımına geri çevirir. Tanımlar runtime'da
    /// üretildiği için referansları kaydetmek mümkün değil; tip + (turret ise)
    /// uzmanlaşma + (silah ise) silah tipi üzerinden yeniden bulunur.
    /// </summary>
    public static ComponentDefinition Resolve(ComponentType type,
                                              TurretBaseType turretBase, TurretSpecType spec,
                                              WeaponType weapon)
    {
        switch (type)
        {
            case ComponentType.Shield:     return Shield;
            case ComponentType.Generator:  return Generator;
            case ComponentType.RepairUnit: return Repair;
            case ComponentType.Storage:    return Storage;
            case ComponentType.Hangar:     return Hangar;
            case ComponentType.Weapon:     return Weapon(weapon);

            case ComponentType.Turret:
            {
                var baseDef = TurretBaseOf(turretBase);
                return spec == TurretSpecType.None ? baseDef : TurretSpec(baseDef, spec);
            }
        }
        return null;
    }

    // ── Ortak kurucu ──────────────────────────────────────────────────────────

    static ComponentDefinition New(string name, ComponentType type,
                                   ResourceType res, int cost, int sell)
    {
        var d = ScriptableObject.CreateInstance<ComponentDefinition>();
        d.componentName = name;
        d.componentType = type;
        d.costResource  = res;
        d.cost          = cost;
        d.sellValue     = sell;
        d.statCostBase  = StatBase(cost);
        return d;
    }
}
