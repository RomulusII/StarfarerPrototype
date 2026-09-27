using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Oyuncunun işaretlediği hedef — SEÇİLİ HEDEF modundaki silahların hedefi —
/// ve üstünde dönen nişangâh.
///
/// **Hedef sol tıkla seçilir**, ateşle aynı girdi. Tıklanan noktada bir hedef
/// varsa işaretlenir; boşluğa tıklamak işareti SİLMEZ. Silseydi elle ateş eden
/// ana silahın her atışı (öngörü için hedefin önüne, boşluğa tıklanır) seçili
/// hedef modundaki turretleri boşa düşürürdü. İşaret hedef yok olunca kalkar.
///
/// **İşaret yalnızca birileri kullanıyorsa vardır** (<see cref="FireControl.AnyAssisted"/>).
/// Hiçbir silah seçili hedef modunda değilken sahnede işe yaramayan bir
/// nişangâh görmek "bu ne yapıyor" sorusunu doğururdu.
///
/// Geçici olarak vurulamayan hedef (Hayalet'in fazı) işareti KAYBETMEZ —
/// silahlar faz bitene kadar bekler. Yalnızca hedefin kendisi yok olunca düşer.
/// </summary>
public class TargetMarker : MonoBehaviour
{
    /// <summary>Tıklamanın hedef yakalama yarıçapı (dünya birimi). Hızlı küçük gemiler
    /// tam üstlerine tıklanarak yakalanamaz.</summary>
    const float PickRadius = 0.6f;

    /// <summary>Nişangâhın hedef silüetine göre büyüklüğü.</summary>
    const float ReticlePadding = 1.35f;
    const float ReticleMin     = 0.55f;
    const float SpinSpeed      = 70f;   // derece/sn

    static readonly Color ReticleColor = new Color(1f, 0.32f, 0.22f, 0.9f);

    static TargetMarker  s_instance;
    static ITurretTarget s_current;

    /// <summary>İşaretli hedef; yoksa ya da yok edildiyse null.</summary>
    public static ITurretTarget Current
    {
        get
        {
            if (s_current != null && (s_current as Object) == null) s_current = null;
            return s_current;
        }
    }

    /// <summary>Geminin donanımı — seçili hedef modunda bir silah var mı sorusu için.</summary>
    public static ShipLoadout Loadout => s_instance != null ? s_instance._loadout : null;

    ShipLoadout    _loadout;
    bool           _wasHeld;
    Transform      _reticle;
    SpriteRenderer _reticleSr;
    float          _spin;

    static Sprite  s_reticleSprite;

    void Awake()
    {
        // Statikler sahne yeniden yüklenince hayatta kalır; eski oyunun
        // hedefi yeni oyuna sızmasın.
        s_instance = this;
        s_current  = null;
        _loadout   = GetComponent<ShipLoadout>();
        BuildReticle();
    }

    void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
        if (_reticle != null) Destroy(_reticle.gameObject);
    }

    void Update()
    {
        bool held    = PointerInput.FireHeld;
        bool pressed = held && !_wasHeld;
        _wasHeld     = held;

        if (pressed && !PointerInput.Locked && !PointerOverUI() && FireControl.AnyAssisted
            && FireControl.TryPointerWorld(out var world))
        {
            var picked = Pick(world);
            if (picked != null) s_current = picked;
        }
    }

    void LateUpdate()
    {
        var t = Current;
        bool show = t != null && FireControl.AnyAssisted;
        _reticle.gameObject.SetActive(show);
        if (!show) return;

        if (!UpgradeUI.IsPaused) _spin += SpinSpeed * Time.deltaTime;

        var tr = t.TargetTransform;
        _reticle.position   = new Vector3(tr.position.x, tr.position.y, tr.position.z - 0.2f);
        _reticle.rotation   = Quaternion.Euler(0f, 0f, _spin);
        _reticle.localScale = Vector3.one * Mathf.Max(ReticleMin, SizeOf(tr) * ReticlePadding);

        // Vurulamazken (faz) sönük: işaret duruyor ama silahlar bekliyor.
        var c = ReticleColor;
        if (!t.IsValidTarget) c.a *= 0.35f;
        _reticleSr.color = c;
    }

    // ── Seçim ─────────────────────────────────────────────────────────────────

    static ITurretTarget Pick(Vector3 world)
    {
        // Yalnızca tıklama anında çalışır; ayırdığı dizi önemsiz.
        var hits = Physics2D.OverlapCircleAll(world, PickRadius);

        ITurretTarget best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            var t = TargetOf(hits[i]);
            if (t == null) continue;
            float d = Vector2.Distance(world, t.TargetTransform.position);
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }

    /// <summary>
    /// Bir collider'ın hedefi. Kalkanlar gövdeden AYRI collider'lardır; kalkana
    /// tıklamak gemiyi seçer.
    /// </summary>
    static ITurretTarget TargetOf(Collider2D col)
    {
        if (col == null) return null;
        var barrier = col.GetComponent<BarrierShield>();
        if (barrier != null) return barrier.owner;
        var bubble = col.GetComponent<BubbleShield>();
        if (bubble != null) return bubble.owner;
        return col.GetComponentInParent<ITurretTarget>();
    }

    static bool PointerOverUI()
    {
        // Dokunma PointerInput.FireHeld içinde zaten eleniyor; fare elenmiyor.
        if (EventSystem.current == null || Mouse.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    static float SizeOf(Transform t)
    {
        var col = t.GetComponent<Collider2D>();
        if (col == null) return ReticleMin;
        var e = col.bounds.extents;
        return Mathf.Max(e.x, e.y) * 2f;
    }

    // ── Görsel ────────────────────────────────────────────────────────────────

    void BuildReticle()
    {
        var go = new GameObject("TargetReticle");
        _reticle = go.transform;   // kök — gemiyle birlikte dönmesin, hedefi izlesin
        _reticleSr = go.AddComponent<SpriteRenderer>();
        _reticleSr.sprite       = ReticleSprite();
        _reticleSr.sortingOrder = 40;
        go.SetActive(false);
    }

    void OnDisable()
    {
        if (_reticle != null) _reticle.gameObject.SetActive(false);
    }

    /// <summary>
    /// Nişangâh: dört parçaya bölünmüş bir halka ve içe bakan dört çentik.
    /// Vurulabilir mühimmatın köşe parantezinden (<see cref="ShootableMarker"/>)
    /// ve kalkan halkalarından ayrı okunmalı: kesik halka + çentik evrensel
    /// "kilitli hedef" dili. Sprite 1 dünya birimi çapında, beyaz; boyutu ve
    /// rengi çizimde verilir.
    /// </summary>
    static Sprite ReticleSprite()
    {
        if (s_reticleSprite != null) return s_reticleSprite;

        const int N = 96;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px  = new Color[N * N];
        float c = (N - 1) * 0.5f;
        float rOut = N * 0.47f, rIn = N * 0.40f;

        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = x - c, dy = y - c;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg, 90f);

            // Halka, eksenlerde 18°'lik boşluklarla dört yaya bölünür
            bool ring = r >= rIn && r <= rOut && ang > 9f && ang < 81f;

            // Eksenlerde içe bakan çentikler
            bool tick = (Mathf.Abs(dx) < 2.2f || Mathf.Abs(dy) < 2.2f)
                     && r >= N * 0.22f && r <= N * 0.36f;

            float a = ring || tick ? 1f : 0f;
            px[y * N + x] = new Color(1f, 1f, 1f, a);
        }

        tex.SetPixels(px);
        tex.Apply();
        s_reticleSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        return s_reticleSprite;
    }

    // ── Kayıt ─────────────────────────────────────────────────────────────────

    public static void Restore(ITurretTarget t) => s_current = t;
}
