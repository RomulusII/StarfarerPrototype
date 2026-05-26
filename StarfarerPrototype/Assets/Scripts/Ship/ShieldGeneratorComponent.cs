using UnityEngine;

/// <summary>
/// Küresel kalkan üreten gemi komponenti.
///
/// Yeni davranış:
///   — Kalkan gecikme olmadan sürekli şarj olur.
///   — Tamamen boşaldığında currentShield = DepletionPenalty (-10) olarak ayarlanır;
///     ReactivationThreshold (+10) değerine ulaşana kadar hasar emme işlevsizdir.
///   — +10'a ulaşınca genişleme animasyonu oynar; animasyon bitince kalkan yeniden aktif olur.
/// </summary>
public class ShieldGeneratorComponent : ShipComponentBase
{
    public float maxShield          = 0f;
    public float currentShield;
    public float rechargeRate       = 1.5f;
    public float rechargeEnergyCost = 5f;

    const float DepletionPenalty      = -10f;
    const float ReactivationThreshold =  10f;

    bool _reactivating;

    /// <summary>Kalkana mermi emebilir durumda mı?</summary>
    public bool IsShieldActive => IsOperational && currentShield > 0f && !_reactivating;

    public bool IsShieldFull
    {
        get
        {
            float effectiveMax = maxShield * GetMultiplier("maxShield");
            return currentShield >= effectiveMax;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        componentName     = "Shield Generator";
        energyConsumption = 0f;
        currentShield     = maxShield;
    }

    void Update()
    {
        if (!IsOperational) return;
        if (BoostController.Mode == BoostMode.Weapon) return;
        if (IsShieldFull) return;

        float rechargeMulti = BoostController.Mode == BoostMode.Shield ? 3f : 1f;
        float energyMulti   = BoostController.Mode == BoostMode.Shield ? 5f : 1f;

        float effectiveRate = rechargeRate * rechargeMulti * GetMultiplier("rechargeRate");
        float effectiveMax  = maxShield    * GetMultiplier("maxShield");

        if (EnergyBus.Instance != null &&
            EnergyBus.Instance.RequestEnergy(rechargeEnergyCost * energyMulti * Time.deltaTime))
        {
            float prev    = currentShield;
            currentShield = Mathf.Min(currentShield + effectiveRate * Time.deltaTime, effectiveMax);

            // Yeniden aktivasyon eşiğini ilk kez geçtiyse animasyonu başlat
            if (!_reactivating && prev < ReactivationThreshold && currentShield >= ReactivationThreshold)
            {
                currentShield = ReactivationThreshold;
                TriggerReactivation();
            }
        }
    }

    void TriggerDepletion()
    {
        var ship   = GetComponentInParent<PlayerShip>();
        var center = ship != null ? (Vector2)ship.transform.position : (Vector2)transform.position;
        ShieldBubbleEffect.SpawnCollapse(center, ShieldEffect.ShieldRadius);
    }

    void TriggerReactivation()
    {
        _reactivating = true;
        var ship   = GetComponentInParent<PlayerShip>();
        var center = ship != null ? (Vector2)ship.transform.position : (Vector2)transform.position;
        ShieldBubbleEffect.SpawnExpand(center, ShieldEffect.ShieldRadius, () =>
        {
            if (this == null) return; // nesne yok edildiyse güvenli çıkış
            _reactivating = false;
        });
    }

    // ── Hasar emme ────────────────────────────────────────────────────────────

    /// <summary>Gelen hasarı emer; kalkandan geçen miktarı döner.</summary>
    public float AbsorbDamage(float incomingDamage)
    {
        if (!IsShieldActive) return incomingDamage;

        if (currentShield >= incomingDamage)
        {
            currentShield -= incomingDamage;
            return 0f;
        }

        float remaining = incomingDamage - currentShield;
        currentShield = DepletionPenalty;
        TriggerDepletion();
        return remaining;
    }

    /// <summary>Uyumluluk için korundu — yeni sistemde gecikme sayacı yok.</summary>
    public void NotifyDamageTaken() { }

    // ── Statik yardımcılar ────────────────────────────────────────────────────

    /// <summary>Sahnede hasar emebilir aktif kalkan var mı?</summary>
    public static bool AnyShieldActive()
    {
        foreach (var sg in FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None))
            if (sg.IsShieldActive) return true;
        return false;
    }

    /// <summary>Tüm kalkanların toplam currentShield değeri (0 veya üzeri olarak kırpılır).</summary>
    public static float GetTotalShield()
    {
        float total = 0f;
        foreach (var sg in FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None))
            total += Mathf.Max(0f, sg.currentShield);
        return total;
    }

    public static float GetTotalMaxShield()
    {
        float total = 0f;
        foreach (var sg in FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None))
            total += sg.maxShield;
        return total;
    }

    /// <summary>Gelen hasarı tüm aktif kalkan generatörlerine sırayla dağıtır.</summary>
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
