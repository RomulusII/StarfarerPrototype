using UnityEngine;

/// <summary>
/// Geminin merkezi enerji sistemi. Singleton.
/// </summary>
public class EnergyBus : MonoBehaviour
{
    public static EnergyBus Instance { get; private set; }

    public float baseMaxEnergy = 50f;

    [SerializeField] private float _currentEnergy;
    public float currentEnergy => _currentEnergy;

    public float maxEnergy => baseMaxEnergy + _bonusCapacity;

    /// <summary>
    /// Karıştırıcıların (Jammer) kıstığı üretim oranı (0–1). Sahnede jammer
    /// varken jeneratörün etkin üretimi düşer — enerji bütçesi artık savaş
    /// içinde de saldırıya açık bir kaynak.
    /// </summary>
    public float JamFactor { get; private set; }

    /// <summary>Kurulu jeneratörlerin ham toplam üretimi.</summary>
    public float RawProduction => _totalProduction;

    /// <summary>Karıştırma sonrası gerçekten akan üretim.</summary>
    public float TotalProduction => _totalProduction * (1f - JamFactor);

    public float TotalConsumption => _totalConsumption;
    public float NetFlow          => TotalProduction - _totalConsumption;

    private float _bonusCapacity    = 0f;
    private float _totalProduction  = 0f;
    private float _totalConsumption = 0f;

    void Awake()
    {
        Instance = this;
        _currentEnergy = baseMaxEnergy;
    }

    public void RegisterProducer(float amount)   => _totalProduction  += amount;
    public void UnregisterProducer(float amount) => _totalProduction   = Mathf.Max(0f, _totalProduction  - amount);
    public void RegisterConsumer(float amount)   => _totalConsumption += amount;
    public void UnregisterConsumer(float amount) => _totalConsumption  = Mathf.Max(0f, _totalConsumption - amount);

    public void AddCapacity(float amount)    => _bonusCapacity += amount;
    public void RemoveCapacity(float amount) => _bonusCapacity  = Mathf.Max(0f, _bonusCapacity - amount);

    public bool RequestEnergy(float amount)
    {
        if (_currentEnergy < amount) return false;
        _currentEnergy -= amount;
        return true;
    }

    float _jamTimer;

    void Update()
    {
        // Jammer taraması her karede yapılmaz — sahne taraması pahalı, etki yavaş
        _jamTimer -= Time.deltaTime;
        if (_jamTimer <= 0f)
        {
            _jamTimer = 0.5f;
            JamFactor = EnemyBot.TotalEnergyJam(transform.position);
        }

        _currentEnergy += NetFlow * Time.deltaTime;
        _currentEnergy  = Mathf.Clamp(_currentEnergy, 0f, maxEnergy);
    }
}
