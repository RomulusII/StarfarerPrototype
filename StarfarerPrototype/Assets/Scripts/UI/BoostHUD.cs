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

    // Etiketler kurulumda bir kez yazılır; dil menüden değişince tazelenmeleri
    // gerekir (GameManager bu HUD'u menüden önce kurar).
    private Text _shieldLbl, _weaponLbl, _upgradeLbl;

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

        _shieldBtn = BuildButton("ShieldBoostBtn",
            new Vector2(0.38f, 0.02f), new Vector2(0.49f, 0.10f),
            () => OnToggle(BoostMode.Shield), out _shieldLbl);

        _weaponBtn = BuildButton("WeaponBoostBtn",
            new Vector2(0.51f, 0.02f), new Vector2(0.62f, 0.10f),
            () => OnToggle(BoostMode.Weapon), out _weaponLbl);

        // Upgrade ekranının tek girişi Tab tuşuydu; Android'de klavye yok, yani
        // telefonda ekran hiç açılamıyordu. Düğme boost şeridinin yanına konur —
        // ikisi de "oyun içi eylem" bandı. Kapatma düğmesi upgrade ekranının
        // KENDİ içindedir, çünkü bu şerit o ekran açıkken gizleniyor.
        BuildButton("UpgradeBtn",
            new Vector2(0.64f, 0.02f), new Vector2(0.74f, 0.10f),
            () => FindFirstObjectByType<UpgradeUI>()?.Toggle(), out _upgradeLbl);

        ApplyTexts();
    }

    void OnEnable()  => Loc.OnLanguageChanged += ApplyTexts;
    void OnDisable() => Loc.OnLanguageChanged -= ApplyTexts;

    void ApplyTexts()
    {
        if (_shieldLbl  != null) _shieldLbl.text  = Loc.T("hud.boost.shield");
        if (_weaponLbl  != null) _weaponLbl.text  = Loc.T("hud.boost.weapon");
        if (_upgradeLbl != null) _upgradeLbl.text = Loc.T("hud.upgrade");
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

    Button BuildButton(string goName,
        Vector2 anchorMin, Vector2 anchorMax,
        UnityEngine.Events.UnityAction onClick, out Text label)
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
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = 16;
        t.fontStyle = FontStyle.Bold;
        t.color     = Color.white;
        t.alignment = TextAnchor.MiddleCenter;

        var tRect = (RectTransform)txtGo.transform;
        tRect.anchorMin = Vector2.zero;
        tRect.anchorMax = Vector2.one;
        tRect.sizeDelta = Vector2.zero;

        label = t;
        return btn;
    }
}
