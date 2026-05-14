using System.Collections.Generic;
using UnityEngine;

public enum EnemyType { Swarm, Armored, Shield, Bomber }

/// <summary>
/// Tüm temel düşman tiplerini tek sınıfta toplar.
/// Swarm/Armored/Shield: ShipBrain taktik state machine + ShipMovement fizik hareketi.
/// Bomber: kendi Approaching→Hovering→Retreating state machine'ini kullanır.
///
/// Hedefleme: her 1.5sn en yakın tehdidi (PlayerShip veya FighterShip) tarar.
/// Böylece oyuncu Fighter'ları çıkardığında dogfight oluşur.
/// </summary>
public class EnemyBot : MonoBehaviour
{
    public EnemyType enemyType = EnemyType.Swarm;

    PlayerShip   _playerShip;
    HealthBar    _healthBar;
    ShipMovement _movement;
    ShipBrain    _brain;

    float _contactDamage;

    // Kalkan botu
    float      _shieldHP;
    float      _maxShieldHP;
    GameObject _shieldVisual;

    // Ateş etme
    float _fireTimer;

    // Hedef güncelleme
    float _targetScanTimer;

    // Bomber state machine
    enum BomberPhase { Approaching, Hovering, Retreating }
    BomberPhase _bomberPhase;
    float       _bomberFireTimer;
    int         _bomberShotsLeft;
    Vector2     _bomberHoverPos;
    const float BomberHoverX  = 2.0f;
    const int   BomberShotMax = 3;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

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
        _healthBar  = GetComponent<HealthBar>();
        _playerShip = FindFirstObjectByType<PlayerShip>();

        switch (enemyType)
        {
            case EnemyType.Swarm:
                _movement.mass        = 1f;
                _movement.enginePower = 3f;
                Apply(hp:30, contact:20f, w:60, h:40, new Color(0.9f, 0.20f, 0.20f));
                _fireTimer = Random.Range(2f, 5f);
                SetupBrain(CombatPattern.Orbit,    engageRange:6f, fireRange:4f,   orbitRadius:4.5f, engageDuration:5f);
                break;

            case EnemyType.Armored:
                _movement.mass        = 5f;
                _movement.enginePower = 7.5f;
                Apply(hp:80, contact:25f, w:80, h:55, new Color(0.42f, 0.45f, 0.50f));
                _fireTimer = Random.Range(3f, 7f);
                SetupBrain(CombatPattern.HoverFire, engageRange:5f, fireRange:3.5f, orbitRadius:4f,   engageDuration:8f);
                break;

            case EnemyType.Shield:
                _movement.mass        = 3f;
                _movement.enginePower = 6f;
                Apply(hp:50, contact:20f, w:70, h:50, new Color(0.25f, 0.35f, 0.85f));
                _shieldHP = _maxShieldHP = 40f;
                BuildShieldVisual(90, 65);
                _fireTimer = Random.Range(1f, 3f);
                SetupBrain(CombatPattern.Orbit,    engageRange:5.5f, fireRange:3.5f, orbitRadius:3.5f, engageDuration:5f);
                break;

            case EnemyType.Bomber:
                _movement.mass        = 2f;
                _movement.enginePower = 7f;
                Apply(hp:40, contact:0f, w:52, h:52, new Color(0.9f, 0.50f, 0.10f));
                _bomberPhase     = BomberPhase.Approaching;
                _bomberFireTimer = 1.2f;
                _bomberShotsLeft = BomberShotMax;
                _bomberHoverPos  = new Vector2(BomberHoverX, transform.position.y);
                break;
        }

        _movement.Initialize(180f);
    }

    void SetupBrain(CombatPattern pattern, float engageRange, float fireRange,
                    float orbitRadius, float engageDuration)
    {
        _brain                  = gameObject.AddComponent<ShipBrain>();
        _brain.pattern          = pattern;
        _brain.engageRange      = engageRange;
        _brain.fireRange        = fireRange;
        _brain.orbitRadius      = orbitRadius;
        _brain.engageDuration   = engageDuration;
        _brain.repositionDelay  = 1f;
    }

    // -------------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------------

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (enemyType == EnemyType.Bomber)
        {
            UpdateBomber();
            return;
        }

        // Hedef tara
        _targetScanTimer -= Time.deltaTime;
        if (_targetScanTimer <= 0f || !_brain.HasTarget)
        {
            _targetScanTimer = 1.5f;
            var threat = FindClosestThreat();
            if (threat != null)
                _brain.SetTarget(threat);
        }

        // Ateş et — ShipBrain ateş menziline girince
        if (_brain.InFireRange)
        {
            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                _fireTimer = FireRate() + Random.Range(0f, 1.5f);
                FireAtTarget();
            }
        }

        // Ekran dışına çıkınca yok et
        if (Vector2.Distance(transform.position, Vector2.zero) > 30f)
            Destroy(gameObject);
    }

    // En yakın tehdidi bulur: PlayerShip veya FighterShip
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

    void UpdateBomber()
    {
        switch (_bomberPhase)
        {
            case BomberPhase.Approaching:
                _movement.MoveToward(_bomberHoverPos);
                if (_movement.IsNear(_bomberHoverPos, 0.2f))
                {
                    _bomberPhase     = BomberPhase.Hovering;
                    _bomberFireTimer = 1.2f;
                }
                break;

            case BomberPhase.Hovering:
                _movement.Brake();
                _bomberFireTimer -= Time.deltaTime;
                if (_bomberFireTimer <= 0f)
                {
                    FireAtComponent();
                    _bomberShotsLeft--;
                    _bomberFireTimer = 1.8f;
                    if (_bomberShotsLeft <= 0)
                        _bomberPhase = BomberPhase.Retreating;
                }
                break;

            case BomberPhase.Retreating:
                _movement.MoveInDirection(Vector2.right);
                break;
        }

        float x = transform.position.x;
        if (x < -15f || x > 20f) Destroy(gameObject);
    }

    // -------------------------------------------------------------------------
    // Ateş etme
    // -------------------------------------------------------------------------

    float FireRate()
    {
        switch (enemyType)
        {
            case EnemyType.Swarm:   return 4f;
            case EnemyType.Armored: return 6f;
            case EnemyType.Shield:  return 3f;
            default:                return 5f;
        }
    }

    float FireDamage()
    {
        switch (enemyType)
        {
            case EnemyType.Swarm:   return 8f;
            case EnemyType.Armored: return 15f;
            case EnemyType.Shield:  return 6f;
            default:                return 8f;
        }
    }

    float BulletSpeed()
    {
        switch (enemyType)
        {
            case EnemyType.Swarm:   return 3f;
            case EnemyType.Armored: return 2f;
            case EnemyType.Shield:  return 3.5f;
            default:                return 2.5f;
        }
    }

    void FireAtTarget()
    {
        Transform t = _brain?.TargetTransform;
        if (t == null) t = _playerShip?.transform;
        if (t == null) return;

        var dir = (t.position - transform.position).normalized;
        var go  = new GameObject("EnemyBullet");
        go.transform.position = transform.position;
        var eb  = go.AddComponent<EnemyBullet>();
        eb.damage = FireDamage();
        eb.speed  = BulletSpeed();
        eb.SetDirection(dir);
    }

    void FireAtComponent()
    {
        var all        = FindObjectsByType<ShipComponentBase>(FindObjectsSortMode.None);
        var operational = new List<ShipComponentBase>();
        foreach (var c in all)
            if (c.IsOperational) operational.Add(c);
        if (operational.Count == 0) return;

        var target = operational[Random.Range(0, operational.Count)];
        var go     = new GameObject("EnemyBullet_Comp");
        go.transform.position = transform.position;
        var eb = go.AddComponent<EnemyBullet>();
        eb.damage          = 18f;
        eb.speed           = 2.25f;
        eb.targetComponent = target;
    }

    // -------------------------------------------------------------------------
    // Hasar alma
    // -------------------------------------------------------------------------

    public void TakeDamage(float amount, WeaponType weaponType = WeaponType.Kinetic)
    {
        if (_healthBar == null) return;
        float hull = CalcDamage(amount, weaponType);
        _healthBar.TakeDamage(hull);
        if (_healthBar.currentHealth <= 0f)
            Destroy(gameObject);
    }

    float CalcDamage(float amount, WeaponType wt)
    {
        switch (enemyType)
        {
            case EnemyType.Armored:
                return wt == WeaponType.Kinetic ? amount * 0.30f :
                       wt == WeaponType.Plasma  ? amount * 1.80f : amount;
            case EnemyType.Shield:
                return ApplyShieldLayer(amount, wt);
            default:
                return wt == WeaponType.Laser ? amount * 1.5f : amount;
        }
    }

    float ApplyShieldLayer(float amount, WeaponType wt)
    {
        if (_shieldHP <= 0f) return amount;
        float shieldMult = wt == WeaponType.Kinetic ? 1.5f :
                           wt == WeaponType.Laser   ? 0.25f : 1f;
        float effective  = amount * shieldMult;
        if (_shieldHP >= effective)
        {
            _shieldHP -= effective;
            RefreshShieldVisual();
            return 0f;
        }
        float overflow = effective - _shieldHP;
        _shieldHP = 0f;
        RefreshShieldVisual();
        return overflow;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || _contactDamage <= 0f) return;
        var ship = other.GetComponent<PlayerShip>();
        ship?.TakeDamage(_contactDamage);
        Destroy(gameObject);
    }

    // -------------------------------------------------------------------------
    // Görsel kurulum
    // -------------------------------------------------------------------------

    void Apply(float hp, float contact, int w, int h, Color color)
    {
        _contactDamage = contact;
        if (_healthBar != null)
        {
            _healthBar.maxHealth     = hp;
            _healthBar.currentHealth = hp;
            _healthBar.barWidth      = w / 100f * 1.3f;
            _healthBar.barOffsetY    = h / 100f * 0.8f;
        }
        GetComponent<BoxCollider2D>().size = new Vector2(w / 100f, h / 100f);
        BuildBody(w, h, color);
    }

    void BuildBody(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h);
        var px  = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        tex.SetPixels(px); tex.Apply();

        var body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = Vector3.one;

        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 0;
    }

    void BuildShieldVisual(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var px  = new Color[w * h];
        var c   = new Color(0.3f, 0.75f, 1f, 0.4f);
        for (int i = 0; i < px.Length; i++) px[i] = c;
        tex.SetPixels(px); tex.Apply();

        _shieldVisual = new GameObject("ShieldVisual");
        _shieldVisual.transform.SetParent(transform, false);
        _shieldVisual.transform.localPosition = Vector3.zero;
        _shieldVisual.transform.localScale    = Vector3.one;

        var sr = _shieldVisual.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 1;
    }

    void RefreshShieldVisual()
    {
        if (_shieldVisual == null) return;
        if (_shieldHP <= 0f) { _shieldVisual.SetActive(false); return; }
        var sr = _shieldVisual.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = new Color(0.3f, 0.75f, 1f, (_shieldHP / _maxShieldHP) * 0.5f);
    }
}
