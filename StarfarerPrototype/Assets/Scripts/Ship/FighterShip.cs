using UnityEngine;

/// <summary>
/// Hangardan üretilen savaş gemisi.
/// Yakın düşmanı hedefler, ateş eder; tehdit yoksa hangar etrafında devriye gezer.
/// Hareket ShipMovement üzerinden yapılır; mass sabit, enginePower speed parametresinden türetilir.
/// </summary>
public class FighterShip : MonoBehaviour
{
    enum Phase { Patrolling, Attacking }

    public float maxHP    = 40f;
    public float currentHP = 40f;
    public float fireRate  = 2f;
    public float damage    = 8f;

    Phase        _phase = Phase.Patrolling;
    EnemyBot     _target;
    Transform    _hangar;
    ShipMovement _movement;
    float        _fireTimer;
    Vector3      _patrolPoint;

    const float AttackRange  = 7f;
    const float FireRange    = 4f;
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

        _movement      = gameObject.AddComponent<ShipMovement>();
        _movement.mass = Mass;
    }

    public void Init(Transform hangar, float speed, float maxHP, float fireRate, float damage)
    {
        _hangar               = hangar;
        this.maxHP            = maxHP;
        this.currentHP        = maxHP;
        this.fireRate         = fireRate;
        this.damage           = damage;
        _movement.enginePower = speed * Mass;   // MaxSpeed ≈ speed
        _movement.Initialize(0f);
        _fireTimer   = Random.Range(0f, fireRate);
        _patrolPoint = GetPatrolPoint();
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        switch (_phase)
        {
            case Phase.Patrolling:
                _movement.MoveToward(_patrolPoint);
                if (Vector2.Distance(transform.position, _patrolPoint) < 0.35f)
                    _patrolPoint = GetPatrolPoint();

                var nearest = FindClosestEnemy(AttackRange);
                if (nearest != null) { _target = nearest; _phase = Phase.Attacking; }
                break;

            case Phase.Attacking:
                if (_target == null || !_target.gameObject.activeInHierarchy)
                { _target = null; _phase = Phase.Patrolling; break; }

                float dist = Vector2.Distance(transform.position, _target.transform.position);
                if (dist > AttackRange + 2f) { _target = null; _phase = Phase.Patrolling; break; }

                if (dist > FireRange)
                    _movement.MoveToward(_target.transform.position);
                else
                    _movement.Brake();

                _fireTimer -= Time.deltaTime;
                if (_fireTimer <= 0f && dist <= FireRange)
                {
                    FireAt(_target);
                    _fireTimer = fireRate;
                }
                break;
        }
    }

    Vector3 GetPatrolPoint()
    {
        Vector3 anchor = _hangar != null ? _hangar.position : Vector3.zero;
        float   ang    = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float   r      = Random.Range(PatrolRadius * 0.5f, PatrolRadius);
        return anchor + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
    }

    EnemyBot FindClosestEnemy(float maxRange)
    {
        var      all   = FindObjectsByType<EnemyBot>(FindObjectsSortMode.None);
        EnemyBot best  = null;
        float    bestD = maxRange;
        foreach (var e in all)
        {
            float d = Vector2.Distance(transform.position, e.transform.position);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    void FireAt(EnemyBot target)
    {
        var dir = ((Vector3)target.transform.position - transform.position).normalized;
        var go  = new GameObject("FighterBullet");
        go.transform.position = transform.position;

        var tb        = go.AddComponent<TurretBullet>();
        tb.damage     = damage;
        tb.speed      = 5f;
        tb.weaponType = WeaponType.Kinetic;
        tb.isGuided   = false;
        tb.SetDirection(dir);

        var tex = MakeTex(8, 4, Color.yellow);
        var sr  = go.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, 8, 4), new Vector2(0f, 0.5f), 100f);
        sr.sortingOrder = 3;

        Destroy(go, 1.5f);
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;
        if (currentHP <= 0f) Destroy(gameObject);
    }

    void BuildVisual()
    {
        var tex = new Texture2D(22, 10);
        var px  = new Color[22 * 10];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0.85f, 0.75f, 0.20f);
        tex.SetPixels(px); tex.Apply();
        var sr        = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, 22, 10), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 5;
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
