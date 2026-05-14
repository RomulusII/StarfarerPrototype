using UnityEngine;

/// <summary>
/// Düşman mermisi. İki mod:
///   Hull   — yön vektörüyle hareket eder, PlayerShip tag'ına çarptığında kalkan üzerinden hasar verir.
///   Komponent — hedef komponente doğru yönelir, yakına gelince doğrudan TakeDamage çağırır.
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    public float              speed           = 5f;
    public float              damage          = 8f;
    public ShipComponentBase  targetComponent;     // null → hull modu

    Vector2 _dir;

    static readonly Color ColHull      = new Color(1f,   0.35f, 0.1f,  1f); // turuncu
    static readonly Color ColComponent = new Color(0.8f, 0.1f,  0.9f,  1f); // mor

    void Awake()
    {
        bool isCompBullet = targetComponent != null;
        Color c = isCompBullet ? ColComponent : ColHull;

        var tex    = new Texture2D(8, 8);
        var pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
        tex.SetPixels(pixels);
        tex.Apply();

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 3;

        if (!isCompBullet)
        {
            var col    = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.06f;
            col.isTrigger = true;

            var rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType    = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
        }

        Destroy(gameObject, 10f);
    }

    /// <summary>Hull modu için hareket yönünü ayarlar.</summary>
    public void SetDirection(Vector2 dir) => _dir = dir.normalized;

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (targetComponent != null)
        {
            if (targetComponent == null || !targetComponent.IsOperational)
            {
                Destroy(gameObject);
                return;
            }

            var toTarget = targetComponent.transform.position - transform.position;
            if (toTarget.magnitude < 0.22f)
            {
                targetComponent.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
            transform.position += toTarget.normalized * speed * Time.deltaTime;
        }
        else
        {
            transform.Translate(_dir * speed * Time.deltaTime, Space.World);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (targetComponent != null) return;

        if (other.CompareTag("Player"))
        {
            var ship = other.GetComponent<PlayerShip>();
            if (ship == null) return;
            ship.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        var collector = other.GetComponent<CollectorShip>();
        if (collector != null) { collector.TakeDamage(damage); Destroy(gameObject); return; }

        var fighter = other.GetComponent<FighterShip>();
        if (fighter != null) { fighter.TakeDamage(damage); Destroy(gameObject); }
    }
}
