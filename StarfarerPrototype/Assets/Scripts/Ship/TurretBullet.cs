using UnityEngine;

/// <summary>
/// Turret mermisi.
/// Normal mod: sabit yön, ömür sonunda yok olur.
/// Güdümlü mod (Roket): her frame hedef EnemyBot'a yönelir.
/// </summary>
public class TurretBullet : MonoBehaviour
{
    public float     damage;
    public float     speed;
    public WeaponType weaponType = WeaponType.Kinetic;
    public bool      isGuided;
    public EnemyBot  guidedTarget;

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

    public void SetDirection(Vector2 dir) => _dir = dir.normalized;

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
            // Hedef yoksa mevcut yönde serbest uçuş — Destroy'a Destroy(go, bulletLifeTime) halleder
            if (guidedTarget != null)
                _dir = ((Vector2)guidedTarget.transform.position - (Vector2)transform.position).normalized;
        }

        transform.Translate(_dir * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (DamageUtil.TryDamage(other, damage, weaponType))
            Destroy(gameObject);
    }
}
