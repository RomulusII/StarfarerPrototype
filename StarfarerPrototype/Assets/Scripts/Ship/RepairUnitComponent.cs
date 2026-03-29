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

        ShipComponentBase target = FindMostDamagedComponent();
        if (target == null) return;

        if (EnergyBus.Instance != null &&
            EnergyBus.Instance.RequestEnergy(energyPerRepair * Time.deltaTime))
        {
            target.Repair(repairRate * Time.deltaTime);
        }
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
