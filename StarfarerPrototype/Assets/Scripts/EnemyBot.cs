using System.Collections.Generic;
using UnityEngine;

public enum EnemyType { Swarm, Armored, Shield, Bomber }

/// <summary>
/// Tüm temel düşman tiplerini tek sınıfta toplar.
///
/// Swarm   — hızlı, zayıf, lazer'e karşı kırılgan. Uzaktan ateş eder.
/// Armored — yavaş, sağlam, kinetike dirençli. Uzaktan ateş eder.
/// Shield  — orta hız, kalkanı var (kinetik kırar, lazer geçemez). Uzaktan ateş eder.
/// Bomber  — yaklaşır, geminin içine girer, komponentlere doğrudan ateş eder.
///
/// Awake'te sadece fizik; görseller + istatistikler Start'ta enemyType'a göre kurulur.
/// Spawner, AddComponent'ten sonra enemyType'ı set eder (Start henüz çalışmaz).
/// </summary>
public class EnemyBot : MonoBehaviour
{
    public EnemyType enemyType = EnemyType.Swarm;

    // Ortak
    float _speed;
    float _contactDamage;
    float _targetY;
    PlayerShip _playerShip;
    HealthBar  _healthBar;

    // Kalkan botu
    float _shieldHP;
    float _maxShieldHP;
    GameObject _shieldVisual;

    // Ateş etme (Swarm/Armored/Shield)
    float _fireTimer;

    // Bomber state machine
    enum BomberPhase { Approaching, Hovering, Retreating }
    BomberPhase _bomberPhase = BomberPhase.Approaching;
    float _bomberFireTimer;
    int   _bomberShotsLeft;
    const float BomberHoverX  = 2.0f;
    const int   BomberShotMax = 3;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        var col = gameObject.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, 0.4f);

        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    void Start()
    {
        _healthBar  = GetComponent<HealthBar>();
        _playerShip = FindFirstObjectByType<PlayerShip>();
        _targetY    = _playerShip != null ? _playerShip.transform.position.y : 0f;

        switch (enemyType)
        {
            case EnemyType.Swarm:
                Apply(hp:30,  speed:3f,   contact:20f, w:60, h:40, new Color(0.9f,  0.20f, 0.20f));
                _fireTimer = Random.Range(2f, 5f);
                break;
            case EnemyType.Armored:
                Apply(hp:80,  speed:1.5f, contact:25f, w:80, h:55, new Color(0.42f, 0.45f, 0.50f));
                _fireTimer = Random.Range(3f, 7f);
                break;
            case EnemyType.Shield:
                Apply(hp:50,  speed:2f,   contact:20f, w:70, h:50, new Color(0.25f, 0.35f, 0.85f));
                _shieldHP = _maxShieldHP = 40f;
                BuildShieldVisual(90, 65);
                _fireTimer = Random.Range(1f, 3f);
                break;
            case EnemyType.Bomber:
                Apply(hp:40,  speed:3.5f, contact:0f,  w:52, h:52, new Color(0.9f,  0.50f, 0.10f));
                _bomberFireTimer = 1.2f;
                _bomberShotsLeft = BomberShotMax;
                break;
        }
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

        // Ortak hareket (Swarm / Armored / Shield)
        var pos = transform.position;
        pos.x -= _speed * Time.deltaTime;
        pos.y  =  Mathf.MoveTowards(pos.y, _targetY, _speed * 0.3f * Time.deltaTime);
        transform.position = pos;

        if (pos.x < -15f) { Destroy(gameObject); return; }

        // Ateş etme
        _fireTimer -= Time.deltaTime;
        if (_fireTimer <= 0f)
        {
            _fireTimer = FireRate() + Random.Range(0f, 1.5f);
            FireAtHull();
        }
    }

    void UpdateBomber()
    {
        var pos = transform.position;

        switch (_bomberPhase)
        {
            case BomberPhase.Approaching:
                pos.x -= _speed * Time.deltaTime;
                if (pos.x <= BomberHoverX)
                {
                    _bomberPhase     = BomberPhase.Hovering;
                    _bomberFireTimer = 1.2f;
                }
                break;

            case BomberPhase.Hovering:
                // Dikey salınım
                pos.y += Mathf.Sin(Time.time * 3.5f) * 0.025f;
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
                pos.x -= _speed * 1.8f * Time.deltaTime;
                break;
        }

        transform.position = pos;
        if (pos.x < -15f) Destroy(gameObject);
    }

    // -------------------------------------------------------------------------
    // Ateş etme yardımcıları
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
            case EnemyType.Swarm:   return 6f;
            case EnemyType.Armored: return 4f;
            case EnemyType.Shield:  return 7f;
            default:                return 5f;
        }
    }

    void FireAtHull()
    {
        if (_playerShip == null) return;
        var dir = (_playerShip.transform.position - transform.position).normalized;

        var go = new GameObject("EnemyBullet");
        go.transform.position = transform.position;
        var eb = go.AddComponent<EnemyBullet>();
        eb.damage = FireDamage();
        eb.speed  = BulletSpeed();
        eb.SetDirection(dir);
    }

    void FireAtComponent()
    {
        var all = FindObjectsByType<ShipComponentBase>(FindObjectsSortMode.None);
        var operational = new List<ShipComponentBase>();
        foreach (var c in all)
            if (c.IsOperational) operational.Add(c);

        if (operational.Count == 0) return;

        var target = operational[Random.Range(0, operational.Count)];
        var go = new GameObject("EnemyBullet_Comp");
        go.transform.position = transform.position;
        var eb = go.AddComponent<EnemyBullet>();
        eb.damage          = 18f;
        eb.speed           = 4.5f;
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

            default: // Swarm + Bomber
                return wt == WeaponType.Laser ? amount * 1.5f : amount;
        }
    }

    float ApplyShieldLayer(float amount, WeaponType wt)
    {
        if (_shieldHP <= 0f) return amount;

        float shieldMult = wt == WeaponType.Kinetic ? 1.5f :
                           wt == WeaponType.Laser   ? 0.25f : 1f;
        float effective = amount * shieldMult;

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

    // -------------------------------------------------------------------------
    // Temas hasarı
    // -------------------------------------------------------------------------

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (_contactDamage <= 0f) return;

        var ship = other.GetComponent<PlayerShip>();
        ship?.TakeDamage(_contactDamage);
        Destroy(gameObject);
    }

    // -------------------------------------------------------------------------
    // Görsel kurulum
    // -------------------------------------------------------------------------

    void Apply(float hp, float speed, float contact, int w, int h, Color color)
    {
        _speed         = speed;
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
        tex.SetPixels(px);
        tex.Apply();

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
        tex.SetPixels(px);
        tex.Apply();

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
