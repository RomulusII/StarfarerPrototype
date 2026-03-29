using UnityEngine;

/// <summary>
/// Basit kırmızı düşman botu.
/// Sağdan sola ilerler, PlayerShip'in Y pozisyonuna yavaşça yönelir.
/// Bullet çarpınca hasar alır, can bitince yok olur.
/// </summary>
public class EnemyBot : MonoBehaviour
{
    public float speed = 2f;

    HealthBar _healthBar;
    float _targetY;

    void Awake()
    {
        // Görsel: 60x40 px, renk (0.9, 0.2, 0.2)
        Texture2D tex = new Texture2D(60, 40);
        Color[] pixels = new Color[60 * 40];
        Color botColor = new Color(0.9f, 0.2f, 0.2f);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = botColor;
        tex.SetPixels(pixels);
        tex.Apply();

        GameObject body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = Vector3.one;

        SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 60, 40), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 0;

        // BoxCollider2D — lokal boyut sprite ile eşleşir (60/100=0.6, 40/100=0.4)
        BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, 0.4f);

        // Kinematic Rigidbody2D — trigger tespiti için zorunlu, fizik etkisi yok
        Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    void Start()
    {
        _healthBar = GetComponent<HealthBar>();

        PlayerShip player = FindFirstObjectByType<PlayerShip>();
        _targetY = player != null ? player.transform.position.y : 0f;
    }

    void Update()
    {
        Vector3 pos = transform.position;
        pos.x -= speed * Time.deltaTime;
        pos.y  = Mathf.MoveTowards(pos.y, _targetY, speed * 0.3f * Time.deltaTime);
        transform.position = pos;

        if (pos.x < -15f)
            Destroy(gameObject);
    }

    public void TakeDamage(float amount)
    {
        if (_healthBar == null) return;
        _healthBar.TakeDamage(amount);
        if (_healthBar.currentHealth <= 0f)
            Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        PlayerShip playerShip = other.GetComponent<PlayerShip>();
        if (playerShip != null)
        {
            playerShip.TakeDamage(20f);
        }
        else
        {
            HealthBar playerHealth = other.GetComponent<HealthBar>();
            if (playerHealth != null)
                playerHealth.TakeDamage(20f);
        }

        Destroy(gameObject);
    }
}
