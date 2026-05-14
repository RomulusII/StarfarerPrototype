using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun HUD'u: enerji + kaynak barları (sol üst köşe, her zaman görünür).
/// Enerji (turuncu) → Metal (yeşil) → Kristal (mavi-yeşil), aynı boyut.
/// </summary>
public class EnergyBar : MonoBehaviour
{
    const float BarW = 600f;
    const float BarH = 40f;
    const float BarX = 20f;
    const float BarY = -20f;
    const float Gap  = 6f;

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

        float yE = BarY;
        float yM = BarY - BarH - Gap;
        float yC = BarY - 2f * (BarH + Gap);

        (_energyFill,  _energyText)  = MakeBar("Energy",  yE,
            new Color(0.28f, 0.18f, 0.00f, 0.88f),
            new Color(1.00f, 0.70f, 0.10f, 1.00f));

        (_metalFill,   _metalText)   = MakeBar("Metal",   yM,
            new Color(0.20f, 0.26f, 0.18f, 0.88f),
            new Color(0.45f, 0.68f, 0.28f, 1.00f));

        (_crystalFill, _crystalText) = MakeBar("Crystal", yC,
            new Color(0.18f, 0.22f, 0.28f, 0.88f),
            new Color(0.28f, 0.58f, 0.80f, 1.00f));
    }

    (RectTransform fill, Text label) MakeBar(string id, float y, Color bgCol, Color fillCol)
    {
        var bgGO = new GameObject(id + "Bg");
        bgGO.transform.SetParent(transform, false);
        bgGO.AddComponent<Image>().color = bgCol;

        var bg          = bgGO.GetComponent<RectTransform>();
        bg.anchorMin        = new Vector2(0f, 1f);
        bg.anchorMax        = new Vector2(0f, 1f);
        bg.pivot            = new Vector2(0f, 1f);
        bg.anchoredPosition = new Vector2(BarX, y);
        bg.sizeDelta        = new Vector2(BarW, BarH);

        // Dolum barı
        var fillGO = new GameObject(id + "Fill");
        fillGO.transform.SetParent(bgGO.transform, false);
        fillGO.AddComponent<Image>().color = fillCol;

        var fill          = fillGO.GetComponent<RectTransform>();
        fill.anchorMin        = new Vector2(0f, 0f);
        fill.anchorMax        = new Vector2(0f, 1f);
        fill.pivot            = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.sizeDelta        = new Vector2(0f, 0f);

        // Yazı — dolum barının üstünde render olur (sonra eklendi = üstte)
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
        // Enerji
        float eCur = 0f, eMax = 1f;
        if (EnergyBus.Instance != null && EnergyBus.Instance.maxEnergy > 0f)
        {
            eCur = EnergyBus.Instance.currentEnergy;
            eMax = EnergyBus.Instance.maxEnergy;
        }
        SetBar(_energyFill, _energyText, eCur, eMax, "ENERJİ");

        // Kaynaklar
        if (ResourceInventory.Instance != null)
        {
            SetBar(_metalFill,   _metalText,   ResourceInventory.Instance.metal,   ResourceInventory.Instance.maxMetal,   "METAL");
            SetBar(_crystalFill, _crystalText, ResourceInventory.Instance.crystal, ResourceInventory.Instance.maxCrystal, "KRİSTAL");
        }
    }

    void SetBar(RectTransform fill, Text lbl, float cur, float max, string name)
    {
        float ratio = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
        fill.sizeDelta = new Vector2(BarW * ratio, 0f);
        lbl.text = $"{name}   {cur:0} / {max:0}";
    }
}
