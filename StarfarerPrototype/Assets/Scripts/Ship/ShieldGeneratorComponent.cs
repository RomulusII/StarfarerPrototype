using UnityEngine;

/// <summary>
/// Küresel kalkan üreten gemi komponenti.
/// Birden fazla örneklenebilir; her biri kendi kalkan havuzunu yönetir.
/// </summary>
public class ShieldGeneratorComponent : ShipComponentBase
{
    public float maxShield        = 100f;
    public float currentShield;
    public float rechargeRate     = 1.5f;
    public float rechargeEnergyCost = 5f;

    [SerializeField] float shieldRechargeDelay          = 5f;
    [SerializeField] private float rechargeDelayAfterDepletion = 10f;

    float _timeSinceLastDamage;
    private bool  _isDepleted    = false;
    private float _depletedTimer = 0f;

    public bool IsShieldFull => currentShield >= maxShield;

    protected override void Awake()
    {
        base.Awake();
        componentName        = "Shield Generator";
        energyConsumption    = 0f;
        currentShield        = maxShield;
        _timeSinceLastDamage = shieldRechargeDelay; // ready to recharge from start
    }

    void Update()
    {
        _timeSinceLastDamage += Time.deltaTime;

        // Flag cleanup: şarj başlayıp currentShield > 0 olduktan sonra flag'i temizle
        if (_isDepleted && currentShield > 0f)
            _isDepleted = false;

        // Yeni depletion tespiti: kalkan az önce 0'a düştü
        if (!_isDepleted && currentShield <= 0f)
        {
            _isDepleted    = true;
            _depletedTimer = 0f;
        }

        // Depletion timer'ı ilerlet
        if (_isDepleted)
            _depletedTimer += Time.deltaTime;

        if (!IsOperational) return;

        // Silah boost aktifken kalkan şarjı durur
        if (BoostController.Mode == BoostMode.Weapon) return;

        if (IsShieldFull)   return;
        if (_timeSinceLastDamage < shieldRechargeDelay) return;
        if (_isDepleted && _depletedTimer < rechargeDelayAfterDepletion) return;

        float rechargeMulti = BoostController.Mode == BoostMode.Shield ? 3f : 1f;
        float energyMulti   = BoostController.Mode == BoostMode.Shield ? 5f : 1f;

        if (EnergyBus.Instance != null &&
            EnergyBus.Instance.RequestEnergy(rechargeEnergyCost * energyMulti * Time.deltaTime))
        {
            currentShield = Mathf.Min(currentShield + rechargeRate * rechargeMulti * Time.deltaTime, maxShield);
        }
    }

    /// <summary>
    /// Hasar alındığında çağrılır; her iki bekleme sayacını da sıfırlar.
    /// </summary>
    public void NotifyDamageTaken()
    {
        _timeSinceLastDamage = 0f;
        _depletedTimer       = 0f;
    }

    /// <summary>
    /// Gelen hasarı kalkana emer. Kalkandan geçen hasarı döner.
    /// </summary>
    public float AbsorbDamage(float incomingDamage)
    {
        if (currentShield >= incomingDamage)
        {
            currentShield -= incomingDamage;
            return 0f;
        }

        float remaining = incomingDamage - currentShield;
        currentShield = 0f;
        return remaining;
    }

    /// <summary>
    /// Sahnedeki tüm aktif ShieldGeneratorComponent'lerin currentShield toplamı.
    /// </summary>
    public static float GetTotalShield()
    {
        float total = 0f;
        foreach (var sg in FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None))
            total += sg.currentShield;
        return total;
    }

    /// <summary>
    /// Sahnedeki tüm aktif ShieldGeneratorComponent'lerin maxShield toplamı.
    /// </summary>
    public static float GetTotalMaxShield()
    {
        float total = 0f;
        foreach (var sg in FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None))
            total += sg.maxShield;
        return total;
    }

    /// <summary>
    /// Gelen hasarı sahnedeki tüm kalkan generatörlerine sırayla dağıtır.
    /// Kalkandan geçen toplam hasarı döner.
    /// </summary>
    public static float AbsorbDamageAll(float incomingDamage)
    {
        var generators = FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None);
        float remaining = incomingDamage;
        foreach (var sg in generators)
        {
            if (remaining <= 0f) break;
            remaining = sg.AbsorbDamage(remaining);
        }
        return remaining;
    }
}
