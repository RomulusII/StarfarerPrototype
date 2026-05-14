using UnityEngine;

/// <summary>
/// Pasif tamir ünitesi. Sahnedeki hasarlı komponentleri enerji harcayarak
/// yavaşça tamir eder. Bir anda tek komponenti hedefler: HP oranı en düşük olan.
/// </summary>
public class RepairUnitComponent : ShipComponentBase
{
    public float repairRate      = 8f;
    public float energyPerRepair = 1f;

    protected override void Awake()
    {
        base.Awake();
        componentName     = "Repair Unit";
        energyConsumption = 0f;
    }

    void Update()
    {
        if (!IsOperational) return;

        float effectiveRate   = repairRate     * GetMultiplier("repairRate");
        float effectiveEnergy = energyPerRepair / GetMultiplier("energyEfficiency");

        if (EnergyBus.Instance == null ||
            !EnergyBus.Instance.RequestEnergy(effectiveEnergy * Time.deltaTime))
            return;

        ShipComponentBase compTarget = FindMostDamagedComponent();
        float compRatio = compTarget != null && compTarget.maxHP > 0f
            ? compTarget.currentHP / compTarget.maxHP : 1f;

        var ps = FindFirstObjectByType<PlayerShip>();
        float hullRatio = ps != null && ps.maxHullHP > 0f
            ? ps.currentHullHP / ps.maxHullHP : 1f;

        // En çok hasarlı hedefi onar (hull veya komponent)
        if (ps != null && hullRatio < 1f && hullRatio <= compRatio)
            ps.currentHullHP = Mathf.Min(ps.maxHullHP, ps.currentHullHP + effectiveRate * Time.deltaTime);
        else if (compTarget != null)
            compTarget.Repair(effectiveRate * Time.deltaTime);
    }

    ShipComponentBase FindMostDamagedComponent()
    {
        var all = FindObjectsByType<ShipComponentBase>(FindObjectsSortMode.None);

        ShipComponentBase target   = null;
        float             lowestRatio = 1f;

        foreach (var comp in all)
        {
            if (comp == this)        continue;
            if (comp.maxHP <= 0f)    continue;
            if (comp.currentHP >= comp.maxHP) continue; // hasar yok

            float ratio = comp.currentHP / comp.maxHP;
            if (ratio < lowestRatio)
            {
                lowestRatio = ratio;
                target      = comp;
            }
        }

        return target;
    }
}
