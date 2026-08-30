using UnityEngine;

/// <summary>
/// Metal, Kristal ve EnergyScrap kaynaklarını yöneten singleton.
/// </summary>
public class ResourceInventory : MonoBehaviour
{
    public static ResourceInventory Instance { get; private set; }

    public float metal      = 0f;
    public float crystal    = 20f;
    public float energyScrap = 0f;

    [Header("Taban Kapasite (depo komponentleri bunun üstüne ekler)")]
    public float baseMetalCapacity   = 150f;

    [Tooltip("Kristal, metalden çok daha yavaş akar: yalnızca kalkanlı " +
             "düşmanlardan ve asteroitlerin %12'sinden geliyor. 50 tavanla " +
             "oyuncu daha ilk kalkan yükseltmesini biriktiremeden tavana " +
             "çarpıp kaynağı yakıyordu — depo kurulana kadar kristal " +
             "toplamanın anlamı yoktu.")]
    public float baseCrystalCapacity = 100f;
    public float maxEnergyScrap      = 200f;

    /// <summary>Taban kapasite + kurulu depoların toplamı.</summary>
    public float maxMetal   => baseMetalCapacity   + StorageComponent.TotalMetalCapacity;
    public float maxCrystal => baseCrystalCapacity + StorageComponent.TotalCrystalCapacity;

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

    // --- ResourceType tabanlı API (ShipLoadout için) ---

    public bool TrySpend(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.RawMaterial:    return SpendMetal(amount);
            case ResourceType.EnergyCrystal:  return SpendCrystal(amount);
            default: return false;
        }
    }

    /// <summary>
    /// Miktar KESİRLİ tutulur. Eskiden int'ti ve toplayıcı kargosunu tam birime
    /// yuvarlamak zorundaydı: level 1'de asteroit parçası 0.5 kaynak düşürdüğü
    /// için kristalin tamamı yuvarlamada kayboluyordu.
    /// </summary>
    public void Add(ResourceType type, float amount)
    {
        switch (type)
        {
            case ResourceType.RawMaterial:   AddMetal(amount);   break;
            case ResourceType.EnergyCrystal: AddCrystal(amount); break;
        }
    }

    public int Get(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.RawMaterial:   return (int)metal;
            case ResourceType.EnergyCrystal: return (int)crystal;
            default: return 0;
        }
    }
}
