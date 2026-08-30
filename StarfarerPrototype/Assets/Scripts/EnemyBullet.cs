using UnityEngine;

/// <summary>
/// Düşman mermisi. İki mod:
///   Hull   — yön vektörüyle hareket eder, PlayerShip tag'ına çarptığında kalkan üzerinden hasar verir.
///   Komponent — hedef komponente doğru yönelir, yakına gelince doğrudan TakeDamage çağırır.
/// bypassShields = true ise hull modunda da kalkanı atlar, doğrudan gövde hasarı verir.
/// </summary>
public class EnemyBullet : MonoBehaviour
{
    public float              speed           = 5f;
    public float              damage          = 8f;
    public ShipComponentBase  targetComponent;     // null → hull modu
    public bool               bypassShields   = false;

    Vector2 _dir;
    bool    _hitHandled; // aynı frame'de çift trigger'ı önler

    static readonly Color ColHull      = new Color(1f,   0.35f, 0.1f,  1f); // turuncu
    static readonly Color ColComponent = new Color(0.8f, 0.1f,  0.9f,  1f); // mor

    void Awake()
    {
        // Awake'de sadece sprite — targetComponent henüz set edilmemiş olabilir.
        // Collider/Rigidbody ve renk düzeltmesi Start()'ta yapılır.
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.EnemyBullet, 8, 8, ColHull); // Start'ta gerekirse düzeltilir
        sr.sortingOrder = 20;
    }

    void Start()
    {
        // targetComponent bu noktada kesin olarak set edilmiştir
        bool isCompBullet = targetComponent != null;

        if (isCompBullet)
        {
            GetComponent<SpriteRenderer>().sprite =
                SkinLibrary.Get(SkinId.EnemyBulletComponent, 8, 8, ColComponent);
            // Komponent mermisi: collider yok — yalnızca proximity ile çarpar
        }
        else
        {
            var col    = gameObject.AddComponent<CircleCollider2D>();
            col.radius    = 0.06f;
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
                HitEffect.SpawnImpact(transform.position, toTarget.normalized,
                                      targetComponent.transform.position,
                                      ImpactSurface.Component, damage);
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
        if (_hitHandled) return;

        // ── Kalkan küresi (gövde kutusundan daha geniş alan) ──────────────────
        if (other.GetComponent<ShieldSphereCollider>() != null)
        {
            // Bypass veya kalkan pasifse geçir — gövde collider'ına ulaşsın
            if (bypassShields) return;
            if (!ShieldGeneratorComponent.AnyShieldActive()) return;

            var ship = other.GetComponentInParent<PlayerShip>();
            if (ship == null) return;

            ShieldEffect.Spawn(transform.position, ship.transform.position);
            // Kalkan küresinin merkezi geminin merkezidir; normal doğrudan
            // küre yüzeyinin normali olur, kıvılcım kabuktan sekiyormuş gibi çıkar.
            HitEffect.SpawnImpact(transform.position, _dir, ship.transform.position,
                                  ImpactSurface.Shield, damage);
            ship.TakeDamage(damage, bypassShields: false);
            _hitHandled = true;
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Player"))
        {
            var ship = other.GetComponent<PlayerShip>();
            if (ship == null) return;

            // Kalkan aktifse ve bypass yoksa efekt göster
            // (gövde box'u kalkan küresini kaçıran mermiler için de güvence)
            bool onShield = !bypassShields && ShieldGeneratorComponent.AnyShieldActive();
            if (onShield)
                ShieldEffect.Spawn(transform.position, ship.transform.position);

            ship.TakeDamage(damage, bypassShields);
            HitEffect.SpawnImpact(transform.position, _dir, ship.transform.position,
                                  onShield ? ImpactSurface.Shield : ImpactSurface.Hull,
                                  damage);
            _hitHandled = true;
            Destroy(gameObject);
            return;
        }

        var collector = other.GetComponent<CollectorShip>();
        if (collector != null) { collector.TakeDamage(damage); HitSmallCraft(other); return; }

        var fighter = other.GetComponent<FighterShip>();
        if (fighter != null) { fighter.TakeDamage(damage); HitSmallCraft(other); }
    }

    /// <summary>
    /// Toplayıcı/savaşçı vuruşunun ortak kuyruğu: kıvılcım, çift-vuruş kilidi,
    /// mermiyi yok et. Hasar ÇAĞIRANDA kalır — ikisinin ortak bir tipi yok ve
    /// tek ortak nokta bu üç satır.
    /// </summary>
    void HitSmallCraft(Collider2D other)
    {
        HitEffect.SpawnImpact(transform.position, _dir, other.transform.position,
                              ImpactSurface.Hull, damage);
        _hitHandled = true;
        Destroy(gameObject);
    }
}
