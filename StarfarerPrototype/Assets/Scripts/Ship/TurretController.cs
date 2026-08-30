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
    public float          burnDuration   = 0.5f; // Lazer spec: beam yanma süresi
    [Tooltip("Saniyede derece — turretin maksimum dönüş hızı.")]
    public float          turnRate       = 180f;

    float   _fireTimer;
    int     _currentMag;
    bool    _reloading;
    Transform _barrel;

    // Hedef kilidi — her karede değil, aralıklarla yeniden değerlendirilir
    ITurretTarget _lockedTarget;
    float         _retargetTimer;

    /// <summary>
    /// Point Defence menzili. Kalkan küresi 2.5 birim; turretler gövdede
    /// ±1.3 birim yayılı duruyor, yani 4.0 birim en uzak slottan bile kalkanın
    /// biraz dışına ulaşır. Daha fazlası PD'yi "kısa menzilli ama sert" olmaktan
    /// çıkarır — o kısıt yüksek DPS'inin karşılığıdır.
    /// </summary>
    const float PDRange = 4f;

    /// <summary>
    /// Işın turretinin hızlı hedeflere verdiği öncelik. Işın ıskalamaz;
    /// mermili turretlerin zorlandığı kaçamak hedefler onun işidir.
    /// </summary>
    const float LaserSpeedBias = 1.5f;

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
        if (!IsOperational)     return;
        if (UpgradeUI.IsPaused) return;

        var target = AcquireTarget();

        if (target != null)
        {
            // Lazer beam anlık (raycast) — lead gereksiz, mevcut pozisyonu hedefle.
            // Diğer tüm spec'ler (roket dahil) mermi seyahat süresi hesaplayarak
            // düşmanın buluşma noktasını öngörür.
            bool isInstant = specType == TurretSpecType.Laser;
            _aimPos = isInstant ? target.position : PredictIntercept(target);
            AimAt(_aimPos);
        }

        _fireTimer -= Time.deltaTime;
        float effectiveFireRate = fireRate / GetMultiplier("fireRate");
        if (_fireTimer <= 0f && !_reloading && target != null && IsAimed(_aimPos))
        {
            bool hasEnergy = EnergyBus.Instance == null ||
                             EnergyBus.Instance.RequestEnergy(energyPerShot);
            if (hasEnergy)
                Fire(target, effectiveFireRate);
        }
    }

    bool IsAimed(Vector3 worldPos)
    {
        var   dir         = worldPos - transform.position;
        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        return Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.z, targetAngle)) < 1f;
    }

    // -------------------------------------------------------------------------
    // Hedefleme
    // -------------------------------------------------------------------------

    /// <summary>
    /// Kilitli hedefi döndürür; kilit düştüyse veya değerlendirme zamanı geldiyse
    /// TurretTargeting'e yeniden seçtirir. Seçim mantığı ve puanlama orada.
    /// </summary>
    Transform AcquireTarget()
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

        return _lockedTarget?.TargetTransform;
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

    // Hedefin mermisiyle buluşacağı noktayı hesaplar
    Vector3 PredictIntercept(Transform target)
    {
        Vector2 toTarget  = (Vector2)(target.position - transform.position);
        Vector2 targetVel = GetTargetVelocity(target);

        float a = Vector2.Dot(targetVel, targetVel) - bulletSpeed * bulletSpeed;
        float b = 2f * Vector2.Dot(targetVel, toTarget);
        float c = Vector2.Dot(toTarget, toTarget);

        float t = 0f;
        if (Mathf.Abs(a) < 0.001f)
        {
            if (Mathf.Abs(b) > 0.001f) t = -c / b;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return target.position;
            float sq = Mathf.Sqrt(disc);
            float t1 = (-b + sq) / (2f * a);
            float t2 = (-b - sq) / (2f * a);
            if      (t1 > 0f && t2 > 0f) t = Mathf.Min(t1, t2);
            else if (t1 > 0f)            t = t1;
            else if (t2 > 0f)            t = t2;
            else return target.position;
        }

        return target.position + (Vector3)(targetVel * t);
    }

    Vector2 GetTargetVelocity(Transform target)
        => _lockedTarget != null ? _lockedTarget.TargetVelocity : Vector2.zero;

    // -------------------------------------------------------------------------
    // Ateş etme
    // -------------------------------------------------------------------------

    void Fire(Transform target, float effectiveFireRate)
    {
        _fireTimer = effectiveFireRate;

        if (specType == TurretSpecType.Gatling)
        {
            if (_currentMag <= 0) { StartCoroutine(Reload()); return; }
            _currentMag--;
            if (_currentMag <= 0) StartCoroutine(Reload());
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
        tb.damage      = damage * GetMultiplier("damage");
        tb.speed       = bulletSpeed;
        tb.weaponType  = BulletWeaponType();

        bool isRocket = specType == TurretSpecType.HomingRocket ||
                        (baseType == TurretBaseType.Missile && specType == TurretSpecType.None);
        tb.isGuided     = isRocket;
        tb.guidedTarget = isRocket ? target : null;
        if (isRocket) { tb.turnRate = 150f; tb.hp = 3f; }

        tb.SetDirection(transform.right);

        BuildBulletVisual(go, specType);
        Destroy(go, bulletLifeTime);
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
        beam.damage          = damage * GetMultiplier("damage");
        beam.weaponType      = WeaponType.Laser;
        beam.continuous      = false;
        beam.burnDuration    = burnDuration;
        beam.energyPerSecond = 0f;   // enerji ateş anında ödendi (TurretController.Update)
        beam.hitsPlayer      = false;
        beam.maxRange        = bulletLifeTime * bulletSpeed; // efektif menzil
        beam.Init();
    }

    IEnumerator Reload()
    {
        _reloading = true;
        yield return new WaitForSeconds(reloadTime);
        _currentMag = magazineSize;
        _reloading  = false;
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

        fireRate       = newDef.turretFireRate       > 0 ? newDef.turretFireRate       : fireRate;
        damage         = newDef.turretDamage         > 0 ? newDef.turretDamage         : damage;
        bulletSpeed    = newDef.turretBulletSpeed    > 0 ? newDef.turretBulletSpeed    : bulletSpeed;
        bulletLifeTime = newDef.turretBulletLifeTime > 0 ? newDef.turretBulletLifeTime : bulletLifeTime;
        energyPerShot  = newDef.turretEnergyPerShot  > 0 ? newDef.turretEnergyPerShot  : energyPerShot;
        magazineSize   = newDef.turretMagazineSize   > 0 ? newDef.turretMagazineSize   : magazineSize;
        reloadTime     = newDef.turretReloadTime     > 0 ? newDef.turretReloadTime     : reloadTime;
        burnDuration   = newDef.turretBurnDuration   > 0 ? newDef.turretBurnDuration   : burnDuration;

        _currentMag = magazineSize;
        ApplySpecTurnRate();
        componentName = BuildLabel();
        RebuildVisual();
    }

    // -------------------------------------------------------------------------
    // Görseller
    // -------------------------------------------------------------------------

    void BuildVisual()
    {
        Color baseColor = TurretColor();

        var baseGo = new GameObject("Base");
        baseGo.transform.SetParent(transform, false);
        var baseSR = baseGo.AddComponent<SpriteRenderer>();
        baseSR.sprite       = SkinLibrary.Get(SkinId.TurretBase + "." + specType.ToString().ToLowerInvariant(), SkinId.TurretBase,
                                  30, 30, baseColor * 0.7f);
        baseSR.sortingOrder = 3;

        _barrel = new GameObject("Barrel").transform;
        _barrel.SetParent(transform, false);
        _barrel.localPosition = new Vector3(0.10f, 0f, 0f);
        var barrelSR = _barrel.gameObject.AddComponent<SpriteRenderer>();
        barrelSR.sprite       = SkinLibrary.Get(SkinId.TurretBarrel + "." + specType.ToString().ToLowerInvariant(), SkinId.TurretBarrel,
                                    20, 8, TurretColor(), new Vector2(0f, 0.5f));
        barrelSR.sortingOrder = 4;
    }

    void RebuildVisual()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        _barrel = null;
        BuildVisual();
    }

    static void BuildBulletVisual(GameObject go, TurretSpecType spec)
    {
        Color c = spec switch
        {
            TurretSpecType.Plasma       => new Color(0.4f, 1f, 0.3f),
            TurretSpecType.Laser        => Color.cyan,
            TurretSpecType.HomingRocket => new Color(1f, 0.5f, 0.1f),
            TurretSpecType.PointDefence => Color.yellow,
            _                           => Color.white,
        };

        int w = spec == TurretSpecType.HomingRocket ? 14 : 8;
        int h = spec == TurretSpecType.HomingRocket ? 6  : 4;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.TurretBullet + "." + spec.ToString().ToLowerInvariant(), SkinId.TurretBullet,
                              w, h, c, new Vector2(0f, 0.5f));
        sr.sortingOrder = 3;
    }

    Color TurretColor() => specType switch
    {
        TurretSpecType.Gatling      => new Color(0.7f, 0.7f, 0.75f),
        TurretSpecType.PointDefence => new Color(1f,   0.9f, 0.2f),
        TurretSpecType.Laser        => new Color(0.2f, 0.8f, 1f),
        TurretSpecType.Plasma       => new Color(0.3f, 0.9f, 0.3f),
        TurretSpecType.HomingRocket => new Color(1f,   0.5f, 0.1f),
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
        burnDuration   = def.turretBurnDuration   > 0 ? def.turretBurnDuration   : burnDuration;

        componentName = BuildLabel();
        _currentMag   = magazineSize;
        ApplySpecTurnRate();
    }
}
