using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerShip'e eklenen komponent yuvası yöneticisi.
/// Kurulum, satış ve yükseltme işlemlerini ResourceInventory üzerinden çalıştırır.
/// </summary>
public class ShipLoadout : MonoBehaviour
{
    public int slotCount = 10;

    private ShipComponentBase[]   _slots;
    private ComponentDefinition[] _installedDefs;
    private GameObject[]          _slotObjects;

    // Silah switch sistemi
    private readonly Dictionary<WeaponType, ComponentDefinition> _unlockedWeapons = new();
    private WeaponType _activeWeaponType = WeaponType.Laser;

    // Silah stat upgrade seviyeleri — silah tipinden bağımsız, kalıcı
    private readonly Dictionary<WeaponType, Dictionary<string, int>> _weaponStatLevels = new();

    public bool IsWeaponTypeUnlocked(WeaponType type) => _unlockedWeapons.ContainsKey(type);
    public WeaponType GetActiveWeaponType() => _activeWeaponType;

    void Awake()
    {
        _slots         = new ShipComponentBase[slotCount];
        _installedDefs = new ComponentDefinition[slotCount];
        _slotObjects   = new GameObject[slotCount];
    }

    void Start()
    {
        InitWeaponChains();
        // Lazer başlangıçta ücretsiz kurulu gelir
        InstallComponent(_laserChain[0], slotIndex: 5, deductCost: false);
    }

    // -------------------------------------------------------------------------
    // Silah zincirleri (statik, oyun ömrü boyunca tek kez oluşturulur)
    // -------------------------------------------------------------------------

    static ComponentDefinition[] _laserChain;
    static ComponentDefinition[] _kineticChain;
    static ComponentDefinition[] _plasmaChain;

    static void InitWeaponChains()
    {
        if (_laserChain != null) return;

        var lm3 = W("Lazer Topu Mk3",   WeaponType.Laser,   3, ResourceType.EnergyCrystal, 60, 25, null,  18f, 0.05f, 3f);
        var lm2 = W("Lazer Topu Mk2",   WeaponType.Laser,   2, ResourceType.EnergyCrystal, 35, 15, lm3,   12f, 0.06f, 2.5f);
        var lm1 = W("Lazer Topu Mk1",   WeaponType.Laser,   1, ResourceType.EnergyCrystal,  0,  0, lm2,    8f, 0.07f, 2f);
        _laserChain = new[] { lm1, lm2, lm3 };

        var km3 = W("Kinetik Top Mk3",  WeaponType.Kinetic, 3, ResourceType.RawMaterial,   50, 20, null,  18f, 0.12f);
        var km2 = W("Kinetik Top Mk2",  WeaponType.Kinetic, 2, ResourceType.RawMaterial,   28, 10, km3,   14f, 0.13f);
        var km1 = W("Kinetik Top Mk1",  WeaponType.Kinetic, 1, ResourceType.RawMaterial,   12,  5, km2,   10f, 0.15f);
        _kineticChain = new[] { km1, km2, km3 };

        var pm3 = W("Plazma Topu Mk3",  WeaponType.Plasma,  3, ResourceType.RawMaterial,   65, 25, null,  45f, 0.10f, 0f, 0.7f,  4);
        var pm2 = W("Plazma Topu Mk2",  WeaponType.Plasma,  2, ResourceType.RawMaterial,   38, 15, pm3,   36f, 0.11f, 0f, 0.75f, 3);
        var pm1 = W("Plazma Topu Mk1",  WeaponType.Plasma,  1, ResourceType.RawMaterial,   20,  8, pm2,   28f, 0.12f, 0f, 0.8f,  3);
        _plasmaChain = new[] { pm1, pm2, pm3 };
    }

    static ComponentDefinition W(string name, WeaponType wt, int tier,
        ResourceType res, int cost, int sell, ComponentDefinition upgTo,
        float dmg = 10f, float rate = 0.15f, float energy = 0f,
        float charge = 0.8f, int burst = 3)
    {
        var d = ScriptableObject.CreateInstance<ComponentDefinition>();
        d.componentName             = name;
        d.componentType             = ComponentType.Weapon;
        d.weaponType                = wt;
        d.tier                      = tier;
        d.costResource              = res;
        d.cost                      = cost;
        d.sellValue                 = sell;
        d.upgradeTo                 = upgTo;
        d.weaponDamage              = dmg;
        d.weaponFireRate            = rate;
        d.weaponEnergyCostPerShot   = energy;
        d.weaponChargeTime          = charge;
        d.weaponBurstCount          = burst;
        return d;
    }

    /// <summary>Bir silah tipinin Mk1 tanımını döner (unlock maliyeti dahil).</summary>
    public static ComponentDefinition GetWeaponChainStart(WeaponType type)
    {
        InitWeaponChains();
        return type switch
        {
            WeaponType.Laser   => _laserChain[0],
            WeaponType.Kinetic => _kineticChain[0],
            WeaponType.Plasma  => _plasmaChain[0],
            _                  => null,
        };
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
                gen.productionAmount = def.productionAmount;
                comp = gen;
                break;

            case ComponentType.Shield:
                var shield = go.AddComponent<ShieldGeneratorComponent>();
                shield.maxShield    = def.maxShield;
                shield.rechargeRate = def.rechargeRate;
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
        }

        _slots[slotIndex]         = comp;
        _installedDefs[slotIndex] = def;
        _slotObjects[slotIndex]   = go;
        return true;
    }

    /// <param name="returnResources">false geçilirse kaynak iade edilmez (Upgrade içi kullanım).</param>
    public bool SellComponent(int slotIndex, bool returnResources = true)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return false;
        if (slotIndex == 5) return false; // Weapon slot satılamaz
        if (_installedDefs[slotIndex] == null) return false;

        if (returnResources && ResourceInventory.Instance != null)
            ResourceInventory.Instance.Add(_installedDefs[slotIndex].costResource,
                                           _installedDefs[slotIndex].sellValue);

        if (_slotObjects[slotIndex] != null)
            Destroy(_slotObjects[slotIndex]);

        _slots[slotIndex]         = null;
        _installedDefs[slotIndex] = null;
        _slotObjects[slotIndex]   = null;
        return true;
    }

    public bool UpgradeComponent(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return false;
        if (_installedDefs[slotIndex] == null) return false;

        ComponentDefinition upgradeTo = _installedDefs[slotIndex].upgradeTo;
        if (upgradeTo == null) return false;

        // Oyuncu sadece farkı öder: yeni maliyet - eski satış değeri
        int diffCost = upgradeTo.cost - _installedDefs[slotIndex].sellValue;
        if (diffCost > 0)
        {
            if (ResourceInventory.Instance == null) return false;
            if (!ResourceInventory.Instance.TrySpend(upgradeTo.costResource, diffCost)) return false;
        }

        ComponentDefinition upgradeTarget = upgradeTo;
        SellComponent(slotIndex, returnResources: false); // eski komponenti yok et, para iade etme
        return InstallComponent(upgradeTarget, slotIndex, deductCost: false);
    }

    public void SwitchWeapon(WeaponType type)
    {
        if (!_unlockedWeapons.TryGetValue(type, out _)) return;
        _activeWeaponType = type;
        _installedDefs[5] = _unlockedWeapons[type];
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
        if (stats.TryGetValue("damage",   out var d)) wc.damage   *= Mathf.Pow(1.5f, d);
        if (stats.TryGetValue("fireRate", out var f)) wc.fireRate /= Mathf.Pow(1.5f, f);
    }

    /// <summary>Yeni bir silah tipini satın alıp açar. Zaten açıksa false döner.</summary>
    public bool UnlockWeaponType(WeaponType type)
    {
        if (_unlockedWeapons.ContainsKey(type)) return false;
        var def = GetWeaponChainStart(type);
        if (def == null) return false;
        if (def.cost > 0)
        {
            if (ResourceInventory.Instance == null) return false;
            if (!ResourceInventory.Instance.TrySpend(def.costResource, def.cost)) return false;
        }
        _unlockedWeapons[type] = def;
        return true;
    }

    /// <summary>Belirli bir silah tipini bağımsız olarak upgrade eder (aktif olmak zorunda değil).</summary>
    public bool UpgradeWeaponType(WeaponType type)
    {
        if (!_unlockedWeapons.TryGetValue(type, out var current)) return false;
        if (current.upgradeTo == null) return false;
        var next    = current.upgradeTo;
        int diff    = next.cost - current.sellValue;
        if (diff > 0)
        {
            if (ResourceInventory.Instance == null) return false;
            if (!ResourceInventory.Instance.TrySpend(next.costResource, diff)) return false;
        }
        _unlockedWeapons[type] = next;
        if (_activeWeaponType == type)
        {
            _installedDefs[5] = next;
            GetComponentInChildren<WeaponController>()?.Configure(next);
        }
        return true;
    }

    /// <summary>Bir silah tipinin mevcut (en son upgrade edilmiş) tanımını döner.</summary>
    public ComponentDefinition GetWeaponDef(WeaponType type) =>
        _unlockedWeapons.TryGetValue(type, out var d) ? d : null;

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
