using System.Collections;
using UnityEngine;

/// <summary>
/// Gemi slot'una kurulunca otomatik ateş açan turret.
/// baseType: Kinetic / Energy / Missile — satın alırken belirlenir, değişmez.
/// specType: uzmanlaşma — Upgrade ekranından değiştirilebilir.
///
/// Hedef seçimi TurretTargeting'e devredilmiştir: puanlama formülü ve kilit
/// histerezisi orada açıklanır. Turret yalnızca kendi menzilini, DPS'ini ve
/// silah tipini bildirir.
/// </summary>
public class TurretController : ShipComponentBase
{
    public TurretBaseType baseType     = TurretBaseType.Kinetic;
    public TurretSpecType specType     = TurretSpecType.None;
    public float          fireRate     = 1f;
    public float          damage       = 5f;
    public float          bulletSpeed  = 4f;
    public float          bulletLifeTime = 3f;
    public float          energyPerShot  = 1f;
    public int            magazineSize   = 10;
    public float          reloadTime     = 3f;

    /// <summary>
    /// Merminin patlama yarıçapı (dünya birimi). 0 = tek hedefli.
    /// Flak ve roket uzmanlaşmalarında dolu (bkz. ComponentCatalog.TurretSpec).
    /// </summary>
    public float          blastRadius    = 0f;

    /// <summary>
    /// Şarapnel kıymığı sayısı. 0 = şarapnel yok. Doluysa <see cref="blastRadius"/>
    /// kıymığın menzili, <see cref="damage"/> kıymık başına hasardır.
    /// </summary>
    public int            shrapnelCount  = 0;
    [Tooltip("Saniyede derece — turretin maksimum dönüş hızı.")]
    public float          turnRate       = 180f;

    /// <summary>
    /// Oyuncunun bu turret için SEÇTİĞİ nişan modu. Geçerli mod
    /// <see cref="EffectiveMode"/>'dur: özel nişan kapalıyken her turret
    /// otomatiktir, Point Defence her zaman otomatiktir, füzeler elle olamaz.
    /// Kayda girer (SlotSave.aimMode).
    /// </summary>
    public AimMode        aimMode        = AimMode.Auto;

    /// <summary>Şu an geçerli nişan modu.</summary>
    public AimMode EffectiveMode => FireControl.EffectiveTurretMode(specType, baseType, aimMode);

    /// <summary>
    /// Elle modda namlunun imlece "dönük" sayıldığı açı. Otomatikteki 1° elle
    /// tutturulamaz: imleç hareket ederken namlu onu hep bir adım geriden izler
    /// ve turret hiç ateş etmezdi.
    /// </summary>
    const float ManualAimTolerance = 4f;

    float   _fireTimer;
    int     _currentMag;
    bool    _reloading;
    float   _reloadTimer;   // eskiden coroutine'di — coroutine'in ilerlemesi kaydedilemez
    Transform _barrel;

    // Hedef kilidi — her karede değil, aralıklarla yeniden değerlendirilir
    ITurretTarget _lockedTarget;
    float         _retargetTimer;

    /// <summary>
    /// Point Defence menzili. Kalkan küresi 2.5 birim; turretler gövdede
    /// ±1.3 birim yayılı duruyor, yani en uzak slottan bile kalkanın belirgin
    /// şekilde dışına ulaşır ve bombayı kabuğa değmeden karşılar.
    ///
    /// Menzil yine de PD'nin TEK kısıtıdır — yüksek DPS'inin ve dar hedef
    /// listesinin karşılığı odur. Diğer turretlerin menzili 27–56 birim, yani
    /// 10.4'te bile PD açık ara en kısa menzilli turret.
    /// </summary>
    const float PDRange = 10.4f;

    /// <summary>
    /// Işın turretinin hızlı hedeflere verdiği öncelik. Işın ıskalamaz;
    /// mermili turretlerin zorlandığı kaçamak hedefler onun işidir.
    /// </summary>
    const float LaserSpeedBias = 1.5f;

    /// <summary>
    /// Lazer turretinin döngüsünün ışın yanan kısmı. Döngü ateş aralığının
    /// kendisidir (fireRate); YARISI yanar, yarısı soğur.
    ///
    /// Eskiden sabit 0.5 sn yanıp 3 sn'lik döngünün geri kalanında susuyordu:
    /// ışın bir çakıp sönüyor, turret zamanının %83'ünde hiçbir şey yapmıyor
    /// gibi görünüyordu. Oran sabit olduğu için yanma süresi ATEŞ HIZI
    /// statıyla birlikte kısalır — sabit bir süre olsaydı hızlanan döngü
    /// önceki ışın bitmeden yenisini açar ve iki ışın üst üste binerdi.
    /// </summary>
    public const float LaserDutyCycle = 0.5f;

    /// <summary>Stat yükseltmesi uygulanmış döngü süresi (sn).</summary>
    public float EffectiveFireInterval => fireRate / GetMultiplier("fireRate");

    /// <summary>Lazer ışınının bir döngüdeki yanma süresi (sn).</summary>
    public float LaserBurnTime => EffectiveFireInterval * LaserDutyCycle;

    /// <summary>
    /// Işının yanarken verdiği saniyelik hasar. Görev oranı sabit olduğu için
    /// ateş hızı statı döngüyü kısaltmakla kalsaydı ortalama DPS hiç
    /// değişmezdi — oyuncu hiçbir şey yapmayan bir yükseltmeye ödeme yapardı
    /// (ana lazerin "Ateş Hızı" hatasının aynısı). Bu yüzden ateş hızı statı
    /// ışının yoğunluğunu da çarpar; ortalama DPS diğer turretlerdeki gibi
    /// hasar × ateş hızı ile büyür.
    /// </summary>
    public float LaserBeamDps => damage * GetMultiplier("damage") * GetMultiplier("fireRate");

    /// <summary>Merminin ömrü boyunca gidebildiği mesafe — bunun ötesi vurulamaz.</summary>
    public float EffectiveRange => specType == TurretSpecType.PointDefence
        ? PDRange
        : bulletLifeTime * bulletSpeed;

    /// <summary>Saniyedeki ham hasar. Lazer sürekli ışın olduğu için damage zaten DPS'tir.</summary>
    public float DamagePerSecond => specType == TurretSpecType.Laser
        ? damage
        : (fireRate > 0.001f ? damage / fireRate : damage);

    /// <summary>
    /// Stat upgrade uygulanmış ATIŞ BAŞINA hasar. Zırh eşiği atış başına işlediği
    /// için hedefleme bunu bilmek zorundadır — DPS yetmez: aynı DPS'i tek güçlü
    /// atışla üreten turret zırhı deler, çok sayıda zayıf atışla üreten delemez.
    /// </summary>
    public float EffectiveShotDamage => damage * GetMultiplier("damage");

    /// <summary>Mermi tipinin hasar sınıfı — hedef dirençleri buna göre işler.</summary>
    public WeaponType ProjectileWeaponType
    {
        get
        {
            if (baseType == TurretBaseType.Energy) return WeaponType.Laser;
            return WeaponType.Kinetic;
        }
    }

    // -------------------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();
        componentName = BuildLabel();
        BuildVisual();
        _currentMag = magazineSize;
        _fireTimer  = Random.Range(0f, fireRate);
        ApplySpecTurnRate();
    }

    Vector3 _aimPos;

    void Update()
    {
        // Şarjör dolumu turret çalışmıyorken de ilerler — eskiden bir
        // coroutine'di ve WaitForSeconds turretin durumuna bakmıyordu.
        if (_reloading && !UpgradeUI.IsPaused)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                _currentMag = magazineSize;
                _reloading  = false;
            }
        }

        if (!IsOperational)     return;
        if (UpgradeUI.IsPaused) return;

        var  mode       = EffectiveMode;
        bool manualFire = false;
        Transform target = null;

        if (mode == AimMode.Manual)
        {
            // Elle: namlu imlece döner, oyuncu tetiğe bastıkça ateş eder. Hedef
            // yok — mermi nereye nişan alındıysa oraya gider (flak orada patlar).
            if (!PointerInput.Locked && FireControl.TryPointerWorld(out var pointer))
            {
                _aimPos = pointer;
                AimAt(_aimPos);
                manualFire = PointerInput.FireHeld;
            }
            if (manualFire) Stats.engagedTime += Time.deltaTime;
        }
        else
        {
            // Otomatik: hedefi turret seçer. Seçili hedef: oyuncunun işaretlediği
            // hedef — menzil dışındaysa ya da hiç yoksa turret BOŞTA bekler,
            // kendi hedefini seçmez (seçseydi otomatikten farkı kalmazdı).
            var aimTarget = mode == AimMode.Auto ? AcquireTargetRef() : MarkedTargetInRange();
            target = aimTarget?.TargetTransform;

            if (target != null)
            {
                // Lazer beam anlık (raycast) — öngörü gereksiz, mevcut pozisyonu
                // hedefle. Diğerleri bilgisayarın öngörüsü kadar buluşma noktasına
                // kayar (FireControl.Lead) — bilgisayarsız turret doğrudan hedefe
                // ateş eder.
                bool isInstant = specType == TurretSpecType.Laser;
                _aimPos = isInstant
                    ? target.position
                    : FireControl.AimPoint(transform.position, target.position,
                                           aimTarget.TargetVelocity, bulletSpeed);
                AimAt(_aimPos);

                // Efektif DPS'in paydası: menzilde bir hedefe kilitli geçen süre
                // (bkz. ComponentStats.engagedTime).
                Stats.engagedTime += Time.deltaTime;
            }
        }

        _fireTimer -= Time.deltaTime;
        float effectiveFireRate = EffectiveFireInterval;
        bool  ready = manualFire ? IsAimed(_aimPos, ManualAimTolerance)
                                 : target != null && IsAimed(_aimPos);
        if (_fireTimer <= 0f && !_reloading && ready)
        {
            bool hasEnergy = EnergyBus.Instance == null ||
                             EnergyBus.Instance.RequestEnergy(energyPerShot);
            if (hasEnergy)
                Fire(target, effectiveFireRate);
        }
    }

    bool IsAimed(Vector3 worldPos, float tolerance = 1f)
    {
        var   dir         = worldPos - transform.position;
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        return Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, targetAngle)) < tolerance;
    }

    /// <summary>
    /// Oyuncunun işaretlediği hedef, bu turret onu vurabiliyorsa. Menzil dışı
    /// ya da geçersiz hedef null döner — turret boşta bekler.
    /// </summary>
    ITurretTarget MarkedTargetInRange()
    {
        var t = TargetMarker.Current;
        if (t == null || !t.IsValidTarget) return null;
        if (Vector2.Distance(transform.position, t.TargetTransform.position) > EffectiveRange) return null;
        return t;
    }

    // -------------------------------------------------------------------------
    // Hedefleme
    // -------------------------------------------------------------------------

    /// <summary>
    /// Kilitli hedefi döndürür; kilit düştüyse veya değerlendirme zamanı geldiyse
    /// TurretTargeting'e yeniden seçtirir. Seçim mantığı ve puanlama orada.
    /// </summary>
    ITurretTarget AcquireTargetRef()
    {
        // Kilit hâlâ geçerli ve menzilde mi?
        bool lockValid = _lockedTarget != null
                      && _lockedTarget.IsValidTarget
                      && Vector2.Distance(transform.position,
                             _lockedTarget.TargetTransform.position) <= EffectiveRange;

        if (!lockValid) _lockedTarget = null;

        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f || _lockedTarget == null)
        {
            _retargetTimer = TurretTargeting.ReevaluateInterval;

            Vector3 shipPos = PlayerShipPosition();
            _lockedTarget = TurretTargeting.Select(
                transform.position, shipPos,
                EffectiveRange, DamagePerSecond, bulletSpeed, ProjectileWeaponType,
                specType == TurretSpecType.PointDefence,
                _lockedTarget,
                // Zırh atış BAŞINA işler; turret kendi atış hasarını bildirmezse
                // zırhlı hedefleri "kolay" sanıp onlara kilitlenir ve mermi harcar.
                EffectiveShotDamage,
                // Yalnızca LAZER uzmanlaşması anlıktır. Enerji turretinin
                // uzmanlaşmamış hâli de WeaponType.Laser hasarı verir ama
                // MERMİ atar — ona hız tercihi tanımak yanlış olurdu.
                specType == TurretSpecType.Laser ? LaserSpeedBias : 0f);
        }

        return _lockedTarget;
    }

    static PlayerShip _cachedShip;

    static Vector3 PlayerShipPosition()
    {
        if (_cachedShip == null) _cachedShip = FindFirstObjectByType<PlayerShip>();
        return _cachedShip != null ? _cachedShip.transform.position : Vector3.zero;
    }

    void AimAt(Vector3 worldPos, bool instant = false)
    {
        var   dir    = worldPos - transform.position;
        float angle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float next   = instant
            ? angle
            : Mathf.MoveTowardsAngle(transform.eulerAngles.z, angle, turnRate * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, next);
    }

    // -------------------------------------------------------------------------
    // Ateş etme
    // -------------------------------------------------------------------------

    void Fire(Transform target, float effectiveFireRate)
    {
        _fireTimer = effectiveFireRate;

        if (specType == TurretSpecType.Gatling)
        {
            if (_currentMag <= 0) { BeginReload(); return; }
            _currentMag--;
            if (_currentMag <= 0) BeginReload();
        }

        SpawnBullet(target);
    }

    void SpawnBullet(Transform target)
    {
        // Lazer spec → anlık ışın atar, mermi değil
        if (specType == TurretSpecType.Laser)
        {
            SpawnLaserBeam();
            return;
        }

        Vector3 spawnPos = transform.position + transform.right * 0.25f;

        var go = new GameObject("TurretBullet");
        go.transform.position = spawnPos;

        var tb = go.AddComponent<TurretBullet>();
        tb.owner       = this;
        Stats.shotsFired++;
        tb.damage      = damage * GetMultiplier("damage");
        tb.speed       = bulletSpeed;
        tb.weaponType  = BulletWeaponType();

        bool isRocket = specType == TurretSpecType.HomingRocket ||
                        specType == TurretSpecType.NuclearRocket ||
                        (baseType == TurretBaseType.Missile && specType == TurretSpecType.None);
        tb.isGuided     = isRocket;
        tb.guidedTarget = isRocket ? target : null;
        if (isRocket) { tb.turnRate = 150f; tb.hp = 3f; }

        // Nükleer başlık daha hantal döner: geniş patlaması kaçamak bir hedefi
        // zaten yakalıyor, üstüne güdümün de keskin olması onu her açıdan
        // güdümlü rokete üstün kılardı — takas olmaktan çıkardı.
        if (specType == TurretSpecType.NuclearRocket) tb.turnRate = 70f;

        // Güdüm bilgisayardan gelir (FireControl.GuidanceMultiplier): iki
        // roketin ARASINDAKİ oran korunur, ikisi birlikte keskinleşir.
        if (isRocket) tb.turnRate *= FireControl.GuidanceMultiplier;

        tb.blastRadius = blastRadius;
        tb.shrapnel    = shrapnelCount;

        // Şarapnel mermisi çarpmayı beklemez: nişan alınan buluşma noktasına
        // varınca patlar. Turret ancak namlu o noktaya 1°'den yakın dönükken
        // ateş ettiği için (IsAimed) mermi noktanın hemen yanından geçer.
        // Menzilin ötesi zaten vurulamaz; menzil sonunda da patlar.
        if (shrapnelCount > 0)
            tb.fuse = Mathf.Min(Vector2.Distance(spawnPos, _aimPos), EffectiveRange);

        // Sapma: bilgisayarın hassasiyeti kadar rastgele hata. Elle modda
        // nişanı oyuncu alıyor — orada bilgisayarın payı yok.
        float spread = EffectiveMode == AimMode.Manual ? 0f : FireControl.RollSpread();
        tb.SetDirection(FireControl.Rotate(transform.right, spread));
        tb.zoomAtFire = CameraController.ZoomOrani;

        BalanceLog.Event("shot_fired")
                  .Str("kaynak", "turret")
                  .Str("spec",   specType.ToString())
                  .Str("silah",  tb.weaponType.ToString())
                  .Num("hasar",  tb.damage)
                  .Num("hiz",    bulletSpeed)
                  .Num("zoom",   tb.zoomAtFire)
                  .End();

        BuildBulletVisual(go, specType, tb.damage);
        tb.visual   = (int)specType;
        tb.lifeTime = bulletLifeTime;
    }

    void SpawnLaserBeam()
    {
        // Child olarak spawn — turret dönerken beam yönü otomatik güncellenir.
        // Turret transform.right ile nişan alır; LaserBeam transform.up kullanır
        // → localRotation -90° ile hizalanır (right → up).
        var go = new GameObject("TurretLaserBeam");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0.25f, 0f, 0f); // namlu ucu
        go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        var beam             = go.AddComponent<LaserBeam>();
        beam.damage          = LaserBeamDps;
        beam.weaponType      = WeaponType.Laser;
        beam.continuous      = false;
        beam.burnDuration    = LaserBurnTime;
        beam.energyPerSecond = 0f;   // enerji ateş anında ödendi (TurretController.Update)
        beam.hitsPlayer      = false;
        beam.maxRange        = bulletLifeTime * bulletSpeed; // efektif menzil
        beam.stats           = Stats;   // ışın atış saymaz: ıskalamaz (bkz. ComponentStats)
        beam.Init();
    }

    void BeginReload()
    {
        if (_reloading) return;
        _reloading   = true;
        _reloadTimer = reloadTime;
    }

    WeaponType BulletWeaponType()
    {
        return specType switch
        {
            TurretSpecType.Plasma => WeaponType.Plasma,
            TurretSpecType.Laser  => WeaponType.Laser,
            TurretSpecType.EMP    => WeaponType.Laser,
            _                     => WeaponType.Kinetic,
        };
    }

    // -------------------------------------------------------------------------
    // Uzmanlaşma (runtime spec değişimi)
    // -------------------------------------------------------------------------

    public void Specialize(TurretSpecType newSpec, ComponentDefinition newDef)
    {
        specType = newSpec;

        // Sayaçlar sıfırlanır: Gatling'in isabet oranı ile Flak'inki aynı
        // sayıya karışırsa ikisi de anlamsızlaşır (ana silahta tiplerin ayrı
        // sayılmasıyla aynı gerekçe).
        Stats = new ComponentStats();

        fireRate       = newDef.turretFireRate       > 0 ? newDef.turretFireRate       : fireRate;
        damage         = newDef.turretDamage         > 0 ? newDef.turretDamage         : damage;
        bulletSpeed    = newDef.turretBulletSpeed    > 0 ? newDef.turretBulletSpeed    : bulletSpeed;
        bulletLifeTime = newDef.turretBulletLifeTime > 0 ? newDef.turretBulletLifeTime : bulletLifeTime;
        energyPerShot  = newDef.turretEnergyPerShot  > 0 ? newDef.turretEnergyPerShot  : energyPerShot;
        magazineSize   = newDef.turretMagazineSize   > 0 ? newDef.turretMagazineSize   : magazineSize;
        reloadTime     = newDef.turretReloadTime     > 0 ? newDef.turretReloadTime     : reloadTime;

        // KOŞULSUZ atanır, yukarıdaki "0 ise koru" deseniyle DEĞİL. O desen
        // "tanımda belirtilmemişse mevcut değeri sürdür" demek; patlama
        // yarıçapında 0 bir eksiklik değil BİR DEĞERDİR — "bu uzmanlaşma
        // patlamaz". Korumalı atansaydı Flak'ten Gatling'e geçen turret
        // patlamaya devam ederdi.
        blastRadius    = newDef.turretBlastRadius;
        shrapnelCount  = newDef.turretShrapnel;    // aynı gerekçe: 0 bir değerdir

        _currentMag = magazineSize;
        ApplySpecTurnRate();
        componentName = BuildLabel();
        RebuildVisual();
    }

    // -------------------------------------------------------------------------
    // Görseller
    // -------------------------------------------------------------------------

    /// <summary>
    /// Taban, namlu ve mermi sprite'ları GRİ TONLAMALIDIR; uzmanlaşma rengini
    /// SpriteRenderer.color çarpar. Alternatif her uzmanlaşma için ayrı bir
    /// görsel çizmekti — altı uzmanlaşma × (taban + namlu + mermi) = 18 sprite,
    /// hepsi aynı şeklin farklı renklisi.
    ///
    /// Bu yüzden SkinLibrary'ye yedek renk olarak BEYAZ verilir: prosedürel
    /// dikdörtgen de beyaz doğar ve rengi yine sr.color'dan alır. İki yol da
    /// aynı sonucu verir — yedeğe rengi gömseydik skinli yolda renk iki kez
    /// çarpılır ve turretler kararırdı.
    /// </summary>
    void BuildVisual()
    {
        Color baseColor = TurretColor();

        var baseGo = new GameObject("Base");
        baseGo.transform.SetParent(transform, false);
        var baseSR = baseGo.AddComponent<SpriteRenderer>();
        baseSR.sprite       = SkinLibrary.Get(SkinId.TurretBase + "." + specType.ToString().ToLowerInvariant(), SkinId.TurretBase,
                                  30, 30, Color.white);
        baseSR.color        = baseColor * 0.7f;
        baseSR.sortingOrder = 3;
        _base = baseGo;

        _barrel = new GameObject("Barrel").transform;
        _barrel.SetParent(transform, false);
        _barrel.localPosition = new Vector3(0.10f, 0f, 0f);
        var barrelSR = _barrel.gameObject.AddComponent<SpriteRenderer>();
        barrelSR.sprite       = SkinLibrary.Get(SkinId.TurretBarrel + "." + specType.ToString().ToLowerInvariant(), SkinId.TurretBarrel,
                                    20, 8, Color.white, new Vector2(0f, 0.5f));
        barrelSR.color        = baseColor;
        barrelSR.sortingOrder = 4;
    }

    GameObject _base;

    /// <summary>
    /// Yalnızca turretin KENDİ görselini (taban + namlu) yeniden kurar.
    ///
    /// Eskiden bütün çocukları siliyordu — ShipComponentBase'in kurduğu konum
    /// halkası ve HP barı da çocuk. Uzmanlaşma değiştiren her turret halkasını
    /// ve HP barını sessizce kaybediyordu.
    /// </summary>
    void RebuildVisual()
    {
        if (_base   != null) Destroy(_base);
        if (_barrel != null) Destroy(_barrel.gameObject);
        _base   = null;
        _barrel = null;
        BuildVisual();
    }

    /// <summary>Kayıttan kurulan mermi de aynı görseli buradan alır.</summary>
    internal static void BuildBulletVisual(GameObject go, TurretSpecType spec, float damage)
    {
        Color c = spec switch
        {
            TurretSpecType.Plasma       => new Color(0.4f, 1f, 0.3f),
            TurretSpecType.Laser        => Color.cyan,
            TurretSpecType.HomingRocket => new Color(1f, 0.5f, 0.1f),
            // Nükleer başlık kendi rengini taşır: aynı turuncu olsaydı oyuncu
            // ekranda hangi roketin patladığını göremezdi — biri 1.2, diğeri
            // 3.2 yarıçapla patlıyor ve bu fark nişan kararını değiştiriyor.
            TurretSpecType.NuclearRocket => new Color(0.6f, 1f, 0.35f),
            TurretSpecType.PointDefence => Color.yellow,
            TurretSpecType.Flak         => new Color(1f, 0.72f, 0.28f),
            _                           => Color.white,
        };

        // Füze mermiden üç kat uzun: siluetten "bu bir füze" okunmalı.
        bool isRocket = spec == TurretSpecType.HomingRocket ||
                        spec == TurretSpecType.NuclearRocket;
        int w = isRocket ? 14 : 8;
        int h = isRocket ? 6  : 4;

        // Sprite gri tonlamalı — rengi tint verir (bkz. BuildVisual).
        // Boyut atış hasarından: hasar statı yükseldikçe mermi büyür.
        var sprite = SkinLibrary.Get(SkinId.TurretBullet + "." + spec.ToString().ToLowerInvariant(), SkinId.TurretBullet,
                                     w, h, Color.white, new Vector2(0f, 0.5f));
        ProjectileLook.Apply(go, sprite, c, c, 3, damage);
    }

    Color TurretColor() => specType switch
    {
        TurretSpecType.Gatling      => new Color(0.7f, 0.7f, 0.75f),
        TurretSpecType.PointDefence => new Color(1f,   0.9f, 0.2f),
        TurretSpecType.Laser        => new Color(0.2f, 0.8f, 1f),
        TurretSpecType.Plasma       => new Color(0.3f, 0.9f, 0.3f),
        TurretSpecType.HomingRocket => new Color(1f,   0.5f, 0.1f),
        TurretSpecType.NuclearRocket => new Color(0.55f, 0.9f, 0.3f),
        TurretSpecType.Flak         => new Color(0.95f, 0.68f, 0.25f),
        _ => baseType switch
        {
            TurretBaseType.Energy  => new Color(0.4f, 0.8f, 0.9f),
            TurretBaseType.Missile => new Color(0.9f, 0.6f, 0.2f),
            _                      => new Color(0.65f, 0.65f, 0.70f),
        }
    };

    void ApplySpecTurnRate()
    {
        turnRate = specType switch
        {
            TurretSpecType.HomingRocket => 90f,
            TurretSpecType.Laser        => 126f,
            _                           => 180f,
        };
    }

    string BuildLabel()
    {
        string baseName = TurretSpecHelper.GetBaseTypeName(baseType);
        if (specType == TurretSpecType.None)
            return baseName;
        return $"{baseName} — {TurretSpecHelper.GetSpecName(specType)}";
    }

    // -------------------------------------------------------------------------
    // Configure (ShipLoadout tarafından çağrılır)
    // -------------------------------------------------------------------------

    public void Configure(ComponentDefinition def)
    {
        baseType       = def.turretBaseType;
        specType       = def.turretSpecType;
        fireRate       = def.turretFireRate       > 0 ? def.turretFireRate       : fireRate;
        damage         = def.turretDamage         > 0 ? def.turretDamage         : damage;
        bulletSpeed    = def.turretBulletSpeed    > 0 ? def.turretBulletSpeed    : bulletSpeed;
        bulletLifeTime = def.turretBulletLifeTime > 0 ? def.turretBulletLifeTime : bulletLifeTime;
        energyPerShot  = def.turretEnergyPerShot  > 0 ? def.turretEnergyPerShot  : energyPerShot;
        magazineSize   = def.turretMagazineSize   > 0 ? def.turretMagazineSize   : magazineSize;
        reloadTime     = def.turretReloadTime     > 0 ? def.turretReloadTime     : reloadTime;
        blastRadius    = def.turretBlastRadius;   // koşulsuz — bkz. Specialize
        shrapnelCount  = def.turretShrapnel;      // koşulsuz — bkz. Specialize

        componentName = BuildLabel();
        _currentMag   = magazineSize;
        ApplySpecTurnRate();

        // Görsel Awake'te VARSAYILAN uzmanlaşmayla (None) kuruldu. Kayıttan ya
        // da katalogdan uzmanlaşmış bir turret kurulunca rengi yanlış kalıyordu.
        RebuildVisual();
    }

    // ── Kayıt ─────────────────────────────────────────────────────────────────

    public override void CaptureRuntime(ComponentRuntimeState s)
    {
        base.CaptureRuntime(s);
        s.fireTimer     = _fireTimer;
        s.magazine      = _currentMag;
        s.reloading     = _reloading;
        s.reloadTimer   = _reloadTimer;
        s.rotation      = transform.eulerAngles.z;
        s.retargetTimer = _retargetTimer;
        s.lockedTarget  = WorldSave.RefOf(_lockedTarget);
    }

    public override void RestoreRuntime(ComponentRuntimeState s)
    {
        base.RestoreRuntime(s);
        _fireTimer         = s.fireTimer;
        _currentMag        = s.magazine;
        _reloading         = s.reloading;
        _reloadTimer       = s.reloadTimer;
        _retargetTimer     = s.retargetTimer;
        _lockedTarget      = WorldSave.ResolveTarget(s.lockedTarget);
        transform.rotation = Quaternion.Euler(0f, 0f, s.rotation);
    }
}
