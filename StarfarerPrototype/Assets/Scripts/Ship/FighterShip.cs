using UnityEngine;

/// <summary>
/// Hangardan üretilen savaş gemisi. Strafe pattern ile düşmanlara saldırır.
/// Hedef yoksa hangar etrafında devriye gezer.
/// ShipBrain taktik hareketini, ShipMovement fiziğini yönetir.
///
/// Dogfight: kıvrak EnemyBot'lar da FighterShip'i tehdit olarak tarar,
/// birbirine yörünge çizerek savaşırlar (bkz. EnemyTypeData.PursuesFighters —
/// ağır gemiler peşine düşmez).
///
/// Hedef seçimi ITurretTarget üzerinden yapılır: düşmanlar ÖNCELİKLİDİR, ama
/// menzilde düşman yoksa asteroitler de vurulur. Asteroit ateş etmez fakat
/// sürüklenip ana gemiye çarpar ve boş gezen bir savaşçının onu parçalamaması
/// için hiçbir sebep yok — üstelik parçalanan asteroit kaynak bırakır.
/// </summary>
public class FighterShip : MonoBehaviour
{
    public float maxHP    = 40f;
    public float currentHP = 40f;
    public float fireRate  = 2f;
    public float damage    = 8f;

    ITurretTarget _currentTarget;
    Transform    _hangar;
    ShipMovement _movement;
    ShipBrain    _brain;
    float        _fireTimer;
    float        _targetScanTimer;
    Vector3      _patrolPoint;

    const float AttackRange  = 7f;
    const float FireRange    = 3f;
    const float PatrolRadius = 2.5f;
    const float Mass         = 1f;

    void Awake()
    {
        BuildVisual();

        var col    = gameObject.AddComponent<CircleCollider2D>();
        col.radius = 0.18f;
        col.isTrigger = true;

        var rb          = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        _movement         = gameObject.AddComponent<ShipMovement>();
        _movement.mass    = Mass;
        _movement.agility = 1.4f;   // küçük avcı: dar kavis
        _movement.grip    = 0.94f;
        _movement.wanderAngle  = 12f;  // dogfight'ta salınarak uçar
        _movement.wanderPeriod = 1.6f;

        _brain                = gameObject.AddComponent<ShipBrain>();
        _brain.pattern        = CombatPattern.Strafe;
        _brain.engageRange    = 5f;
        _brain.fireRange      = FireRange;
        _brain.orbitRadius    = 3.5f;
        _brain.engageDuration = 3f;
        _brain.repositionDelay = 0.8f;
        _brain.leashToCombatArea = true;   // dogfight'ta ekrandan çıkmasın
    }

    public void Init(Transform hangar, float speed, float maxHP, float fireRate, float damage)
    {
        _hangar               = hangar;
        this.maxHP            = maxHP;
        this.currentHP        = maxHP;
        this.fireRate         = fireRate;
        this.damage           = damage;
        _movement.enginePower = speed * Mass;
        _movement.Initialize(0f);
        _fireTimer   = Random.Range(0f, fireRate);
        _patrolPoint = GetPatrolPoint();
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        // Hedef tara
        _targetScanTimer -= Time.deltaTime;
        if (_targetScanTimer <= 0f)
        {
            _targetScanTimer = 1.5f;
            var nearest = FindTarget(AttackRange);
            if (nearest != null)
            {
                _currentTarget = nearest;
                _brain.SetTarget(nearest.TargetTransform);
            }
            else if (!IsTargetAlive(_currentTarget))
            {
                _currentTarget = null;
                _brain.ClearTarget();
            }
        }

        // Hedef kaybedildiyse sıfırla
        if (!IsTargetAlive(_currentTarget))
        {
            _currentTarget = null;
            _brain.ClearTarget();
        }

        if (_brain.HasTarget)
        {
            // ShipBrain hareketi yönetir; biz sadece ateş ederiz
            _fireTimer -= Time.deltaTime;
            if (_brain.InFireRange && _fireTimer <= 0f)
            {
                FireAt(_currentTarget);
                _fireTimer = fireRate;
            }
        }
        else
        {
            // Hedef yok: hangar etrafında devriye
            _movement.MoveToward(_patrolPoint);
            if (Vector2.Distance(transform.position, _patrolPoint) < 0.35f)
                _patrolPoint = GetPatrolPoint();
        }
    }

    // -------------------------------------------------------------------------
    // Yardımcılar
    // -------------------------------------------------------------------------

    Vector3 GetPatrolPoint()
    {
        Vector3 anchor = _hangar != null ? _hangar.position : Vector3.zero;
        float   ang    = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float   r      = Random.Range(PatrolRadius * 0.5f, PatrolRadius);
        return anchor + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
    }

    static bool IsTargetAlive(ITurretTarget t)
        => t != null && t.IsValidTarget && t.TargetTransform != null;

    /// <summary>
    /// Menzildeki en yakın düşman; düşman yoksa en yakın asteroit.
    /// İki taramanın SIRALI olması bilinçli: yanı başındaki bir kaya, uzaktaki
    /// bir düşmandan daha yakın olsa bile savaşçıyı dövüşten çekmemeli.
    /// </summary>
    ITurretTarget FindTarget(float maxRange)
    {
        ITurretTarget best  = null;
        float         bestD = maxRange;

        foreach (var e in FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
        {
            if (!e.IsValidTarget) continue;
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < bestD) { bestD = d; best = e; }
        }
        if (best != null) return best;

        bestD = maxRange;
        foreach (var a in FindObjectsByType<Asteroid>(FindObjectsSortMode.None))
        {
            if (!a.IsValidTarget) continue;
            float d = Vector2.Distance(transform.position, a.transform.position);
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    void FireAt(ITurretTarget target)
    {
        if (!IsTargetAlive(target)) return;
        var dir = (target.TargetTransform.position - transform.position).normalized;
        var go  = new GameObject("FighterBullet");
        go.transform.position = transform.position;

        var tb        = go.AddComponent<TurretBullet>();
        tb.damage     = damage;
        tb.speed      = 5f;
        tb.weaponType = WeaponType.Kinetic;
        tb.isGuided   = false;
        tb.SetDirection(dir);

        var sr  = go.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.FighterBullet, 8, 4, Color.yellow,
                              new Vector2(0f, 0.5f));
        sr.sortingOrder = 3;

        Destroy(go, 1.5f);
    }

    public void SetSpeed(float speed)
    {
        _movement.enginePower = speed * Mass;
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;
        if (currentHP <= 0f) Destroy(gameObject);
    }

    void BuildVisual()
    {
        var sr        = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.Fighter, 22, 10,
                              new Color(0.85f, 0.75f, 0.20f));
        sr.sortingOrder = 5;
    }
}
