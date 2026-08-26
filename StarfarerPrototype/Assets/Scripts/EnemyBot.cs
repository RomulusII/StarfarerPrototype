using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven düşman gemisi. Tüm istatistikler ve davranış parametreleri
/// EnemyTypeData ScriptableObject'inden okunur — yeni tip eklemek kod değişikliği gerektirmez.
///
/// data null ise EnemySpawner tarafından atanmadan önce Start() bekler;
/// null kalırsa CreateSwarm() ile fallback oluşturulur.
/// </summary>
public class EnemyBot : MonoBehaviour, ITurretTarget
{
    public EnemyTypeData data;

    PlayerShip   _playerShip;
    HealthBar    _healthBar;
    ShipMovement _movement;
    ShipBrain    _brain;

    float _contactDamage;

    // Kalkan
    float      _shieldHP;
    float      _maxShieldHP;
    GameObject _shieldVisual;
    float      _shieldRechargeTimer;
    const float ShieldRechargeDelay = 4f;
    const float ShieldRechargeRate  = 5f;

    // Ateş etme
    float _fireTimer;
    float _fireRateBase;

    // Namlu + dolum göstergesi
    Transform      _barrelTransform;
    Transform      _reloadFillTransform;
    SpriteRenderer _reloadFillSR;
    bool           _fireFlash;

    // Hedef tarama
    float _targetScanTimer;

    // Approach (Bomber tipi) state machine
    enum ApproachPhase { Approaching, Hovering, Retreating }
    enum ArPhase { Engaging, Disengaging, Braking }
    ApproachPhase _approachPhase;
    float         _approachFireTimer;
    int           _approachShotsLeft;
    Vector2       _approachHoverPos;
    Vector2       _approachEscapeDir = Vector2.right;
    const float   ApproachHoverX  = 2.0f;
    const int     ApproachShotMax = 3;

    // BombRun state
    float       _bombRunFireTimer;
    const float BombRunSpeed = 1.5f;

    // AttackRun state — hareket ShipMovement uçuş modeline devredilmiştir
    float             _arDisengageTimer;
    float             _arEscapeAngle;
    ArPhase           _arPhase;
    ShipComponentBase _arTarget;
    const float ArFireAngle     = 20f;   // ateş için max açı sapması
    const float ArDisengageTime = 1.0f;  // ateş sonrası düz uçuş süresi (sn)
    const float ArAimSpeed      = 0.4f;  // saldırıya geçmek için max hız
    const float ArAimAngle      = 12f;   // saldırıya geçmek için max açı hatası
    const float ArAimDistanceFactor = 2f; // fireRange × bu mesafede nişana geçer, salınım kesilir
    const float ArEscapeFactor       = 1.25f; // escapeAngle × bu = AttackRun kaçış sapması
    const float ApproachEscapeFactor = 1.1f;  // escapeAngle × bu = Approach geri çekilme sapması

    // Velocity tracking
    Vector2        _prevPos;
    public Vector2 Velocity { get; private set; }

    // Spawn anındaki burun yönü — InitMovement belirler, Initialize uygular
    float _initialFacing = 180f;

    // Kaçış tarafı her sortide değişir — deterministik imza manevrası
    int _escapeSide = 1;

    // Kalkan kapasitesinin kaçta kaçı kristal olarak düşer
    const float CrystalPerShieldPoint = 0.1f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        var col        = gameObject.AddComponent<BoxCollider2D>();
        col.size       = new Vector2(0.6f, 0.4f);
        var rb         = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        _movement      = gameObject.AddComponent<ShipMovement>();
    }

    void Start()
    {
        if (data == null)
            data = EnemyTypeData.CreateSwarm();

        _healthBar  = GetComponent<HealthBar>();
        _playerShip = FindFirstObjectByType<PlayerShip>();

        _movement.mass        = data.mass;
        _movement.enginePower = data.enginePower;
        _movement.agility     = data.agility;
        _movement.grip        = data.grip;
        _movement.wanderAngle  = data.evasionAngle;
        _movement.wanderPeriod = data.evasionPeriod;

        ApplyStats();

        if (data.maxShield > 0f)
            InitShield();

        _fireRateBase = data.fireRate;
        _fireTimer    = Random.Range(0f, _fireRateBase);
        _escapeSide   = Random.Range(0, 2) == 0 ? 1 : -1;   // yalnızca başlangıç tarafı
        _prevPos      = transform.position;

        InitMovement();

        bool needsBarrel = data.weaponKind != EnemyWeaponKind.None
                        && data.weaponKind != EnemyWeaponKind.ComponentBurst
                        && data.movementKind != EnemyMovementKind.Approach
                        && data.movementKind != EnemyMovementKind.BombRun
                        && data.movementKind != EnemyMovementKind.AttackRun;
        if (needsBarrel)
            BuildBarrel(data.barrelColor);

        _movement.Initialize(_initialFacing);
    }

    void ApplyStats()
    {
        _contactDamage = data.contactDamage;

        if (_healthBar != null)
        {
            _healthBar.maxHealth     = data.maxHP;
            _healthBar.currentHealth = data.maxHP;
            _healthBar.barWidth      = data.bodyWidth  / 100f * 1.3f;
            _healthBar.barOffsetY    = data.bodyHeight / 100f * 0.8f;
        }

        // Hitbox görselden AYRIDIR. Sprite'lar gelince bodyWidth/bodyHeight
        // değişecek; collider buna bağlı kalsaydı vurma zorluğu kayar ve bu
        // oturumda konan tüm denge sayıları geçersizleşirdi.
        GetComponent<BoxCollider2D>().size = new Vector2(
            data.EffectiveHitboxWidth  / 100f,
            data.EffectiveHitboxHeight / 100f);

        BuildBody(data.bodyWidth, data.bodyHeight, data.bodyColor);
    }

    void InitShield()
    {
        _shieldHP = _maxShieldHP = data.maxShield;
        BuildShieldVisual(data.bodyWidth + 20, data.bodyHeight + 15);
        if (_healthBar != null)
        {
            _healthBar.maxShield     = _maxShieldHP;
            _healthBar.currentShield = _shieldHP;
        }
    }

    void InitMovement()
    {
        switch (data.movementKind)
        {
            case EnemyMovementKind.Charge:
                SetupBrain(CombatPattern.Orbit,
                    data.engageRange, data.fireRange, data.orbitRadius, data.engageDuration);
                break;

            case EnemyMovementKind.HoverFire:
                SetupBrain(CombatPattern.HoverFire,
                    data.engageRange, data.fireRange, data.orbitRadius, data.engageDuration);
                break;

            case EnemyMovementKind.Approach:
                _approachPhase     = ApproachPhase.Approaching;
                _approachFireTimer = 1.2f;
                _approachShotsLeft = ApproachShotMax;
                _approachHoverPos  = new Vector2(ApproachHoverX, transform.position.y);
                break;

            case EnemyMovementKind.Strafe:
                SetupBrain(CombatPattern.Strafe,
                    data.engageRange, data.fireRange, data.orbitRadius, data.engageDuration);
                break;

            case EnemyMovementKind.Stationary:
                break;

            case EnemyMovementKind.BombRun:
                _bombRunFireTimer = _fireRateBase * 0.5f;
                break;

            case EnemyMovementKind.AttackRun:
                _arTarget         = PickComponentTarget();
                Vector2 initDir   = _arTarget != null
                    ? ((Vector2)_arTarget.transform.position - (Vector2)transform.position).normalized
                    : Vector2.left;
                _initialFacing    = Mathf.Atan2(initDir.y, initDir.x) * Mathf.Rad2Deg;
                _arPhase          = ArPhase.Engaging;
                _arDisengageTimer = 0f;
                break;
        }
    }

    void SetupBrain(CombatPattern pattern, float engageRange, float fireRange,
                    float orbitRadius, float engageDuration)
    {
        _brain                 = gameObject.AddComponent<ShipBrain>();
        _brain.pattern         = pattern;
        _brain.engageRange     = engageRange;
        _brain.fireRange       = fireRange;
        _brain.orbitRadius     = orbitRadius;
        _brain.engageDuration  = engageDuration;
        _brain.repositionDelay = 1f;
        _brain.escapeAngle     = data.escapeAngle;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (Time.deltaTime > 0f)
            Velocity = ((Vector2)transform.position - _prevPos) / Time.deltaTime;
        _prevPos = transform.position;

        UpdateSpecialBehaviours();

        if (data.movementKind == EnemyMovementKind.Approach)
        {
            UpdateApproach();
            return;
        }

        if (data.movementKind == EnemyMovementKind.BombRun)
        {
            UpdateBombRun();
            return;
        }

        if (data.movementKind == EnemyMovementKind.AttackRun)
        {
            UpdateAttackRun();
            return;
        }

        _targetScanTimer -= Time.deltaTime;
        if (_targetScanTimer <= 0f || (_brain != null && !_brain.HasTarget))
        {
            _targetScanTimer = 1.5f;
            var threat = FindClosestThreat();
            if (threat != null) _brain?.SetTarget(threat);
        }

        _fireTimer -= Time.deltaTime;
        if (_brain != null && _brain.InFireRange && _fireTimer <= 0f)
        {
            _fireTimer = _fireRateBase;
            FireAtTarget();
        }

        UpdateBarrel();

        if (data.maxShield > 0f)
            UpdateShieldRecharge();

        if (Vector2.Distance(transform.position, Vector2.zero) > 30f)
            Destroy(gameObject);
    }

    Transform FindClosestThreat()
    {
        Transform best  = null;
        float     bestD = float.MaxValue;

        if (_playerShip != null)
        {
            float d = Vector2.Distance(transform.position, _playerShip.transform.position);
            if (d < bestD) { bestD = d; best = _playerShip.transform; }
        }

        var fighters = FindObjectsByType<FighterShip>(FindObjectsSortMode.None);
        foreach (var f in fighters)
        {
            float d = Vector2.Distance(transform.position, f.transform.position);
            if (d < bestD) { bestD = d; best = f.transform; }
        }

        return best;
    }

    void UpdateApproach()
    {
        switch (_approachPhase)
        {
            case ApproachPhase.Approaching:
                _movement.MoveToward(_approachHoverPos, evasive: true);
                if (_movement.IsNear(_approachHoverPos, 0.2f))
                {
                    _approachPhase     = ApproachPhase.Hovering;
                    _approachFireTimer = 1.2f;
                }
                break;

            case ApproachPhase.Hovering:
                _movement.Brake();
                _approachFireTimer -= Time.deltaTime;
                if (_approachFireTimer <= 0f)
                {
                    FireAtComponent();
                    _approachShotsLeft--;
                    _approachFireTimer = _fireRateBase;
                    if (_approachShotsLeft <= 0)
                    {
                        _approachPhase = ApproachPhase.Retreating;
                        // Düz geri değil, sabit açıyla çapraz kaç; taraf sortiden
                        // sortiye değişir — kalıp öğrenilebilir kalsın
                        _escapeSide = -_escapeSide;
                        _approachEscapeDir = ShipMovement.Rotate(
                            Vector2.right, data.escapeAngle * ApproachEscapeFactor * _escapeSide);
                    }
                }
                break;

            case ApproachPhase.Retreating:
                _movement.MoveInDirection(_approachEscapeDir, evasive: true);
                break;
        }

        float x = transform.position.x;
        if (x < -15f || x > 20f) Destroy(gameObject);
    }

    void UpdateBombRun()
    {
        transform.Translate(Vector2.left * BombRunSpeed * Time.deltaTime, Space.World);

        _bombRunFireTimer -= Time.deltaTime;
        if (_bombRunFireTimer <= 0f)
        {
            _bombRunFireTimer = _fireRateBase;
            DropBomb();
        }

        if (transform.position.x < -15f) Destroy(gameObject);
    }

    /// <summary>
    /// Saldırı sortisi: hedefe dalış → ateş → kaçış → fren + yeni hedefe nişan.
    /// Hareket tamamen ShipMovement uçuş modeline aittir; burada sadece
    /// hangi yöne bakılacağı ve gaz durumu belirlenir.
    /// </summary>
    void UpdateAttackRun()
    {
        // Hedef component geçersizse yenisini seç
        if (_arTarget == null || !_arTarget.IsOperational)
            _arTarget = PickComponentTarget();
        if (_arTarget == null)
        {
            // Operasyonel komponent yok — motor kapalı süzül, hedef bekle
            _movement.Coast();
            if (Vector2.Distance(transform.position, Vector2.zero) > 30f)
                Destroy(gameObject);
            return;
        }

        Vector2 toTarget  = (Vector2)_arTarget.transform.position - (Vector2)transform.position;
        float   angleDiff = _movement.HeadingErrorTo(toTarget);
        float   dist      = toTarget.magnitude;

        switch (_arPhase)
        {
            case ArPhase.Engaging:
            {
                // Burnu hedefe çevir, tam gaz dal. Burun sapmışsa uçuş modeli
                // gazı zaten kısar — gemi önce toparlanır, sonra ivmelenir.
                // Nişan mesafesine girene kadar kaçamak salınım açık kalır.
                bool lining = dist <= data.fireRange * ArAimDistanceFactor;
                _movement.MoveInDirection(toTarget, evasive: !lining);

                _fireTimer -= Time.deltaTime;
                if (dist <= data.fireRange && angleDiff <= ArFireAngle && _fireTimer <= 0f)
                {
                    _fireTimer        = _fireRateBase;
                    _arPhase          = ArPhase.Disengaging;
                    _arDisengageTimer = ArDisengageTime;

                    // Kaçış açısını kaydet — Disengaging fazında bu açıya dönülür
                    // İmza kaçışı: sabit açı, her sortide ters taraf
                    _escapeSide    = -_escapeSide;
                    _arEscapeAngle = _movement.FacingAngle
                                   + data.escapeAngle * ArEscapeFactor * _escapeSide;

                    var go = new GameObject("EnemyBullet_Comp");
                    go.transform.position = transform.position;
                    var eb                = go.AddComponent<EnemyBullet>();
                    eb.damage             = data.fireDamage;
                    eb.speed              = data.bulletSpeed;
                    eb.targetComponent    = _arTarget;
                }
                break;
            }

            case ArPhase.Disengaging:
            {
                // Kaçış açısına dön ve gazla uzaklaş — hızlı olduğu için geniş kavis
                float   rad     = _arEscapeAngle * Mathf.Deg2Rad;
                Vector2 escape  = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                _movement.MoveInDirection(escape, evasive: true);

                _arDisengageTimer -= Time.deltaTime;
                if (_arDisengageTimer <= 0f)
                {
                    _arPhase  = ArPhase.Braking;
                    _arTarget = PickComponentTarget(); // bir sonraki saldırı için yeni hedef
                }
                break;
            }

            case ArPhase.Braking:
            {
                // Burun yeni hedefte, retro ile yavaşla. Hız düştükçe dönüş hızı
                // arttığı için gemi burada dar kavis çizip nişan alabilir.
                _movement.FaceAndBrake(toTarget);

                if (_movement.Speed <= ArAimSpeed && angleDiff <= ArAimAngle)
                {
                    _arPhase   = ArPhase.Engaging;
                    _fireTimer = 0f; // her saldırı sortisi ateşe hazır başlar
                }
                break;
            }
        }

        if (Vector2.Distance(transform.position, Vector2.zero) > 30f)
            Destroy(gameObject);
    }

    void DropBomb()
    {
        var go = new GameObject("Bomb");
        go.transform.position = transform.position;

        var bomb   = go.AddComponent<Bomb>();
        bomb.damage = data.fireDamage;
        bomb.speed  = data.bulletSpeed;

        Vector2 dir = _playerShip != null
            ? ((Vector2)_playerShip.transform.position - (Vector2)transform.position).normalized
            : Vector2.left;
        bomb.SetDirection(dir);
    }

    // ── Ateş etme ─────────────────────────────────────────────────────────────

    void UpdateBarrel()
    {
        if (_barrelTransform == null) return;

        if (_fireFlash)
        {
            _fireFlash = false;
            if (_reloadFillSR != null)
                _reloadFillSR.color = ReloadColor(0f);
            if (_reloadFillTransform != null)
                _reloadFillTransform.localScale = new Vector3(0f, 1f, 1f);
        }

        Transform target = _brain?.TargetTransform;
        if (target != null)
        {
            var   dir   = (target.position - _barrelTransform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _barrelTransform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
        else
        {
            _barrelTransform.rotation = transform.rotation;
        }

        if (_reloadFillTransform != null && !_fireFlash)
        {
            float ratio = _fireRateBase > 0f ? 1f - Mathf.Clamp01(_fireTimer / _fireRateBase) : 1f;
            _reloadFillTransform.localScale = new Vector3(ratio, 1f, 1f);
            if (_reloadFillSR != null) _reloadFillSR.color = ReloadColor(ratio);
        }
    }

    static Color ReloadColor(float t) =>
        new Color(1f, Mathf.Lerp(0.1f, 0.55f, t), 0f, Mathf.Lerp(0.1f, 0.65f, t));

    void FireAtTarget()
    {
        Transform t = _brain?.TargetTransform ?? _playerShip?.transform;
        if (t == null) return;

        var dir = (t.position - transform.position).normalized;

        if (data.weaponKind == EnemyWeaponKind.Laser)
        {
            FireLaserBeam(dir);
        }
        else
        {
            var spawnPos = _barrelTransform != null
                ? _barrelTransform.position + _barrelTransform.right * 0.18f
                : transform.position;

            var go = new GameObject("EnemyBullet");
            go.transform.position   = spawnPos;
            float bulletScale       = Mathf.Clamp(data.fireDamage / 10f, 0.3f, 1.5f);
            go.transform.localScale = Vector3.one * bulletScale;

            var eb    = go.AddComponent<EnemyBullet>();
            eb.damage = data.fireDamage;
            eb.speed  = data.bulletSpeed;
            eb.SetDirection(dir);
        }

        _fireFlash = true;
        if (_reloadFillSR != null)
            _reloadFillSR.color = new Color(1f, 1f, 0.8f, 0.95f);
        if (_reloadFillTransform != null)
            _reloadFillTransform.localScale = new Vector3(1f, 1f, 1f);
    }

    void FireLaserBeam(Vector3 dir)
    {
        // Düşman ile birlikte hareket etmesi için barrel'ın (veya enemy'nin) child'ı olarak spawn et
        var parent   = _barrelTransform != null ? _barrelTransform : transform;
        var localOff = _barrelTransform != null ? Vector3.right * 0.18f : Vector3.zero;

        var go = new GameObject("EnemyLaserBeam");
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.localPosition = localOff;
        go.transform.up            = dir; // world-space yön; parent pozisyon değişince beam de taşınır

        var beam             = go.AddComponent<LaserBeam>();
        beam.damage          = data.fireDamage;
        beam.weaponType      = WeaponType.Laser;
        beam.continuous      = false;
        beam.burnDuration    = 1.5f;
        beam.energyPerSecond = 0f; // düşmanlar enerji sistemi kullanmaz
        beam.hitsPlayer      = true;
        beam.maxRange        = data.fireRange + 2f;
        beam.Init();
    }

    ShipComponentBase PickComponentTarget()
    {
        var all         = FindObjectsByType<ShipComponentBase>(FindObjectsSortMode.None);
        var operational = System.Array.FindAll(all, c => c.IsOperational);
        if (operational.Length == 0) return null;
        return operational[Random.Range(0, operational.Length)];
    }

    /// <summary>AttackRun için: gemi burnuna doğru ilerleyen, kalkan bypass eden mermi.</summary>
    void FireForward()
    {
        Vector2 dir = _movement.Facing;

        var go = new GameObject("EnemyBullet_AR");
        go.transform.position = transform.position;
        var eb = go.AddComponent<EnemyBullet>();
        eb.damage        = data.fireDamage;
        eb.speed         = data.bulletSpeed;
        eb.bypassShields = true;
        eb.SetDirection(dir);
    }

    void FireAtComponent()
    {
        var all         = FindObjectsByType<ShipComponentBase>(FindObjectsSortMode.None);
        var operational = new List<ShipComponentBase>();
        foreach (var c in all)
            if (c.IsOperational) operational.Add(c);
        if (operational.Count == 0) return;

        var target = operational[Random.Range(0, operational.Count)];
        var go     = new GameObject("EnemyBullet_Comp");
        go.transform.position = transform.position;
        var eb = go.AddComponent<EnemyBullet>();
        eb.damage          = data.fireDamage;
        eb.speed           = data.bulletSpeed;
        eb.targetComponent = target;
    }

    // ── ITurretTarget ─────────────────────────────────────────────────────────

    public Transform TargetTransform => transform;
    public Vector2   TargetVelocity  => Velocity;
    // Faz hâlindeki hayalet geçerli hedef değildir — turretler ona kilitlenip
    // mermilerini boşa harcamasın.
    public bool      IsValidTarget   => this != null && isActiveAndEnabled && !IsPhased
                                     && _healthBar != null && _healthBar.currentHealth > 0f;

    public float ThreatValue => data != null ? Mathf.Max(1, data.threatScore) : 1f;

    public float ArmorValue => EffectiveArmor;

    /// <summary>Kalkanın içine girmiş küçük/yakın saldırganlar PD'nin işi.</summary>
    public bool IsPointDefencePriority =>
        data != null && (data.movementKind == EnemyMovementKind.Approach
                      || data.movementKind == EnemyMovementKind.BombRun
                      || data.movementKind == EnemyMovementKind.AttackRun);

    /// <summary>
    /// Kalkan + gövdeyi bu silah tipiyle bitirmek için gereken ham hasar.
    /// Direnci yüksek katman ham hasarı büyütür, düşük katman küçültür.
    ///
    /// Zırh burada hesaba KATILMAZ çünkü zırhın etkisi atış başına hasara
    /// bağlıdır ve o bilgi çağıran turrette durur; turret kendi atış hasarını
    /// bilerek <see cref="ArmorPenaltyFor"/> ile çarpar. Zırhı buraya sabit
    /// gömseydik, zayıf atışlı turretler vuramadıkları hedefe kilitlenirdi.
    /// </summary>
    public float RawDamageToKill(WeaponType weaponType)
    {
        float raw = 0f;

        if (data != null && data.maxShield > 0f && _shieldHP > 0f)
        {
            var mods = data.shieldResistances != null && data.shieldResistances.Length > 0
                ? data.shieldResistances
                : DefaultShieldResistances;
            raw += _shieldHP / Mathf.Max(MultiplierFor(weaponType, mods), 0.01f);
        }

        float hull = _healthBar != null ? _healthBar.currentHealth : 0f;
        raw += hull / Mathf.Max(MultiplierFor(weaponType, data?.hullResistances), 0.01f);

        return raw;
    }

    static float MultiplierFor(WeaponType wt, DamageModifier[] mods)
    {
        if (mods == null) return 1f;
        foreach (var m in mods)
            if (m.weaponType == wt) return m.multiplier;
        return 1f;
    }

    // ── Hasar alma ────────────────────────────────────────────────────────────

    public void TakeDamage(float amount, WeaponType weaponType = WeaponType.Kinetic)
    {
        if (_healthBar == null) return;
        if (IsPhased) return;   // Hayalet: faz sırasında hiçbir şey geçmez

        // Zırh EŞİĞİ dirençlerden ÖNCE, atış başına uygulanır. Sıra önemlidir:
        // zırh ham atışı budar, dirençler kalanı ölçekler. Ters sırada olsaydı
        // dirençli düşmanlara karşı zırh iki kez cezalandırırdı.
        float shot = BalanceConfig.Instance.ApplyArmor(amount, EffectiveArmor);

        float hull = data.maxShield > 0f && _shieldHP > 0f
            ? ApplyShieldLayer(shot, weaponType)
            : ApplyResistances(shot, weaponType, data.hullResistances);

        _healthBar.TakeDamage(hull);
        if (_healthBar.currentHealth <= 0f)
        {
            SpawnSplitFragments();
            SpawnDebris();
            Destroy(gameObject);
        }
    }

    // ── Zırh ve özel davranışlar ──────────────────────────────────────────────

    /// <summary>
    /// Bu düşmanın toplam zırhı: levelin taban zırhı + tipin bonusu.
    /// EnemySpawner ölçekleme sırasında data.armor'a zaten leveli eklemiştir.
    /// </summary>
    public float EffectiveArmor => data != null ? Mathf.Max(0f, data.armor) : 0f;

    /// <summary>Hayalet fazı — vurulamaz olduğu pencere.</summary>
    public bool IsPhased => _phaseTimer > 0f;

    float _phaseTimer;      // > 0 iken vurulamaz
    float _phaseCooldown;   // bir sonraki faza kalan süre
    float _auraTimer;
    static float _jamRefreshTimer;

    void UpdateSpecialBehaviours()
    {
        if (data == null) return;

        UpdatePhasing();
        UpdateRepairAura();
    }

    /// <summary>
    /// Periyodik vurulamazlık. Sürekli DPS'i cezalandırır, burst'ü ödüllendirir —
    /// oyuncunun faz penceresini öğrenmesi gerekir.
    /// </summary>
    void UpdatePhasing()
    {
        if (data.phaseInterval <= 0f) return;

        if (_phaseTimer > 0f)
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer <= 0f) SetPhaseVisual(false);
            return;
        }

        _phaseCooldown -= Time.deltaTime;
        if (_phaseCooldown > 0f) return;

        _phaseCooldown = data.phaseInterval;
        _phaseTimer    = Mathf.Max(0.1f, data.phaseDuration);
        SetPhaseVisual(true);
    }

    void SetPhaseVisual(bool phased)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            var c = sr.color;
            c.a      = phased ? 0.25f : 1f;
            sr.color = c;
        }
    }

    /// <summary>
    /// Onarıcı aurası — menzildeki düşmanların HP'sini geri getirir. Oyuncunun
    /// DPS'i aurayı aşamıyorsa hedefler hiç ölmez; öncelik hedeflemeyi zorunlu kılar.
    /// </summary>
    void UpdateRepairAura()
    {
        if (data.repairAura <= 0f) return;

        _auraTimer -= Time.deltaTime;
        if (_auraTimer > 0f) return;
        _auraTimer = 0.25f;   // her karede tarama yapmaya değmez

        float heal = data.repairAura * 0.25f;
        float r2   = data.repairAuraRange * data.repairAuraRange;

        foreach (var other in FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
        {
            if (other == this || other._healthBar == null) continue;
            if (((Vector2)other.transform.position - (Vector2)transform.position).sqrMagnitude > r2)
                continue;
            other._healthBar.currentHealth =
                Mathf.Min(other._healthBar.maxHealth, other._healthBar.currentHealth + heal);
        }
    }

    /// <summary>
    /// Bölünen düşman — ölünce iki küçük parçaya ayrılır. Tek hedefe odaklanan
    /// yüksek hasarlı build'i cezalandırır, alan hasarı talebi yaratır.
    /// </summary>
    void SpawnSplitFragments()
    {
        if (data == null || data.splitInto == null) return;

        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), 0f);
            var frag = EnemySpawner.Spawn(data.splitInto, transform.position + offset);
            if (frag == null) continue;

            // Parçalar zayıflar; yoksa bölünmek düşmanı güçlendirirdi
            var hb = frag.GetComponent<HealthBar>();
            if (hb != null)
            {
                hb.maxHealth     *= data.splitHpRatio;
                hb.currentHealth  = hb.maxHealth;
            }
        }
    }

    /// <summary>
    /// Karıştırıcıların toplam enerji kısıtı (0–1). EnergyBus üretimi bu oranda düşer.
    /// Statik sorgu: sahada kaç jammer varsa etkileri toplanır, %80'de tavanlanır.
    /// </summary>
    public static float TotalEnergyJam(Vector3 shipPos)
    {
        float jam = 0f;
        foreach (var e in FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
        {
            if (e.data == null || e.data.energyDrain <= 0f) continue;
            float d = Vector2.Distance(shipPos, e.transform.position);
            if (d > e.data.energyDrainRange) continue;
            jam += e.data.energyDrain;
        }
        return Mathf.Min(jam, 0.8f);
    }

    float ApplyShieldLayer(float amount, WeaponType wt)
    {
        float effective = ApplyResistances(amount, wt,
            data.shieldResistances != null && data.shieldResistances.Length > 0
                ? data.shieldResistances
                : DefaultShieldResistances);

        _shieldRechargeTimer = ShieldRechargeDelay;

        if (_shieldHP >= effective)
        {
            _shieldHP -= effective;
            SyncShieldBar();
            RefreshShieldVisual();
            return 0f;
        }

        float overflow = effective - _shieldHP;
        _shieldHP = 0f;
        SyncShieldBar();
        RefreshShieldVisual();
        return ApplyResistances(overflow, wt, data.hullResistances);
    }

    // Kalkan direnci tanımlanmamışsa kullanılan oyun sabitleri
    static readonly DamageModifier[] DefaultShieldResistances =
    {
        new DamageModifier { weaponType = WeaponType.Kinetic, multiplier = 1.5f  },
        new DamageModifier { weaponType = WeaponType.Laser,   multiplier = 0.25f },
    };

    static float ApplyResistances(float amount, WeaponType wt, DamageModifier[] mods)
    {
        if (mods == null) return amount;
        foreach (var m in mods)
            if (m.weaponType == wt) return amount * m.multiplier;
        return amount;
    }

    void UpdateShieldRecharge()
    {
        if (_shieldHP >= _maxShieldHP) return;
        _shieldRechargeTimer -= Time.deltaTime;
        if (_shieldRechargeTimer > 0f) return;

        _shieldHP = Mathf.Min(_shieldHP + ShieldRechargeRate * Time.deltaTime, _maxShieldHP);
        SyncShieldBar();
        RefreshShieldVisual();
    }

    void SyncShieldBar()
    {
        if (_healthBar != null) _healthBar.currentShield = _shieldHP;
    }

    void SpawnDebris()
    {
        // Parçalanma görsel efekti
        if (data != null)
            DeathEffect.Spawn(transform.position, data.bodyColor, data.bodyWidth, data.bodyHeight);

        // Toplam miktar = tehdit puanı × levelin drop oranı.
        // Eskiden sabit ×4'tü; gelir bu yüzden yalnızca wave bütçesiyle büyüyordu
        // ve 100. levelde 125× düşman spawn etmek gerekirdi. Artık iki bileşen
        // ayrı: bütçe yavaş (×1.018/level), düşman başına değer hızlı (×1.031).
        float perThreat   = BalanceConfig.Instance.DropPerThreat(GameProgress.CurrentLevel);
        float total       = (data != null ? data.threatScore : 1) * perThreat;
        var   resType     = data != null ? data.debrisResourceType : ResourceType.RawMaterial;
        int   pieceCount  = Random.Range(2, 5);

        // Toplam miktarı rastgele boyutlarda parçalara böl
        float[] weights = new float[pieceCount];
        float   wSum    = 0f;
        for (int i = 0; i < pieceCount; i++) { weights[i] = Random.value + 0.1f; wSum += weights[i]; }

        for (int i = 0; i < pieceCount; i++)
        {
            float amount = total * (weights[i] / wSum);
            var   go     = new GameObject("Debris");
            go.transform.position = transform.position;
            var d = go.AddComponent<Debris>();
            d.Init(Random.insideUnitCircle.normalized * Random.Range(0.3f, 1.2f), amount, resType);
        }

        SpawnCrystalDebris();
    }

    /// <summary>
    /// Kalkanı olan her gemi, gövde enkazına ek olarak enerji kristali bırakır —
    /// kalkan teknolojisi kristal tabanlıdır. Miktar kalkan kapasitesiyle orantılıdır,
    /// yani bölüm HP çarpanı arttıkça kristal getirisi de artar.
    /// </summary>
    void SpawnCrystalDebris()
    {
        if (data == null || data.maxShield <= 0f) return;

        float amount = data.maxShield * CrystalPerShieldPoint;
        if (amount < 0.5f) return;

        var go = new GameObject("Debris_Crystal");
        go.transform.position = transform.position;
        go.AddComponent<Debris>().Init(
            Random.insideUnitCircle.normalized * Random.Range(0.3f, 1.2f),
            amount, ResourceType.EnergyCrystal);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Oyuncu gemisiyle fiziksel çarpışma yok; düşmanlar üstünden geçer
    }

    // ── Görsel kurulum ────────────────────────────────────────────────────────

    void BuildBody(int w, int h, Color c)
    {
        var tex = MakeTex(w, h, c);
        var body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = data.sizeOrder;
    }

    void BuildBarrel(Color barrelColor)
    {
        var root = new GameObject("Barrel");
        root.transform.SetParent(transform, false);
        _barrelTransform = root.transform;

        var barrelSR = root.AddComponent<SpriteRenderer>();
        barrelSR.sprite       = Sprite.Create(MakeTex(18, 3, barrelColor),
                                    new Rect(0, 0, 18, 3), new Vector2(0f, 0.5f), 100f);
        barrelSR.sortingOrder = data.sizeOrder + 1;

        var fillGO = new GameObject("ReloadFill");
        fillGO.transform.SetParent(root.transform, false);
        fillGO.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        _reloadFillTransform = fillGO.transform;
        _reloadFillTransform.localScale = new Vector3(0f, 1f, 1f);

        _reloadFillSR = fillGO.AddComponent<SpriteRenderer>();
        _reloadFillSR.sprite       = Sprite.Create(MakeTex(18, 2, Color.white),
                                         new Rect(0, 0, 18, 2), new Vector2(0f, 0.5f), 100f);
        _reloadFillSR.sortingOrder = data.sizeOrder + 2;
        _reloadFillSR.color        = ReloadColor(0f);
    }

    void BuildShieldVisual(int w, int h)
    {
        _shieldVisual = new GameObject("ShieldVisual");
        _shieldVisual.transform.SetParent(transform, false);
        var sr = _shieldVisual.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(MakeTex(w, h, Color.white),
                              new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = data.sizeOrder + 1;
        sr.color        = new Color(0.3f, 0.75f, 1f, 0.55f);
    }

    void RefreshShieldVisual()
    {
        if (_shieldVisual == null) return;
        if (_shieldHP <= 0f) { _shieldVisual.SetActive(false); return; }
        _shieldVisual.SetActive(true);
        var sr = _shieldVisual.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = new Color(0.3f, 0.75f, 1f, (_shieldHP / _maxShieldHP) * 0.55f);
    }

    static Texture2D MakeTex(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h);
        var px  = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
