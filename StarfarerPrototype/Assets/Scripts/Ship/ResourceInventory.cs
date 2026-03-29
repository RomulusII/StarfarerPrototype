using UnityEngine;

/// <summary>
/// Metal, Kristal ve EnergyScrap kaynaklarını yöneten singleton.
/// </summary>
public class ResourceInventory : MonoBehaviour
{
    public static ResourceInventory Instance { get; private set; }

    public float metal      = 100f;
    public float crystal    = 20f;
    public float energyScrap = 0f;

    public float maxMetal      = 500f;
    public float maxCrystal    = 100f;
    public float maxEnergyScrap = 200f;

    void Awake()
    {
        Instance = this;
    }

    public void AddMetal(float amount)      => metal      = Mathf.Min(metal      + amount, maxMetal);
    public void AddCrystal(float amount)    => crystal    = Mathf.Min(crystal    + amount, maxCrystal);
    public void AddEnergyScrap(float amount) => energyScrap = Mathf.Min(energyScrap + amount, maxEnergyScrap);

    public bool SpendMetal(float amount)
    {
        if (!HasMetal(amount)) return false;
        metal -= amount;
        return true;
    }

    public bool SpendCrystal(float amount)
    {
        if (!HasCrystal(amount)) return false;
        crystal -= amount;
        return true;
    }

    public bool SpendEnergyScrap(float amount)
    {
        if (!HasEnergyScrap(amount)) return false;
        energyScrap -= amount;
        return true;
    }

    public bool HasMetal(float amount)      => metal      >= amount;
    public bool HasCrystal(float amount)    => crystal    >= amount;
    public bool HasEnergyScrap(float amount) => energyScrap >= amount;
}
