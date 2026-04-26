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

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (isGuided)
        {
            if (guidedTarget == null) { Destroy(gameObject); return; }
            _dir = ((Vector2)guidedTarget.transform.position - (Vector2)transform.position).normalized;
        }

        transform.Translate(_dir * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponent<EnemyBot>();
        if (enemy == null) return;
        enemy.TakeDamage(damage, weaponType);
        Destroy(gameObject);
    }
}
