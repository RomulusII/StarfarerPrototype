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

    /// <summary>
    /// Ateşlendiği andaki boost modu — denge kaydı için. ANLIK moda bakılamaz:
    /// mermi yolda giderken oyuncu boost'u kapatabilir, oysa bu merminin boyutu
    /// ve hasarı ateşlendiği anda belirlendi. İsabet oranı boost'a göre
    /// ayrıştırılacaksa (mermi boyutu ×0.6 ile ×1.5 arasında değişiyor) etiketin
    /// mermiyle birlikte TAŞINMASI gerekir.
    /// </summary>
    public BoostMode boostAtFire = BoostMode.None;

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

            // İsabet oranının payı. Payda shot_fired'dır: ıskalayan mermi ömrü
            // dolunca sessizce yok olur, yani "ateşlendi ama isabet yok" farkı
            // ıskalamayı verir. Işınlar bu sayıma GİRMEZ — ıskalamazlar.
            BalanceLog.Event("shot_hit")
                      .Str("kaynak", "ana")
                      .Str("silah",  weaponType.ToString())
                      .Str("boost",  boostAtFire.ToString())
                      .Str("yuzey",  surface.ToString())
                      .Str("hedef",  DamageUtil.TypeNameOf(other))
                      .Num("hasar",  damage)
                      .Bool("oldurdu", lethal)
                      .End();

            HitEffect.SpawnImpact(transform.position, transform.up, other.transform.position,
                                  surface, damage, lethal);
            if (surface == ImpactSurface.Shield)
                DamageUtil.ShieldFlash(other, transform.position);
            Destroy(gameObject);
        }
    }
}
