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

    // Kaynak göstergeleri
    private Text _hamMaddeText;
    private Text _kristalText;

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
                RefreshResourceDisplay();
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

        if (slotIndex == 5)
        {
            var activeDef = _loadout?.GetWeaponDef(_loadout.GetActiveWeaponType());
            _popupTitle.text = activeDef != null
                ? $"Slot 5 \u2014 {activeDef.componentName}"
                : "Slot 5 \u2014 Ana Silah";
            FillBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText,
                    activeDef ?? installed);
        }
        else
        {
            _popupTitle.text = empty
                ? $"Slot {slotIndex} \u2014 Bo\u015f"
                : $"Slot {slotIndex} \u2014 {installed.componentName}";
            if (installed != null)
                FillBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText, installed);
            else
                ClearBox(_leftNameText, _leftTypeText, _leftTierText, _leftCostText);
        }

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

        bool canAfford = ResourceInventory.Instance != null &&
                         ResourceInventory.Instance.Get(def.costResource) >= def.cost;
        _rightCostText.color = canAfford
            ? new Color(0.3f, 0.9f, 0.45f, 1f)
            : new Color(1f, 0.25f, 0.25f, 1f);
    }

    /// <summary>Sağ kutuyu temizler (hover çıkışı).</summary>
    public void HideDetail() =>
        ClearBox(_rightNameText, _rightTypeText, _rightTierText, _rightCostText);

    // -------------------------------------------------------------------------
    // Popup Content
    // -------------------------------------------------------------------------

    void RefreshResourceDisplay()
    {
        if (ResourceInventory.Instance == null) return;
        _hamMaddeText.text = $"Ham Madde: {ResourceInventory.Instance.Get(ResourceType.RawMaterial)}";
        _kristalText.text  = $"Kristal: {ResourceInventory.Instance.Get(ResourceType.EnergyCrystal)}";
    }

    void InstallAndRefresh(ComponentDefinition def, int slotIndex)
    {
        if (_loadout == null) return;
        if (_loadout.InstallComponent(def, slotIndex))
        {
            RefreshResourceDisplay();
            OnSlotClicked(slotIndex);
        }
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

            bool canAfford = ResourceInventory.Instance != null &&
                             ResourceInventory.Instance.Get(def.costResource) >= def.cost;

            var capturedDef   = def;
            var capturedSlot  = slotIndex;
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

        BuildStatUpgradeSection(slotIndex, def);
    }

    void BuildStatUpgradeSection(int slotIndex, ComponentDefinition def)
    {
        var stats = GetStatsForType(def.componentType);
        if (stats == null || stats.Length == 0) return;

        var comp = _loadout?.GetSlotComponent(slotIndex);

        MakeTextLabel(_popupContent.transform, "\u2500\u2500 STAT UPGRADE \u2500\u2500", 11, TextAnchor.MiddleLeft);

        foreach (var (key, statLabel) in stats)
        {
            int curLevel = comp != null && comp.StatLevels.TryGetValue(key, out var lvl) ? lvl : 0;
            bool maxed   = curLevel >= ShipComponentBase.MaxStatLevel;
            int cost     = maxed ? 0 : StatUpgradeCost(def, curLevel);
            bool canAfford = !maxed && ResourceInventory.Instance != null &&
                             ResourceInventory.Instance.Get(def.costResource) >= cost;

            var row = CreateRow(_popupContent.transform);

            var lblGo = new GameObject("StatLabel", typeof(RectTransform));
            lblGo.transform.SetParent(row.transform, false);
            var lbl       = lblGo.AddComponent<Text>();
            lbl.text      = maxed ? $"{statLabel} Lv {curLevel}/5 MAX" : $"{statLabel} Lv {curLevel}/5  ({cost})";
            lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.fontSize  = 13;
            lbl.color     = maxed ? new Color(1f, 0.85f, 0.2f, 1f) : Color.white;
            lbl.alignment = TextAnchor.MiddleLeft;
            var lblLE     = lblGo.AddComponent<LayoutElement>();
            lblLE.flexibleWidth   = 1f;
            lblLE.preferredHeight = 36f;

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
                MakeTextLabel(_popupContent.transform, "\u2500\u2500 STAT UPGRADE \u2500\u2500", 11, TextAnchor.MiddleLeft);
                headerShown = true;
            }

            MakeTextLabel(_popupContent.transform, typeLabel, 12, TextAnchor.MiddleLeft);

            var capturedType = type;
            foreach (var (key, statLabel) in new[] { ("damage", "Hasar"), ("fireRate", "Ate\u015f H\u0131z\u0131") })
            {
                int curLevel = _loadout.GetWeaponStatLevel(type, key);
                bool maxed   = curLevel >= ShipComponentBase.MaxStatLevel;
                int cost     = maxed ? 0 : StatUpgradeCost(curDef, curLevel);
                bool canAfford = !maxed && ResourceInventory.Instance != null &&
                                 ResourceInventory.Instance.Get(curDef.costResource) >= cost;

                var row = CreateRow(_popupContent.transform);

                var lblGo = new GameObject("StatLabel", typeof(RectTransform));
                lblGo.transform.SetParent(row.transform, false);
                var lbl       = lblGo.AddComponent<Text>();
                lbl.text      = maxed ? $"{statLabel} Lv {curLevel}/5 MAX" : $"{statLabel} Lv {curLevel}/5  ({cost})";
                lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                lbl.fontSize  = 13;
                lbl.color     = maxed ? new Color(1f, 0.85f, 0.2f, 1f) : Color.white;
                lbl.alignment = TextAnchor.MiddleLeft;
                var lblLE     = lblGo.AddComponent<LayoutElement>();
                lblLE.flexibleWidth   = 1f;
                lblLE.preferredHeight = 36f;

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

    static int StatUpgradeCost(ComponentDefinition def, int currentLevel)
    {
        int baseCost = Mathf.Max(1, Mathf.RoundToInt(def.cost * 0.2f));
        return Mathf.RoundToInt(baseCost * Mathf.Pow(2f, currentLevel));
    }

    static (string key, string label)[] GetStatsForType(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Generator:  return new[] { ("production", "\u00dcretim") };
            case ComponentType.Shield:     return new[] { ("rechargeRate", "\u015earj H\u0131z\u0131"), ("maxShield", "Max Kalkan") };
            case ComponentType.RepairUnit: return new[] { ("repairRate", "Tamir H\u0131z\u0131"), ("energyEfficiency", "Enerji Verimi") };
            case ComponentType.Turret:     return new[] { ("damage", "Hasar"), ("fireRate", "Ate\u015f H\u0131z\u0131") };
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
        RefreshResourceDisplay();
        OnSlotClicked(slotIndex);
    }

    void WeaponStatUpgradeAndRefresh(WeaponType type, string key, ComponentDefinition def, int cost)
    {
        if (_loadout == null) return;
        if (!ResourceInventory.Instance.TrySpend(def.costResource, cost)) return;
        _loadout.ApplyWeaponStatUpgrade(type, key);
        RefreshResourceDisplay();
        OnSlotClicked(5);
    }

    // Her silah tipi için ayrı satır: isim/tier + Satın Al veya Seç + Upgrade
    void BuildWeaponSwitchRows()
    {
        MakeTextLabel(_popupContent.transform, "ANA S\u0130LAH", 13, TextAnchor.MiddleLeft);

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

            // İsim + tier
            ComponentDefinition curDef  = unlocked ? _loadout.GetWeaponDef(type) : ShipLoadout.GetWeaponChainStart(type);
            string tierStr = unlocked ? $" Mk{curDef?.tier ?? 1}" : " —";
            var nameGo = new GameObject("WeaponLabel", typeof(RectTransform));
            nameGo.transform.SetParent(row.transform, false);
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.text      = label + tierStr;
            nameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize  = 15;
            nameTxt.color     = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            nameTxt.alignment = TextAnchor.MiddleLeft;
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.flexibleWidth   = 1f;
            nameLE.preferredHeight = 40f;

            if (!unlocked)
            {
                // Satın Al butonu
                ComponentDefinition unlockDef = ShipLoadout.GetWeaponChainStart(type);
                bool canAfford = ResourceInventory.Instance != null && unlockDef != null &&
                                 ResourceInventory.Instance.Get(unlockDef.costResource) >= unlockDef.cost;
                string costLabel = unlockDef != null ? $"Sat\u0131n Al\n{unlockDef.cost}" : "Sat\u0131n Al";
                var buyBtn = AddButton(row.transform, costLabel, () => UnlockWeaponAndRefresh(capturedType), 80f);
                if (!canAfford)
                {
                    buyBtn.interactable = false;
                    buyBtn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
                }
            }
            else
            {
                // Seç butonu
                var selBtn = AddButton(row.transform, "Se\u00e7", () => SwitchWeaponAndRefresh(capturedType), 60f);
                if (isActive)
                    selBtn.GetComponent<Image>().color = new Color(0.10f, 0.55f, 0.20f, 1f);

                // Upgrade butonu
                if (curDef?.upgradeTo != null)
                {
                    int diff = curDef.upgradeTo.cost - curDef.sellValue;
                    bool canAfford = ResourceInventory.Instance != null &&
                                     ResourceInventory.Instance.Get(curDef.upgradeTo.costResource) >= diff;
                    string upLabel = $"\u2191Mk{curDef.upgradeTo.tier}\n{diff}";
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
        {
            RefreshResourceDisplay();
            OnSlotClicked(5);
        }
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
        {
            RefreshResourceDisplay();
            OnSlotClicked(5);
        }
    }

    void UpgradeAndRefresh(int slotIndex)
    {
        if (_loadout == null) return;
        if (_loadout.UpgradeComponent(slotIndex))
        {
            RefreshResourceDisplay();
            OnSlotClicked(slotIndex);
        }
    }

    void SellAndRefresh(int slotIndex)
    {
        if (_loadout == null) return;
        if (_loadout.SellComponent(slotIndex))
        {
            RefreshResourceDisplay();
            OnSlotClicked(slotIndex);
        }
    }

    // -------------------------------------------------------------------------
    // Hardcoded Catalog
    // -------------------------------------------------------------------------

    static ComponentDefinition[] _weaponDefs;

    static ComponentDefinition[] GetCatalogDefs(int slotIndex)
    {
        return GetComponentDefs();
    }

    static ComponentDefinition[] GetComponentDefs()
    {
        if (_catalogDefs != null) return _catalogDefs;

        var shield = ScriptableObject.CreateInstance<ComponentDefinition>();
        shield.componentName = "Kalkan Jenerat\u00f6r\u00fc Mk1";
        shield.componentType = ComponentType.Shield;
        shield.tier = 1; shield.costResource = ResourceType.RawMaterial; shield.cost = 10;

        var repair = ScriptableObject.CreateInstance<ComponentDefinition>();
        repair.componentName = "Onar\u0131m Birimi Mk1";
        repair.componentType = ComponentType.RepairUnit;
        repair.tier = 1; repair.costResource = ResourceType.RawMaterial; repair.cost = 8;

        var gen = ScriptableObject.CreateInstance<ComponentDefinition>();
        gen.componentName = "Enerji Jenerat\u00f6r\u00fc Mk1";
        gen.componentType = ComponentType.Generator;
        gen.tier = 1; gen.costResource = ResourceType.RawMaterial; gen.cost = 15;

        // Turretler
        var gatling = T("Gatling Turret",   TurretType.Gatling,      ResourceType.RawMaterial,    12,
            fireRate:1f,  damage:5f,  speed:9f,  life:3f,  energy:0.5f, mag:10, reload:3f);
        var plasma  = T("Plazma Turret",    TurretType.Plasma,       ResourceType.EnergyCrystal,  20,
            fireRate:20f, damage:25f, speed:5f,  life:4f,  energy:4f);
        var laser   = T("Lazer Turret",     TurretType.Laser,        ResourceType.EnergyCrystal,  18,
            fireRate:5f,  damage:15f, speed:14f, life:4f,  energy:3f);
        var rocket  = T("Roket Turret",     TurretType.Rocket,       ResourceType.RawMaterial,    22,
            fireRate:30f, damage:50f, speed:7f,  life:10f, energy:0.5f);
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

    static ComponentDefinition[] GetWeaponDefs()
    {
        if (_weaponDefs != null) return _weaponDefs;

        var kinetic = ScriptableObject.CreateInstance<ComponentDefinition>();
        kinetic.componentName      = "Kinetik Top Mk1";
        kinetic.componentType      = ComponentType.Weapon;
        kinetic.weaponType         = WeaponType.Kinetic;
        kinetic.tier               = 1;
        kinetic.costResource       = ResourceType.RawMaterial;
        kinetic.cost               = 12;
        kinetic.weaponDamage       = 12f;
        kinetic.weaponFireRate     = 0.15f;

        var laser = ScriptableObject.CreateInstance<ComponentDefinition>();
        laser.componentName            = "Lazer Topu Mk1";
        laser.componentType            = ComponentType.Weapon;
        laser.weaponType               = WeaponType.Laser;
        laser.tier                     = 1;
        laser.costResource             = ResourceType.EnergyCrystal;
        laser.cost                     = 18;
        laser.weaponDamage             = 8f;
        laser.weaponFireRate           = 0.07f;
        laser.weaponEnergyCostPerShot  = 2f;

        var plasma = ScriptableObject.CreateInstance<ComponentDefinition>();
        plasma.componentName      = "Plazma Topu Mk1";
        plasma.componentType      = ComponentType.Weapon;
        plasma.weaponType         = WeaponType.Plasma;
        plasma.tier               = 1;
        plasma.costResource       = ResourceType.RawMaterial;
        plasma.cost               = 20;
        plasma.weaponDamage       = 28f;
        plasma.weaponFireRate     = 0.12f;
        plasma.weaponChargeTime   = 0.8f;
        plasma.weaponBurstCount   = 3;

        _weaponDefs = new[] { kinetic, laser, plasma };
        return _weaponDefs;
    }

    static string TypeLabel(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Shield:     return "Kalkan";
            case ComponentType.RepairUnit: return "Onar\u0131m Birimi";
            case ComponentType.Generator:  return "Enerji Jenerat\u00f6r\u00fc";
            case ComponentType.Weapon:     return "Silah";
            case ComponentType.Turret:     return "Turret";
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

        _hamMaddeText = MakeLabel(_generalPanel.transform, "Ham Madde: \u2014", 12, FontStyle.Normal);
        _kristalText  = MakeLabel(_generalPanel.transform, "Kristal: \u2014",  12, FontStyle.Normal);
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
        le.preferredHeight = 44f;

        AttachLabel(go.transform, label, 16);
        return btn;
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
