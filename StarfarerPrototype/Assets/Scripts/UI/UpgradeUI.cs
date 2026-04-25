using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class UpgradeUI : MonoBehaviour
{
    public static UpgradeUI Instance { get; private set; }
    public static bool IsPaused = false;

    private Canvas      _canvas;
    private ShipLoadout _loadout;

    // Paneller
    private GameObject _generalPanel;
    private GameObject _slotInfoPanel;
    private GameObject _hoverDetailPanel;
    private GameObject _listPanel;

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

        if (installed != null)
            FillBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText, installed);
        else
            ClearBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText);

        ClearBox(_rightNameText, _rightTypeText, _rightTierText, _rightCostText);

        foreach (Transform child in _popupContent.transform)
            Destroy(child.gameObject);

        if (empty)
            BuildEmptyContent(slotIndex);
        else
            BuildFilledContent(slotIndex, installed);
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

    void BuildEmptyContent(int slotIndex)
    {
        foreach (var def in GetCatalogDefs())
        {
            var row = new GameObject("CatalogRow", typeof(RectTransform));
            row.transform.SetParent(_popupContent.transform, false);

            var rowH = row.AddComponent<HorizontalLayoutGroup>();
            rowH.spacing                = 8f;
            rowH.childForceExpandWidth  = false;
            rowH.childForceExpandHeight = true;
            rowH.childAlignment         = TextAnchor.MiddleLeft;

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 52f;
            rowLE.flexibleWidth   = 1f;

            var nameArea = new GameObject("NameArea", typeof(RectTransform));
            nameArea.transform.SetParent(row.transform, false);
            nameArea.AddComponent<Image>().color = Color.clear;

            var hover = nameArea.AddComponent<ComponentRowHover>();
            hover.def = def;

            var nameAreaLE = nameArea.AddComponent<LayoutElement>();
            nameAreaLE.flexibleWidth   = 1f;
            nameAreaLE.preferredHeight = 52f;

            var nameTxtGo = new GameObject("NameText", typeof(RectTransform));
            nameTxtGo.transform.SetParent(nameArea.transform, false);
            var nameTxt = nameTxtGo.AddComponent<Text>();
            nameTxt.text      = def.componentName;
            nameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize  = 17;
            nameTxt.color     = Color.white;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            var nRect = (RectTransform)nameTxtGo.transform;
            nRect.anchorMin = Vector2.zero;
            nRect.anchorMax = Vector2.one;
            nRect.sizeDelta = Vector2.zero;

            var capturedDef = def;
            AddButton(row.transform, "Kur", () =>
                Debug.Log($"Kur: {capturedDef.componentName} -> slot {slotIndex}"), 100f);
        }
    }

    void BuildFilledContent(int slotIndex, ComponentDefinition def)
    {
        MakeTextLabel(_popupContent.transform, $"Tier {def.tier}", 15, TextAnchor.MiddleLeft);

        var btnRow = CreateRow(_popupContent.transform);

        if (def.upgradeTo != null)
            AddButton(btnRow.transform, "Upgrade", () =>
                Debug.Log($"Upgrade: slot {slotIndex}"), 100f);

        if (slotIndex != 5)
            AddButton(btnRow.transform, "Sat", () =>
                Debug.Log($"Sat: slot {slotIndex}"), 100f);
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
    // Canvas / Panel Builder
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

        BuildGeneralPanel();
        BuildSlotInfoPanel();
        BuildHoverDetailPanel();
        BuildListPanel();
    }

    // Sol şerit — tam yükseklik, dar
    void BuildGeneralPanel()
    {
        _generalPanel = new GameObject("GeneralPanel", typeof(RectTransform));
        _generalPanel.transform.SetParent(transform, false);
        _generalPanel.AddComponent<Image>().color = new Color(0.10f, 0.06f, 0.13f, 0.95f);

        var r = (RectTransform)_generalPanel.transform;
        r.anchorMin        = new Vector2(0f, 0f);
        r.anchorMax        = new Vector2(0.11f, 1f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta        = Vector2.zero;

        var vl = _generalPanel.AddComponent<VerticalLayoutGroup>();
        vl.padding                = new RectOffset(10, 10, 12, 12);
        vl.spacing                = 8f;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        var headerTxt = MakeLabel(_generalPanel.transform, "GENEL B\u0130LG\u0130LER", 12, FontStyle.Bold);
        headerTxt.color = new Color(0.4f, 0.3f, 0.55f, 1f);

        foreach (var s in new[] { "HP", "Kalkan", "Enerji" })
        {
            var t = MakeLabel(_generalPanel.transform, s, 12, FontStyle.Normal);
            t.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        foreach (var s in new[] { "Ham Madde: \u2014", "Kristal: \u2014" })
            MakeLabel(_generalPanel.transform, s, 12, FontStyle.Normal);
    }

    // Sol üst — kurulu component bilgisi
    void BuildSlotInfoPanel()
    {
        _slotInfoPanel = new GameObject("SlotInfoPanel", typeof(RectTransform));
        _slotInfoPanel.transform.SetParent(transform, false);
        _slotInfoPanel.AddComponent<Image>().color = new Color(0.05f, 0.10f, 0.18f, 0.95f);

        var r = (RectTransform)_slotInfoPanel.transform;
        r.anchorMin        = new Vector2(0.115f, 0.70f);
        r.anchorMax        = new Vector2(0.375f, 0.98f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta        = Vector2.zero;

        var vl = _slotInfoPanel.AddComponent<VerticalLayoutGroup>();
        vl.padding                = new RectOffset(14, 14, 12, 12);
        vl.spacing                = 10f;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        var headerTxt = MakeLabel(_slotInfoPanel.transform, "SLOT B\u0130LG\u0130S\u0130", 12, FontStyle.Bold);
        headerTxt.color = new Color(0.29f, 0.47f, 0.67f, 1f);

        _leftNameText = MakeLabel(_slotInfoPanel.transform, "", 22, FontStyle.Bold);
        _leftTypeText = MakeLabel(_slotInfoPanel.transform, "", 15, FontStyle.Normal);
        _leftTypeText.color = new Color(0.3f, 0.75f, 0.9f, 1f);
        _leftTierText = MakeLabel(_slotInfoPanel.transform, "", 14, FontStyle.Normal);
        _leftTierText.color = new Color(1f, 0.85f, 0.2f, 1f);
        _leftCostText = MakeLabel(_slotInfoPanel.transform, "", 14, FontStyle.Normal);
        _leftCostText.color = new Color(0.3f, 0.9f, 0.45f, 1f);
    }

    // Sağ üst — hover/tap ile seçilen opsiyon
    void BuildHoverDetailPanel()
    {
        _hoverDetailPanel = new GameObject("HoverDetailPanel", typeof(RectTransform));
        _hoverDetailPanel.transform.SetParent(transform, false);
        _hoverDetailPanel.AddComponent<Image>().color = new Color(0.04f, 0.12f, 0.05f, 0.95f);

        var r = (RectTransform)_hoverDetailPanel.transform;
        r.anchorMin        = new Vector2(0.785f, 0.70f);
        r.anchorMax        = new Vector2(0.995f, 0.98f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta        = Vector2.zero;

        var vl = _hoverDetailPanel.AddComponent<VerticalLayoutGroup>();
        vl.padding                = new RectOffset(16, 16, 12, 12);
        vl.spacing                = 10f;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        var headerTxt = MakeLabel(_hoverDetailPanel.transform, "OPS\u0130YON DETAYI", 12, FontStyle.Bold);
        headerTxt.color = new Color(0.29f, 0.60f, 0.29f, 1f);

        _rightNameText = MakeLabel(_hoverDetailPanel.transform, "", 22, FontStyle.Bold);
        _rightTypeText = MakeLabel(_hoverDetailPanel.transform, "", 15, FontStyle.Normal);
        _rightTypeText.color = new Color(0.3f, 0.75f, 0.9f, 1f);
        _rightTierText = MakeLabel(_hoverDetailPanel.transform, "", 14, FontStyle.Normal);
        _rightTierText.color = new Color(1f, 0.85f, 0.2f, 1f);
        _rightCostText = MakeLabel(_hoverDetailPanel.transform, "", 14, FontStyle.Normal);
        _rightCostText.color = new Color(0.3f, 0.9f, 0.45f, 1f);
    }

    // Sağ alt — component listesi
    void BuildListPanel()
    {
        _listPanel = new GameObject("ListPanel", typeof(RectTransform));
        _listPanel.transform.SetParent(transform, false);
        _listPanel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 0.95f);

        var r = (RectTransform)_listPanel.transform;
        r.anchorMin        = new Vector2(0.785f, 0.025f);
        r.anchorMax        = new Vector2(0.995f, 0.545f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta        = Vector2.zero;

        // Başlık (listenin üst %12'si)
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(_listPanel.transform, false);
        _popupTitle           = titleGo.AddComponent<Text>();
        _popupTitle.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _popupTitle.fontSize  = 18;
        _popupTitle.fontStyle = FontStyle.Bold;
        _popupTitle.color     = Color.white;
        _popupTitle.alignment = TextAnchor.MiddleCenter;
        var titleRect = (RectTransform)titleGo.transform;
        titleRect.anchorMin        = new Vector2(0f, 0.88f);
        titleRect.anchorMax        = new Vector2(1f, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta        = Vector2.zero;

        // İçerik (satır listesi)
        _popupContent = new GameObject("Content", typeof(RectTransform));
        _popupContent.transform.SetParent(_listPanel.transform, false);
        var contentRect = (RectTransform)_popupContent.transform;
        contentRect.anchorMin        = new Vector2(0f, 0f);
        contentRect.anchorMax        = new Vector2(1f, 0.86f);
        contentRect.pivot            = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta        = Vector2.zero;

        var vLayout = _popupContent.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing                = 6f;
        vLayout.padding                = new RectOffset(12, 12, 8, 8);
        vLayout.childAlignment         = TextAnchor.UpperLeft;
        vLayout.childForceExpandWidth  = true;
        vLayout.childForceExpandHeight = false;
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
        h.spacing                = 8f;
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
        le.preferredHeight = 44f;

        AttachLabel(go.transform, label, 16);
    }

    // Tüm panel içi text için ortak yardımcı
    static Text MakeLabel(Transform parent, string text, int fontSize, FontStyle style)
    {
        var go = new GameObject("Txt", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.text      = text;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = fontSize;
        t.fontStyle = style;
        t.color     = Color.white;
        t.alignment = TextAnchor.UpperLeft;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 10f;
        le.flexibleWidth   = 1f;

        return t;
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
