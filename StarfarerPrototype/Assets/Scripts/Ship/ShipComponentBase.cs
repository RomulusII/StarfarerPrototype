using UnityEngine;

/// <summary>
/// Tüm gemi komponentlerinin base class'ı.
///
/// HP sıfırlandığında zorluk ayarına göre davranır:
///   Easy   — deaktif kalır (_deactivated=true), RepairUnit maxHP'ye tamir edince yeniden açılır.
///   Normal/Hard — ShipLoadout slot'u temizlenir, GO yok edilir.
/// </summary>
public abstract class ShipComponentBase : MonoBehaviour
{
    public string componentName;
    public float  maxHP            = 100f;
    public float  currentHP;
    public float  energyConsumption = 0f;

    bool _deactivated = false;

    public bool IsDeactivated => _deactivated;
    public bool IsOperational  => currentHP > 0f && !_deactivated;

    protected virtual void Awake()
    {
        currentHP = maxHP;
    }

    public virtual void TakeDamage(float amount)
    {
        if (!IsOperational) return;
        currentHP = Mathf.Max(0f, currentHP - amount);
        if (currentHP == 0f)
            OnComponentDestroyed();
    }

    public virtual void Repair(float amount)
    {
        currentHP = Mathf.Min(currentHP + amount, maxHP);

        if (_deactivated && currentHP >= maxHP)
        {
            _deactivated = false;
            if (EnergyBus.Instance != null)
                EnergyBus.Instance.RegisterConsumer(energyConsumption);
        }
    }

    protected virtual void OnComponentDestroyed()
    {
        if (DifficultyManager.Current == Difficulty.Easy)
        {
            _deactivated = true;
            if (EnergyBus.Instance != null)
                EnergyBus.Instance.UnregisterConsumer(energyConsumption);
        }
        else
        {
            var loadout = FindFirstObjectByType<ShipLoadout>();
            loadout?.ClearSlotForComponent(this);
            Destroy(gameObject);
        }
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
