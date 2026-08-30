using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// PlayerShip'e eklenen komponent yuvası yöneticisi.
/// Kurulum, satış ve yükseltme işlemlerini ResourceInventory üzerinden çalıştırır.
/// </summary>
public class ShipLoadout : MonoBehaviour
{
    public int slotCount = 10;

    /// <summary>
    /// Ana silahın slotu. SABİT bir gerçektir, türetilmez.
    ///
    /// Eskiden UpgradeUI bunu _slotsByType[Weapon]'dan türetiyordu ve kayıt
    /// yüklendikten sonra o liste BOŞ kalıyordu: SaveSystem.Apply önce
    /// ClearAllSlots() çağırıyor, sonra slotları geri kuruyor — ama silahlar
    /// slot olarak KAYDEDİLMİYOR (ayrı listede tutuluyorlar). Sonuç: kayıttan
    /// devam eden oyuncuda WeaponSlot = -1 oluyor, slot 1 "Boş" görünüyor,
    /// ana silah paneli hiç açılmıyor ve o slota kalkan/turret kurulabiliyordu.
    /// </summary>
    public const int WeaponSlotIndex = 1;

    private ShipComponentBase[]   _slots;
    private ComponentDefinition[] _installedDefs;
    private GameObject[]          _slotObjects;

    // Silah switch sistemi
    private readonly Dictionary<WeaponType, ComponentDefinition> _unlockedWeapons = new();
    private WeaponType _activeWeaponType = WeaponType.Kinetic;

    // Silah stat upgrade seviyeleri — silah tipinden bağımsız, kalıcı
    private readonly Dictionary<WeaponType, Dictionary<string, int>> _weaponStatLevels = new();

    // Tip → slot indeksi haritası (Turret gibi çoklu olabilecek tipler List içinde)
    private readonly Dictionary<ComponentType, List<int>> _slotsByType = new();

    public bool IsWeaponTypeUnlocked(WeaponType type) => _unlockedWeapons.ContainsKey(type);
    public WeaponType GetActiveWeaponType() => _activeWeaponType;

    public IEnumerable<int> GetSlotsByType(ComponentType type) =>
        _slotsByType.TryGetValue(type, out var list) ? list : Enumerable.Empty<int>();

    void Awake()
    {
        // Statikler sahne yeniden yüklenince hayatta kalır; yeni gemi yeni
        // kalkan durumuyla başlamalı.
        ShieldGeneratorComponent.ResetStatics();

        _slots         = new ShipComponentBase[slotCount];
        _installedDefs = new ComponentDefinition[slotCount];
        _slotObjects   = new GameObject[slotCount];
    }

    void Start()
    {
        // Raylı Top başlangıçta ücretsiz kurulu gelir
        InstallComponent(ComponentCatalog.Weapon(WeaponType.Kinetic),
                         WeaponSlotIndex, deductCost: false);

        // Kalan başlangıç donanımı katalogdan gelir — bunlar mağazadakilerin
        // ta kendisidir, ayrı bir "başlangıç sürümü" yoktur.
        foreach (var (def, slot) in ComponentCatalog.StartingLoadout)
            InstallComponent(def, slot, deductCost: false);
    }

    /// <summary>Bir silah tipinin tanımı (unlock maliyeti dahil).</summary>
    public static ComponentDefinition GetWeaponBase(WeaponType type)
        => ComponentCatalog.Weapon(type);

    /// <summary>
    /// Stat upgrade'lerine harcanan toplam kaynak. Satış iadesi bunu da kapsar —
    /// eskiden yalnızca komponentin sellValue'su dönüyordu, yani kalkana binlerce
    /// kristal stat basıp satan oyuncu 18 kristal alıyordu.
    /// </summary>
    public static int StatSpent(ComponentDefinition def, ShipComponentBase comp)
    {
        if (def == null || comp == null) return 0;
        int   baseCost = def.statCostBase > 0 ? def.statCostBase : def.cost;
        var   cfg      = BalanceConfig.Instance;
        int   total    = 0;
        foreach (var kv in comp.StatLevels) total += cfg.StatTotalSpent(baseCost, kv.Value, kv.Key);
        return total;
    }

    /// <summary>Satışta geri dönen miktar: kurulum iadesi + stat harcamasının bir kısmı.</summary>
    public static int SellRefund(ComponentDefinition def, ShipComponentBase comp)
    {
        if (def == null) return 0;
        int statRefund = Mathf.RoundToInt(StatSpent(def, comp) * BalanceConfig.Instance.sellRefundRatio);
        return def.sellValue + statRefund;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <param name="deductCost">false geçilirse kaynak düşülmez (Upgrade içi kullanım).</param>
    public bool InstallComponent(ComponentDefinition def, int slotIndex, bool deductCost = true)
    {
        if (def == null) return false;
        if (slotIndex < 0 || slotIndex >= slotCount) return false;
        if (!IsSlotEmpty(slotIndex)) return false;

        // Ana silah slotu yalnızca silah kabul eder. Kayıt yüklendikten sonra
        // slot kaydı boş kaldığı dönemde oraya kalkan kurulabiliyordu; slot
        // sahiplenildiğinde de silah bir daha geri gelmiyordu.
        bool isWeapon = def.componentType == ComponentType.Weapon;
        if (isWeapon != (slotIndex == WeaponSlotIndex)) return false;

        // Enerji kapısı — kuracak enerji yoksa kaynak da harcanmaz
        if (deductCost && !HasEnergyHeadroom(def.baseEnergyCost)) return false;

        if (deductCost)
        {
            if (ResourceInventory.Instance == null) return false;
            if (!ResourceInventory.Instance.TrySpend(def.costResource, def.cost)) return false;
        }

        GameObject go = new GameObject(def.componentName);
        go.transform.SetParent(transform);
        go.transform.localPosition = GetSlotLocalPosition(slotIndex);
        go.transform.localScale    = Vector3.one;

        ShipComponentBase comp = null;

        switch (def.componentType)
        {
            case ComponentType.Generator:
                var gen = go.AddComponent<GeneratorComponent>();
                gen.Init(def.productionAmount);
                comp = gen;
                break;

            case ComponentType.Shield:
                var shield = go.AddComponent<ShieldGeneratorComponent>();
                shield.maxShield     = def.maxShield;
                shield.rechargeRate  = def.rechargeRate;
                // Orphan HP varsa kaldığı yerden devam et, yoksa tam dolu başla
                shield.currentShield = ShieldGeneratorComponent.TakeOrphanShield(def.maxShield);
                comp = shield;
                break;

            case ComponentType.RepairUnit:
                var repair = go.AddComponent<RepairUnitComponent>();
                repair.repairRate = def.repairRate;
                comp = repair;
                break;

            case ComponentType.Weapon:
                _unlockedWeapons[def.weaponType] = def;
                _activeWeaponType = def.weaponType;
                var wc = GetComponentInChildren<WeaponController>();
                if (wc != null) wc.Configure(def);
                Destroy(go);
                go = null;
                break;

            case ComponentType.Turret:
                var tc = go.AddComponent<TurretController>();
                tc.Configure(def);
                comp = tc;
                break;

            case ComponentType.Hangar:
                var hc = go.AddComponent<HangarComponent>();
                comp = hc;
                break;

            case ComponentType.Storage:
                var st = go.AddComponent<StorageComponent>();
                st.Init(def.storageMetal, def.storageCrystal);
                st.componentName = def.componentName;
                comp = st;
                break;
        }

        // Enerji kaydı: Awake/OnEnable sıfırla kaydolur, gerçek değeri burada alır
        comp?.SetEnergyBase(def.baseEnergyCost);

        _slots[slotIndex]         = comp;
        _installedDefs[slotIndex] = def;
        _slotObjects[slotIndex]   = go;

        if (!_slotsByType.ContainsKey(def.componentType))
            _slotsByType[def.componentType] = new List<int>();
        if (!_slotsByType[def.componentType].Contains(slotIndex))
            _slotsByType[def.componentType].Add(slotIndex);

        return true;
    }

    /// <param name="returnResources">false geçilirse kaynak iade edilmez (Upgrade içi kullanım).</param>
    public bool SellComponent(int slotIndex, bool returnResources = true)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return false;
        var defToSell = _installedDefs[slotIndex];
        if (defToSell?.componentType == ComponentType.Weapon ||
            defToSell?.componentType == ComponentType.Hangar) return false; // Weapon + Hangar satılamaz
        if (_installedDefs[slotIndex] == null) return false;

        if (returnResources && ResourceInventory.Instance != null)
            ResourceInventory.Instance.Add(defToSell.costResource,
                                           SellRefund(defToSell, _slots[slotIndex]));

        if (_slotObjects[slotIndex] != null)
            Destroy(_slotObjects[slotIndex]);

        var removedType = _installedDefs[slotIndex].componentType;
        _slots[slotIndex]         = null;
        _installedDefs[slotIndex] = null;
        _slotObjects[slotIndex]   = null;

        if (_slotsByType.TryGetValue(removedType, out var list))
            list.Remove(slotIndex);

        return true;
    }

    // -------------------------------------------------------------------------
    // Enerji kapısı
    // -------------------------------------------------------------------------

    /// <summary>Ek tüketim jeneratörün üretimine sığıyor mu?</summary>
    public static bool HasEnergyHeadroom(float additionalDraw)
        => EnergyShortfall(additionalDraw) <= 0f;

    /// <summary>Ne kadar enerji eksik? 0 = yeterli.</summary>
    public static float EnergyShortfall(float additionalDraw)
    {
        var bus = EnergyBus.Instance;
        if (bus == null || additionalDraw <= 0f) return 0f;
        return Mathf.Max(0f, bus.TotalConsumption + additionalDraw - bus.TotalProduction);
    }

    /// <summary>Bu statı bir seviye yükseltmenin getireceği EK enerji yükü.</summary>
    public float StatUpgradeEnergyDelta(int slotIndex, string key)
    {
        var comp = GetSlotComponent(slotIndex);
        return comp != null ? comp.NextUpgradeEnergyDelta(key) : 0f;
    }

    public void SwitchWeapon(WeaponType type)
    {
        if (!_unlockedWeapons.TryGetValue(type, out var def)) return;
        _activeWeaponType = type;
        _installedDefs[WeaponSlotIndex] = def;   // slot başlığı aktif silahı göstersin
        ApplyActiveWeaponStats();
    }

    public int GetWeaponStatLevel(WeaponType type, string key)
    {
        if (!_weaponStatLevels.TryGetValue(type, out var stats)) return 0;
        return stats.TryGetValue(key, out var lvl) ? lvl : 0;
    }

    /// <summary>UI tarafından çağrılır; maliyet zaten düşülmüştür.</summary>
    public void ApplyWeaponStatUpgrade(WeaponType type, string key)
    {
        if (!_weaponStatLevels.ContainsKey(type))
            _weaponStatLevels[type] = new Dictionary<string, int>();
        int cur = GetWeaponStatLevel(type, key);
        if (cur >= ShipComponentBase.MaxStatLevel) return;
        _weaponStatLevels[type][key] = cur + 1;
        if (_activeWeaponType == type) ApplyActiveWeaponStats();
    }

    void ApplyActiveWeaponStats()
    {
        if (!_unlockedWeapons.TryGetValue(_activeWeaponType, out var def)) return;
        var wc = GetComponentInChildren<WeaponController>();
        if (wc == null) return;
        wc.Configure(def);
        if (!_weaponStatLevels.TryGetValue(_activeWeaponType, out var stats)) return;
        var cfg = BalanceConfig.Instance;
        if (stats.TryGetValue("damage",   out var d)) wc.damage   *= cfg.StatMultiplier(d);
        if (stats.TryGetValue("fireRate", out var f)) wc.fireRate /= cfg.StatMultiplier(f);
    }

    /// <summary>Yeni bir silah tipini satın alıp açar. Zaten açıksa false döner.</summary>
    public bool UnlockWeaponType(WeaponType type)
    {
        if (_unlockedWeapons.ContainsKey(type)) return false;
        var def = GetWeaponBase(type);
        if (def == null) return false;
        if (def.cost > 0)
        {
            if (ResourceInventory.Instance == null) return false;
            if (!ResourceInventory.Instance.TrySpend(def.costResource, def.cost)) return false;
        }
        _unlockedWeapons[type] = def;
        return true;
    }

    /// <summary>Bir silah tipinin tanımını döner (açılmışsa null değil).</summary>
    public ComponentDefinition GetWeaponDef(WeaponType type) =>
        _unlockedWeapons.TryGetValue(type, out var d) ? d : null;

    /// <summary>
    /// Turret'in uzmanlaşmasını değiştirir. Stat upgrade seviyeleri korunur.
    /// refundMultiplier: mevcut spec maliyetinin ne kadarı iade edilir (zorluk derecesine göre).
    /// </summary>
    public bool SpecializeTurret(int slotIndex, TurretSpecType newSpec,
                                 ComponentDefinition newSpecDef, float refundMultiplier = 0.5f)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return false;
        var currentDef = _installedDefs[slotIndex];
        if (currentDef == null || currentDef.componentType != ComponentType.Turret) return false;
        var tc = _slots[slotIndex] as TurretController;
        if (tc == null) return false;

        // Mevcut spec maliyetini iade et
        if (currentDef.turretSpecType != TurretSpecType.None && currentDef.specCost > 0)
        {
            int refund = Mathf.RoundToInt(currentDef.specCost * refundMultiplier);
            ResourceInventory.Instance?.Add(currentDef.specCostResource, refund);
        }

        // Yeni spec için kaynak düş
        if (newSpecDef.specCost > 0)
        {
            if (ResourceInventory.Instance == null ||
                !ResourceInventory.Instance.TrySpend(newSpecDef.specCostResource, newSpecDef.specCost))
            {
                // Ödeme başarısız — iadeyi geri al
                if (currentDef.turretSpecType != TurretSpecType.None && currentDef.specCost > 0)
                {
                    int refund = Mathf.RoundToInt(currentDef.specCost * refundMultiplier);
                    ResourceInventory.Instance?.TrySpend(currentDef.specCostResource, refund);
                }
                return false;
            }
        }

        tc.Specialize(newSpec, newSpecDef);
        _installedDefs[slotIndex] = newSpecDef;
        return true;
    }

    // -------------------------------------------------------------------------
    // Kayıt / yükleme desteği
    // -------------------------------------------------------------------------

    /// <summary>Kurulu her slotu tanımı ve komponentiyle birlikte gezer.</summary>
    public IEnumerable<(int slot, ComponentDefinition def, ShipComponentBase comp)> EnumerateSlots()
    {
        for (int i = 0; i < slotCount; i++)
            if (_installedDefs[i] != null)
                yield return (i, _installedDefs[i], _slots[i]);
    }

    /// <summary>Tüm slotları boşaltır — yükleme öncesi temiz sayfa.</summary>
    public void ClearAllSlots()
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (_slotObjects[i] != null) Destroy(_slotObjects[i]);
            _slots[i]         = null;
            _installedDefs[i] = null;
            _slotObjects[i]   = null;
        }
        _slotsByType.Clear();
        StorageComponent.Invalidate();

        // Slotları boşaltmak mevcut kalkan jeneratörünü de yok eder; onun
        // artığının kayıttan gelen jeneratöre karışmaması gerekir.
        ShieldGeneratorComponent.ResetStatics();
    }

    /// <summary>Kayıttan bir slotu stat seviyeleriyle birlikte geri kurar.</summary>
    public bool RestoreSlot(int slotIndex, ComponentDefinition def, Dictionary<string, int> stats)
    {
        if (!InstallComponent(def, slotIndex, deductCost: false)) return false;

        var comp = _slots[slotIndex];
        if (comp != null && stats != null)
        {
            foreach (var kv in stats) comp.StatLevels[kv.Key] = kv.Value;
            comp.SetEnergyBase(def.baseEnergyCost);
            foreach (var key in stats.Keys) comp.OnStatUpgraded(key);
        }
        return true;
    }

    /// <summary>Kayıttan silah durumunu geri kurar.</summary>
    public void RestoreWeapon(WeaponType type, int damageLevel, int fireRateLevel)
    {
        var def = ComponentCatalog.Weapon(type);
        if (def == null) return;

        _unlockedWeapons[type] = def;
        _weaponStatLevels[type] = new Dictionary<string, int>
        {
            { "damage",   damageLevel   },
            { "fireRate", fireRateLevel },
        };
    }

    /// <summary>
    /// Aktif silahı seçer, statlarını uygular ve silah SLOTUNU geri kaydeder
    /// (yükleme sonu).
    ///
    /// Slot kaydı şart: ClearAllSlots kayıt yüklenirken slot 1'i de boşaltıyor
    /// ama silahlar slot olarak kaydedilmiyor. Burada geri yazılmazsa slot 1
    /// oyunun geri kalanı boyunca "boş" kalır.
    /// </summary>
    public void FinishRestore(WeaponType active)
    {
        if (_unlockedWeapons.ContainsKey(active)) _activeWeaponType = active;
        ApplyActiveWeaponStats();

        if (_unlockedWeapons.TryGetValue(_activeWeaponType, out var def))
        {
            _installedDefs[WeaponSlotIndex] = def;
            if (!_slotsByType.TryGetValue(ComponentType.Weapon, out var list))
                _slotsByType[ComponentType.Weapon] = list = new List<int>();
            if (!list.Contains(WeaponSlotIndex)) list.Add(WeaponSlotIndex);
        }
    }

    public ShipComponentBase GetSlotComponent(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return null;
        return _slots[slotIndex];
    }

    Vector3 GetSlotLocalPosition(int slotIndex)
    {
        foreach (var sv in GetComponentsInChildren<SlotVisual>())
            if (sv.slotIndex == slotIndex)
                return sv.transform.localPosition;
        return Vector3.zero;
    }

    /// <summary>Normal/Hard modda komponent yok edilince slot'u temizler.</summary>
    public void ClearSlotForComponent(ShipComponentBase comp)
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (_slots[i] == comp)
            {
                _slots[i]         = null;
                _installedDefs[i] = null;
                _slotObjects[i]   = null;
                return;
            }
        }
    }

    public ComponentDefinition GetSlotDef(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return null;
        return _installedDefs[slotIndex];
    }

    public bool IsSlotEmpty(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return false;
        return _installedDefs[slotIndex] == null;
    }
}
