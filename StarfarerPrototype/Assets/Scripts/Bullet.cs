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
        // Ömür MENZİLDEN türer, sabit değil. 3 saniye + 6 hız = 18 birimlik bir
        // menzil demekti; kadraj ise zoom-out'ta 32 birime açılıyor, yani mermi
        // ekranın ortasında buharlaşıyordu.
        Destroy(gameObject, ViewBounds.MaxShotRange / Mathf.Max(speed, 0.01f));
    }

    void Update()
    {
        transform.Translate(Vector3.up * speed * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Yüzey hasardan ÖNCE okunur: bu vuruş kalkanı düşürecekse bile
        // çarpmanın kendisi kalkana olmuştur.
        var surface = DamageUtil.SurfaceOf(other);

        if (DamageUtil.TryDamage(other, damage, weaponType))
        {
            bool lethal = other.GetComponent<HealthBar>()?.currentHealth <= 0f;
            HitEffect.SpawnImpact(transform.position, transform.up, other.transform.position,
                                  surface, damage, lethal);
            if (surface == ImpactSurface.Shield)
                DamageUtil.ShieldFlash(other, transform.position);
            Destroy(gameObject);
        }
    }
}
