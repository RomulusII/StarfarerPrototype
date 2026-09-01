using UnityEngine;

/// <summary>
/// Enerji üreten gemi komponenti. İki stat izi taşır:
///
///   "production" — saniyedeki üretim (AKIŞ)
///   "capacitor"  — enerji tamponunun büyüklüğü (STOK)
///
/// İkisi farklı sorunları çözer ve bu yüzden gerçek bir seçim oluştururlar.
/// Üretim, sürekli tüketimin (kalkan şarjı, tamir, lazer) tavanını belirler.
/// Kapasitör ise BURST kapasitesidir: turretlerin aynı anda ateşlemesi, plazma
/// şarjı ve kalkan boost'u anlık olarak üretimin çok üstünde enerji ister —
/// tampon boşsa o atışlar hiç yapılamaz. Üretimi yükseltmek onu da çözer ama
/// çok daha pahalıya; tampon ucuz ve dar bir cevaptır.
///
/// Bonus TOPLAMSALDIR (zırh iziyle aynı gerekçe): çarpımsal olsaydı ikinci
/// jeneratör birincinin katı kadar tampon üretirdi.
/// </summary>
public class GeneratorComponent : ShipComponentBase
{
    public float productionAmount = 10f;

    /// <summary>Stat anahtarları — UpgradeUI ve kayıt aynı adları kullanır.</summary>
    public const string ProductionKey = "production";
    public const string CapacitorKey  = "capacitor";

    float _effectiveProduction;
    float _appliedCapacity;

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
        ApplyCapacitor();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (EnergyBus.Instance != null)
        {
            EnergyBus.Instance.UnregisterProducer(_effectiveProduction);
            EnergyBus.Instance.RemoveCapacity(_appliedCapacity);
        }
        _appliedCapacity = 0f;
    }

    /// <summary>Bu jeneratörün eklediği tampon kapasitesi.</summary>
    public float CapacitorBonus => CapacitorBonusAt(GetStatLevel(CapacitorKey));

    /// <summary>
    /// Verilen seviyedeki tampon bonusu. Her seviye tamponu %50 büyütür:
    ///
    ///     bonus(L) = tabanTampon × (1.5^L − 1)
    ///
    /// Taban EnergyBus'tan OKUNUR, sabit yazılmaz — böylece "her seviye +%50"
    /// ifadesi taban kapasite değiştiğinde de doğru kalır.
    ///
    /// Bonus jeneratörler arasında TOPLAMSALDIR (zırh iziyle aynı gerekçe):
    /// çarpımsal olsaydı ikinci jeneratör birincinin katı kadar tampon üretir
    /// ve tek doğru oyun "hepsini jeneratörle doldur" olurdu. Seviye içindeki
    /// büyüme çarpımsal, jeneratörler arası toplamsal.
    /// </summary>
    public static float CapacitorBonusAt(int level)
    {
        float baseEnergy = EnergyBus.Instance != null ? EnergyBus.Instance.baseMaxEnergy : 50f;
        float step       = BalanceConfig.Instance.capacitorStatStep;
        return baseEnergy * (Mathf.Pow(step, Mathf.Max(0, level)) - 1f);
    }

    /// <summary>
    /// EnergyBus kaydını tazeler. Eklenen miktar alanda tutulur ve yalnızca
    /// FARKI uygulanır — yoksa her yükseltme kapasiteyi bir kez daha eklerdi.
    /// </summary>
    void ApplyCapacitor()
    {
        if (EnergyBus.Instance == null) return;
        float next = CapacitorBonus;
        if (Mathf.Approximately(next, _appliedCapacity)) return;

        EnergyBus.Instance.RemoveCapacity(_appliedCapacity);
        EnergyBus.Instance.AddCapacity(next);
        _appliedCapacity = next;
    }

    /// <summary>
    /// Başlangıç üretim miktarını doğru şekilde ayarlar.
    /// AddComponent sonrası hemen çağrılmalıdır — Awake'teki kayıt düzeltirilir.
    /// </summary>
    public void Init(float production)
    {
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.UnregisterProducer(_effectiveProduction);
        productionAmount     = production;
        _effectiveProduction = production;
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.RegisterProducer(_effectiveProduction);
    }

    public override void OnStatUpgraded(string key)
    {
        if (key == CapacitorKey) { ApplyCapacitor(); return; }
        if (key != ProductionKey) return;

        float newEffective = productionAmount * GetMultiplier(ProductionKey);
        if (EnergyBus.Instance != null)
        {
            EnergyBus.Instance.UnregisterProducer(_effectiveProduction);
            EnergyBus.Instance.RegisterProducer(newEffective);
        }
        _effectiveProduction = newEffective;
    }
}
