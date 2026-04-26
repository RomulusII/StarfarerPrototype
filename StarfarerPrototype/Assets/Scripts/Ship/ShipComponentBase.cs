using System.Collections.Generic;
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

    // ── Stat upgrade sistemi ──────────────────────────────────────────────────
    // key → seviye (0-5). 0 = upgrade yapılmamış.
    public readonly Dictionary<string, int> StatLevels = new();
    public const int MaxStatLevel = 5;

    /// <summary>Verilen stat key'i için upgrade çarpanını döner. Seviye başına ×1.5.</summary>
    public float GetMultiplier(string key) =>
        Mathf.Pow(1.5f, StatLevels.TryGetValue(key, out var lvl) ? lvl : 0);

    /// <summary>Verilen stat'ın mevcut seviyesini döner.</summary>
    public int GetStatLevel(string key) =>
        StatLevels.TryGetValue(key, out var lvl) ? lvl : 0;

    /// <summary>Stat upgrade'i uygular. UI tarafından çağrılır, maliyet zaten düşülmüştür.</summary>
    public void ApplyStatUpgrade(string key)
    {
        int cur = GetStatLevel(key);
        if (cur >= MaxStatLevel) return;
        StatLevels[key] = cur + 1;
        OnStatUpgraded(key);
    }

    /// <summary>Alt sınıflar override eder — stat değişince etkin değeri yeniden hesaplar.</summary>
    public virtual void OnStatUpgraded(string key) { }

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
