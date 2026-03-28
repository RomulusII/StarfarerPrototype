using UnityEngine;

/// <summary>
/// Maksimum enerji kapasitesini artıran gemi komponenti.
/// </summary>
public class CapacitorComponent : ShipComponentBase
{
    public float capacityBonus = 25f;

    protected override void Awake()
    {
        base.Awake();
        componentName     = "Capacitor";
        energyConsumption = 0f;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.AddCapacity(capacityBonus);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.RemoveCapacity(capacityBonus);
    }

    public override void OnDestroyed()
    {
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.RemoveCapacity(capacityBonus);
        base.OnDestroyed();
    }
}
