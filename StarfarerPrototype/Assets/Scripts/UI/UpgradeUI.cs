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

    // Türetmek yerine sabitten okunur. Türetilmiş hâli kayıttan devam edildiğinde
    // -1 dönüyordu (bkz. ShipLoadout.WeaponSlotIndex).
    const int WeaponSlot = ShipLoadout.WeaponSlotIndex;

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
    private Text _leftEnergyText;   // eski tier satırı — artık enerji yükü
    private Text _leftCostText;
    private Text _leftUpgradeCostText;

    // SlotInfo sağ sütun — bileşen aktüel istatistikleri
    private Text _slotStatsText;

    // HoverDetail — opsiyon detayı (bileşen hover veya stat açıklaması)
    private Text _rightNameText;
    private Text _rightTypeText;
    private Text _rightEnergyText;  // eski tier satırı — artık enerji yükü
    private Text _rightCostText;
    private Text _optionDescText;

    // Seçili stat takibi
    private string _selectedStatKey  = null;
    private int    _selectedStatSlot = -1;
    private int    _currentSlotIndex = -1;


    // =========================================================================
    // Lifecycle
    // =========================================================================

    void Awake()
    {
        Instance = this;
        BuildCanvas();
        _canvas.enabled = false;
    }

    /// <summary>GameManager tarafından game over anında çağrılır.</summary>
    public void ForceClose()
    {
        if (_canvas == null) return;
        _canvas.enabled = false;
        IsPaused        = false;
        FindFirstObjectByType<CameraController>()?.RestoreFromUpgrade();
    }

    void Update()
    {
        if (GameManager.IsGameOver) return;   // game over → hiçbir şey yapma
        if (StartMenuUI.IsOpen)      return;   // açılış menüsü → oyun girdileri kilitli

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
            ClearBox(_rightNameText, _rightTypeText, _rightEnergyText, _rightCostText);
            if (_optionDescText != null) _optionDescText.text = "";
        }
        _currentSlotIndex = slotIndex;

        // Ana silah slotu asla boş sayılmaz: silah bir ShipComponentBase değil,
        // ayrı bir listede yaşıyor. IsSlotEmpty ona "boş" diyebilir.
        bool empty = slotIndex != WeaponSlot &&
                     (_loadout == null || _loadout.IsSlotEmpty(slotIndex));
        ComponentDefinition installed = empty ? null : _loadout.GetSlotDef(slotIndex);

        if (slotIndex == WeaponSlot)
        {
            var activeDef = _loadout?.GetWeaponDef(_loadout.GetActiveWeaponType());
            _popupTitle.text = activeDef != null
                ? $"Slot {WeaponSlot} — {activeDef.componentName}"
                : $"Slot {WeaponSlot} — Ana Silah";
            FillBox(_leftNameText, _leftTypeText, _leftEnergyText, _leftCostText,
                    activeDef ?? installed);
        }
        else
        {
            _popupTitle.text = empty
                ? $"Slot {slotIndex} — Boş"
                : $"Slot {slotIndex} — {installed.componentName}";
            if (installed != null)
                FillBox(_leftNameText, _leftTypeText, _leftEnergyText, _leftCostText, installed);
            else
                ClearBox(_leftNameText, _leftTypeText, _leftEnergyText, _leftCostText);
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
        FillBox(_rightNameText, _rightTypeText, _rightEnergyText, _rightCostText, def);

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
        ClearBox(_rightNameText, _rightTypeText, _rightEnergyText, _rightCostText);
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
        ClearBox(_rightNameText, _rightTypeText, _rightEnergyText, _rightCostText);
        if (_optionDescText != null) _optionDescText.text = "";
        UpdateSlotInfoStats();
        UpdateCostDisplay(_currentSlotIndex);
    }

    void ShowStatDescriptionInternal(string statKey)
    {
        _rightNameText.text = "";
        _rightTypeText.text = "";
        _rightEnergyText.text = "";
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
        if (slotIndex == WeaponSlot)
        {
            BuildWeaponSwitchRows();
            BuildWeaponStatSection();
            return;
        }

        var installed = _loadout?.GetSlotComponent(slotIndex);

        var btnRow = CreateRow(_popupContent.transform);

        // Tier zinciri kaldırıldı — "Upgrade" butonu yok. Bir komponenti
        // güçlendirmenin tek yolu stat seviyeleridir.

        // Satışta ne geri döneceği görünür olmalı: stat yatırımı da iadeye dahil
        int refund = ShipLoadout.SellRefund(def, installed);
        AddButton(btnRow.transform, $"Sat ({refund})", () => SellAndRefresh(slotIndex), 130f);

        if (def.componentType == ComponentType.Hangar)
            BuildHangarStatSection(slotIndex, def);
        else
        {
            if (def.componentType == ComponentType.Turret)
                BuildTurretSpecSection(slotIndex, def);
            BuildStatUpgradeSection(slotIndex, def);
        }
    }

    void BuildTurretSpecSection(int slotIndex, ComponentDefinition def)
    {
        MakeTextLabel(_popupContent.transform, "── UZMANLAŞMA ──", 20, TextAnchor.MiddleLeft);

        var currentSpec = def.turretSpecType;
        var availableSpecs = TurretSpecHelper.GetSpecsForBase(def.turretBaseType);

        // "Temel" (uzmanlaşmasız) butonu
        if (currentSpec != TurretSpecType.None)
        {
            int refund = Mathf.RoundToInt(def.specCost * 0.5f);
            var degradeRow = CreateRow(_popupContent.transform);
            var capturedSlot = slotIndex;
            var capturedDef  = def;
            AddButton(degradeRow.transform,
                $"← Temel Tip (+{refund})",
                () =>
                {
                    // Uzmanlaşmasız hâl katalogda zaten var; burada elle yeniden
                    // kurulması gerekmiyor (kurulsaydı ikinci bir tanım kaynağı olurdu).
                    var bd = ComponentCatalog.TurretBaseOf(capturedDef.turretBaseType);
                    if (_loadout.SpecializeTurret(capturedSlot, TurretSpecType.None, bd))
                        OnSlotClicked(capturedSlot);
                }, 160f);
        }

        // Mevcut spec'ten farklı seçenekleri göster
        foreach (var spec in availableSpecs)
        {
            if (spec == currentSpec) continue;

            var specDef  = GetSpecDef(def, spec);
            bool canAfford = ResourceInventory.Instance != null &&
                             ResourceInventory.Instance.Get(specDef.specCostResource) >= specDef.specCost;

            string costLabel = specDef.specCost > 0 ? $" ({specDef.specCost})" : " (ücretsiz)";
            string label     = spec == currentSpec
                ? $"✓ {TurretSpecHelper.GetSpecName(spec)}"
                : $"{TurretSpecHelper.GetSpecName(spec)}{costLabel}";

            var capturedSpec = spec;
            var capturedDef  = specDef;
            var capturedSlot = slotIndex;

            var row = CreateRow(_popupContent.transform);
            var btn = AddButton(row.transform, label,
                () =>
                {
                    if (_loadout.SpecializeTurret(capturedSlot, capturedSpec, capturedDef))
                        OnSlotClicked(capturedSlot);
                }, 200f);

            if (!canAfford)
            {
                btn.interactable = false;
                btn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
            }
        }
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
            int cost     = maxed ? 0 : StatUpgradeCost(def, curLevel, key);
            bool canAfford = !maxed && ResourceInventory.Instance != null &&
                             ResourceInventory.Instance.Get(def.costResource) >= cost;

            // Enerji kapısı kaynaktan AYRI bir durumdur; ikisi aynı gri butonla
            // anlatılırsa oyuncu jeneratörü suçlamayı akıl edemez.
            float eDelta    = maxed || _loadout == null ? 0f : _loadout.StatUpgradeEnergyDelta(slotIndex, key);
            float eShort    = ShipLoadout.EnergyShortfall(eDelta);
            bool  hasEnergy = eShort <= 0f;

            bool isSelected = !maxed && _selectedStatKey == key && _selectedStatSlot == slotIndex;

            var row = CreateRow(_popupContent.transform);

            var lblGo = new GameObject("StatLabel", typeof(RectTransform));
            lblGo.transform.SetParent(row.transform, false);
            var lbl       = lblGo.AddComponent<Text>();
            lbl.text      = maxed
                ? $"{statLabel} Lv {curLevel}/{ShipComponentBase.MaxStatLevel} MAX"
                : $"{statLabel} Lv {curLevel}/{ShipComponentBase.MaxStatLevel}  ({cost})"
                  + (eDelta > 0.01f ? $"  ⚡{eDelta:0.#}" : "");
            lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lbl.fontSize  = 24;
            lbl.color     = maxed       ? new Color(1f, 0.85f, 0.2f, 1f)   :
                            !hasEnergy  ? new Color(1f, 0.60f, 0.2f, 1f)   :
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
                if (!canAfford || !hasEnergy)
                {
                    btn.interactable = false;
                    btn.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
                }
            }

            if (!maxed && !hasEnergy)
            {
                var warn = MakeTextLabel(_popupContent.transform,
                    $"⚡ Enerji yetersiz — {eShort:0.#} eksik. Jeneratörü yükselt " +
                    "veya bir komponent sat.", 18, TextAnchor.MiddleLeft);
                warn.color = new Color(1f, 0.60f, 0.2f, 1f);
            }
        }
    }

    void BuildWeaponStatSection()
    {
        var weaponTypes = new[]
        {
            (WeaponType.Laser,   "Lazer"),
            (WeaponType.Kinetic, "Raylı Top"),
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
            foreach (var (key, statLabel) in WeaponStatsFor(type))
            {
                int curLevel = _loadout.GetWeaponStatLevel(type, key);
                bool maxed   = curLevel >= ShipComponentBase.MaxStatLevel;
                int cost     = maxed ? 0 : StatUpgradeCost(curDef, curLevel, key);
                bool canAfford = !maxed && ResourceInventory.Instance != null &&
                                 ResourceInventory.Instance.Get(curDef.costResource) >= cost;

                bool isSelected = !maxed && _selectedStatKey == key && _selectedStatSlot == WeaponSlot;

                var row = CreateRow(_popupContent.transform);

                var lblGo = new GameObject("StatLabel", typeof(RectTransform));
                lblGo.transform.SetParent(row.transform, false);
                var lbl       = lblGo.AddComponent<Text>();
                lbl.text      = maxed ? $"{statLabel} Lv {curLevel}/{ShipComponentBase.MaxStatLevel} MAX" : $"{statLabel} Lv {curLevel}/{ShipComponentBase.MaxStatLevel}  ({cost})";
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
                    click.slotIndex = WeaponSlot;
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
            int cost     = maxed ? 0 : StatUpgradeCost(def, curLevel, key);
            bool canAfford = !maxed && ResourceInventory.Instance != null &&
                             ResourceInventory.Instance.Get(def.costResource) >= cost;

            bool isSelected = !maxed && _selectedStatKey == key && _selectedStatSlot == slotIndex;

            var row = CreateRow(_popupContent.transform);
            var lblGo = new GameObject("StatLabel", typeof(RectTransform));
            lblGo.transform.SetParent(row.transform, false);
            var lbl       = lblGo.AddComponent<Text>();
            lbl.text      = maxed ? $"{label} Lv {curLevel}/{ShipComponentBase.MaxStatLevel} MAX" : $"{label} Lv {curLevel}/{ShipComponentBase.MaxStatLevel}  ({cost})";
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
        int cost   = maxed ? 0 : StatUpgradeCost(def, level, key);
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

    /// <summary>
    /// Stat fiyatı komponentin statCostBase'inden ve STATIN KENDİ çarpanından
    /// gelir. Anahtarın geçilmesi şart: zırh gibi ayrıcalıklı izler aynı
    /// komponentin diğer statlarıyla aynı fiyata satılmamalı.
    /// </summary>
    static int StatUpgradeCost(ComponentDefinition def, int currentLevel, string key = null)
    {
        int baseCost = def.statCostBase > 0 ? def.statCostBase : def.cost;
        return BalanceConfig.Instance.StatUpgradeCost(baseCost, currentLevel, key);
    }

    static (string key, string label)[] GetStatsForType(ComponentType type)
    {
        switch (type)
        {
            case ComponentType.Generator:  return new[] { (GeneratorComponent.ProductionKey, "Üretim"), (GeneratorComponent.CapacitorKey, "Kapasitör") };
            case ComponentType.Shield:     return new[] { ("rechargeRate", "Şarj Hızı"), ("maxShield", "Max Kalkan") };
            case ComponentType.RepairUnit: return new[] { ("repairRate", "Tamir Hızı"), ("energyEfficiency", "Enerji Verimi"), (RepairUnitComponent.ArmorKey, "Zırh") };
            case ComponentType.Storage:    return new[] { (StorageComponent.CapacityKey, "Kapasite") };
            case ComponentType.Turret:     return new[] { ("damage", "Hasar"), ("fireRate", "Ateş Hızı") };
            default:                       return null;
        }
    }

    void StatUpgradeAndRefresh(int slotIndex, string key, ComponentDefinition def, int cost)
    {
        if (_loadout == null) return;
        var comp = _loadout.GetSlotComponent(slotIndex);
        if (comp == null) return;

        // Enerji kapısı önce: kaynağı harcayıp enerjiye takılmak kötü bir sürpriz
        if (!ShipLoadout.HasEnergyHeadroom(_loadout.StatUpgradeEnergyDelta(slotIndex, key))) return;

        if (!ResourceInventory.Instance.TrySpend(def.costResource, cost)) return;
        comp.ApplyStatUpgrade(key);
        OnSlotClicked(slotIndex);
    }

    void WeaponStatUpgradeAndRefresh(WeaponType type, string key, ComponentDefinition def, int cost)
    {
        if (_loadout == null) return;
        if (!ResourceInventory.Instance.TrySpend(def.costResource, cost)) return;
        _loadout.ApplyWeaponStatUpgrade(type, key);
        OnSlotClicked(WeaponSlot);
    }

    /// <summary>
    /// Bu silah tipinde satın alınmaya DEĞER statlar.
    ///
    /// Lazer sürekli ışındır: WeaponController.UpdateLaser fireRate'i hiç okumaz,
    /// sol tık basılı olduğu sürece yanar. "Ateş Hızı" satırı yine de listeleniyor
    /// ve satın alınabiliyordu — oyuncu hiçbir şey yapmayan bir yükseltmeye ödeme
    /// yapıyordu. Artık listelenmiyor.
    /// </summary>
    static (string key, string label)[] WeaponStatsFor(WeaponType type)
        => type == WeaponType.Laser
            ? new[] { ("damage", "Hasar") }
            : new[] { ("damage", "Hasar"), ("fireRate", "Ateş Hızı") };

    void BuildWeaponSwitchRows()
    {
        MakeTextLabel(_popupContent.transform, "ANA SİLAH", 24, TextAnchor.MiddleLeft);

        var weaponTypes = new[]
        {
            (WeaponType.Laser,   "Lazer"),
            (WeaponType.Kinetic, "Raylı Top"),
            (WeaponType.Plasma,  "Plazma"),
        };

        foreach (var (type, label) in weaponTypes)
        {
            bool unlocked = _loadout != null && _loadout.IsWeaponTypeUnlocked(type);
            bool isActive = _loadout != null && _loadout.GetActiveWeaponType() == type;
            var capturedType = type;

            var row = CreateRow(_popupContent.transform);

            ComponentDefinition curDef = unlocked ? _loadout.GetWeaponDef(type)
                                                  : ShipLoadout.GetWeaponBase(type);
            var nameGo = new GameObject("WeaponLabel", typeof(RectTransform));
            nameGo.transform.SetParent(row.transform, false);
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.text      = label;
            nameTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameTxt.fontSize  = 28;
            nameTxt.color     = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            nameTxt.alignment = TextAnchor.MiddleLeft;
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.flexibleWidth   = 1f;
            nameLE.preferredHeight = 58f;

            if (!unlocked)
            {
                ComponentDefinition unlockDef = ShipLoadout.GetWeaponBase(type);
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
                // Tier yok: bir silah tipi satın alındıktan sonra yalnızca stat
                // seviyeleriyle güçlenir. Buradaki tek karar hangisini kuşandığın.
                var selBtn = AddButton(row.transform, "Seç", () => SwitchWeaponAndRefresh(capturedType), 60f);
                if (isActive)
                    selBtn.GetComponent<Image>().color = new Color(0.10f, 0.55f, 0.20f, 1f);
            }
        }
    }

    void UnlockWeaponAndRefresh(WeaponType type)
    {
        if (_loadout == null) return;
        if (_loadout.UnlockWeaponType(type))
            OnSlotClicked(WeaponSlot);
    }

    void SwitchWeaponAndRefresh(WeaponType type)
    {
        _loadout?.SwitchWeapon(type);
        OnSlotClicked(WeaponSlot);
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

        if (slotIndex == WeaponSlot)
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
                    total += StatUpgradeCost(def, i, kvp.Key);

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
                int upgCost    = StatUpgradeCost(def, curLevel, _selectedStatKey);
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

        // Silahın kendi fiyatı + stat upgrade maliyetleri
        int total = curDef.cost;
        foreach (var (key, _) in WeaponStatsFor(type))
        {
            int level = _loadout.GetWeaponStatLevel(type, key);
            for (int i = 0; i < level; i++)
                total += StatUpgradeCost(curDef, i, key);
        }

        string resLabel = ResourceLabel(curDef.costResource);
        _leftCostText.text  = $"{total} {resLabel}";
        _leftCostText.color = new Color(0.3f, 0.9f, 0.45f, 1f);

        if (_leftUpgradeCostText == null) return;
        if (!string.IsNullOrEmpty(_selectedStatKey) && _selectedStatSlot == WeaponSlot)
        {
            int curLevel = _loadout.GetWeaponStatLevel(type, _selectedStatKey);
            if (curLevel < ShipComponentBase.MaxStatLevel)
            {
                int upgCost    = StatUpgradeCost(curDef, curLevel, _selectedStatKey);
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
        if (slotIndex == WeaponSlot) return BuildWeaponStatsText();

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

                if (tc.specType == TurretSpecType.Laser)
                {
                    // Lazer: DPS during beam × burnDuration / fireRate = gerçek DPS
                    float beamDps      = dmg * tc.burnDuration / tc.fireRate;
                    sb.AppendLine(DeltaLine("Işın hasarı", dmg, "damage", false));
                    sb.AppendLine($"Yanma: {tc.burnDuration:0.##}s / {tc.fireRate:0.#}s");
                    sb.AppendLine($"Etkin DPS: {beamDps:0.##}");
                    sb.AppendLine($"Enerji/atış: {eng:0.##}");
                }
                else
                {
                    sb.AppendLine(DeltaLine("Hasar",   dmg, "damage",   false));
                    sb.AppendLine(DeltaLine("Ateş/sn", sps, "fireRate", false));
                    sb.AppendLine($"Enerji/atış: {eng:0.##}");
                    if (tc.magazineSize > 0)
                    {
                        sb.AppendLine($"Şajör: {tc.magazineSize}");
                        sb.AppendLine($"Yeniden şarj: {tc.reloadTime:0.#}s");
                    }
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

                var ship = _loadout != null ? _loadout.GetComponent<PlayerShip>() : null;
                if (ship != null)
                {
                    sb.AppendLine(DeltaLine("Zırh (+HP)", ru.HullBonus(ship.baseMaxHullHP),
                                            RepairUnitComponent.ArmorKey, false));
                    sb.AppendLine($"Gemi HP: {ship.currentHullHP:0}/{ship.maxHullHP:0}");
                }
                break;
            }
            case ComponentType.Generator:
            {
                var gen = comp as GeneratorComponent;
                if (gen == null) break;
                float prod = gen.productionAmount * gen.GetMultiplier(GeneratorComponent.ProductionKey);
                sb.AppendLine(DeltaLine("Üretim/sn", prod, GeneratorComponent.ProductionKey, false));
                sb.AppendLine(DeltaLine("Tampon +", gen.CapacitorBonus,
                                        GeneratorComponent.CapacitorKey, false,
                                        GeneratorComponent.CapacitorBonusAt(
                                            gen.GetStatLevel(GeneratorComponent.CapacitorKey) + 1)));
                if (EnergyBus.Instance != null)
                    sb.AppendLine($"Max enerji: {EnergyBus.Instance.maxEnergy:0}");
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
            case ComponentType.Storage:
            {
                var st = comp as StorageComponent;
                if (st == null) break;
                sb.AppendLine(DeltaLine("Metal kap.",   st.EffMetalCapacity,
                                        StorageComponent.CapacityKey, false));
                sb.AppendLine(DeltaLine("Kristal kap.", st.EffCrystalCapacity,
                                        StorageComponent.CapacityKey, false));
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
    /// <param name="explicitNext">
    /// Bir sonraki seviyedeki DEĞER. Adımı statStep'ten farklı olan izler
    /// (kapasitör) bunu verir; verilmezse delta genel adımdan hesaplanır.
    /// </param>
    string DeltaLine(string label, float current, string statKey, bool isDecreasing,
                     float? explicitNext = null)
    {
        string valStr = $"{current:0.##}";
        if (_selectedStatKey != statKey || _selectedStatSlot != _currentSlotIndex)
            return $"{label}: {valStr}";

        if (explicitNext.HasValue)
            return $"{label}: {valStr}  (+{explicitNext.Value - current:0.##})";

        float step = BalanceConfig.Instance.statStep;
        float delta;
        string sign;
        if (!isDecreasing) { delta = current * (step - 1f);      sign = "+"; }
        else               { delta = current * (1f - 1f / step); sign = "-"; }

        return $"{label}: {valStr}  ({sign}{delta:0.##})";
    }

    /// <summary>
    /// Stat açıklamaları. Çarpan metni BalanceConfig'ten okunur — sabit "×1.5"
    /// yazılsaydı denge ayarı değiştiğinde UI yalan söylerdi.
    /// </summary>
    static string GetStatDescription(string key)
    {
        var    cfg  = BalanceConfig.Instance;
        string up   = $"Her seviye ×{cfg.statStep:0.##} çarpanı ekler.";
        string down = $"Her seviye ÷{cfg.statStep:0.##} azalma sağlar.";
        string nrg  = $"\n\nEnerji tüketimi de ×{cfg.energyGrowth:0.##} büyür — " +
                      "jeneratör yetişmezse yükseltme yapılamaz.";

        switch (key)
        {
            case "damage":
                return $"Turretin verdiği hasarını artırır.\n{up}\n\n" +
                       "Zırhlı düşmanlarda atış başına hasar belirleyicidir: " +
                       "zırh her atıştan sabit miktar düşer." + nrg;
            case "fireRate":
                return $"Ateş hızını artırır.\n{up}\n\n" +
                       "Zırha karşı hasar kadar iyi değildir — daha çok atış, " +
                       "her biri zırhtan aynı cezayı yer." + nrg;
            case "armor":
                return $"Ana geminin maksimum gövde HP'sini artırır.\n{up}\n\n" +
                       "Kalkanın arkasındaki son katman: kalkanı delen her şey " +
                       "(komponent mermileri, kalkan çökünce gelen her atış) buradan " +
                       "yer. Bu izi taşıyan her onarım birimi bonusunu EKLER, çarpmaz." +
                       "\n\nDiğer izlerden pahalıdır — doğrudan hayatta kalma satın alır." + nrg;
            case "capacity":
                return $"Deponun kaynak kapasitesini artırır.\n{up}\n\n" +
                       "Geç seviyelerde tek bir yükseltme binlerce kaynak tutar; " +
                       "tavan yetmezse o parayı biriktirmek mümkün değildir." + nrg;
            case "repairRate":       return $"Saniyede tamir edilen HP miktarını artırır.\n{up}{nrg}";
            case "energyEfficiency": return $"Tamir için harcanan enerji miktarını azaltır.\n{down}";
            case "production":
                return $"Saniyede üretilen enerji miktarını artırır.\n{up}\n\n" +
                       "Diğer tüm yükseltmelerin kapısıdır: üretim yetmezse " +
                       "hiçbir komponent seviye alamaz.";
            case "capacitor":
                return $"Enerji tamponunu (max enerji) büyütür.\n" +
                       $"Her seviye tamponu %{(cfg.capacitorStatStep - 1f) * 100f:0} büyütür " +
                       "— diğer statlardan farklı, çünkü tampon bir AKIŞ değil STOK.\n\n" +
                       "Üretim AKIŞI, kapasitör STOKU belirler. Turretlerin aynı " +
                       "anda ateşlemesi, plazma şarjı ve kalkan boost'u anlık " +
                       "olarak üretimin çok üstünde enerji ister — tampon boşsa " +
                       "o atışlar hiç yapılamaz.\n\nÜretimi yükseltmek bunu da " +
                       "çözer ama çok daha pahalıya; tampon ucuz ve dar bir cevaptır." + nrg;
            case "rechargeRate":     return $"Kalkan şarj hızını artırır.\n{up}{nrg}";
            case "maxShield":        return $"Maksimum kalkan kapasitesini artırır.\n{up}{nrg}";
            case "productionSpeed":  return $"Toplayıcı/savaşçı üretim hızını artırır.\n{up}{nrg}";
            case "maxHP":            return $"Gemi bileşenlerinin dayanaklılığını artırır.\n{up}{nrg}";
            case "salvageRate":      return $"Enkaz toplama hızını artırır.\n{up}{nrg}";
            case "speed":            return $"Toplayıcı/savaşçı gemi hızını artırır.\n{up}{nrg}";
            case "maxCollectors":    return $"Hangarın maksimum toplayıcı sayısını artırır.\nHer seviye +1 toplayıcı ekler.{nrg}";
            case "maxFighters":      return $"Hangarın maksimum savaşçı sayısını artırır.\nHer seviye +1 savaşçı ekler.{nrg}";
            default:                 return key;
        }
    }

    // =========================================================================
    // Hardcoded Catalog
    // =========================================================================

    static ComponentDefinition[] _weaponDefs;

    static ComponentDefinition[] GetCatalogDefs(int slotIndex) => ComponentCatalog.Purchasable;

    /// <summary>Turret uzmanlaşma tanımı — veri ComponentCatalog'da tutulur.</summary>
    static ComponentDefinition GetSpecDef(ComponentDefinition baseDef, TurretSpecType spec)
        => ComponentCatalog.TurretSpec(baseDef, spec);

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
            case ComponentType.Storage:    return "Depo";
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
        _leftEnergyText = MakeLabel(leftCol.transform, "", 24, FontStyle.Normal);
        _leftEnergyText.color = new Color(1f, 0.85f, 0.2f, 1f);
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
        _rightEnergyText = MakeLabel(_hoverDetailPanel.transform, "", 24, FontStyle.Normal);
        _rightEnergyText.color = new Color(1f, 0.85f, 0.2f, 1f);
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

    /// <summary>
    /// Tier satırı kaldırıldı; yeri boş bırakılmak yerine ENERJİ yüküne verildi.
    /// Enerji her yükseltme kararının gerçek kapısı — kaynak kadar sık bakılan
    /// sayı o, tier ise artık yok.
    /// </summary>
    static void FillBox(Text n, Text ty, Text ti, Text c, ComponentDefinition def)
    {
        if (def == null) return;
        n.text  = def.componentName;
        ty.text = TypeLabel(def.componentType);
        ti.text = def.baseEnergyCost > 0f ? $"⚡ {def.baseEnergyCost:0.#}/s (Sv0)" : "";
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

    static Text MakeTextLabel(Transform parent, string text, int fontSize, TextAnchor alignment)
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

        return t;
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
