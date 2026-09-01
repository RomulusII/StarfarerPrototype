using UnityEngine;

/// <summary>
/// Yavaş hareket eden, Point Defence tarafından vurulabilen bomba mermisi.
/// BombRunner düşmanı tarafından fırlatılır; düz çizgide ilerler, yüksek hasar verir.
/// </summary>
public class Bomb : MonoBehaviour, ITurretTarget
{
    public float speed  = 2.5f;
    public float damage = 30f;
    public float hp     = 1f;

    Vector2 _dir;

    static readonly Color BombColor = new Color(1f, 0.35f, 0f);

    // Çerçeve bombanın kendi renginden AÇIK: bombanın üstünde okunması gerek,
    // ona karışması değil.
    static readonly Color MarkerColor = new Color(1f, 0.85f, 0.45f);

    void Awake()
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.Bomb, 14, 14, BombColor);
        sr.sortingOrder = 3;

        var col    = gameObject.AddComponent<CircleCollider2D>();
        col.radius    = 0.10f;
        col.isTrigger = true;

        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Bomba oyundaki TEK vurulabilir mermi. Düşman mermisiyle neredeyse aynı
        // renkte (ikisi de sıcak turuncu) olduğu için oyuncu hangisinin
        // durdurulabileceğini göremiyordu; yanıp sönen çerçeve o farkı söyler.
        ShootableMarker.Attach(transform, 0.30f, MarkerColor, sortingOrder: 2);

        Destroy(gameObject, 8f);
    }

    public Vector2 Velocity => _dir * speed;

    // ── ITurretTarget ─────────────────────────────────────────────────────────
    // Bomba tek vuruşta yok olur; öldürme maliyeti sembolik tutulur ki
    // puanlamada mesafe ve aciliyet belirleyici olsun.

    public Transform TargetTransform        => transform;
    public Vector2   TargetVelocity         => Velocity;
    public bool      IsValidTarget          => this != null && isActiveAndEnabled;
    public float     ThreatValue            => Mathf.Max(1f, damage / 2f);
    public PointDefenceClass PdClass => PointDefenceClass.Munition;

    public float RawDamageToKill(WeaponType weaponType) => 1f;

    public float ArmorValue => 0f;   // bomba zırhsız — her atış tam geçer

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
            var ship = other.GetComponent<PlayerShip>();
            if (ship == null) return;

            // Bombanın kendi kalkan küresi dalı yok — kalkanın içinden geçip
            // gövdede patlar, ama hasarı yine kalkandan geçer (TakeDamage).
            // Efekt de bunu izlemeli: kalkan ayaktaysa kalkan patlaması.
            bool onShield = ShieldGeneratorComponent.AnyShieldActive();
            if (onShield)
                ShieldEffect.Spawn(transform.position, ship.transform.position);

            ship.TakeDamage(damage);
            HitEffect.SpawnImpact(transform.position, _dir, ship.transform.position,
                                  onShield ? ImpactSurface.Shield : ImpactSurface.Hull,
                                  damage);
            Destroy(gameObject);
            return;
        }

        var collector = other.GetComponent<CollectorShip>();
        if (collector != null)
        {
            collector.TakeDamage(damage);
            HitEffect.SpawnImpact(transform.position, _dir, other.transform.position,
                                  ImpactSurface.Hull, damage);
            Destroy(gameObject);
        }
    }
}
