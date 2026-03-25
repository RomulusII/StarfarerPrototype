using UnityEngine;

/// <summary>
/// Ateşlenen mermi. Kendi yerel yukarı yönünde ileri gider,
/// 3 saniye sonra otomatik olarak yok olur.
/// </summary>
public class Bullet : MonoBehaviour
{
    public float speed = 8f;

    void Awake()
    {
        // Trigger collider — EnemyBot ile çarpışma tespiti için
        BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.1f, 0.3f); // sprite boyutu: 10x30 @ ppu=100

        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
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
        EnemyBot enemy = other.GetComponent<EnemyBot>();
        if (enemy != null)
        {
            enemy.TakeDamage(10f);
            Destroy(gameObject);
        }
    }
}
