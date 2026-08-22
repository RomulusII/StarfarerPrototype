using UnityEngine;

/// <summary>
/// Mermilerle parçalanabilen asteroit. Düşman değildir — ateş edilmezse
/// sürüklenip gemiye çarpar ve hasar verir.
///
/// Kurallar:
///   - Üç boyut: Large → Medium → Small. Büyük boyutlar yok edilince bir küçüğüne bölünür.
///   - Yalnızca Small boyut enkaz (kaynak) bırakır; büyük parçalar sadece bölünür.
///   - Hız iki bileşenden oluşur: sürüklenme (korunur) + ayrılma itmesi (sönümlenir).
///     Parçalar böylece dağılıp uzaklaşmaz, ana parçanın yanıbaşında kümelenir.
///   - Kaya kinetiğe zayıf (×2), lazere dirençli (×0.25).
///   - Ana gemiye çarparsa hasar verir ve dağılır — bölünmez, kaynak bırakmaz.
///   - Hasar DamageUtil üzerinden gelir; tüm silahlar otomatik olarak işler.
/// </summary>
public class Asteroid : MonoBehaviour
{
    public enum Size { Small, Medium, Large }

    [Header("Durum")]
    public Size  size = Size.Large;
    public float hp;

    Vector2   _drift;       // ana sürüklenme — korunur, asteroidi sahnede taşır
    Vector2   _separation;  // parçalanma itmesi — sönümlenir, parçalar dağılıp gitmez
    float     _spin;
    bool      _dead;
    HealthBar _healthBar;

    public Vector2 Velocity => _drift + _separation;

    // Boyut başına: HP, sprite kenarı (px), gemiye çarpma hasarı
    const float SmallHP  = 12f, MediumHP  = 28f, LargeHP  = 60f;
    const int   SmallPx  = 22,  MediumPx  = 40,  LargePx  = 68;
    const float SmallDmg = 8f,  MediumDmg = 18f, LargeDmg = 30f;

    // Small parçalanınca düşen kaynak ve bunun kristal çıkma olasılığı.
    // Hedef: tamamen parçalanan bir BÜYÜK asteroit ≈ bir kalkanlı düşman kadar
    // kristal versin (~4 kristal). Kristal ekonomisinin ayar noktası burasıdır.
    const float SmallResourceAmount = 5f;
    const float CrystalChance       = 0.12f;

    // Bölünme
    const int   MinFragments    = 2;
    const int   MaxFragments    = 3;
    const float FragmentPush    = 0.55f; // ayrılma hızı (birim/sn) — sönümleneceği için kısa ömürlü
    const float FragmentSpread  = 0.22f; // ayrılma noktasının merkeze uzaklığı
    const float DriftInherit    = 0.5f;  // parçanın miras aldığı sürüklenme oranı
    const float SeparationDrag  = 1.2f;  // ayrılma itmesinin sönümlenme hızı (birim/sn²)

    // Silah dirençleri — kaya kinetiğe zayıf, lazere dirençli
    const float KineticMultiplier = 2.0f;
    const float LaserMultiplier   = 0.25f;

    const float DespawnX = -17f;

    static readonly Color RockColor = new Color(0.45f, 0.40f, 0.34f);

    // ── Kurulum ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Verilen konumda bir asteroit oluşturur.
    /// drift korunur; separation kısa sürede sönümlenir.
    /// </summary>
    public static Asteroid Spawn(Vector3 position, Size size, Vector2 drift,
                                 Vector2 separation = default)
    {
        var go = new GameObject("Asteroid_" + size);
        go.transform.position = position;
        var a = go.AddComponent<Asteroid>();
        a.size        = size;
        a._drift      = drift;
        a._separation = separation;
        return a;
    }

    void Awake()
    {
        _spin      = Random.Range(-35f, 35f);
        _healthBar = gameObject.AddComponent<HealthBar>();
    }

    void Start()
    {
        hp = HPFor(size);

        float span = PxFor(size) / 100f;
        _healthBar.maxHealth     = hp;
        _healthBar.currentHealth = hp;
        _healthBar.barWidth      = span * 1.2f;
        _healthBar.barOffsetY    = span * 0.75f;

        var col       = gameObject.AddComponent<CircleCollider2D>();
        col.radius    = RadiusFor(size);
        col.isTrigger = true;

        var rb          = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        BuildVisual();
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        // Ayrılma itmesi sönümlenir — parçalar biraz açılır, sonra sürüklenmeye döner
        if (_separation.sqrMagnitude > 0.000001f)
            _separation = Vector2.MoveTowards(_separation, Vector2.zero,
                                              SeparationDrag * Time.deltaTime);

        transform.position += (Vector3)(Velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, _spin * Time.deltaTime);

        if (transform.position.x < DespawnX) Destroy(gameObject);
    }

    // ── Hasar ─────────────────────────────────────────────────────────────────

    /// <summary>DamageUtil tarafından çağrılır. Kaya kinetiğe zayıf, lazere dirençli.</summary>
    public void TakeDamage(float amount, WeaponType weaponType = WeaponType.Kinetic)
    {
        if (_dead) return;

        float effective = amount * ResistanceFor(weaponType);
        hp -= effective;
        if (_healthBar != null) _healthBar.TakeDamage(effective);
        if (hp > 0f) return;

        _dead = true;
        Shatter();
        Destroy(gameObject);
    }

    /// <summary>Bir küçük boyuta bölünür; en küçük boyutsa enkaz bırakır.</summary>
    void Shatter()
    {
        DeathEffect.Spawn(transform.position, RockColor, PxFor(size), PxFor(size));

        if (size == Size.Small)
        {
            DropDebris();
            return;
        }

        Size  next      = size == Size.Large ? Size.Medium : Size.Small;
        int   count     = Random.Range(MinFragments, MaxFragments + 1);
        float baseAngle = Random.Range(0f, 360f);

        for (int i = 0; i < count; i++)
        {
            // Parçalar merkezden eşit açılarla dağılır — patlama gibi görünsün
            float   ang = (baseAngle + 360f / count * i + Random.Range(-25f, 25f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

            Spawn(transform.position + (Vector3)(dir * FragmentSpread),
                  next,
                  _drift * DriftInherit,
                  dir * (FragmentPush * Random.Range(0.7f, 1.3f)));
        }
    }

    void DropDebris()
    {
        var type = Random.value < CrystalChance
            ? ResourceType.EnergyCrystal
            : ResourceType.RawMaterial;

        var go = new GameObject(type == ResourceType.EnergyCrystal ? "Debris_Crystal" : "Debris");
        go.transform.position = transform.position;
        go.AddComponent<Debris>().Init(
            Velocity * 0.4f + Random.insideUnitCircle.normalized * Random.Range(0.15f, 0.4f),
            SmallResourceAmount, type);
    }

    // ── Gemiye çarpma ─────────────────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_dead) return;

        // Kalkan küresi — kalkan aktifse asteroit orada dağılır
        if (other.GetComponent<ShieldSphereCollider>() != null)
        {
            if (!ShieldGeneratorComponent.AnyShieldActive()) return;

            var shielded = other.GetComponentInParent<PlayerShip>();
            if (shielded == null) return;

            ShieldEffect.Spawn(transform.position, shielded.transform.position);
            HitShip(shielded);
            return;
        }

        if (!other.CompareTag("Player")) return;

        var ship = other.GetComponent<PlayerShip>();
        if (ship == null) ship = other.GetComponentInParent<PlayerShip>();
        if (ship == null) return;

        HitShip(ship);
    }

    /// <summary>Çarpma: hasar verir ve dağılır — bölünmez, kaynak bırakmaz.</summary>
    void HitShip(PlayerShip ship)
    {
        _dead = true;
        ship.TakeDamage(ImpactDamageFor(size));
        DeathEffect.Spawn(transform.position, RockColor, PxFor(size), PxFor(size));
        Destroy(gameObject);
    }

    // ── Boyut tabloları ───────────────────────────────────────────────────────

    static float HPFor(Size s)
    {
        if (s == Size.Small)  return SmallHP;
        if (s == Size.Medium) return MediumHP;
        return LargeHP;
    }

    static int PxFor(Size s)
    {
        if (s == Size.Small)  return SmallPx;
        if (s == Size.Medium) return MediumPx;
        return LargePx;
    }

    static float ImpactDamageFor(Size s)
    {
        if (s == Size.Small)  return SmallDmg;
        if (s == Size.Medium) return MediumDmg;
        return LargeDmg;
    }

    static float RadiusFor(Size s) => PxFor(s) / 100f * 0.45f;

    static float ResistanceFor(WeaponType wt)
    {
        if (wt == WeaponType.Kinetic) return KineticMultiplier;
        if (wt == WeaponType.Laser)   return LaserMultiplier;
        return 1f;
    }

    // ── Görsel ────────────────────────────────────────────────────────────────

    void BuildVisual()
    {
        int px  = PxFor(size);
        var tex = new Texture2D(px, px);
        tex.filterMode = FilterMode.Point;
        var buf = new Color[px * px];

        float c = (px - 1) * 0.5f;

        // Kenar düzensiz bir kaya silueti: yarıçap açıya göre hafif dalgalanır
        float phase = Random.Range(0f, Mathf.PI * 2f);
        float lobes = Random.Range(3f, 6f);

        for (int y = 0; y < px; y++)
        for (int x = 0; x < px; x++)
        {
            float dx   = x - c;
            float dy   = y - c;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float ang  = Mathf.Atan2(dy, dx);
            float edge = c * (0.80f + 0.16f * Mathf.Sin(ang * lobes + phase));

            if (dist > edge) { buf[y * px + x] = Color.clear; continue; }

            // Sol üstten aydınlatma + hafif doku gürültüsü
            float light = 0.75f + 0.35f * ((-dx + dy) / (c * 2f));
            float noise = Mathf.PerlinNoise(x * 0.28f, y * 0.28f) * 0.3f + 0.85f;
            float k     = Mathf.Clamp(light * noise, 0.45f, 1.35f);
            buf[y * px + x] = new Color(RockColor.r * k, RockColor.g * k, RockColor.b * k, 1f);
        }

        tex.SetPixels(buf);
        tex.Apply();

        var body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 1;
    }
}
