using UnityEngine;

/// <summary>
/// Enerji üreten gemi komponenti.
/// </summary>
public class GeneratorComponent : ShipComponentBase
{
    public float productionAmount = 10f;

    protected override void Awake()
    {
        base.Awake();
        componentName     = "Generator";
        energyConsumption = 0f;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.RegisterProducer(productionAmount);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.UnregisterProducer(productionAmount);
    }
}
