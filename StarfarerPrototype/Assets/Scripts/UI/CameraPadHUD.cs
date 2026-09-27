using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ekranın sol altındaki kamera pedi: dört yön, ortada kadrajı sıfırlama,
/// yanında yaklaş / uzaklaş. Klavyedeki WASD / Q / E / C ile birebir aynı işi
/// yapar (<see cref="CameraController.ReadControls"/> ikisini birlikte okur).
///
/// Düğmeler BASILI TUTULDUKÇA çalışır, tıklamayla değil — kaydırma bir
/// süreçtir. Birden fazla düğme aynı anda tutulabilir (çapraz kaydırma,
/// kaydırırken zoom); dokunmatikte her parmak kendi düğmesini tutar.
///
/// Sol altta çünkü sağ el nişan alıyor: masaüstünde fare, telefonda sağ baş
/// parmak. Ped sol elin/sol başparmağın doğal yeri ve boost şeridinden,
/// sağ alttaki hız düğmelerinden uzak.
/// </summary>
public class CameraPadHUD : MonoBehaviour
{
    // ── Dışarıya açılan durum ─────────────────────────────────────────────────

    static Vector2 s_pan;
    static float   s_zoom;
    static bool    s_reset;
    static int     s_heldCount;

    /// <summary>Basılı yön düğmelerinin toplamı (-1..1 her eksende).</summary>
    public static Vector2 Pan => s_pan;

    /// <summary>-1 yaklaş, +1 uzaklaş, 0 yok.</summary>
    public static float Zoom => s_zoom;

    /// <summary>
    /// Pedde basılı tutulan bir düğme var mı. Fareyle pede basmak aynı zamanda
    /// sol tuştur; bu olmadan pedi tutan oyuncu ana silahı pede doğru ateşlerdi
    /// (bkz. PointerInput.FireHeld).
    /// </summary>
    public static bool AnyHeld => s_heldCount > 0;

    /// <summary>Sıfırlama isteği bir kez okunur.</summary>
    public static bool ConsumeReset()
    {
        bool r = s_reset;
        s_reset = false;
        return r;
    }

    // ── Görünüm ───────────────────────────────────────────────────────────────

    const float Btn    = 84f;   // referans çözünürlükte (1920×1080) düğme kenarı
    const float Gap    = 6f;
    const float Margin = 24f;

    static readonly Color ColIdle = new Color(0.18f, 0.18f, 0.22f, 0.55f);
    static readonly Color ColHeld = new Color(0.30f, 0.55f, 0.85f, 0.85f);
    static readonly Color ColIcon = new Color(1f, 1f, 1f, 0.85f);

    Canvas _canvas;

    void Awake()
    {
        // Statikler sahne yeniden yüklenince hayatta kalır; basılı kalmış bir
        // düğme yeni oyunda kamerayı kendiliğinden kaydırmasın.
        s_pan = Vector2.zero; s_zoom = 0f; s_reset = false; s_heldCount = 0;

        _canvas              = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 15;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Klavye ipucu yalnızca klavyesi olan cihazda: telefonda "W" yazısı gürültü.
        bool hints = !Application.isMobilePlatform;

        // 3×3 ızgara (sütun, satır; satır 0 altta) + sağında zoom sütunu
        AddButton(1, 2, new Vector2( 0,  1), 0f, 0f,  90f, hints ? "W" : null);
        AddButton(0, 1, new Vector2(-1,  0), 0f, 0f, 180f, hints ? "A" : null);
        AddButton(2, 1, new Vector2( 1,  0), 0f, 0f,   0f, hints ? "D" : null);
        AddButton(1, 0, new Vector2( 0, -1), 0f, 0f, 270f, hints ? "S" : null);
        AddButton(1, 1, Vector2.zero,        0f, 1f,   0f, hints ? "C" : null);   // sıfırla
        AddButton(3.25f, 1.5f, Vector2.zero, -1f, 0f, 0f, hints ? "Q" : null, "+");
        AddButton(3.25f, 0.5f, Vector2.zero,  1f, 0f, 0f, hints ? "E" : null, "-");
    }

    void Update()
    {
        bool show = !UpgradeUI.IsPaused && !GameManager.IsGameOver && !StartMenuUI.IsOpen;
        if (_canvas.enabled != show)
        {
            _canvas.enabled = show;
            // Gizlenirken basılı düğme kalmasın: pointer-up gizli canvas'a gelmez.
            if (!show) foreach (var h in GetComponentsInChildren<PadButton>()) h.Release();
        }
    }

    /// <param name="zoom">-1 yaklaş, +1 uzaklaş.</param>
    /// <param name="reset">1 ise basınca kadrajı sıfırlar.</param>
    void AddButton(float col, float row, Vector2 pan, float zoom, float reset,
                   float arrowAngle, string hint, string text = null)
    {
        var go = new GameObject("CamPad", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var img   = go.AddComponent<Image>();
        img.color = ColIdle;

        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
        r.sizeDelta        = new Vector2(Btn, Btn);
        r.anchoredPosition = new Vector2(Margin + col * (Btn + Gap), Margin + row * (Btn + Gap));

        var pb = go.AddComponent<PadButton>();
        pb.pan = pan; pb.zoom = zoom; pb.reset = reset > 0f; pb.bg = img;

        // İkon: yön düğmesinde üçgen, sıfırlamada halka, zoom'da +/−.
        if (text != null)
            AddText(go.transform, text, 48, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
        else
        {
            var icon = new GameObject("Icon", typeof(RectTransform));
            icon.transform.SetParent(go.transform, false);
            var ii = icon.AddComponent<Image>();
            ii.sprite        = reset > 0f ? RingSprite() : TriangleSprite();
            ii.color         = ColIcon;
            ii.raycastTarget = false;
            var ir = (RectTransform)icon.transform;
            ir.sizeDelta        = new Vector2(Btn * 0.45f, Btn * 0.45f);
            ir.anchoredPosition = Vector2.zero;
            ir.localRotation    = Quaternion.Euler(0f, 0f, arrowAngle);
        }

        if (hint != null)
            AddText(go.transform, hint, 16, TextAnchor.UpperLeft,
                    new Vector2(0.06f, 0.05f), new Vector2(1f, 0.97f));
    }

    static void AddText(Transform parent, string text, int size, TextAnchor anchor,
                        Vector2 min, Vector2 max)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text          = text;
        t.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize      = size;
        t.fontStyle     = FontStyle.Bold;
        t.color         = new Color(1f, 1f, 1f, 0.75f);
        t.alignment     = anchor;
        t.raycastTarget = false;
        var r = (RectTransform)go.transform;
        r.anchorMin = min; r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    // ── İkonlar ───────────────────────────────────────────────────────────────
    // Ok karakterleri (▲ ◀) yerleşik fontta her platformda yok — WebGL'de
    // yedek font olmadığı için kare kutu çıkardı. Şekiller doku olarak çizilir.

    static Sprite s_triangle, s_ring;

    static Sprite TriangleSprite()
    {
        if (s_triangle != null) return s_triangle;
        const int N = 64;
        s_triangle = MakeSprite(N, (x, y) =>
        {
            // Sağa bakan üçgen: sol kenar tabanı, sağ uç sivri
            float u = x / (float)(N - 1), v = y / (float)(N - 1);
            float half = 0.5f * (1f - u) * 0.9f;
            return u > 0.12f && u < 0.92f && Mathf.Abs(v - 0.5f) < half;
        });
        return s_triangle;
    }

    static Sprite RingSprite()
    {
        if (s_ring != null) return s_ring;
        const int N = 64;
        s_ring = MakeSprite(N, (x, y) =>
        {
            float dx = x - (N - 1) * 0.5f, dy = y - (N - 1) * 0.5f;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);
            return (d > N * 0.30f && d < N * 0.42f) || d < N * 0.09f;
        });
        return s_ring;
    }

    static Sprite MakeSprite(int n, System.Func<int, int, bool> inside)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px  = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = new Color(1f, 1f, 1f, inside(x, y) ? 1f : 0f);
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
    }

    // ── Basılı tutulan düğme ──────────────────────────────────────────────────

    /// <summary>
    /// Basılıyken katkısını statik toplama ekler, bırakınca geri alır. Parmak
    /// düğmeden kayıp giderse de bırakılmış sayılır — yoksa düğmenin dışında
    /// kaldırılan parmak kamerayı sonsuza dek kaydırırdı.
    /// </summary>
    class PadButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Vector2 pan;
        public float   zoom;
        public bool    reset;
        public Image   bg;

        bool _held;

        public void OnPointerDown(PointerEventData e)
        {
            if (reset) s_reset = true;
            if (_held) return;
            _held = true;
            s_pan  += pan;
            s_zoom += zoom;
            s_heldCount++;
            bg.color = ColHeld;
        }

        public void OnPointerUp(PointerEventData e)   => Release();
        public void OnPointerExit(PointerEventData e) => Release();

        public void Release()
        {
            if (!_held) return;
            _held = false;
            s_pan  -= pan;
            s_zoom -= zoom;
            s_heldCount--;
            bg.color = ColIdle;
        }

        void OnDisable() => Release();
    }
}
