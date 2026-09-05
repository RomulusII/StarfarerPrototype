using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun HUD'u: enerji + kaynak barları (üst satırda yan yana, her zaman görünür).
/// ENERJİ — METAL — KRİSTAL, ekranın tamamına yayılan tek satır.
/// </summary>
public class EnergyBar : MonoBehaviour
{
    const float BarH = 44f;  // piksel yükseklik (1080 referansında)

    RectTransform _energyFill;
    RectTransform _metalFill;
    RectTransform _crystalFill;
    Text          _energyText;
    Text          _metalText;
    Text          _crystalText;
    Text          _warningText;

    // Bar başlıkları her karede yazılıyor; sözlük araması kare başına üç kez
    // olmasın diye önbelleklenir (bu dosyadaki uyarı maskesiyle aynı gerekçe).
    string _energyLabel, _metalLabel, _crystalLabel;

    void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // Üç eşit genişlikte bar, aynı y konumunda (üst)
        (_energyFill,  _energyText)  = MakeBar("Energy",  0f,     0.333f,
            new Color(0.28f, 0.18f, 0.00f, 0.88f),
            new Color(1.00f, 0.70f, 0.10f, 1.00f));

        (_metalFill,   _metalText)   = MakeBar("Metal",   0.334f, 0.667f,
            new Color(0.20f, 0.26f, 0.18f, 0.88f),
            new Color(0.45f, 0.68f, 0.28f, 1.00f));

        (_crystalFill, _crystalText) = MakeBar("Crystal", 0.668f, 1.0f,
            new Color(0.18f, 0.22f, 0.28f, 0.88f),
            new Color(0.28f, 0.58f, 0.80f, 1.00f));

        _warningText = MakeWarningLine();

        ApplyTexts();
    }

    void OnEnable()  => Loc.OnLanguageChanged += ApplyTexts;
    void OnDisable() => Loc.OnLanguageChanged -= ApplyTexts;

    void ApplyTexts()
    {
        _energyLabel  = Loc.T("hud.energy");
        _metalLabel   = Loc.T("hud.metal");
        _crystalLabel = Loc.T("hud.crystal");

        // Uyarı metni maske değişmedikçe yeniden kurulmuyor; dil değiştiğinde
        // maske aynı kalacağı için önbelleği elle geçersiz kılmak gerekir.
        _warnMask = -1;
    }

    /// <summary>
    /// Barların hemen ALTINDAKİ uyarı satırı. Barın kendisi doluluğu zaten
    /// gösteriyor ama oyuncu savaş sırasında üç barı da okumuyor: enerjinin
    /// bittiğini atış yapamayınca, deponun dolduğunu ise HİÇ fark etmiyordu —
    /// tavana çarpan kaynak sessizce yanıyor. Uyarı barların yanına değil
    /// altına konur; üstteki şerit sayının yeri, bu satır olayın yeri.
    /// </summary>
    Text MakeWarningLine()
    {
        var go = new GameObject("Warnings");
        go.transform.SetParent(transform, false);

        var txt       = go.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 26;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.raycastTarget = false;   // oynanışa dokunmaz
        txt.text      = "";

        var tr = go.GetComponent<RectTransform>();
        tr.anchorMin        = new Vector2(0f, 1f);
        tr.anchorMax        = new Vector2(1f, 1f);
        tr.pivot            = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -BarH - 6f);
        tr.sizeDelta        = new Vector2(0f, 34f);
        return txt;
    }

    (RectTransform fill, Text label) MakeBar(string id, float xMin, float xMax, Color bgCol, Color fillCol)
    {
        var bgGO = new GameObject(id + "Bg");
        bgGO.transform.SetParent(transform, false);
        bgGO.AddComponent<Image>().color = bgCol;

        var bg = bgGO.GetComponent<RectTransform>();
        // Yatay: anchor tabanlı üçte bir genişlik
        // Dikey: en üst kenar (anchorMin.y = anchorMax.y = 1), BarH piksel aşağı uzanır
        bg.anchorMin        = new Vector2(xMin, 1f);
        bg.anchorMax        = new Vector2(xMax, 1f);
        bg.pivot            = new Vector2(0f,   1f);
        bg.anchoredPosition = Vector2.zero;
        bg.sizeDelta        = new Vector2(0f, BarH);

        // Dolum barı — yatay yayılır, tam yükseklik
        var fillGO = new GameObject(id + "Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        fillGO.AddComponent<Image>().color = fillCol;

        var fill          = fillGO.GetComponent<RectTransform>();
        fill.anchorMin        = new Vector2(0f, 0f);
        fill.anchorMax        = new Vector2(0f, 1f);
        fill.pivot            = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.sizeDelta        = Vector2.zero;

        // Metin — dolum barının üzerinde render edilir
        var txtGO = new GameObject(id + "Text");
        txtGO.transform.SetParent(bgGO.transform, false);

        var txt       = txtGO.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 22;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = new Color(1f, 1f, 1f, 0.92f);
        txt.alignment = TextAnchor.MiddleLeft;

        var tr          = txtGO.GetComponent<RectTransform>();
        tr.anchorMin        = Vector2.zero;
        tr.anchorMax        = Vector2.one;
        tr.anchoredPosition = new Vector2(10f, 0f);
        tr.sizeDelta        = new Vector2(-10f, 0f);

        return (fill, txt);
    }

    void Update()
    {
        float eCur = 0f, eMax = 1f;
        if (EnergyBus.Instance != null && EnergyBus.Instance.maxEnergy > 0f)
        {
            eCur = EnergyBus.Instance.currentEnergy;
            eMax = EnergyBus.Instance.maxEnergy;
        }
        SetBar(_energyFill, _energyText, eCur, eMax, _energyLabel);

        if (ResourceInventory.Instance != null)
        {
            SetBar(_metalFill,   _metalText,   ResourceInventory.Instance.metal,   ResourceInventory.Instance.maxMetal,   _metalLabel);
            SetBar(_crystalFill, _crystalText, ResourceInventory.Instance.crystal, ResourceInventory.Instance.maxCrystal, _crystalLabel);
        }

        UpdateWarnings(eCur, eMax);
    }

    // ── Uyarılar ──────────────────────────────────────────────────────────────

    /// <summary>Enerjinin "az" sayıldığı oran.</summary>
    const float LowEnergyRatio = 0.10f;

    static readonly Color WarnColor     = new Color(1.00f, 0.75f, 0.20f, 1f); // sarı: yaklaşıyor
    static readonly Color CriticalColor = new Color(1.00f, 0.32f, 0.24f, 1f); // kırmızı: oldu

    /// <summary>
    /// İki kademe: YAKLAŞIYOR (sarı, sabit) ve OLDU (kırmızı, yanıp söner).
    /// Ayrım anlamlıdır — birincisi "önlem al", ikincisi "şu an kaybediyorsun".
    /// Aynı anda birden fazla uyarı olabilir; en yüksek kademe rengi belirler.
    ///
    /// Yanıp sönme <c>unscaledTime</c> ile sürer: upgrade ekranı açıkken oyun
    /// duruyor ama uyarı orada da okunmalı — zaten oyuncunun sorunu çözmek için
    /// gideceği yer o ekran.
    /// </summary>
    // Uyarılar bir BİT MASKESİNE indirilir ve metin yalnızca maske değiştiğinde
    // yeniden kurulur. Her karede string üretmek HUD'un tamamı için kare başına
    // çöp demekti; maske ayrıca "hangi kademe" sorusunu tek karşılaştırmaya
    // indiriyor (sprite önbelleğiyle aynı gerekçe).
    const int WNoEnergy    = 1 << 0;
    const int WLowEnergy   = 1 << 1;
    const int WMetalFull   = 1 << 2;
    const int WMetalNear   = 1 << 3;
    const int WCrystalFull = 1 << 4;
    const int WCrystalNear = 1 << 5;

    const int CriticalMask = WNoEnergy | WMetalFull | WCrystalFull;

    int _warnMask = -1;   // -1 = henüz hiç kurulmadı

    void UpdateWarnings(float energyCur, float energyMax)
    {
        int mask = 0;

        if (EnergyBus.Instance != null)
        {
            if (energyCur <= 0.01f)                                            mask |= WNoEnergy;
            else if (energyMax > 0f && energyCur / energyMax < LowEnergyRatio) mask |= WLowEnergy;
        }

        var inv = ResourceInventory.Instance;
        if (inv != null)
        {
            if (inv.IsFull(ResourceType.RawMaterial))              mask |= WMetalFull;
            else if (inv.IsNearlyFull(ResourceType.RawMaterial))   mask |= WMetalNear;

            if (inv.IsFull(ResourceType.EnergyCrystal))            mask |= WCrystalFull;
            else if (inv.IsNearlyFull(ResourceType.EnergyCrystal)) mask |= WCrystalNear;
        }

        if (mask != _warnMask)
        {
            _warnMask         = mask;
            _warningText.text = BuildWarningText(mask);
        }

        if (mask == 0) return;

        var c = (mask & CriticalMask) != 0 ? CriticalColor : WarnColor;
        if ((mask & CriticalMask) != 0)
            // 2.2 Hz nabız — dikkat çeker ama tamamen kaybolmaz (taban 0.45)
            c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.2f));

        _warningText.color = c;
    }

    static string BuildWarningText(int mask)
    {
        if (mask == 0) return "";

        var sb = new System.Text.StringBuilder();
        void Add(string s) { if (sb.Length > 0) sb.Append("    "); sb.Append(s); }

        if ((mask & WNoEnergy)    != 0) Add(Loc.T("hud.warn.noEnergy"));
        if ((mask & WLowEnergy)   != 0) Add(Loc.T("hud.warn.lowEnergy"));
        if ((mask & WMetalFull)   != 0) Add(Loc.T("hud.warn.metalFull"));
        if ((mask & WMetalNear)   != 0) Add(Loc.T("hud.warn.metalNear"));
        if ((mask & WCrystalFull) != 0) Add(Loc.T("hud.warn.crystalFull"));
        if ((mask & WCrystalNear) != 0) Add(Loc.T("hud.warn.crystalNear"));

        return sb.ToString();
    }

    void SetBar(RectTransform fill, Text lbl, float cur, float max, string name)
    {
        float ratio = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
        var aMax   = fill.anchorMax;
        aMax.x     = ratio;
        fill.anchorMax = aMax;
        fill.sizeDelta = Vector2.zero;
        lbl.text = $"{name}   {cur.ToString("0", Loc.Culture)} / {max.ToString("0", Loc.Culture)}";
    }
}
