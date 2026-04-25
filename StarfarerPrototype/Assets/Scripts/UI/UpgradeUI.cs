using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Tab tuşuyla oyunu durduran, world-space SlotVisual'ları aktif eden ve
/// slot tıklanınca sağ kenarda popup panel açan yönetici.
///
/// Panel layout (500x400):
///   Başlık     — top anchor, 40px
///   DetailStrip— iki eşit kutu (sol=kurulu, sağ=seçili), 160px
///   Content    — component listesi / dolu slot butonları
///   CloseBtn   — bottom anchor, 40px
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

    // Sol kutu — kurulu component
    private Text _leftNameText;
    private Text _leftTypeText;
    private Text _leftTierText;
    private Text _leftCostText;

    // Sağ kutu — hover/tap ile seçili opsiyon
    private Text _rightNameText;
    private Text _rightTypeText;
    private Text _rightTierText;
    private Text _rightCostText;

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
        ComponentDefinition installed = empty ? null : _loadout.GetSlotDef(slotIndex);

        Debug.Log($"[UpgradeUI] slot={slotIndex} empty={empty} def={installed?.componentName ?? "null"}");

        _popupTitle.text = empty
            ? $"Slot {slotIndex} \u2014 Bo\u015f"
            : $"Slot {slotIndex} \u2014 {installed.componentName}";

        // Sol kutu: kurulu component (boşsa temizle)
        if (installed != null)
            FillBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText, installed);
        else
            ClearBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText);

        // Sağ kutu: sıfırla (yeni slot seçildi)
        ClearBox(_rightNameText, _rightTypeText, _rightTierText, _rightCostText);

        // Liste içeriğini yeniden oluştur
        foreach (Transform child in _popupContent.transform)
            Destroy(child.gameObject);

        if (empty)
            BuildEmptyContent(slotIndex);
        else
            BuildFilledContent(slotIndex, installed);

        _popupPanel.SetActive(true);
        Debug.Log($"[UpgradeUI] popup SetActive(true)");
    }

    /// <summary>Sağ kutuyu doldurur (hover / tap).</summary>
    public void ShowDetail(ComponentDefinition def)
    {
        if (def == null) return;
        FillBox(_rightNameText, _rightTypeText, _rightTierText, _rightCostText, def);
    }

    /// <summary>Sağ kutuyu temizler (hover çıkışı).</summary>
    public void HideDetail() =>
        ClearBox(_rightNameText, _rightTypeText, _rightTierText, _rightCostText);

    // -------------------------------------------------------------------------
    // Popup Content
    // -------------------------------------------------------------------------

    void ClosePopup() => _popupPanel.SetActive(false);

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

            // Hover / tap alanı (görünmez arka plan + ComponentRowHover)
            var nameArea = new GameObject("NameArea", typeof(RectTransform));
            nameArea.transform.SetParent(row.transform, false);

            nameArea.AddComponent<Image>().color = Color.clear;

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
            var nRect = (RectTransform)nameTxtGo.transform;
            nRect.anchorMin = Vector2.zero;
            nRect.anchorMax = Vector2.one;
            nRect.sizeDelta = Vector2.zero;

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

        _popupPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

        var panelRect = (RectTransform)_popupPanel.transform;
        panelRect.anchorMin        = new Vector2(1f, 0.5f);
        panelRect.anchorMax        = new Vector2(1f, 0.5f);
        panelRect.pivot            = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-20f, 0f);
        panelRect.sizeDelta        = new Vector2(500f, 400f);

        // Başlık — top 10px, 40px yükseklik
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

        // Detay şeridi — başlığın hemen altı, 160px
        // Panel 400px: top=350px from bottom (0.875), bottom=190px from bottom (0.475)
        BuildDetailStrip();

        // Liste alanı — şeridin altı, kapat butonunun üstü
        // top=0.455 (182px), bottom=0.13 (52px) → ~130px yükseklik
        _popupContent = new GameObject("Content", typeof(RectTransform));
        _popupContent.transform.SetParent(_popupPanel.transform, false);
        var contentRect = (RectTransform)_popupContent.transform;
        contentRect.anchorMin        = new Vector2(0f, 0.13f);
        contentRect.anchorMax        = new Vector2(1f, 0.455f);
        contentRect.pivot            = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta        = Vector2.zero;

        var vLayout = _popupContent.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing                = 6f;
        vLayout.padding                = new RectOffset(12, 12, 6, 6);
        vLayout.childAlignment         = TextAnchor.UpperLeft;
        vLayout.childForceExpandWidth  = true;
        vLayout.childForceExpandHeight = false;

        BuildCloseButton();

        _popupPanel.SetActive(false);
    }

    void BuildDetailStrip()
    {
        var strip = new GameObject("DetailStrip", typeof(RectTransform));
        strip.transform.SetParent(_popupPanel.transform, false);

        var stripRect = (RectTransform)strip.transform;
        stripRect.anchorMin        = new Vector2(0f, 0.475f);
        stripRect.anchorMax        = new Vector2(1f, 0.875f);
        stripRect.pivot            = new Vector2(0.5f, 0.5f);
        stripRect.anchoredPosition = Vector2.zero;
        stripRect.sizeDelta        = Vector2.zero;

        var hl = strip.AddComponent<HorizontalLayoutGroup>();
        hl.spacing                = 6f;
        hl.padding                = new RectOffset(10, 10, 8, 8);
        hl.childForceExpandWidth  = true;
        hl.childForceExpandHeight = true;
        hl.childAlignment         = TextAnchor.UpperLeft;

        BuildDetailBox(strip.transform,
            new Color(0.08f, 0.10f, 0.22f, 1f),
            out _leftNameText, out _leftTypeText, out _leftTierText, out _leftCostText);

        BuildDetailBox(strip.transform,
            new Color(0.08f, 0.20f, 0.12f, 1f),
            out _rightNameText, out _rightTypeText, out _rightTierText, out _rightCostText);
    }

    static void BuildDetailBox(Transform parent, Color bgColor,
        out Text nameText, out Text typeText, out Text tierText, out Text costText)
    {
        var box = new GameObject("DetailBox", typeof(RectTransform));
        box.transform.SetParent(parent, false);

        box.AddComponent<Image>().color = bgColor;
        box.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var vl = box.AddComponent<VerticalLayoutGroup>();
        vl.padding                = new RectOffset(10, 10, 8, 8);
        vl.spacing                = 5f;
        vl.childAlignment         = TextAnchor.UpperLeft;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        nameText = MakeDetailText(box.transform, "", 15, FontStyle.Bold);
        typeText = MakeDetailText(box.transform, "", 13, FontStyle.Normal);
        tierText = MakeDetailText(box.transform, "", 13, FontStyle.Normal);
        costText = MakeDetailText(box.transform, "", 13, FontStyle.Normal);
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
        t.alignment = TextAnchor.UpperLeft;

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
    // Detail Helpers
    // -------------------------------------------------------------------------

    static void FillBox(Text n, Text ty, Text ti, Text c, ComponentDefinition def)
    {
        n.text  = def.componentName;
        ty.text = TypeLabel(def.componentType);
        ti.text = $"Tier {def.tier}";
        c.text  = $"{def.cost} Ham Madde";
    }

    static void ClearBox(Text n, Text ty, Text ti, Text c)
    {
        n.text = ty.text = ti.text = c.text = "";
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
/// PC: hover → sağ kutuyu doldurur / temizler.
/// Mobil: tap → sağ kutuyu doldurur (açık kalır).
/// </summary>
[RequireComponent(typeof(Image))]
public class ComponentRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public ComponentDefinition def;

    public void OnPointerEnter(PointerEventData _) => UpgradeUI.Instance?.ShowDetail(def);
    public void OnPointerExit(PointerEventData _)  => UpgradeUI.Instance?.HideDetail();
    public void OnPointerClick(PointerEventData _) => UpgradeUI.Instance?.ShowDetail(def);
}
