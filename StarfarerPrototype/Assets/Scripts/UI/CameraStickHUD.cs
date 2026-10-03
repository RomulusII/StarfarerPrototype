using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Dokunmatik kamera kontrolü: ekranın sol şeridinde YÜZEN bir joystick ve en
/// soldaki dikey zoom şeridi. Masaüstünde yerini <see cref="CameraPadHUD"/>
/// (WASD ile aynı düğmeler) tutar; ikisi aynı anda kurulmaz.
///
/// **Yüzen joystick:** başparmak şeridin neresine değerse çubuğun merkezi orası
/// olur; sürükleme yönü ve mesafesi kaydırmanın yönü ve hızıdır. Sabit bir
/// ped, başparmağın ekrana bakmadan doğru düğmeyi bulmasını ister — telefonda
/// en çok kaçan şey tam olarak bu.
///
/// **Zoom şeridi** aynı mantıkla çalışır: değdiğin yer sıfır, yukarı sürükle
/// yaklaş, aşağı sürükle uzaklaş; ne kadar uzağa o kadar hızlı.
///
/// **Şeride çift dokunmak kadrajı sıfırlar** (klavyede C).
///
/// Şerit UI'dır: oradaki dokunuşlar nişan/ateş SAYILMAZ (PointerInput nişan
/// parmağını UI dışındaki ilk dokunuştan seçer), yani sol başparmak kamerayı
/// sürerken sağ başparmak serbestçe nişan alır. Bedeli: şeridin içine dokunarak
/// nişan alınamaz — bu yüzden şerit dar (ekranın %22'si) ve gemi (soldan %29)
/// onun dışında kalıyor.
/// </summary>
public class CameraStickHUD : MonoBehaviour
{
    // ── Dışarıya açılan durum ─────────────────────────────────────────────────

    static Vector2 s_pan;
    static float   s_zoom;
    static bool    s_reset;

    /// <summary>Joystick çıktısı, her eksende -1..1 (analog).</summary>
    public static Vector2 Pan => s_pan;

    /// <summary>-1 yaklaş, +1 uzaklaş (analog).</summary>
    public static float Zoom => s_zoom;

    public static bool ConsumeReset()
    {
        bool r = s_reset;
        s_reset = false;
        return r;
    }

    // ── Ölçüler (1920×1080 referansında) ──────────────────────────────────────

    const float ZoneWidth   = 0.22f;   // ekran genişliğinin
    const float ZoneTop     = 0.86f;   // üst şeridin altı
    const float StripWidth  = 0.05f;   // zoom şeridi, en solda
    const float StickRadius = 120f;    // çubuğun tam sapma mesafesi
    const float KnobSize    = 96f;
    const float DeadZone    = 0.12f;
    const float DoubleTap   = 0.30f;   // sn

    static readonly Color ColBase  = new Color(1f, 1f, 1f, 0.22f);
    static readonly Color ColKnob  = new Color(0.55f, 0.75f, 1f, 0.70f);
    static readonly Color ColHint  = new Color(1f, 1f, 1f, 0.08f);
    static readonly Color ColStrip = new Color(1f, 1f, 1f, 0.07f);

    Canvas _canvas;

    void Awake()
    {
        s_pan = Vector2.zero; s_zoom = 0f; s_reset = false;

        _canvas              = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 14;   // boost şeridinin ve hız düğmelerinin altında

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Joystick bölgesi (zoom şeridinin sağı)
        var zone = MakeRect("StickZone", transform,
                            new Vector2(StripWidth, 0f), new Vector2(ZoneWidth, ZoneTop));
        zone.gameObject.AddComponent<Image>().color = Color.clear;   // görünmez ama dokunuş alır

        var stick = zone.gameObject.AddComponent<Stick>();
        stick.vertical = false;
        stick.Build(Circle(), ColBase, ColKnob, ColHint, new Vector2(0.5f, 0.28f));

        // Zoom şeridi
        var strip = MakeRect("ZoomStrip", transform,
                             new Vector2(0f, 0.18f), new Vector2(StripWidth, 0.78f));
        strip.gameObject.AddComponent<Image>().color = ColStrip;
        AddLabel(strip, "+", new Vector2(0f, 0.86f), new Vector2(1f, 1f));
        AddLabel(strip, "-", new Vector2(0f, 0f),    new Vector2(1f, 0.14f));

        var zs = strip.gameObject.AddComponent<Stick>();
        zs.vertical = true;
        zs.Build(Circle(), ColBase, ColKnob, Color.clear, new Vector2(0.5f, 0.5f));
    }

    void Update()
    {
        bool show = !UpgradeUI.IsPaused && !GameManager.IsGameOver && !StartMenuUI.IsOpen;
        if (_canvas.enabled != show)
        {
            _canvas.enabled = show;
            if (!show) foreach (var st in GetComponentsInChildren<Stick>()) st.Release();
        }
    }

    // ── Yardımcılar ───────────────────────────────────────────────────────────

    static RectTransform MakeRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = min; r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    static void AddLabel(RectTransform parent, string text, Vector2 min, Vector2 max)
    {
        var r = MakeRect("Label", parent, min, max);
        var t = r.gameObject.AddComponent<Text>();
        t.text          = text;
        t.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize      = 40;
        t.fontStyle     = FontStyle.Bold;
        t.alignment     = TextAnchor.MiddleCenter;
        t.color         = new Color(1f, 1f, 1f, 0.35f);
        t.raycastTarget = false;
    }

    static Sprite s_circle;

    /// <summary>Yumuşak kenarlı halka + soluk dolgu; çubuğun tabanı ve topuzu paylaşır.</summary>
    static Sprite Circle()
    {
        if (s_circle != null) return s_circle;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px  = new Color[N * N];
        float c = (N - 1) * 0.5f, R = N * 0.48f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float d    = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float edge = Mathf.Clamp01(R - d);                       // dış kenar yumuşatma
            float ring = Mathf.Clamp01(1f - Mathf.Abs(d - (R - 3f)) / 3f);
            float a    = Mathf.Max(ring, 0.35f) * edge;
            px[y * N + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        s_circle = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        return s_circle;
    }

    // ── Tek bir yüzen çubuk ───────────────────────────────────────────────────

    /// <summary>
    /// Dokunulan yerde doğan çubuk. <see cref="vertical"/> ise yalnızca dikey
    /// eksen okunur ve zoom'a yazılır; değilse iki eksen kaydırmaya yazılır.
    /// Her çubuk kendi parmağını izler — iki çubuk iki parmakla aynı anda
    /// kullanılabilir.
    /// </summary>
    class Stick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public bool vertical;

        RectTransform _rect, _base, _knob;
        Image         _hint;
        Vector2       _origin;           // yerel koordinat
        int           _pointer = -1;
        float         _lastTap = -10f;
        Vector2       _lastTapPos;

        public void Build(Sprite sprite, Color baseCol, Color knobCol, Color hintCol, Vector2 hintAnchor)
        {
            _rect = (RectTransform)transform;

            // Boştayken çubuğun nerede olduğunu hatırlatan soluk bir halka
            if (hintCol.a > 0f)
            {
                var h = MakeRect("Hint", _rect, hintAnchor, hintAnchor);
                h.sizeDelta = Vector2.one * StickRadius * 2f;
                _hint = h.gameObject.AddComponent<Image>();
                _hint.sprite = sprite; _hint.color = hintCol; _hint.raycastTarget = false;
            }

            _base = MakeRect("Base", _rect, Vector2.zero, Vector2.zero);
            _base.sizeDelta = Vector2.one * (vertical ? KnobSize * 1.3f : StickRadius * 2f);
            var bi = _base.gameObject.AddComponent<Image>();
            bi.sprite = sprite; bi.color = baseCol; bi.raycastTarget = false;

            _knob = MakeRect("Knob", _rect, Vector2.zero, Vector2.zero);
            _knob.sizeDelta = Vector2.one * KnobSize;
            var ki = _knob.gameObject.AddComponent<Image>();
            ki.sprite = sprite; ki.color = knobCol; ki.raycastTarget = false;

            Show(false);
        }

        void Show(bool on)
        {
            _base.gameObject.SetActive(on);
            _knob.gameObject.SetActive(on);
            if (_hint != null) _hint.enabled = !on;
        }

        bool ToLocal(PointerEventData e, out Vector2 local)
            => RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, e.position, null, out local);

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointer != -1) return;
            if (!ToLocal(e, out _origin)) return;
            _pointer = e.pointerId;

            // Yerel koordinat pivot'a göre; görselleri o noktaya koy.
            _base.anchorMin = _base.anchorMax = _rect.pivot;
            _knob.anchorMin = _knob.anchorMax = _rect.pivot;
            _base.anchoredPosition = _knob.anchoredPosition = _origin;
            Show(true);

            // Çift dokunuş: kadrajı sıfırla (yalnızca joystick bölgesinde)
            if (!vertical)
            {
                if (Time.unscaledTime - _lastTap < DoubleTap &&
                    Vector2.Distance(_origin, _lastTapPos) < StickRadius)
                    s_reset = true;
                _lastTap    = Time.unscaledTime;
                _lastTapPos = _origin;
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointer || !ToLocal(e, out var p)) return;

            Vector2 d = p - _origin;
            if (vertical) d.x = 0f;
            Vector2 v = Vector2.ClampMagnitude(d / StickRadius, 1f);
            _knob.anchoredPosition = _origin + v * StickRadius;

            // Ölü bölge: başparmağın titremesi kamerayı kıpırdatmasın
            float m = v.magnitude;
            Vector2 o = m < DeadZone ? Vector2.zero : v * ((m - DeadZone) / (1f - DeadZone) / m);

            if (vertical) s_zoom = -o.y;   // yukarı = yaklaş
            else          s_pan  = o;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == _pointer) Release();
        }

        public void Release()
        {
            if (_pointer == -1) return;
            _pointer = -1;
            if (vertical) s_zoom = 0f; else s_pan = Vector2.zero;
            Show(false);
        }

        void OnDisable() => Release();
    }
}
