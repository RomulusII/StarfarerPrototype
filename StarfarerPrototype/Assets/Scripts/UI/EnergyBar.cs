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
        SetBar(_energyFill, _energyText, eCur, eMax, "ENERJİ");

        if (ResourceInventory.Instance != null)
        {
            SetBar(_metalFill,   _metalText,   ResourceInventory.Instance.metal,   ResourceInventory.Instance.maxMetal,   "METAL");
            SetBar(_crystalFill, _crystalText, ResourceInventory.Instance.crystal, ResourceInventory.Instance.maxCrystal, "KRİSTAL");
        }
    }

    void SetBar(RectTransform fill, Text lbl, float cur, float max, string name)
    {
        float ratio = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
        var aMax   = fill.anchorMax;
        aMax.x     = ratio;
        fill.anchorMax = aMax;
        fill.sizeDelta = Vector2.zero;
        lbl.text = $"{name}   {cur:0} / {max:0}";
    }
}
