using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven boss gemisi. BossShipData'dan kendini kurar.
/// Herhangi bir wave'de spawn edilebilir (boss bölümü, elite, yancı).
///
/// Faz sistemi: HP eşiklerine göre ateş hızı artar, drone spawn sıklaşır,
/// son fazda hedefe yaklaşmaya çalışır.
/// Hardpoint'ler yok edildikçe ilgili yetenek devre dışı kalır.
/// </summary>
public class BossShip : MonoBehaviour
{
    public BossShipData data;

    // Bileşenler
    ShipMovement   _movement;
    PlayerShip     _playerShip;
    HealthBar      _healthBar;

    // Hull
    float _hullHP;
    float _maxHullHP;

    // Kalkan (ShieldGenerator hardpoint yaşıyorken aktif)
    float _shieldHP;
    float _maxShieldHP  = 300f;
    float _shieldRechargeTimer;
    const float ShieldRechargeDelay = 5f;
    const float ShieldRechargeRate  = 8f;
    GameObject  _shieldVisual;

    // Hardpoint'ler
    readonly List<BossHardpoint> _hardpoints = new();

    // Fazlar
    int         _currentPhaseIndex = -1;
    BossPhase   _currentPhase;

    // Ateş zamanlayıcıları (her Cannon/Laser hardpoint için ayrı)
    readonly Dictionary<BossHardpoint, float> _fireTimers = new();

    // Drone spawn
    float _droneSpawnTimer;
    bool  _dead;

    // Hareket
    float _targetY;
    float _yChangeTimer;

    // ── Kurulum ───────────────────────────────────────────────────────────────

    void Awake()
    {
        var rb         = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        _movement      = gameObject.AddComponent<ShipMovement>();
    }

    void Start()
    {
        if (data == null) data = BossShipData.CreateCarrierCommand();

        _playerShip = FindFirstObjectByType<PlayerShip>();

        _movement.mass        = data.mass;
        _movement.enginePower = data.enginePower;
        _movement.Initialize(180f);

        _maxHullHP = data.maxHP;
        _hullHP    = data.maxHP;

        BuildBody();
        BuildHardpoints();
        CheckShieldState();

        _targetY       = transform.position.y;
        _yChangeTimer  = Random.Range(2f, 5f);

        ApplyPhase(0);
    }

    void BuildBody()
    {
        // Collider
        var col    = gameObject.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(data.bodyWidth / 100f, data.bodyHeight / 100f);

        // HP barı
        _healthBar               = gameObject.AddComponent<HealthBar>();
        _healthBar.maxHealth     = _maxHullHP;
        _healthBar.currentHealth = _hullHP;
        _healthBar.barWidth      = data.bodyWidth / 100f * 1.4f;
        _healthBar.barOffsetY    = data.bodyHeight / 100f * 0.9f;

        // Görsel gövde
        var tex  = MakeTex(data.bodyWidth, data.bodyHeight, data.bodyColor);
        var body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex,
                              new Rect(0, 0, data.bodyWidth, data.bodyHeight),
                              new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 0;
    }

    void BuildHardpoints()
    {
        float hw = data.bodyWidth  / 100f * 0.5f;
        float hh = data.bodyHeight / 100f * 0.5f;

        foreach (var def in data.hardpoints)
        {
            var go = new GameObject($"HP_{def.label}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                def.localOffset.x * hw,
                def.localOffset.y * hh,
                0f);

            var hp = go.AddComponent<BossHardpoint>();
            hp.Init(def);
            hp.OnDestroyed += OnHardpointDestroyed;
            _hardpoints.Add(hp);

            if (def.type == HardpointType.Cannon || def.type == HardpointType.Laser)
                _fireTimers[hp] = Random.Range(0f, def.fireRate);
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (UpgradeUI.IsPaused || _dead) return;

        UpdateMovement();
        UpdatePhase();
        UpdateWeapons();
        UpdateDrones();
        UpdateShield();
    }

    void UpdateMovement()
    {
        _yChangeTimer -= Time.deltaTime;
        if (_yChangeTimer <= 0f)
        {
            _yChangeTimer = Random.Range(3f, 6f);
            _targetY      = Random.Range(-data.verticalRange, data.verticalRange);
        }

        float targetX = _currentPhase.rushPlayer && _playerShip != null
            ? _playerShip.transform.position.x + 4f
            : data.preferredX;

        var target = new Vector2(targetX, _targetY);
        _movement.MoveToward(target);
    }

    void UpdatePhase()
    {
        float hpRatio = _hullHP / _maxHullHP;

        // Fazlar hpThreshold'a göre küçükten büyüğe kontrol edilir
        for (int i = data.phases.Length - 1; i >= 0; i--)
        {
            if (hpRatio <= data.phases[i].hpThreshold && _currentPhaseIndex != i)
            {
                ApplyPhase(i);
                break;
            }
        }
    }

    void ApplyPhase(int index)
    {
        if (index < 0 || index >= data.phases.Length) return;
        _currentPhaseIndex = index;
        _currentPhase      = data.phases[index];
        _droneSpawnTimer   = _currentPhase.spawnInterval;
    }

    void UpdateWeapons()
    {
        if (_playerShip == null) return;

        var keys = new List<BossHardpoint>(_fireTimers.Keys);
        foreach (var hp in keys)
        {
            if (!hp.IsAlive) continue;

            float baseRate = hp.def.fireRate;
            float rate     = baseRate / _currentPhase.fireRateMultiplier;

            _fireTimers[hp] -= Time.deltaTime;
            if (_fireTimers[hp] <= 0f)
            {
                _fireTimers[hp] = rate;
                FireHardpoint(hp);
            }
        }
    }

    void FireHardpoint(BossHardpoint hp)
    {
        if (_playerShip == null) return;

        var dir = ((Vector2)_playerShip.transform.position - (Vector2)hp.transform.position).normalized;

        var go = new GameObject("BossBullet");
        go.transform.position = hp.transform.position;
        var eb    = go.AddComponent<EnemyBullet>();
        eb.damage = hp.def.fireDamage;
        eb.speed  = hp.def.bulletSpeed;
        eb.SetDirection(dir);
    }

    void UpdateDrones()
    {
        if (!HasAliveDroneBay()) return;
        if (_currentPhase.spawnPool == null || _currentPhase.spawnPool.Length == 0) return;

        _droneSpawnTimer -= Time.deltaTime;
        if (_droneSpawnTimer > 0f) return;

        _droneSpawnTimer = _currentPhase.spawnInterval;
        SpawnDrone();
    }

    bool HasAliveDroneBay()
    {
        foreach (var hp in _hardpoints)
            if (hp.IsAlive && hp.Type == HardpointType.DroneBay)
                return true;
        return false;
    }

    void SpawnDrone()
    {
        var pool = _currentPhase.spawnPool;
        var type = pool[Random.Range(0, pool.Length)];

        // Boss'un alt veya üst kenarından çıkar
        float side  = Random.value > 0.5f ? 1f : -1f;
        var   pos   = (Vector2)transform.position
                    + new Vector2(Random.Range(-1f, 0f), side * (data.bodyHeight / 100f * 0.6f));

        var go = new GameObject($"Drone_{type.displayName}");
        go.transform.position = pos;
        go.AddComponent<HealthBar>();
        go.AddComponent<EnemyBot>().data = type;
    }

    void UpdateShield()
    {
        bool genAlive = false;
        foreach (var hp in _hardpoints)
            if (hp.IsAlive && hp.Type == HardpointType.ShieldGenerator)
                { genAlive = true; break; }

        if (!genAlive)
        {
            if (_shieldVisual != null) _shieldVisual.SetActive(false);
            return;
        }

        if (_shieldHP < _maxShieldHP)
        {
            _shieldRechargeTimer -= Time.deltaTime;
            if (_shieldRechargeTimer <= 0f)
                _shieldHP = Mathf.Min(_shieldHP + ShieldRechargeRate * Time.deltaTime, _maxShieldHP);
        }

        RefreshShieldVisual();
    }

    void CheckShieldState()
    {
        bool hasShieldGen = false;
        foreach (var def in data.hardpoints)
            if (def.type == HardpointType.ShieldGenerator)
                { hasShieldGen = true; break; }

        if (!hasShieldGen) return;

        _shieldHP    = _maxShieldHP;
        BuildShieldVisual();

        if (_healthBar != null)
        {
            _healthBar.maxShield     = _maxShieldHP;
            _healthBar.currentShield = _shieldHP;
        }
    }

    // ── Hasar alma ────────────────────────────────────────────────────────────

    public void TakeDamage(float amount, WeaponType wt = WeaponType.Kinetic)
    {
        if (_dead) return;

        bool shieldGenAlive = false;
        foreach (var hp in _hardpoints)
            if (hp.IsAlive && hp.Type == HardpointType.ShieldGenerator)
                { shieldGenAlive = true; break; }

        if (shieldGenAlive && _shieldHP > 0f)
        {
            amount = AbsorbShield(amount, wt);
            if (amount <= 0f) return;
        }

        _hullHP = Mathf.Max(0f, _hullHP - amount);
        if (_healthBar != null) _healthBar.TakeDamage(amount);

        if (_hullHP <= 0f) Die();
    }

    float AbsorbShield(float amount, WeaponType wt)
    {
        float mult = wt == WeaponType.Kinetic ? 1.5f :
                     wt == WeaponType.Laser   ? 0.25f : 1f;
        float eff  = amount * mult;
        _shieldRechargeTimer = ShieldRechargeDelay;

        if (_shieldHP >= eff)
        {
            _shieldHP -= eff;
            SyncShieldBar();
            return 0f;
        }

        float overflow = eff - _shieldHP;
        _shieldHP = 0f;
        SyncShieldBar();
        return overflow;
    }

    void SyncShieldBar()
    {
        if (_healthBar != null) _healthBar.currentShield = _shieldHP;
    }

    void OnHardpointDestroyed(BossHardpoint hp)
    {
        // Kalkan jeni yok olunca kalkan da düşsün
        if (hp.Type == HardpointType.ShieldGenerator)
        {
            _shieldHP = 0f;
            SyncShieldBar();
        }
    }

    void Die()
    {
        if (_dead) return;
        _dead = true;

        // Tüm hardpoint'leri de yok et
        foreach (var hp in _hardpoints)
            if (hp.IsAlive) hp.TakeDamage(99999f);

        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        // Arka arkaya patlama efektleri
        for (int i = 0; i < 8; i++)
        {
            var go = new GameObject("Debris");
            go.transform.position = transform.position
                + (Vector3)Random.insideUnitCircle * (data.bodyWidth / 100f * 0.6f);
            var d = go.AddComponent<Debris>();
            d.Init(Random.insideUnitCircle.normalized * Random.Range(0.5f, 2f),
                   Random.Range(8f, 20f));
            yield return new WaitForSeconds(0.18f);
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (data.contactDamage <= 0f) return;
        other.GetComponent<PlayerShip>()?.TakeDamage(data.contactDamage);
    }

    // ── Görsel yardımcılar ────────────────────────────────────────────────────

    void BuildShieldVisual()
    {
        int sw = data.bodyWidth  + 30;
        int sh = data.bodyHeight + 20;

        _shieldVisual = new GameObject("ShieldVisual");
        _shieldVisual.transform.SetParent(transform, false);
        var sr = _shieldVisual.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(MakeTex(sw, sh, Color.white),
                        new Rect(0, 0, sw, sh), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 1;
        sr.color        = new Color(0.3f, 0.75f, 1f, 0.45f);
    }

    void RefreshShieldVisual()
    {
        if (_shieldVisual == null) return;
        _shieldVisual.SetActive(_shieldHP > 0f);
        var sr = _shieldVisual.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = new Color(0.3f, 0.75f, 1f, (_shieldHP / _maxShieldHP) * 0.45f);
    }

    static Texture2D MakeTex(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h);
        var px  = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        tex.SetPixels(px); tex.Apply();
        return tex;
    }
}
