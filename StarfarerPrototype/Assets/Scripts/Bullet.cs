using UnityEngine;

/// <summary>
/// Ateşlenen mermi. Kendi yerel yukarı yönünde ileri gider,
/// menzili kadar uçtuktan sonra otomatik olarak yok olur.
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

    /// <summary>
    /// Ateşlendiği andaki kadraj genişliği (0 = dinlenme, 1 = tam zoom-out).
    /// <see cref="boostAtFire"/> ile aynı gerekçeyle mermiyle TAŞINIR: isabet
    /// anındaki zoom başka bir şeydir, oysa nişan ateş anındaki kadrajda
    /// alındı. İsabet oranını zoom'a göre ayırmanın tek doğru anahtarı bu.
    /// </summary>
    public float zoomAtFire;

    /// <summary>
    /// Ömür (sn). 0 ya da altı: menzilden türet (varsayılan). Kayıttan kurulan
    /// mermi buraya KALAN süreyi yazar.
    /// </summary>
    public float lifeTime;

    float _bornAt;
    bool  _started;

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
        if (lifeTime <= 0f)
            lifeTime = ViewBounds.MaxShotRange / Mathf.Max(speed, 0.01f);

        _bornAt  = Time.time;
        _started = true;
        Destroy(gameObject, lifeTime);
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
                      .Num("zoom",   zoomAtFire)
                      .Bool("oldurdu", lethal)
                      .End();

            HitEffect.SpawnImpact(transform.position, transform.up, other.transform.position,
                                  surface, damage, lethal);
            if (surface == ImpactSurface.Shield)
                DamageUtil.ShieldFlash(other, transform.position);
            Destroy(gameObject);
        }
    }

    // ── Kayıt ─────────────────────────────────────────────────────────────────

    /// <summary>Start'ı çalışmamış merminin ömrü henüz türetilmedi: 0 = türet.</summary>
    float LifeLeft => _started ? Mathf.Max(0.01f, lifeTime - (Time.time - _bornAt)) : lifeTime;

    public PlayerBulletState CaptureState() => new PlayerBulletState
    {
        pos        = transform.position,
        rotation   = transform.eulerAngles.z,
        scale      = transform.localScale.x,
        speed      = speed,
        damage     = damage,
        zoom       = zoomAtFire,
        life       = LifeLeft,
        weaponType = (int)weaponType,
        boost      = (int)boostAtFire,
    };

    /// <summary>
    /// Görsel WeaponController.SpawnBullet ile aynı çağrıdan gelir — ana silahın
    /// mermi tanesi yalnızca kinetik silahta var.
    /// </summary>
    public static Bullet Rebuild(PlayerBulletState s)
    {
        var go = new GameObject("Bullet");
        go.transform.SetPositionAndRotation(s.pos, Quaternion.Euler(0f, 0f, s.rotation));
        go.transform.localScale = Vector3.one * s.scale;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.PlayerBulletKinetic, 10, 30, Color.white);
        sr.sortingOrder = 20;

        var b = go.AddComponent<Bullet>();
        b.speed       = s.speed;
        b.damage      = s.damage;
        b.weaponType  = (WeaponType)s.weaponType;
        b.boostAtFire = (BoostMode)s.boost;
        b.zoomAtFire  = s.zoom;
        b.lifeTime    = s.life;
        return b;
    }
}
