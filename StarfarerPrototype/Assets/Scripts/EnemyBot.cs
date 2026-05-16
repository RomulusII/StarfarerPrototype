using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven düşman gemisi. Tüm istatistikler ve davranış parametreleri
/// EnemyTypeData ScriptableObject'inden okunur — yeni tip eklemek kod değişikliği gerektirmez.
///
/// data null ise EnemySpawner tarafından atanmadan önce Start() bekler;
/// null kalırsa CreateSwarm() ile fallback oluşturulur.
/// </summary>
public class EnemyBot : MonoBehaviour
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
    ApproachPhase _approachPhase;
    float         _approachFireTimer;
    int           _approachShotsLeft;
    Vector2       _approachHoverPos;
    const float   ApproachHoverX  = 2.0f;
    const int     ApproachShotMax = 3;

    // BombRun state
    float       _bombRunFireTimer;
    const float BombRunSpeed = 1.5f;

    // Velocity tracking
    Vector2        _prevPos;
    public Vector2 Velocity { get; private set; }

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

        ApplyStats();

        if (data.maxShield > 0f)
            InitShield();

        _fireRateBase = data.fireRate;
        _fireTimer    = Random.Range(0f, _fireRateBase);
        _prevPos      = transform.position;

        InitMovement();

        bool needsBarrel = data.weaponKind != EnemyWeaponKind.None
                        && data.weaponKind != EnemyWeaponKind.ComponentBurst
                        && data.movementKind != EnemyMovementKind.Approach
                        && data.movementKind != EnemyMovementKind.BombRun;
        if (needsBarrel)
            BuildBarrel(data.barrelColor);

        _movement.Initialize(180f);
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

        GetComponent<BoxCollider2D>().size = new Vector2(data.bodyWidth / 100f, data.bodyHeight / 100f);
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
                SetupBrain(CombatPattern.Orbit,
                    data.engageRange, data.fireRange, data.orbitRadius, data.engageDuration);
                break;

            case EnemyMovementKind.Stationary:
                break;

            case EnemyMovementKind.BombRun:
                _bombRunFireTimer = _fireRateBase * 0.5f;
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
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (Time.deltaTime > 0f)
            Velocity = ((Vector2)transform.position - _prevPos) / Time.deltaTime;
        _prevPos = transform.position;

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
                _movement.MoveToward(_approachHoverPos);
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
                        _approachPhase = ApproachPhase.Retreating;
                }
                break;

            case ApproachPhase.Retreating:
                _movement.MoveInDirection(Vector2.right);
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
        var go  = new GameObject("EnemyBullet");
        go.transform.position = _barrelTransform != null
            ? _barrelTransform.position + _barrelTransform.right * 0.18f
            : transform.position;

        var eb    = go.AddComponent<EnemyBullet>();
        eb.damage = data.fireDamage;
        eb.speed  = data.bulletSpeed;
        eb.SetDirection(dir);

        _fireFlash = true;
        if (_reloadFillSR != null)
            _reloadFillSR.color = new Color(1f, 1f, 0.8f, 0.95f);
        if (_reloadFillTransform != null)
            _reloadFillTransform.localScale = new Vector3(1f, 1f, 1f);
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

    // ── Hasar alma ────────────────────────────────────────────────────────────

    public void TakeDamage(float amount, WeaponType weaponType = WeaponType.Kinetic)
    {
        if (_healthBar == null) return;

        float hull = data.maxShield > 0f && _shieldHP > 0f
            ? ApplyShieldLayer(amount, weaponType)
            : ApplyResistances(amount, weaponType, data.hullResistances);

        _healthBar.TakeDamage(hull);
        if (_healthBar.currentHealth <= 0f)
        {
            SpawnDebris();
            Destroy(gameObject);
        }
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
        int count = Random.Range(2, 5);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Debris");
            go.transform.position = transform.position;
            var d = go.AddComponent<Debris>();
            d.Init(Random.insideUnitCircle.normalized * Random.Range(0.3f, 1.2f),
                   Random.Range(5f, 15f));
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || _contactDamage <= 0f) return;
        other.GetComponent<PlayerShip>()?.TakeDamage(_contactDamage);
        Destroy(gameObject);
    }

    // ── Görsel kurulum ────────────────────────────────────────────────────────

    void BuildBody(int w, int h, Color c)
    {
        var tex = MakeTex(w, h, c);
        var body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 0;
    }

    void BuildBarrel(Color barrelColor)
    {
        var root = new GameObject("Barrel");
        root.transform.SetParent(transform, false);
        _barrelTransform = root.transform;

        var barrelSR = root.AddComponent<SpriteRenderer>();
        barrelSR.sprite       = Sprite.Create(MakeTex(18, 3, barrelColor),
                                    new Rect(0, 0, 18, 3), new Vector2(0f, 0.5f), 100f);
        barrelSR.sortingOrder = 2;

        var fillGO = new GameObject("ReloadFill");
        fillGO.transform.SetParent(root.transform, false);
        fillGO.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        _reloadFillTransform = fillGO.transform;
        _reloadFillTransform.localScale = new Vector3(0f, 1f, 1f);

        _reloadFillSR = fillGO.AddComponent<SpriteRenderer>();
        _reloadFillSR.sprite       = Sprite.Create(MakeTex(18, 2, Color.white),
                                         new Rect(0, 0, 18, 2), new Vector2(0f, 0.5f), 100f);
        _reloadFillSR.sortingOrder = 3;
        _reloadFillSR.color        = ReloadColor(0f);
    }

    void BuildShieldVisual(int w, int h)
    {
        _shieldVisual = new GameObject("ShieldVisual");
        _shieldVisual.transform.SetParent(transform, false);
        var sr = _shieldVisual.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(MakeTex(w, h, Color.white),
                              new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 1;
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
