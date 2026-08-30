using UnityEngine;

/// <summary>
/// Turret mermisi.
/// Normal mod: sabit yön, ömür sonunda yok olur.
/// Güdümlü mod (Roket): sınırlı dönüş hızıyla hedefe yönelir — organik yay çizer.
/// guidedTarget Transform'dur; EnemyBot ve BossShip dahil her hedefi izler.
/// </summary>
public class TurretBullet : MonoBehaviour
{
    public float      damage;
    public float      speed;
    public WeaponType weaponType  = WeaponType.Kinetic;
    public bool       isGuided;
    public Transform  guidedTarget;
    [Tooltip("Saniyede derece — roketin maksimum dönüş hızı.")]
    public float      turnRate    = 120f;
    [Tooltip("0 = vurulabilir değil. Roketler için ayarlanır.")]
    public float      hp         = 0f;

    Vector2 _dir;

    public void TakeDamage(float amount)
    {
        if (hp <= 0f) return;
        hp -= amount;
        if (hp <= 0f) Destroy(gameObject);
    }

    void Awake()
    {
        var col    = gameObject.AddComponent<CircleCollider2D>();
        col.radius    = 0.07f;
        col.isTrigger = true;

        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    public void SetDirection(Vector2 dir)
    {
        _dir = dir.normalized;
        ApplyRotation();
    }

    Transform FindClosestTarget()
    {
        Transform best  = null;
        float     bestD = float.MaxValue;

        foreach (var e in FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
        {
            float d = Vector2.Distance(e.transform.position, transform.position);
            if (d < bestD) { bestD = d; best = e.transform; }
        }

        var boss = FindFirstObjectByType<BossShip>();
        if (boss != null)
        {
            float d = Vector2.Distance(boss.transform.position, transform.position);
            if (d < bestD) best = boss.transform;
        }

        return best;
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (isGuided)
        {
            if (guidedTarget == null)
                guidedTarget = FindClosestTarget();

            if (guidedTarget != null)
            {
                Vector2 desired = ((Vector2)guidedTarget.position
                                  - (Vector2)transform.position).normalized;
                float maxTurn  = turnRate * Time.deltaTime;
                float angle    = Vector2.SignedAngle(_dir, desired);
                float clamped  = Mathf.Clamp(angle, -maxTurn, maxTurn);
                _dir = Rotate(_dir, clamped).normalized;
                ApplyRotation();
            }
        }

        transform.Translate(_dir * speed * Time.deltaTime, Space.World);
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    void ApplyRotation()
    {
        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var bomb = other.GetComponent<Bomb>();
        if (bomb != null)
        {
            bomb.TakeDamage(damage);
            // Bomba tek vuruşta gider: Point Defence'in işini yaptığı görünsün
            HitEffect.SpawnImpact(transform.position, _dir, other.transform.position,
                                  ImpactSurface.Hull, damage, lethal: true);
            Destroy(gameObject);
            return;
        }

        if (DamageUtil.TryDamage(other, damage, weaponType))
        {
            bool lethal = other.GetComponent<HealthBar>()?.currentHealth <= 0f;
            HitEffect.SpawnImpact(transform.position, _dir, other.transform.position,
                                  DamageUtil.SurfaceOf(other), damage, lethal);
            Destroy(gameObject);
        }
    }
}
