using System.Text;
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

    // Genel panel — canlı stat kutuları
    private RectTransform _hpFill, _shieldFill, _energyFill;
    private Text          _hpStatText, _shieldStatText, _energyStatText;

    // SlotInfo sol sütun — kurulu bileşen temel bilgisi
    private Text _leftNameText;
    private Text _leftTypeText;
    private Text _leftTierText;
    private Text _leftCostText;
    private Text _leftUpgradeCostText;

    // SlotInfo sağ sütun — bileşen aktüel istatistikleri
    private Text _slotStatsText;

    // HoverDetail — opsiyon detayı (bileşen hover veya stat açıklaması)
    private Text _rightNameText;
    private Text _rightTypeText;
    private Text _rightTierText;
    private Text _rightCostText;
    private Text _optionDescText;

    // Seçili stat takibi
    private string _selectedStatKey  = null;
    private int    _selectedStatSlot = -1;
    private int    _currentSlotIndex = -1;

    private static ComponentDefinition[] _catalogDefs;

    // =========================================================================
    // Lifecycle
    // =========================================================================

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
                SpeedController.Instance?.Pause();
            }
            else
            {
                _canvas.enabled = false;
                IsPaused        = false;
                FindFirstObjectByType<CameraController>()?.RestoreFromUpgrade();
                SpeedController.Instance?.Resume();
            }
        }

        if (_canvas != null && _canvas.enabled)
            UpdateStatBoxes();
    }

    // =========================================================================
    // Public API
    // =========================================================================

    public void SetLoadout(ShipLoadout loadout) => _loadout = loadout;

    public void OnSlotClicked(int slotIndex)
    {
        Debug.Log($"[UpgradeUI] OnSlotClicked: slot={slotIndex}");

        // Farklı slot seçilince stat seçimini sıfırla
        if (slotIndex != _currentSlotIndex)
        {
            _selectedStatKey  = null;
            _selectedStatSlot = -1;
            ClearBox(_rightNameText, _rightTypeText, _rightTierText, _rightCostText);
            if (_optionDescText != null) _optionDescText.text = "";
        }
        _currentSlotIndex = slotIndex;

        bool empty = _loadout == null || _loadout.IsSlotEmpty(slotIndex);
        ComponentDefinition installed = empty ? null : _loadout.GetSlotDef(slotIndex);

        if (slotIndex == 5)
        {
            var activeDef = _loadout?.GetWeaponDef(_loadout.GetActiveWeaponType());
            _popupTitle.text = activeDef != null
                ? $"Slot 5 — {activeDef.componentName}"
                : "Slot 5 — Ana Silah";
            FillBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText,
                    activeDef ?? installed);
        }
        else
        {
            _popupTitle.text = empty
                ? $"Slot {slotIndex} — Boş"
                : $"Slot {slotIndex} — {installed.componentName}";
            if (installed != null)
                FillBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText, installed);
            else
                ClearBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText);
        }

        foreach (Transform child in _popupContent.transform)
            Destroy(child.gameObject);

        if (empty)
            BuildEmptyContent(slotIndex);
        else
            BuildFilledContent(slotIndex, installed);

        // Sağ sütun istatistiklerini güncelle
        UpdateSlotInfoStats();
        UpdateCostDisplay(slotIndex);

        // Eğer aynı slotta stat seçiliyse açıklamayı yeniden göster
        if (!string.IsNullOrEmpty(_selectedStatKey) && _selectedStatSlot == slotIndex)
            ShowStatDescriptionInternal(_selectedStatKey);
    }

    /// <summary>Katalog hover — sağ paneli bileşen bilgisiyle doldurur.</summary>
    public void ShowDetail(ComponentDefinition def)
    {
        if (def == null) return;
        if (_optionDescText != null) _optionDescText.text = "";
        FillBox(_rightNameText, _rightTypeText, _rightTierText, _rightCostText, def);

        bool canAfford = ResourceInventory.Instance != null &&
                         ResourceInventory.Instance.Get(def.costResource) >= def.cost;
        _rightCostText.text  = $"{def.cost} {ResourceLabel(def.costResource)}";
        _rightCostText.color = canAfford
            ? new Color(0.3f, 0.9f, 0.45f, 1f)
            : new Color(1f, 0.25f, 0.25f, 1f);
    }

    /// <summary>Katalog hover çıkışı — stat seçiliyse temizleme.</summary>
    public void HideDetail()
    {
        if (!string.IsNullOrEmpty(_selectedStatKey)) return;
        ClearBox(_rightNameText, _rightTypeText, _rightTierText, _rightCostText);
        if (_optionDescText != null) _optionDescText.text = "";
    }

    /// <summary>Stat etiketine tıklandığında çağrılır (toggle davranışı).</summary>
    public void ShowStatDescription(string statKey, int slotIndex)
    {
        if (_selectedStatKey == statKey && _selectedStatSlot == slotIndex)
        {
            DeselectStat();
            return;
        }
        _selectedStatKey  = statKey;
        _selectedStatSlot = slotIndex;
        ShowStatDescriptionInternal(statKey);
        UpdateSlotInfoStats();
        UpdateCostDisplay(_currentSlotIndex);
    }

    public void DeselectStat()
    {
        _selectedStatKey  = null;
        _selectedStatSlot = -1;
        ClearBox(_rightNameText, _rightTypeText, _rightTierText, _rightCostText);
        if (_optionDescText != null) _optionDescText.text = "";
        UpdateSlotInfoStats();
        UpdateCostDisplay(_currentSlotIndex);
    }

    void ShowStatDescriptionInternal(string statKey)
    {
        _rightNameText.text = "";
        _rightTypeText.text = "";
        _rightTierText.text = "";
        _rightCostText.text = "";
        if (_optionDescText != null)
            _optionDescText.text = GetStatDescription(statKey);
    }

    // =========================================================================
    // Popup Content
    // =========================================================================

    void RefreshResourceDisplay() { /* Kaynak göstergesi kaldırıldı — EnergyBar HUD'da gösterilir */ }

    void InstallAndRefresh(ComponentDefinition def, int slotIndex)
    {
        if (_loadout == null) return;
        if (_loadout.InstallComponent(def, slotIndex))
            OnSlotClicked(slotIndex);
    }

    void BuildEmptyContent(int slotIndex)
    {
        foreach (var def in GetCatalogDefs(slotIndex))
        {
            var row = new GameObject("CatalogRow", typeof(RectTransform));
            row.transform.SetParent(_popupContent.transform, false);

            var rowH = row.AddComponent<HorizontalLayoutGroup>();
            rowH.spacing                = 8f;
            rowH.childForceExpandWidth  = false;
            rowH.childForceExpandHeight = true;
            rowH.childAlignment         = TextAnchor.MiddleLeft;

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 74f;
            rowLE.flexibleWidth   = 1f;

            var nameArea = new GameObject("NameArea", typeof(RectTransform));
            nameArea.transform.SetParent(row.transform, false);
            nameArea.AddComponent<Image>().color = Color.clear;

            var hover = nameArea.AddComponent<ComponentRowHover>();
            hover.def = def;

            var nameAreaLE = nameArea.AddComponent<LayoutElement>();
            nameAreaLE.flexibleWidth   = 1f;
            nameAreaLE.preferredHeight = 74f;

            var nameTxtGo = new GameObject("NameText", typeof(RectTransform));
            nameTxtGo.transform.SetParent(nameArea.transform, false);
            var nameTxt = nameTxtGo.AddComponent<Text>();
            nameTxt.text      = def.componentName;
            nameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize  = 32;
            nameTxt.color     = Color.white;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            var nRect = (RectTransform)nameTxtGo.transform;
            nRect.anchorMin = Vector2.zero;
            nRect.anchorMax = Vector2.one;
            nRect.sizeDelta = Vector2.zero;

            bool canAfford = ResourceInventory.Instance != null &&
                             ResourceInventory.Instance.Get(def.costResource) >= def.cost;

            var capturedDef  = def;
            var capturedSlot = slotIndex;
            var kurBtn = AddButton(row.transform, "Kur", () => InstallAndRefresh(capturedDef, capturedSlot), 100f);

            if (!canAfford)
            {
                kurBtn.interactable = false;
                kurBtn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
            }
        }
    }

    void BuildFilledContent(int slotIndex, ComponentDefinition def)
    {
        if (slotIndex == 5)
        {
            BuildWeaponSwitchRows();
            BuildWeaponStatSection();
            return;
        }

        MakeTextLabel(_popupContent.transform, $"Tier {def.tier}", 15, TextAnchor.MiddleLeft);

        var btnRow = CreateRow(_popupContent.transform);

        if (def.upgradeTo != null)
        {
            bool canAfford = ResourceInventory.Instance != null &&
                             ResourceInventory.Instance.Get(def.upgradeTo.costResource) >=
                             (def.upgradeTo.cost - def.sellValue);

            var upgradeBtn = AddButton(btnRow.transform, "Upgrade", () => UpgradeAndRefresh(slotIndex), 100f);
            if (!canAfford)
            {
                upgradeBtn.interactable = false;
                upgradeBtn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
            }
        }

        AddButton(btnRow.transform, "Sat", () => SellAndRefresh(slotIndex), 100f);

        if (def.componentType == ComponentType.Hangar)
            BuildHangarStatSection(slotIndex, def);
        else
            BuildStatUpgradeSection(slotIndex, def);
    }

    void BuildStatUpgradeSection(int slotIndex, ComponentDefinition def)
    {
        var stats = GetStatsForType(def.componentType);
        if (stats == null || stats.Length == 0) return;

        var comp = _loadout?.GetSlotComponent(slotIndex);

        MakeTextLabel(_popupContent.transform, "── STAT UPGRADE ──", 20, TextAnchor.MiddleLeft);

        foreach (var (key, statLabel) in stats)
        {
            int curLevel = comp != null && comp.StatLevels.TryGetValue(key, out var lvl) ? lvl : 0;
            bool maxed   = curLevel >= ShipComponentBase.MaxStatLevel;
            int cost     = maxed ? 0 : StatUpgradeCost(def, curLevel);
            bool canAfford = !maxed && ResourceInventory.Instance != null &&
                             ResourceInventory.Instance.Get(def.costResource) >= cost;

            bool isSelected = !maxed && _selectedStatKey == key && _selectedStatSlot == slotIndex;

            var row = CreateRow(_popupContent.transform);

            var lblGo = new GameObject("StatLabel", typeof(RectTransform));
            lblGo.transform.SetParent(row.transform, false);
            var lbl       = lblGo.AddComponent<Text>();
            lbl.text      = maxed ? $"{statLabel} Lv {curLevel}/5 MAX" : $"{statLabel} Lv {curLevel}/5  ({cost})";
            lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.fontSize  = 24;
            lbl.color     = maxed       ? new Color(1f, 0.85f, 0.2f, 1f)   :
                            isSelected  ? new Color(0.5f, 1f, 0.6f, 1f)    :
                                          Color.white;
            lbl.alignment       = TextAnchor.MiddleLeft;
            lbl.raycastTarget   = true;
            var lblLE           = lblGo.AddComponent<LayoutElement>();
            lblLE.flexibleWidth   = 1f;
            lblLE.preferredHeight = 52f;

            if (!maxed)
            {
                var click       = lblGo.AddComponent<StatLabelClick>();
                click.statKey   = key;
                click.slotIndex = slotIndex;
            }

            if (!maxed)
            {
                var capturedKey  = key;
                var capturedCost = cost;
                var capturedDef  = def;
                var capturedSlot = slotIndex;
                var btn = AddButton(row.transform, "+", () => StatUpgradeAndRefresh(capturedSlot, capturedKey, capturedDef, capturedCost), 44f);
                if (!canAfford)
                {
                    btn.interactable = false;
                    btn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
                }
            }
        }
    }

    void BuildWeaponStatSection()
    {
        var weaponTypes = new[]
        {
            (WeaponType.Laser,   "Lazer"),
            (WeaponType.Kinetic, "Kinetik"),
            (WeaponType.Plasma,  "Plazma"),
        };

        bool headerShown = false;
        foreach (var (type, typeLabel) in weaponTypes)
        {
            if (_loadout == null || !_loadout.IsWeaponTypeUnlocked(type)) continue;
            var curDef = _loadout.GetWeaponDef(type);
            if (curDef == null) continue;

            if (!headerShown)
            {
                MakeTextLabel(_popupContent.transform, "── STAT UPGRADE ──", 20, TextAnchor.MiddleLeft);
                headerShown = true;
            }

            MakeTextLabel(_popupContent.transform, typeLabel, 22, TextAnchor.MiddleLeft);

            var capturedType = type;
            foreach (var (key, statLabel) in new[] { ("damage", "Hasar"), ("fireRate", "Ateş Hızı") })
            {
                int curLevel = _loadout.GetWeaponStatLevel(type, key);
                bool maxed   = curLevel >= ShipComponentBase.MaxStatLevel;
                int cost     = maxed ? 0 : StatUpgradeCost(curDef, curLevel);
                bool canAfford = !maxed && ResourceInventory.Instance != null &&
                                 ResourceInventory.Instance.Get(curDef.costResource) >= cost;

                bool isSelected = !maxed && _selectedStatKey == key && _selectedStatSlot == 5;

                var row = CreateRow(_popupContent.transform);

                var lblGo = new GameObject("StatLabel", typeof(RectTransform));
                lblGo.transform.SetParent(row.transform, false);
                var lbl       = lblGo.AddComponent<Text>();
                lbl.text      = maxed ? $"{statLabel} Lv {curLevel}/5 MAX" : $"{statLabel} Lv {curLevel}/5  ({cost})";
                lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lbl.fontSize  = 24;
                lbl.color     = maxed      ? new Color(1f, 0.85f, 0.2f, 1f) :
                                isSelected ? new Color(0.5f, 1f, 0.6f, 1f)  :
                                             Color.white;
                lbl.alignment     = TextAnchor.MiddleLeft;
                lbl.raycastTarget = true;
                var lblLE         = lblGo.AddComponent<LayoutElement>();
                lblLE.flexibleWidth   = 1f;
                lblLE.preferredHeight = 52f;

                if (!maxed)
                {
                    var click       = lblGo.AddComponent<StatLabelClick>();
                    click.statKey   = key;
                    click.slotIndex = 5;
                }

                if (!maxed)
                {
                    var capturedKey  = key;
                    var capturedCost = cost;
                    var capturedDef  = curDef;
                    var btn = AddButton(row.transform, "+", () => WeaponStatUpgradeAndRefresh(capturedType, capturedKey, capturedDef, capturedCost), 44f);
                    if (!canAfford)
                    {
                        btn.interactable = false;
                        btn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
                    }
                }
            }
        }
    }

    void BuildHangarStatSection(int slotIndex, ComponentDefinition def)
    {
        var hangar = _loadout?.GetSlotComponent(slotIndex) as HangarComponent;
        if (hangar == null) return;

        MakeTextLabel(_popupContent.transform, "── HANGAR ──", 20, TextAnchor.MiddleLeft);

        BuildHangarCountRow(slotIndex, def, hangar, "maxCollectors", "Max Toplayıcı", hangar.MaxCollectors);
        BuildHangarCountRow(slotIndex, def, hangar, "maxFighters",   "Max Savaşçı",   hangar.MaxFighters);

        MakeTextLabel(_popupContent.transform, "── STAT UPGRADE ──", 20, TextAnchor.MiddleLeft);

        foreach (var (key, label) in new[]
        {
            ("productionSpeed", "Üretim Hızı"),
            ("maxHP",           "Gemi Max HP"),
            ("fireRate",        "Savaşçı Ateş"),
            ("salvageRate",     "Toplama Hızı"),
            ("speed",           "Gemi Hızı"),
        })
        {
            int curLevel = hangar.StatLevels.TryGetValue(key, out var lvl) ? lvl : 0;
            bool maxed   = curLevel >= ShipComponentBase.MaxStatLevel;
            int cost     = maxed ? 0 : StatUpgradeCost(def, curLevel);
            bool canAfford = !maxed && ResourceInventory.Instance != null &&
                             ResourceInventory.Instance.Get(def.costResource) >= cost;

            bool isSelected = !maxed && _selectedStatKey == key && _selectedStatSlot == slotIndex;

            var row = CreateRow(_popupContent.transform);
            var lblGo = new GameObject("StatLabel", typeof(RectTransform));
            lblGo.transform.SetParent(row.transform, false);
            var lbl       = lblGo.AddComponent<Text>();
            lbl.text      = maxed ? $"{label} Lv {curLevel}/5 MAX" : $"{label} Lv {curLevel}/5  ({cost})";
            lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.fontSize  = 24;
            lbl.color     = maxed      ? new Color(1f, 0.85f, 0.2f, 1f) :
                            isSelected ? new Color(0.5f, 1f, 0.6f, 1f)  :
                                         Color.white;
            lbl.alignment     = TextAnchor.MiddleLeft;
            lbl.raycastTarget = true;
            var lblLE         = lblGo.AddComponent<LayoutElement>();
            lblLE.flexibleWidth   = 1f;
            lblLE.preferredHeight = 50f;

            if (!maxed)
            {
                var click       = lblGo.AddComponent<StatLabelClick>();
                click.statKey   = key;
                click.slotIndex = slotIndex;
            }

            if (!maxed)
            {
                var capturedKey  = key;
                var capturedCost = cost;
                var capturedDef  = def;
                var capturedSlot = slotIndex;
                var btn = AddButton(row.transform, "+", () => StatUpgradeAndRefresh(capturedSlot, capturedKey, capturedDef, capturedCost), 44f);
                if (!canAfford)
                {
                    btn.interactable = false;
                    btn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
                }
            }
        }
    }

    void BuildHangarCountRow(int slotIndex, ComponentDefinition def, HangarComponent hangar, string key, string label, int currentCount)
    {
        int level  = hangar.GetStatLevel(key);
        bool maxed = level >= ShipComponentBase.MaxStatLevel;
        int cost   = maxed ? 0 : StatUpgradeCost(def, level);
        bool canAfford = !maxed && ResourceInventory.Instance != null &&
                         ResourceInventory.Instance.Get(def.costResource) >= cost;

        bool isSelected = !maxed && _selectedStatKey == key && _selectedStatSlot == slotIndex;

        var row = CreateRow(_popupContent.transform);
        var lblGo = new GameObject("StatLabel", typeof(RectTransform));
        lblGo.transform.SetParent(row.transform, false);
        var lbl       = lblGo.AddComponent<Text>();
        lbl.text      = maxed ? $"{label}: {currentCount} MAX" : $"{label}: {currentCount}  (+1 → {cost})";
        lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.fontSize  = 24;
        lbl.color     = maxed      ? new Color(1f, 0.85f, 0.2f, 1f) :
                        isSelected ? new Color(0.5f, 1f, 0.6f, 1f)  :
                                     Color.white;
        lbl.alignment     = TextAnchor.MiddleLeft;
        lbl.raycastTarget = true;
        var lblLE         = lblGo.AddComponent<LayoutElement>();
        lblLE.flexibleWidth   = 1f;
        lblLE.preferredHeight = 34f;

        if (!maxed)
        {
            var click       = lblGo.AddComponent<StatLabelClick>();
            click.statKey   = key;
            click.slotIndex = slotIndex;
        }

        if (!maxed)
        {
            var capturedKey  = key;
            var capturedCost = cost;
            var capturedDef  = def;
            var capturedSlot = slotIndex;
            var btn = AddButton(row.transform, "+1", () => StatUpgradeAndRefresh(capturedSlot, capturedKey, capturedDef, capturedCost), 50f);
            if (!canAfford)
            {
                btn.interactable = false;
                btn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
            }
        }
    }

    static int StatUpgradeCost(ComponentDefinition def, int currentLevel)
    {
        int baseCost = Mathf.Max(1, Mathf.RoundToInt(def.cost * 0.2f));
        return Mathf.RoundToInt(baseCost * Mathf.Pow(2f, currentLevel));
    }

    static (string key, string label)[] GetStatsForType(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Generator:  return new[] { ("production", "Üretim") };
            case ComponentType.Shield:     return new[] { ("rechargeRate", "Şarj Hızı"), ("maxShield", "Max Kalkan") };
            case ComponentType.RepairUnit: return new[] { ("repairRate", "Tamir Hızı"), ("energyEfficiency", "Enerji Verimi") };
            case ComponentType.Turret:     return new[] { ("damage", "Hasar"), ("fireRate", "Ateş Hızı") };
            default:                       return null;
        }
    }

    void StatUpgradeAndRefresh(int slotIndex, string key, ComponentDefinition def, int cost)
    {
        if (_loadout == null) return;
        var comp = _loadout.GetSlotComponent(slotIndex);
        if (comp == null) return;
        if (!ResourceInventory.Instance.TrySpend(def.costResource, cost)) return;
        comp.ApplyStatUpgrade(key);
        OnSlotClicked(slotIndex);
    }

    void WeaponStatUpgradeAndRefresh(WeaponType type, string key, ComponentDefinition def, int cost)
    {
        if (_loadout == null) return;
        if (!ResourceInventory.Instance.TrySpend(def.costResource, cost)) return;
        _loadout.ApplyWeaponStatUpgrade(type, key);
        OnSlotClicked(5);
    }

    void BuildWeaponSwitchRows()
    {
        MakeTextLabel(_popupContent.transform, "ANA SİLAH", 24, TextAnchor.MiddleLeft);

        var weaponTypes = new[]
        {
            (WeaponType.Laser,   "Lazer"),
            (WeaponType.Kinetic, "Kinetik"),
            (WeaponType.Plasma,  "Plazma"),
        };

        foreach (var (type, label) in weaponTypes)
        {
            bool unlocked = _loadout != null && _loadout.IsWeaponTypeUnlocked(type);
            bool isActive = _loadout != null && _loadout.GetActiveWeaponType() == type;
            var capturedType = type;

            var row = CreateRow(_popupContent.transform);

            ComponentDefinition curDef  = unlocked ? _loadout.GetWeaponDef(type) : ShipLoadout.GetWeaponChainStart(type);
            string tierStr = unlocked ? $" Mk{curDef?.tier ?? 1}" : " —";
            var nameGo = new GameObject("WeaponLabel", typeof(RectTransform));
            nameGo.transform.SetParent(row.transform, false);
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.text      = label + tierStr;
            nameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize  = 28;
            nameTxt.color     = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            nameTxt.alignment = TextAnchor.MiddleLeft;
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.flexibleWidth   = 1f;
            nameLE.preferredHeight = 58f;

            if (!unlocked)
            {
                ComponentDefinition unlockDef = ShipLoadout.GetWeaponChainStart(type);
                bool canAfford = ResourceInventory.Instance != null && unlockDef != null &&
                                 ResourceInventory.Instance.Get(unlockDef.costResource) >= unlockDef.cost;
                string costLabel = unlockDef != null ? $"Satın Al\n{unlockDef.cost}" : "Satın Al";
                var buyBtn = AddButton(row.transform, costLabel, () => UnlockWeaponAndRefresh(capturedType), 80f);
                if (!canAfford)
                {
                    buyBtn.interactable = false;
                    buyBtn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
                }
            }
            else
            {
                var selBtn = AddButton(row.transform, "Seç", () => SwitchWeaponAndRefresh(capturedType), 60f);
                if (isActive)
                    selBtn.GetComponent<Image>().color = new Color(0.10f, 0.55f, 0.20f, 1f);

                if (curDef?.upgradeTo != null)
                {
                    int diff = curDef.upgradeTo.cost - curDef.sellValue;
                    bool canAfford = ResourceInventory.Instance != null &&
                                     ResourceInventory.Instance.Get(curDef.upgradeTo.costResource) >= diff;
                    string upLabel = $"↑Mk{curDef.upgradeTo.tier}\n{diff}";
                    var upBtn = AddButton(row.transform, upLabel, () => UpgradeWeaponTypeAndRefresh(capturedType), 70f);
                    if (!canAfford)
                    {
                        upBtn.interactable = false;
                        upBtn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
                    }
                }
            }
        }
    }

    void UnlockWeaponAndRefresh(WeaponType type)
    {
        if (_loadout == null) return;
        if (_loadout.UnlockWeaponType(type))
            OnSlotClicked(5);
    }

    void SwitchWeaponAndRefresh(WeaponType type)
    {
        _loadout?.SwitchWeapon(type);
        OnSlotClicked(5);
    }

    void UpgradeWeaponTypeAndRefresh(WeaponType type)
    {
        if (_loadout == null) return;
        if (_loadout.UpgradeWeaponType(type))
            OnSlotClicked(5);
    }

    void UpgradeAndRefresh(int slotIndex)
    {
        if (_loadout == null) return;
        if (_loadout.UpgradeComponent(slotIndex))
            OnSlotClicked(slotIndex);
    }

    void SellAndRefresh(int slotIndex)
    {
        if (_loadout == null) return;
        if (_loadout.SellComponent(slotIndex))
            OnSlotClicked(slotIndex);
    }

    // =========================================================================
    // Stat Açıklamaları ve Bileşen İstatistikleri
    // =========================================================================

    void UpdateSlotInfoStats()
    {
        if (_slotStatsText == null) return;
        _slotStatsText.text = _currentSlotIndex >= 0
            ? BuildComponentStatsText(_currentSlotIndex)
            : "";
    }

    void UpdateCostDisplay(int slotIndex)
    {
        if (_leftCostText == null) return;

        if (slotIndex < 0)
        {
            _leftCostText.text = "";
            if (_leftUpgradeCostText != null) _leftUpgradeCostText.text = "";
            return;
        }

        if (slotIndex == 5)
        {
            UpdateWeaponCostDisplay();
            return;
        }

        var def  = _loadout?.GetSlotDef(slotIndex);
        var comp = _loadout?.GetSlotComponent(slotIndex);
        if (def == null)
        {
            _leftCostText.text = "";
            if (_leftUpgradeCostText != null) _leftUpgradeCostText.text = "";
            return;
        }

        // Toplam harcanan: kurulum + tüm stat upgrade maliyetleri
        int total = def.cost;
        if (comp != null)
            foreach (var kvp in comp.StatLevels)
                for (int i = 0; i < kvp.Value; i++)
                    total += StatUpgradeCost(def, i);

        string resLabel = ResourceLabel(def.costResource);
        _leftCostText.text  = $"{total} {resLabel}";
        _leftCostText.color = new Color(0.3f, 0.9f, 0.45f, 1f);

        // Seçili stat upgrade masrafı
        if (_leftUpgradeCostText == null) return;
        if (!string.IsNullOrEmpty(_selectedStatKey) && _selectedStatSlot == slotIndex)
        {
            int curLevel = comp != null && comp.StatLevels.TryGetValue(_selectedStatKey, out var lvl) ? lvl : 0;
            if (curLevel < ShipComponentBase.MaxStatLevel)
            {
                int upgCost    = StatUpgradeCost(def, curLevel);
                bool canAfford = ResourceInventory.Instance != null &&
                                 ResourceInventory.Instance.Get(def.costResource) >= upgCost;
                _leftUpgradeCostText.text  = $"(+{upgCost} {resLabel})";
                _leftUpgradeCostText.color = canAfford
                    ? new Color(0.3f, 0.9f, 0.45f, 1f)
                    : new Color(1f, 0.25f, 0.25f, 1f);
            }
            else _leftUpgradeCostText.text = "";
        }
        else _leftUpgradeCostText.text = "";
    }

    void UpdateWeaponCostDisplay()
    {
        if (_loadout == null) return;
        var type   = _loadout.GetActiveWeaponType();
        var curDef = _loadout.GetWeaponDef(type);
        if (curDef == null) return;

        // Zincir boyunca harcanan miktar (Mk1→current)
        int total = 0;
        var it  = ShipLoadout.GetWeaponChainStart(type);
        ComponentDefinition prev = null;
        while (it != null)
        {
            total += prev == null ? it.cost : Mathf.Max(0, it.cost - prev.sellValue);
            if (it == curDef) break;
            prev = it;
            it   = it.upgradeTo;
        }
        // Stat upgrade maliyetleri
        foreach (var key in new[] { "damage", "fireRate" })
        {
            int level = _loadout.GetWeaponStatLevel(type, key);
            for (int i = 0; i < level; i++)
                total += StatUpgradeCost(curDef, i);
        }

        string resLabel = ResourceLabel(curDef.costResource);
        _leftCostText.text  = $"{total} {resLabel}";
        _leftCostText.color = new Color(0.3f, 0.9f, 0.45f, 1f);

        if (_leftUpgradeCostText == null) return;
        if (!string.IsNullOrEmpty(_selectedStatKey) && _selectedStatSlot == 5)
        {
            int curLevel = _loadout.GetWeaponStatLevel(type, _selectedStatKey);
            if (curLevel < ShipComponentBase.MaxStatLevel)
            {
                int upgCost    = StatUpgradeCost(curDef, curLevel);
                bool canAfford = ResourceInventory.Instance != null &&
                                 ResourceInventory.Instance.Get(curDef.costResource) >= upgCost;
                _leftUpgradeCostText.text  = $"(+{upgCost} {resLabel})";
                _leftUpgradeCostText.color = canAfford
                    ? new Color(0.3f, 0.9f, 0.45f, 1f)
                    : new Color(1f, 0.25f, 0.25f, 1f);
            }
            else _leftUpgradeCostText.text = "";
        }
        else _leftUpgradeCostText.text = "";
    }

    static string ResourceLabel(ResourceType res) =>
        res == ResourceType.EnergyCrystal ? "Kristal" : "Ham Madde";

    string BuildComponentStatsText(int slotIndex)
    {
        if (slotIndex == 5) return BuildWeaponStatsText();

        var def  = _loadout?.GetSlotDef(slotIndex);
        var comp = _loadout?.GetSlotComponent(slotIndex);
        if (def == null || comp == null) return "";

        var sb = new StringBuilder();

        switch (def.componentType)
        {
            case ComponentType.Turret:
            {
                var tc = comp as TurretController;
                if (tc == null) break;
                float dmg = tc.damage    * tc.GetMultiplier("damage");
                float sps = tc.GetMultiplier("fireRate") / tc.fireRate;
                float eng = tc.energyPerShot;

                sb.AppendLine(DeltaLine("Hasar",        dmg, "damage",    false));
                sb.AppendLine(DeltaLine("Ateş/sn", sps, "fireRate",  false));
                sb.AppendLine($"Enerji/atış: {eng:0.##}");
                if (tc.magazineSize > 0)
                {
                    sb.AppendLine($"Şajör: {tc.magazineSize}");
                    sb.AppendLine($"Yeniden şarj: {tc.reloadTime:0.#}s");
                }
                break;
            }
            case ComponentType.RepairUnit:
            {
                var ru = comp as RepairUnitComponent;
                if (ru == null) break;
                float rate = ru.repairRate    * ru.GetMultiplier("repairRate");
                float eng  = ru.energyPerRepair / ru.GetMultiplier("energyEfficiency");
                sb.AppendLine(DeltaLine("Tamir/sn",  rate, "repairRate",       false));
                sb.AppendLine(DeltaLine("Enerji/sn", eng,  "energyEfficiency", true));
                break;
            }
            case ComponentType.Generator:
            {
                var gen = comp as GeneratorComponent;
                if (gen == null) break;
                float prod = gen.productionAmount * gen.GetMultiplier("production");
                sb.AppendLine(DeltaLine("Üretim/sn", prod, "production", false));
                break;
            }
            case ComponentType.Shield:
            {
                var sg = comp as ShieldGeneratorComponent;
                if (sg == null) break;
                float maxS     = sg.maxShield    * sg.GetMultiplier("maxShield");
                float recharge = sg.rechargeRate * sg.GetMultiplier("rechargeRate");
                sb.AppendLine(DeltaLine("Max kalkan", maxS,     "maxShield",    false));
                sb.AppendLine(DeltaLine("Şarj/sn", recharge, "rechargeRate", false));
                break;
            }
            case ComponentType.Hangar:
            {
                var hc = comp as HangarComponent;
                if (hc == null) break;
                sb.AppendLine($"Max toplayıcı: {hc.MaxCollectors}");
                sb.AppendLine($"Max savaşçı: {hc.MaxFighters}");
                float eff = hc.EffProductionTime;
                sb.AppendLine($"Üretim süresi: {eff:0.#}s");
                sb.AppendLine($"Gemi hızı: {hc.EffShipSpeed:0.#}");
                break;
            }
        }

        return sb.ToString().TrimEnd();
    }

    string BuildWeaponStatsText()
    {
        if (_loadout == null) return "";
        var type = _loadout.GetActiveWeaponType();
        var def  = _loadout.GetWeaponDef(type);
        if (def == null) return "";

        // WeaponController'dan güncel değerleri al (stat upgrade uygulanmış)
        var wc = _loadout.GetComponentInChildren<WeaponController>();
        if (wc == null)
        {
            return $"Hasar: {def.weaponDamage:0.#}\nAteş aralığı: {def.weaponFireRate:0.##}s";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Hasar: {wc.damage:0.#}");
        sb.AppendLine($"Ateş aralığı: {wc.fireRate:0.##}s");
        if (wc.energyCostPerShot > 0f)
            sb.AppendLine($"Enerji/atış: {wc.energyCostPerShot:0.#}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Seçili stat bu satırla eşleşiyorsa delta değeri ekler.
    /// isDecreasing=true → upgrade azaltıcı (enerji verimi gibi).
    /// </summary>
    string DeltaLine(string label, float current, string statKey, bool isDecreasing)
    {
        string valStr = $"{current:0.##}";
        if (_selectedStatKey != statKey || _selectedStatSlot != _currentSlotIndex)
            return $"{label}: {valStr}";

        float delta;
        string sign;
        if (!isDecreasing) { delta = current * 0.5f;            sign = "+"; }
        else               { delta = current * (1f - 1f/1.5f);  sign = "-"; }

        return $"{label}: {valStr}  ({sign}{delta:0.##})";
    }

    static string GetStatDescription(string key)
    {
        switch (key)
        {
            case "damage":           return "Turretin verdiği hasarı artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "fireRate":         return "Ateş hızını artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "repairRate":       return "Saniyede tamir edilen HP miktarını artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "energyEfficiency": return "Tamir için harcanan enerji miktarını azaltır.\nHer seviye ÷1.5 azalma sağlar.";
            case "production":       return "Saniyede üretilen enerji miktarını artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "rechargeRate":     return "Kalkan şarj hızını artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "maxShield":        return "Maksimum kalkan kapasitesini artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "productionSpeed":  return "Toplayıcı/savaşçı üretim hızını artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "maxHP":            return "Gemi bileşenlerinin dayanaklılığını artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "salvageRate":      return "Enkaz toplama hızını artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "speed":            return "Toplayıcı/savaşçı gemi hızını artırır.\nHer seviye ×1.5 çarpanı ekler.";
            case "maxCollectors":    return "Hangarın maksimum toplayıcı sayısını artırır.\nHer seviye +1 toplayıcı ekler.";
            case "maxFighters":      return "Hangarın maksimum savaşçı sayısını artırır.\nHer seviye +1 savaşçı ekler.";
            default:                 return key;
        }
    }

    // =========================================================================
    // Hardcoded Catalog
    // =========================================================================

    static ComponentDefinition[] _weaponDefs;

    static ComponentDefinition[] GetCatalogDefs(int slotIndex) => GetComponentDefs();

    static ComponentDefinition[] GetComponentDefs()
    {
        if (_catalogDefs != null) return _catalogDefs;

        var shield = ScriptableObject.CreateInstance<ComponentDefinition>();
        shield.componentName = "Kalkan Jeneratörü Mk1";
        shield.componentType = ComponentType.Shield;
        shield.tier          = 1;
        shield.costResource  = ResourceType.RawMaterial;
        shield.cost          = 10;
        shield.sellValue     = 5;
        shield.maxShield     = 100f;   // Bug fix: katalog tanımında değer eksikti
        shield.rechargeRate  = 1.5f;   // Bug fix: katalog tanımında değer eksikti

        var repair = ScriptableObject.CreateInstance<ComponentDefinition>();
        repair.componentName = "Onarım Birimi Mk1";
        repair.componentType = ComponentType.RepairUnit;
        repair.tier          = 1;
        repair.costResource  = ResourceType.RawMaterial;
        repair.cost          = 8;
        repair.sellValue     = 4;
        repair.repairRate    = 8f;

        var gen = ScriptableObject.CreateInstance<ComponentDefinition>();
        gen.componentName    = "Enerji Jeneratörü Mk1";
        gen.componentType    = ComponentType.Generator;
        gen.tier             = 1;
        gen.costResource     = ResourceType.RawMaterial;
        gen.cost             = 15;
        gen.sellValue        = 7;
        gen.productionAmount = 10f;

        // Turretler — Lazer ve Plazma RawMaterial kullanır (önceden Crystal'dı, kaynak sıkıntısı yaratıyordu)
        var gatling = T("Gatling Turret",   TurretType.Gatling,      ResourceType.RawMaterial,    12,
            fireRate:1f,  damage:5f,  speed:9f,  life:3f,  energy:0.5f, mag:10, reload:3f);
        var plasma  = T("Plazma Turret",    TurretType.Plasma,       ResourceType.RawMaterial,    20,
            fireRate:20f, damage:25f, speed:5f,  life:4f,  energy:4f);
        var laser   = T("Lazer Turret",     TurretType.Laser,        ResourceType.RawMaterial,    18,
            fireRate:5f,  damage:15f, speed:14f, life:4f,  energy:3f);
        var rocket  = T("Roket Turret",     TurretType.Rocket,       ResourceType.RawMaterial,    22,
            fireRate:30f, damage:50f, speed:7f,  life:5f, energy:0.5f);
        var pd      = T("Point Defence",    TurretType.PointDefence, ResourceType.RawMaterial,    15,
            fireRate:1f,  damage:4f,  speed:8f,  life:0.8f,energy:1f);

        _catalogDefs = new[] { shield, repair, gen, gatling, plasma, laser, rocket, pd };
        return _catalogDefs;
    }

    static ComponentDefinition T(string name, TurretType tt, ResourceType res, int cost,
        float fireRate, float damage, float speed, float life, float energy,
        int mag = 0, float reload = 0f)
    {
        var d = ScriptableObject.CreateInstance<ComponentDefinition>();
        d.componentName        = name;
        d.componentType        = ComponentType.Turret;
        d.tier                 = 1;
        d.costResource         = res;
        d.cost                 = cost;
        d.sellValue            = cost / 2;
        d.turretType           = tt;
        d.turretFireRate       = fireRate;
        d.turretDamage         = damage;
        d.turretBulletSpeed    = speed;
        d.turretBulletLifeTime = life;
        d.turretEnergyPerShot  = energy;
        d.turretMagazineSize   = mag;
        d.turretReloadTime     = reload;
        return d;
    }

    static string TypeLabel(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Shield:     return "Kalkan";
            case ComponentType.RepairUnit: return "Onarım Birimi";
            case ComponentType.Generator:  return "Enerji Jeneratörü";
            case ComponentType.Weapon:     return "Silah";
            case ComponentType.Turret:     return "Turret";
            case ComponentType.Hangar:     return "Hangar";
            default:                       return type.ToString();
        }
    }

    // =========================================================================
    // Canvas / Panel Builder
    // =========================================================================

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
    // Üst %5 boş bırakılır: EnergyBar HUD görünür kalsın
    void BuildGeneralPanel()
    {
        _generalPanel = new GameObject("GeneralPanel", typeof(RectTransform));
        _generalPanel.transform.SetParent(transform, false);
        _generalPanel.AddComponent<Image>().color = new Color(0.10f, 0.06f, 0.13f, 0.95f);

        var r = (RectTransform)_generalPanel.transform;
        r.anchorMin        = new Vector2(0f, 0f);
        r.anchorMax        = new Vector2(0.11f, 0.95f);  // üst %5 serbest
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta        = Vector2.zero;

        var vl = _generalPanel.AddComponent<VerticalLayoutGroup>();
        vl.padding                = new RectOffset(10, 10, 12, 12);
        vl.spacing                = 8f;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        var headerTxt = MakeLabel(_generalPanel.transform, "GENEL BİLGİLER", 36, FontStyle.Bold);
        headerTxt.color = new Color(0.4f, 0.3f, 0.55f, 1f);

        (_hpFill,     _hpStatText)     = MakeStatBox(_generalPanel.transform, "HP",
            new Color(0.12f, 0.22f, 0.12f, 0.88f), new Color(0.25f, 0.72f, 0.30f, 1f));
        (_shieldFill, _shieldStatText) = MakeStatBox(_generalPanel.transform, "KALKAN",
            new Color(0.10f, 0.14f, 0.26f, 0.88f), new Color(0.25f, 0.55f, 0.90f, 1f));
        (_energyFill, _energyStatText) = MakeStatBox(_generalPanel.transform, "ENERJİ",
            new Color(0.22f, 0.14f, 0.04f, 0.88f), new Color(0.95f, 0.65f, 0.10f, 1f));
        // Ham madde ve kristal göstergeleri kaldırıldı — EnergyBar HUD'da mevcut
    }

    // Sol üst — kurulu bileşen bilgisi (genişletildi, 2 sütun)
    void BuildSlotInfoPanel()
    {
        _slotInfoPanel = new GameObject("SlotInfoPanel", typeof(RectTransform));
        _slotInfoPanel.transform.SetParent(transform, false);
        _slotInfoPanel.AddComponent<Image>().color = new Color(0.05f, 0.10f, 0.18f, 0.95f);
        _slotInfoPanel.AddComponent<DeselectOnClick>();

        var r = (RectTransform)_slotInfoPanel.transform;
        r.anchorMin        = new Vector2(0.115f, 0.67f);
        r.anchorMax        = new Vector2(0.57f,  0.95f);  // genişletildi + üst boşluk
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta        = Vector2.zero;

        var outerVL = _slotInfoPanel.AddComponent<VerticalLayoutGroup>();
        outerVL.padding                = new RectOffset(14, 14, 12, 12);
        outerVL.spacing                = 8f;
        outerVL.childForceExpandWidth  = true;
        outerVL.childForceExpandHeight = false;

        // Başlık
        var headerTxt = MakeLabel(_slotInfoPanel.transform, "SLOT BİLGİSİ", 36, FontStyle.Bold);
        headerTxt.color = new Color(0.29f, 0.47f, 0.67f, 1f);

        // İçerik alanı — iki sütun yan yana
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(_slotInfoPanel.transform, false);
        var contentLE = contentGo.AddComponent<LayoutElement>();
        contentLE.flexibleWidth   = 1f;
        contentLE.preferredHeight = 220f;

        var hLayout = contentGo.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing                = 14f;
        hLayout.childForceExpandWidth  = true;
        hLayout.childForceExpandHeight = true;

        // Sol sütun — temel bilgi
        var leftCol = new GameObject("LeftCol", typeof(RectTransform));
        leftCol.transform.SetParent(contentGo.transform, false);
        var leftVL = leftCol.AddComponent<VerticalLayoutGroup>();
        leftVL.spacing                = 6f;
        leftVL.childForceExpandWidth  = true;
        leftVL.childForceExpandHeight = false;
        leftCol.AddComponent<LayoutElement>().flexibleWidth = 1f;

        _leftNameText = MakeLabel(leftCol.transform, "", 36, FontStyle.Bold);
        _leftTypeText = MakeLabel(leftCol.transform, "", 26, FontStyle.Normal);
        _leftTypeText.color = new Color(0.3f, 0.75f, 0.9f, 1f);
        _leftTierText = MakeLabel(leftCol.transform, "", 24, FontStyle.Normal);
        _leftTierText.color = new Color(1f, 0.85f, 0.2f, 1f);
        _leftCostText = MakeLabel(leftCol.transform, "", 22, FontStyle.Normal);
        _leftCostText.color = new Color(0.3f, 0.9f, 0.45f, 1f);
        _leftUpgradeCostText = MakeLabel(leftCol.transform, "", 22, FontStyle.Normal);
        _leftUpgradeCostText.color = new Color(1f, 0.25f, 0.25f, 1f);

        // Sağ sütun — bileşen istatistikleri
        var rightCol = new GameObject("RightCol", typeof(RectTransform));
        rightCol.transform.SetParent(contentGo.transform, false);
        var rightVL = rightCol.AddComponent<VerticalLayoutGroup>();
        rightVL.spacing                = 4f;
        rightVL.childForceExpandWidth  = true;
        rightVL.childForceExpandHeight = false;
        var rightLE = rightCol.AddComponent<LayoutElement>();
        rightLE.flexibleWidth = 1f;
        rightLE.minWidth      = 220f;

        var statsGo = new GameObject("StatsText", typeof(RectTransform));
        statsGo.transform.SetParent(rightCol.transform, false);
        _slotStatsText                   = statsGo.AddComponent<Text>();
        _slotStatsText.font              = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _slotStatsText.fontSize          = 22;
        _slotStatsText.color             = new Color(0.85f, 0.95f, 1f, 1f);
        _slotStatsText.alignment         = TextAnchor.UpperLeft;
        _slotStatsText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _slotStatsText.verticalOverflow   = VerticalWrapMode.Overflow;
        var statsLE = statsGo.AddComponent<LayoutElement>();
        statsLE.flexibleWidth   = 1f;
        statsLE.preferredHeight = 220f;
    }

    // Sağ üst — hover/tap ile seçilen opsiyon detayı
    void BuildHoverDetailPanel()
    {
        _hoverDetailPanel = new GameObject("HoverDetailPanel", typeof(RectTransform));
        _hoverDetailPanel.transform.SetParent(transform, false);
        _hoverDetailPanel.AddComponent<Image>().color = new Color(0.04f, 0.12f, 0.05f, 0.95f);
        _hoverDetailPanel.AddComponent<DeselectOnClick>();

        var r = (RectTransform)_hoverDetailPanel.transform;
        r.anchorMin        = new Vector2(0.785f, 0.67f);
        r.anchorMax        = new Vector2(0.995f, 0.95f);  // üst boşluk
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta        = Vector2.zero;

        var vl = _hoverDetailPanel.AddComponent<VerticalLayoutGroup>();
        vl.padding                = new RectOffset(16, 16, 12, 12);
        vl.spacing                = 10f;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;

        var headerTxt = MakeLabel(_hoverDetailPanel.transform, "OPSİYON DETAYI", 36, FontStyle.Bold);
        headerTxt.color = new Color(0.29f, 0.60f, 0.29f, 1f);

        // Bileşen bilgisi alanı (katalog hover için)
        _rightNameText = MakeLabel(_hoverDetailPanel.transform, "", 36, FontStyle.Bold);
        _rightTypeText = MakeLabel(_hoverDetailPanel.transform, "", 26, FontStyle.Normal);
        _rightTypeText.color = new Color(0.3f, 0.75f, 0.9f, 1f);
        _rightTierText = MakeLabel(_hoverDetailPanel.transform, "", 24, FontStyle.Normal);
        _rightTierText.color = new Color(1f, 0.85f, 0.2f, 1f);
        _rightCostText = MakeLabel(_hoverDetailPanel.transform, "", 24, FontStyle.Normal);
        _rightCostText.color = new Color(0.3f, 0.9f, 0.45f, 1f);

        // Stat açıklama alanı (stat label click için)
        var descGo = new GameObject("OptionDesc", typeof(RectTransform));
        descGo.transform.SetParent(_hoverDetailPanel.transform, false);
        _optionDescText                   = descGo.AddComponent<Text>();
        _optionDescText.font              = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _optionDescText.fontSize          = 22;
        _optionDescText.color             = new Color(0.85f, 0.98f, 0.85f, 1f);
        _optionDescText.alignment         = TextAnchor.UpperLeft;
        _optionDescText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _optionDescText.verticalOverflow   = VerticalWrapMode.Overflow;
        var descLE = descGo.AddComponent<LayoutElement>();
        descLE.flexibleWidth   = 1f;
        descLE.preferredHeight = 160f;
    }

    // Sağ alt — bileşen listesi
    void BuildListPanel()
    {
        _listPanel = new GameObject("ListPanel", typeof(RectTransform));
        _listPanel.transform.SetParent(transform, false);
        _listPanel.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.08f, 0.95f);
        _listPanel.AddComponent<DeselectOnClick>();

        var r = (RectTransform)_listPanel.transform;
        r.anchorMin        = new Vector2(0.785f, 0.025f);
        r.anchorMax        = new Vector2(0.995f, 0.545f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta        = Vector2.zero;

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(_listPanel.transform, false);
        _popupTitle           = titleGo.AddComponent<Text>();
        _popupTitle.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _popupTitle.fontSize  = 34;
        _popupTitle.fontStyle = FontStyle.Bold;
        _popupTitle.color     = Color.white;
        _popupTitle.alignment = TextAnchor.MiddleCenter;
        var titleRect = (RectTransform)titleGo.transform;
        titleRect.anchorMin        = new Vector2(0f, 0.88f);
        titleRect.anchorMax        = new Vector2(1f, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta        = Vector2.zero;

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

    // =========================================================================
    // Detail Helpers
    // =========================================================================

    static void FillBox(Text n, Text ty, Text ti, Text c, ComponentDefinition def)
    {
        if (def == null) return;
        n.text  = def.componentName;
        ty.text = TypeLabel(def.componentType);
        ti.text = $"Tier {def.tier}";
        // Maliyet UpdateCostDisplay() tarafından ayrıca set edilir
        if (c != null) c.text = "";
    }

    static void ClearBox(Text n, Text ty, Text ti, Text c)
    {
        if (n == null) return;
        n.text = ty.text = ti.text = c.text = "";
    }

    // =========================================================================
    // UI Helpers
    // =========================================================================

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
        le.preferredHeight = 58f;
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

    static Button AddButton(Transform parent, string label, UnityAction onClick, float width = 80f)
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
        le.preferredHeight = 62f;

        AttachLabel(go.transform, label, 28);
        return btn;
    }

    static (RectTransform fill, Text txt) MakeStatBox(Transform parent, string title, Color bgColor, Color fillColor)
    {
        var boxGo = new GameObject(title + "Box", typeof(RectTransform));
        boxGo.transform.SetParent(parent, false);
        boxGo.AddComponent<Image>().color = bgColor;
        var boxLE           = boxGo.AddComponent<LayoutElement>();
        boxLE.preferredHeight = 62f;
        boxLE.flexibleWidth   = 1f;

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(boxGo.transform, false);
        fillGo.AddComponent<Image>().color = fillColor;
        var fillRT              = (RectTransform)fillGo.transform;
        fillRT.anchorMin        = new Vector2(0f, 0f);
        fillRT.anchorMax        = new Vector2(0f, 1f);
        fillRT.pivot            = new Vector2(0f, 0.5f);
        fillRT.anchoredPosition = Vector2.zero;
        fillRT.sizeDelta        = Vector2.zero;

        var txtGo = new GameObject("Txt", typeof(RectTransform));
        txtGo.transform.SetParent(boxGo.transform, false);
        var txt       = txtGo.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 18;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = new Color(1f, 1f, 1f, 0.95f);
        txt.alignment = TextAnchor.MiddleLeft;
        var txtRT              = (RectTransform)txtGo.transform;
        txtRT.anchorMin        = Vector2.zero;
        txtRT.anchorMax        = Vector2.one;
        txtRT.anchoredPosition = new Vector2(6f, 0f);
        txtRT.sizeDelta        = new Vector2(-6f, 0f);

        return (fillRT, txt);
    }

    void UpdateStatBoxes()
    {
        var ps = _loadout != null ? _loadout.GetComponent<PlayerShip>() : null;

        // HP: tüm RepairUnit bileşenlerinin toplam tamir hızı
        float totalRepairRate = 0f;
        if (_loadout != null)
        {
            foreach (var ru in _loadout.GetComponentsInChildren<RepairUnitComponent>())
            {
                if (ru.IsOperational)
                    totalRepairRate += ru.repairRate * ru.GetMultiplier("repairRate");
            }
        }
        float hullHP  = ps != null ? ps.currentHullHP : 0f;
        float maxHull = ps != null ? ps.maxHullHP      : 1f;
        UpdateStatBox(_hpFill, _hpStatText, hullHP, maxHull, "HP",
            totalRepairRate > 0f ? $"+{totalRepairRate:0.#}/s" : "");

        // Kalkan: tüm ShieldGenerator toplamı
        float totalShield = 0f, totalMaxShield = 0f, totalRecharge = 0f;
        if (_loadout != null)
        {
            foreach (var sg in _loadout.GetComponentsInChildren<ShieldGeneratorComponent>())
            {
                if (!sg.IsOperational) continue;
                totalShield    += sg.currentShield;
                totalMaxShield += sg.maxShield     * sg.GetMultiplier("maxShield");
                totalRecharge  += sg.rechargeRate  * sg.GetMultiplier("rechargeRate");
            }
        }
        UpdateStatBox(_shieldFill, _shieldStatText,
            totalShield, Mathf.Max(1f, totalMaxShield), "KALKAN",
            totalMaxShield > 0f
                ? (totalRecharge > 0f ? $"+{totalRecharge:0.#}/s" : "")
                : "yok");

        // Enerji
        float eCur = 0f, eMax = 1f, eProd = 0f;
        if (EnergyBus.Instance != null)
        {
            eCur  = EnergyBus.Instance.currentEnergy;
            eMax  = EnergyBus.Instance.maxEnergy;
            eProd = EnergyBus.Instance.TotalProduction;
        }
        UpdateStatBox(_energyFill, _energyStatText, eCur, eMax, "ENERJİ",
            eProd > 0f ? $"+{eProd:0.#}/s" : "");
    }

    static void UpdateStatBox(RectTransform fill, Text lbl, float cur, float max, string name, string rate)
    {
        float ratio  = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
        fill.anchorMax = new Vector2(ratio, 1f);
        fill.sizeDelta = Vector2.zero;
        string rateStr = string.IsNullOrEmpty(rate) ? "" : $"  ({rate})";
        lbl.text = $"{name}  {cur:0}/{max:0}{rateStr}";
    }

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

// =============================================================================
// Yardımcı MonoBehaviour'lar
// =============================================================================

/// <summary>Katalog satırı hover → sağ paneli bileşen bilgisiyle doldurur.</summary>
[RequireComponent(typeof(Image))]
public class ComponentRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public ComponentDefinition def;

    public void OnPointerEnter(PointerEventData _) => UpgradeUI.Instance?.ShowDetail(def);
    public void OnPointerExit(PointerEventData _)  => UpgradeUI.Instance?.HideDetail();
    public void OnPointerClick(PointerEventData _) => UpgradeUI.Instance?.ShowDetail(def);
}

/// <summary>Stat upgrade etiketine tıklanınca açıklama gösterir (toggle).</summary>
public class StatLabelClick : MonoBehaviour, IPointerClickHandler
{
    public string statKey;
    public int    slotIndex;

    public void OnPointerClick(PointerEventData eventData)
    {
        UpgradeUI.Instance?.ShowStatDescription(statKey, slotIndex);
    }
}

/// <summary>Panel arka planına tıklanınca stat seçimini temizler.</summary>
public class DeselectOnClick : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        UpgradeUI.Instance?.DeselectStat();
    }
}
