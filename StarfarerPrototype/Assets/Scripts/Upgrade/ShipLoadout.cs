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
    private GameObject[]          _slotObjects; // WeaponController gibi ShipComponentBase olmayan tipler için

    void Awake()
    {
        _slots         = new ShipComponentBase[slotCount];
        _installedDefs = new ComponentDefinition[slotCount];
        _slotObjects   = new GameObject[slotCount];
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
        go.transform.localPosition = Vector3.zero;
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
                go.AddComponent<WeaponController>(); // şimdilik stub; WeaponController ShipComponentBase değil
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

    public ShipComponentBase GetSlotComponent(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slotCount) return null;
        return _slots[slotIndex];
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
