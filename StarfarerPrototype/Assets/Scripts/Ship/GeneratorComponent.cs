using UnityEngine;

/// <summary>
/// Enerji üreten gemi komponenti.
/// Stat upgrade: "production" → üretim miktarını artırır, EnergyBus kaydını günceller.
/// </summary>
public class GeneratorComponent : ShipComponentBase
{
    public float productionAmount = 10f;

    float _effectiveProduction;

    protected override void Awake()
    {
        base.Awake();
        componentName        = "Generator";
        energyConsumption    = 0f;
        _effectiveProduction = productionAmount;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.RegisterProducer(_effectiveProduction);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.UnregisterProducer(_effectiveProduction);
    }

    public override void OnStatUpgraded(string key)
    {
        if (key != "production") return;
        float newEffective = productionAmount * GetMultiplier("production");
        if (EnergyBus.Instance != null)
        {
            EnergyBus.Instance.UnregisterProducer(_effectiveProduction);
            EnergyBus.Instance.RegisterProducer(newEffective);
        }
        _effectiveProduction = newEffective;
    }
}
