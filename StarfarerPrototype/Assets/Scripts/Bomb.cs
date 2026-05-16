using UnityEngine;

/// <summary>
/// Yavaş hareket eden, Point Defence tarafından vurulabilen bomba mermisi.
/// BombRunner düşmanı tarafından fırlatılır; düz çizgide ilerler, yüksek hasar verir.
/// </summary>
public class Bomb : MonoBehaviour
{
    public float speed  = 2.5f;
    public float damage = 30f;
    public float hp     = 1f;

    Vector2 _dir;

    static readonly Color BombColor = new Color(1f, 0.35f, 0f);

    void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(MakeTex(14, 14, BombColor),
                              new Rect(0, 0, 14, 14), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 3;

        var col    = gameObject.AddComponent<CircleCollider2D>();
        col.radius    = 0.10f;
        col.isTrigger = true;

        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        Destroy(gameObject, 8f);
    }

    public Vector2 Velocity => _dir * speed;

    public void SetDirection(Vector2 dir) => _dir = dir.normalized;

    public void TakeDamage(float amount)
    {
        hp -= amount;
        if (hp <= 0f) Destroy(gameObject);
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;
        transform.Translate(_dir * speed * Time.deltaTime, Space.World);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerShip>()?.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        var collector = other.GetComponent<CollectorShip>();
        if (collector != null) { collector.TakeDamage(damage); Destroy(gameObject); }
    }

    static Texture2D MakeTex(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h);
        var px  = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
