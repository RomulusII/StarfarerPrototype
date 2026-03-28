using UnityEngine;

/// <summary>
/// Tüm gemi komponentlerinin base class'ı.
/// </summary>
public abstract class ShipComponentBase : MonoBehaviour
{
    public string componentName;
    public float maxHP = 100f;
    public float currentHP;
    public float energyConsumption = 0f;

    public bool IsOperational => currentHP > 0f;

    protected virtual void Awake()
    {
        currentHP = maxHP;
    }

    public virtual void TakeDamage(float amount)
    {
        currentHP = Mathf.Max(0f, currentHP - amount);
        if (currentHP == 0f)
            OnDestroyed();
    }

    public virtual void OnDestroyed()
    {
        Destroy(gameObject);
    }

    public virtual void Repair(float amount)
    {
        currentHP = Mathf.Min(currentHP + amount, maxHP);
    }

    protected virtual void OnEnable()
    {
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.RegisterConsumer(energyConsumption);
    }

    protected virtual void OnDisable()
    {
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.UnregisterConsumer(energyConsumption);
    }
}
