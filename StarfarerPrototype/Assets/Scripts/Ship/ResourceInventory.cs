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
        // Tavana çarpan kısım YANAR. Gelirin ne kadarının boşa gittiğini ancak
        // burada görebiliriz: Add() sessizce kırpıyor, kimse haber vermiyordu.
        // "Depo baskısı ilginç bir karar mı, yoksa vergi mi" sorusunun cevabı
        // yanan/düşen oranıdır.
        float before = AmountOf(type);

        switch (type)
        {
            case ResourceType.RawMaterial:   AddMetal(amount);   break;
            case ResourceType.EnergyCrystal: AddCrystal(amount); break;
        }

        float gained = AmountOf(type) - before;
        BalanceLog.Event("resource")
                  .Str("tip",    type.ToString())
                  .Str("olay",   "toplandi")
                  .Num("miktar", gained)
                  .Num("yanan",  amount - gained)
                  .Num("stok",   AmountOf(type))
                  .Num("tavan",  CapacityOf(type))
                  .End();
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

    // ── Doluluk ───────────────────────────────────────────────────────────────
    //
    // Tavana çarpan kaynak SESSİZCE yanıyordu: Add() kırpıyor, kimse haber
    // vermiyor, toplayıcı da dolu bir tipi toplamayı sürdürüyordu. Doluluk
    // bilgisinin sahibi envanterdir — HUD uyarısı da toplayıcının hedef
    // seçimi de buradan okur, iki ayrı eşik yazılmaz.

    /// <summary>"Dolmak üzere" eşiği — HUD bu orandan sonra uyarır.</summary>
    public const float NearFullRatio = 0.90f;

    public float AmountOf(ResourceType type) => type switch
    {
        ResourceType.RawMaterial   => metal,
        ResourceType.EnergyCrystal => crystal,
        _                          => 0f,
    };

    public float CapacityOf(ResourceType type) => type switch
    {
        ResourceType.RawMaterial   => maxMetal,
        ResourceType.EnergyCrystal => maxCrystal,
        _                          => 0f,
    };

    /// <summary>Doluluk oranı (0–1). Kapasite yoksa dolu sayılır.</summary>
    public float FillRatio(ResourceType type)
    {
        float cap = CapacityOf(type);
        return cap > 0f ? Mathf.Clamp01(AmountOf(type) / cap) : 1f;
    }

    /// <summary>
    /// Bu tipten bir birim daha alacak yer var mı? Tam eşitlik yerine küçük bir
    /// pay bırakılır: kesirli miktarlar tavana asla tam oturmaz ve toplayıcı
    /// "neredeyse dolu" bir depoya sonsuza dek sefer yapardı.
    /// </summary>
    public bool IsFull(ResourceType type) => AmountOf(type) >= CapacityOf(type) - 0.01f;

    public bool IsNearlyFull(ResourceType type) => FillRatio(type) >= NearFullRatio;
}
