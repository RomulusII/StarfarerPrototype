using UnityEngine;

/// <summary>
/// Turret mermisi.
/// Normal mod: sabit yön, ömür sonunda yok olur.
/// Güdümlü mod (Roket): sınırlı dönüş hızıyla hedefe yönelir — organik yay çizer.
/// </summary>
public class TurretBullet : MonoBehaviour
{
    public float      damage;
    public float      speed;
    public WeaponType weaponType  = WeaponType.Kinetic;
    public bool       isGuided;
    public EnemyBot   guidedTarget;
    [Tooltip("Saniyede derece — roketin maksimum dönüş hızı.")]
    public float      turnRate    = 120f;

    Vector2 _dir;

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

    EnemyBot FindClosestEnemy()
    {
        var all   = FindObjectsByType<EnemyBot>(FindObjectsSortMode.None);
        EnemyBot best  = null;
        float    bestD = float.MaxValue;
        foreach (var e in all)
        {
            float d = Vector2.Distance(e.transform.position, transform.position);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (isGuided)
        {
            if (guidedTarget == null)
                guidedTarget = FindClosestEnemy();

            if (guidedTarget != null)
            {
                Vector2 desired  = ((Vector2)guidedTarget.transform.position
                                   - (Vector2)transform.position).normalized;
                float   maxTurn  = turnRate * Time.deltaTime;
                float   angle    = Vector2.SignedAngle(_dir, desired);
                float   clamped  = Mathf.Clamp(angle, -maxTurn, maxTurn);
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
            Destroy(bomb.gameObject);
            Destroy(gameObject);
            return;
        }

        if (DamageUtil.TryDamage(other, damage, weaponType))
            Destroy(gameObject);
    }
}
