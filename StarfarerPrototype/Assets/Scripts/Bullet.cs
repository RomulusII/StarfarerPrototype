using UnityEngine;

/// <summary>
/// Ateşlenen mermi. Kendi yerel yukarı yönünde ileri gider,
/// 3 saniye sonra otomatik olarak yok olur.
/// </summary>
public class Bullet : MonoBehaviour
{
    public float      speed      = 8f;
    public float      damage     = 10f;
    public WeaponType weaponType = WeaponType.Kinetic;

    void Awake()
    {
        BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.1f, 0.3f);

        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    void Start()
    {
        Destroy(gameObject, 3f);
    }

    void Update()
    {
        transform.Translate(Vector3.up * speed * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (DamageUtil.TryDamage(other, damage, weaponType))
        {
            Vector2 surfaceNormal = ((Vector2)transform.position - (Vector2)other.transform.position).normalized;
            HitEffect.SpawnSparks(transform.position, transform.up, surfaceNormal);
            Destroy(gameObject);
        }
    }
}
