using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Tab tuşuyla oyunu durduran, world-space SlotVisual'ları aktif eden ve
/// slot tıklanınca sağ kenarda popup panel açan yönetici.
/// </summary>
public class UpgradeUI : MonoBehaviour
{
    public static UpgradeUI Instance { get; private set; }
    public static bool IsPaused = false;

    private Canvas      _canvas;
    private ShipLoadout _loadout;

    private GameObject _popupPanel;
    private Text       _popupTitle;
    private GameObject _popupContent;

    private GameObject _detailPanel;
    private Text       _detailNameText;
    private Text       _detailTypeText;
    private Text       _detailTierText;
    private Text       _detailCostText;

    // Hardcoded katalog — Step 3'te ScriptableObject asset listesiyle değiştirilecek
    private static ComponentDefinition[] _catalogDefs;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        Instance = this;
        BuildCanvas();
        _canvas.enabled = false;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            bool opening = !_canvas.enabled;

            if (opening)
            {
                var cam = FindFirstObjectByType<CameraController>();
                if (cam != null)
                {
                    Vector3 shipPos = _loadout != null ? _loadout.transform.position : Vector3.zero;
                    cam.ZoomToShip(shipPos, null);
                }
                _canvas.enabled = true;
                IsPaused        = true;
                Time.timeScale  = 0f;
            }
            else
            {
                ClosePopup();
                _canvas.enabled = false;
                IsPaused        = false;
                FindFirstObjectByType<CameraController>()?.RestoreFromUpgrade();
                Time.timeScale  = 1f;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void SetLoadout(ShipLoadout loadout) => _loadout = loadout;

    public void OnSlotClicked(int slotIndex)
    {
        Debug.Log($"[UpgradeUI] OnSlotClicked: slot={slotIndex}");

        bool empty = _loadout == null || _loadout.IsSlotEmpty(slotIndex);
        ComponentDefinition def = empty ? null : _loadout.GetSlotDef(slotIndex);

        Debug.Log($"[UpgradeUI] slot={slotIndex} empty={empty} def={def?.componentName ?? "null"}");

        _popupTitle.text = empty
            ? $"Slot {slotIndex} \u2014 Bo\u015f"
            : $"Slot {slotIndex} \u2014 {def.componentName}";

        // _detailPanel korunur, geri kalan child'lar temizlenir
        foreach (Transform child in _popupContent.transform)
        {
            if (child.gameObject != _detailPanel)
                Destroy(child.gameObject);
        }
        HideDetail();

        if (empty)
            BuildEmptyContent(slotIndex);
        else
            BuildFilledContent(slotIndex, def);

        _popupPanel.SetActive(true);
        Debug.Log($"[UpgradeUI] popup SetActive(true)");
    }

    public void ShowDetail(ComponentDefinition def)
    {
        if (def == null) return;
        _detailNameText.text = def.componentName;
        _detailTypeText.text = TypeLabel(def.componentType);
        _detailTierText.text = $"Tier {def.tier}";
        _detailCostText.text = $"{def.cost} Ham Madde";
        _detailPanel.SetActive(true);
    }

    public void HideDetail() => _detailPanel.SetActive(false);

    // -------------------------------------------------------------------------
    // Popup Content
    // -------------------------------------------------------------------------

    void ClosePopup()
    {
        HideDetail();
        _popupPanel.SetActive(false);
    }

    void BuildEmptyContent(int slotIndex)
    {
        foreach (var def in GetCatalogDefs())
        {
            var row = new GameObject("CatalogRow", typeof(RectTransform));
            row.transform.SetParent(_popupContent.transform, false);

            var rowH = row.AddComponent<HorizontalLayoutGroup>();
            rowH.spacing                = 10f;
            rowH.childForceExpandWidth  = false;
            rowH.childForceExpandHeight = true;
            rowH.childAlignment         = TextAnchor.MiddleLeft;

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 40f;
            rowLE.flexibleWidth   = 1f;

            // Tıklanabilir / hover'lanabilir isim alanı
            var nameArea = new GameObject("NameArea", typeof(RectTransform));
            nameArea.transform.SetParent(row.transform, false);

            var nameAreaImg = nameArea.AddComponent<Image>();
            nameAreaImg.color = Color.clear;           // raycasting için görünmez arka plan

            var hover = nameArea.AddComponent<ComponentRowHover>();
            hover.def = def;

            var nameAreaLE = nameArea.AddComponent<LayoutElement>();
            nameAreaLE.flexibleWidth   = 1f;
            nameAreaLE.preferredHeight = 40f;

            var nameTxtGo = new GameObject("NameText", typeof(RectTransform));
            nameTxtGo.transform.SetParent(nameArea.transform, false);
            var nameTxt = nameTxtGo.AddComponent<Text>();
            nameTxt.text      = def.componentName;
            nameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize  = 16;
            nameTxt.color     = Color.white;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            var nameTxtRect = (RectTransform)nameTxtGo.transform;
            nameTxtRect.anchorMin = Vector2.zero;
            nameTxtRect.anchorMax = Vector2.one;
            nameTxtRect.sizeDelta = Vector2.zero;

            var capturedDef = def;
            AddButton(row.transform, "Kur", () =>
                Debug.Log($"Kur: {capturedDef.componentName} -> slot {slotIndex}"), 90f);
        }
    }

    void BuildFilledContent(int slotIndex, ComponentDefinition def)
    {
        MakeTextLabel(_popupContent.transform, $"Tier {def.tier}", 16, TextAnchor.MiddleLeft);

        var btnRow = CreateRow(_popupContent.transform);

        if (def.upgradeTo != null)
            AddButton(btnRow.transform, "Upgrade", () =>
                Debug.Log($"Upgrade: slot {slotIndex}"), 90f);

        if (slotIndex != 5)
            AddButton(btnRow.transform, "Sat", () =>
                Debug.Log($"Sat: slot {slotIndex}"), 70f);
    }

    // -------------------------------------------------------------------------
    // Hardcoded Catalog
    // -------------------------------------------------------------------------

    static ComponentDefinition[] GetCatalogDefs()
    {
        if (_catalogDefs != null) return _catalogDefs;

        var shield = ScriptableObject.CreateInstance<ComponentDefinition>();
        shield.componentName = "Kalkan Jenerat\u00f6r\u00fc Mk1";
        shield.componentType = ComponentType.Shield;
        shield.tier          = 1;
        shield.costResource  = ResourceType.RawMaterial;
        shield.cost          = 10;

        var repair = ScriptableObject.CreateInstance<ComponentDefinition>();
        repair.componentName = "Onar\u0131m Birimi Mk1";
        repair.componentType = ComponentType.RepairUnit;
        repair.tier          = 1;
        repair.costResource  = ResourceType.RawMaterial;
        repair.cost          = 8;

        var gen = ScriptableObject.CreateInstance<ComponentDefinition>();
        gen.componentName = "Enerji Jenerat\u00f6r\u00fc Mk1";
        gen.componentType = ComponentType.Generator;
        gen.tier          = 1;
        gen.costResource  = ResourceType.RawMaterial;
        gen.cost          = 15;

        _catalogDefs = new[] { shield, repair, gen };
        return _catalogDefs;
    }

    static string TypeLabel(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Shield:     return "Kalkan";
            case ComponentType.RepairUnit: return "Onar\u0131m Birimi";
            case ComponentType.Generator:  return "Enerji Jenerat\u00f6r\u00fc";
            case ComponentType.Weapon:     return "Silah";
            default:                       return type.ToString();
        }
    }

    // -------------------------------------------------------------------------
    // Canvas Builder
    // -------------------------------------------------------------------------

    void BuildCanvas()
    {
        _canvas              = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 20;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        BuildPopupPanel();
    }

    void BuildPopupPanel()
    {
        _popupPanel = new GameObject("SlotPopup", typeof(RectTransform));
        _popupPanel.transform.SetParent(transform, false);

        var bg = _popupPanel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        var panelRect = (RectTransform)_popupPanel.transform;
        panelRect.anchorMin        = new Vector2(1f, 0.5f);
        panelRect.anchorMax        = new Vector2(1f, 0.5f);
        panelRect.pivot            = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-20f, 0f);
        panelRect.sizeDelta        = new Vector2(500f, 400f);

        // Başlık
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(_popupPanel.transform, false);
        _popupTitle           = titleGo.AddComponent<Text>();
        _popupTitle.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _popupTitle.fontSize  = 22;
        _popupTitle.fontStyle = FontStyle.Bold;
        _popupTitle.color     = Color.white;
        _popupTitle.alignment = TextAnchor.MiddleCenter;
        var titleRect = (RectTransform)titleGo.transform;
        titleRect.anchorMin        = new Vector2(0f, 1f);
        titleRect.anchorMax        = new Vector2(1f, 1f);
        titleRect.pivot            = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -10f);
        titleRect.sizeDelta        = new Vector2(0f, 40f);

        // İçerik alanı (detay paneli + satır listesi birlikte)
        _popupContent = new GameObject("Content", typeof(RectTransform));
        _popupContent.transform.SetParent(_popupPanel.transform, false);
        var contentRect = (RectTransform)_popupContent.transform;
        contentRect.anchorMin        = new Vector2(0f, 0.14f);
        contentRect.anchorMax        = new Vector2(1f, 0.87f);
        contentRect.pivot            = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta        = Vector2.zero;

        var vLayout = _popupContent.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing                = 6f;
        vLayout.padding                = new RectOffset(12, 12, 6, 6);
        vLayout.childAlignment         = TextAnchor.UpperLeft;
        vLayout.childForceExpandWidth  = true;
        vLayout.childForceExpandHeight = false;

        BuildDetailPanel();
        BuildCloseButton();

        _popupPanel.SetActive(false);
    }

    void BuildDetailPanel()
    {
        _detailPanel = new GameObject("DetailPanel", typeof(RectTransform));
        _detailPanel.transform.SetParent(_popupContent.transform, false);

        var bg = _detailPanel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.22f, 1f);

        var le = _detailPanel.AddComponent<LayoutElement>();
        le.preferredHeight = 160f;
        le.flexibleWidth   = 1f;

        var vl = _detailPanel.AddComponent<VerticalLayoutGroup>();
        vl.padding                = new RectOffset(14, 14, 12, 10);
        vl.spacing                = 5f;
        vl.childAlignment         = TextAnchor.UpperLeft;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        _detailNameText = MakeDetailText(_detailPanel.transform, "", 18, FontStyle.Bold);
        _detailTypeText = MakeDetailText(_detailPanel.transform, "", 14, FontStyle.Normal);
        _detailTierText = MakeDetailText(_detailPanel.transform, "", 14, FontStyle.Normal);
        _detailCostText = MakeDetailText(_detailPanel.transform, "", 14, FontStyle.Normal);

        _detailPanel.SetActive(false);
    }

    static Text MakeDetailText(Transform parent, string text, int fontSize, FontStyle style)
    {
        var go = new GameObject("DetailText", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.text      = text;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = fontSize;
        t.fontStyle = style;
        t.color     = Color.white;
        t.alignment = TextAnchor.MiddleLeft;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8f;
        le.flexibleWidth   = 1f;

        return t;
    }

    void BuildCloseButton()
    {
        var go = new GameObject("CloseButton", typeof(RectTransform));
        go.transform.SetParent(_popupPanel.transform, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.55f, 0.1f, 0.1f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(ClosePopup);

        var rect = (RectTransform)go.transform;
        rect.anchorMin        = new Vector2(0.5f, 0f);
        rect.anchorMax        = new Vector2(0.5f, 0f);
        rect.pivot            = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 12f);
        rect.sizeDelta        = new Vector2(110f, 40f);

        AttachLabel(go.transform, "Kapat", 15);
    }

    // -------------------------------------------------------------------------
    // UI Helpers
    // -------------------------------------------------------------------------

    static GameObject CreateRow(Transform parent)
    {
        var go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing                = 10f;
        h.childForceExpandWidth  = false;
        h.childForceExpandHeight = false;
        h.childAlignment         = TextAnchor.MiddleLeft;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 40f;
        le.flexibleWidth   = 1f;

        return go;
    }

    static void MakeTextLabel(Transform parent, string text, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.text      = text;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = fontSize;
        t.color     = Color.white;
        t.alignment = alignment;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8f;
        le.flexibleWidth   = 1f;
    }

    static void AddButton(Transform parent, string label, UnityAction onClick, float width = 80f)
    {
        var go = new GameObject($"Btn_{label}", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.38f, 0.75f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = width;
        le.preferredHeight = 40f;

        AttachLabel(go.transform, label, 15);
    }

    static void AttachLabel(Transform parent, string text, int fontSize)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.text      = text;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = fontSize;
        t.color     = Color.white;
        t.alignment = TextAnchor.MiddleCenter;

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
    }
}

/// <summary>
/// Katalog satırının isim alanına eklenir.
/// PC: hover ile detay panelini açar/kapatır.
/// Mobil: tıklama ile detay panelini açar.
/// </summary>
[RequireComponent(typeof(Image))]
public class ComponentRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public ComponentDefinition def;

    public void OnPointerEnter(PointerEventData _) => UpgradeUI.Instance?.ShowDetail(def);
    public void OnPointerExit(PointerEventData _)  => UpgradeUI.Instance?.HideDetail();
    public void OnPointerClick(PointerEventData _) => UpgradeUI.Instance?.ShowDetail(def);
}
