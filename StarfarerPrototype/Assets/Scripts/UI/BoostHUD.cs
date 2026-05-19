using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun sırasında ekranın alt ortasında görünen iki boost toggle butonu.
/// Kalkan Boost ve Silah Boost birbirini iptal eder.
/// Upgrade ekranı açıkken gizlenir.
/// </summary>
public class BoostHUD : MonoBehaviour
{
    private Canvas _canvas;
    private Button _shieldBtn;
    private Button _weaponBtn;

    static readonly Color ColNormal  = new Color(0.18f, 0.18f, 0.22f, 0.92f);
    static readonly Color ColShield  = new Color(0.10f, 0.35f, 0.80f, 1f);
    static readonly Color ColWeapon  = new Color(0.75f, 0.25f, 0.10f, 1f);

    void Awake()
    {
        _canvas              = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 15;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        _shieldBtn = BuildButton("ShieldBoostBtn", "KALKAN\nBOOST",
            new Vector2(0.38f, 0.02f), new Vector2(0.49f, 0.10f),
            () => OnToggle(BoostMode.Shield));

        _weaponBtn = BuildButton("WeaponBoostBtn", "SİLAH\nBOOST",
            new Vector2(0.51f, 0.02f), new Vector2(0.62f, 0.10f),
            () => OnToggle(BoostMode.Weapon));
    }

    void Update()
    {
        _canvas.enabled = !UpgradeUI.IsPaused && !GameManager.IsGameOver;
        if (!_canvas.enabled) return;

        _shieldBtn.GetComponent<Image>().color =
            BoostController.Mode == BoostMode.Shield ? ColShield : ColNormal;

        _weaponBtn.GetComponent<Image>().color =
            BoostController.Mode == BoostMode.Weapon ? ColWeapon : ColNormal;
    }

    void OnToggle(BoostMode mode)
    {
        BoostController.Toggle(mode);
    }

    Button BuildButton(string goName, string label,
        Vector2 anchorMin, Vector2 anchorMax,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(goName, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var img = go.AddComponent<Image>();
        img.color = ColNormal;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var rect = (RectTransform)go.transform;
        rect.anchorMin        = anchorMin;
        rect.anchorMax        = anchorMax;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = Vector2.zero;

        var txtGo = new GameObject("Label", typeof(RectTransform));
        txtGo.transform.SetParent(go.transform, false);

        var t = txtGo.AddComponent<Text>();
        t.text      = label;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = 16;
        t.fontStyle = FontStyle.Bold;
        t.color     = Color.white;
        t.alignment = TextAnchor.MiddleCenter;

        var tRect = (RectTransform)txtGo.transform;
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.sizeDelta = Vector2.zero;

        return btn;
    }
}
