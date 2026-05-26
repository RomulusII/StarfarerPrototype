using UnityEngine;

/// <summary>
/// Küresel kalkan üreten gemi komponenti.
///
/// Davranış:
///   — Gecikme olmadan sürekli şarj olur.
///   — Tamamen boşaldığında currentShield = -10; +10'a ulaşınca genişleme animasyonu
///     oynar ve bittikten sonra kalkan aktif olur.
///   — Generator SATILDIĞINDA veya yok edildiğinde mevcut kalkan HP'si
///     s_orphanShield'e kopyalanır; kalkan bu değerle hasar emmeye devam eder.
///   — Yeni generator kurulunca orphan HP varsa oradan başlar, yoksa maxShield'den.
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
    bool _depleted;     // true yalnızca DepletionPenalty'ye düşüldüğünde; animasyon bitince temizlenir

    // ── Orphan state: generator yokken kalan kalkan HP'si ─────────────────────
    static float s_orphanShield    = 0f;
    static float s_orphanMaxShield = 0f; // display için son bilinen max

    /// <summary>Kurulum sırasında çağrılır; orphan HP varsa onu tüketir ve başlangıç değeri döner.</summary>
    public static float TakeOrphanShield(float installedMax)
    {
        if (s_orphanShield <= 0f)
        {
            // Orphan yok: tam dolu başla
            return installedMax;
        }
        float hp = Mathf.Min(s_orphanShield, installedMax);
        s_orphanShield    = 0f;
        s_orphanMaxShield = 0f;
        return hp;
    }

    // ── Operasyonel durum ─────────────────────────────────────────────────────

    /// <summary>
    /// Hasar emebilir mi?
    /// IsOperational kasıtlı olarak kontrol edilmez:
    /// generator satılıp deaktif olsa bile kalan kalkan HP'si koruma sağlamaya devam etmeli.
    /// Şarj etme Update()'te IsOperational kontrolüyle zaten durur.
    /// </summary>
    // _depleted = true iken kalkan -10'dan +10'a şarj oluyor ama henüz aktif değil
    public bool IsShieldActive => !_depleted && currentShield > 0f && !_reactivating;

    public bool IsShieldFull
    {
        get
        {
            float effectiveMax = maxShield * GetMultiplier("maxShield");
            return currentShield >= effectiveMax;
        }
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        componentName     = "Shield Generator";
        energyConsumption = 0f;
        // currentShield ShipLoadout.InstallComponent tarafından Awake'den SONRA set edilir
    }

    void OnDestroy()
    {
        // Kalkan HP'sini orphan state'e kaydet — generator yokken de koruma sürsün
        if (currentShield > 0f)
        {
            s_orphanShield    = Mathf.Max(s_orphanShield, currentShield); // en yüksek aktif değeri sakla
            s_orphanMaxShield = maxShield;
        }
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

            // Yalnızca deplete sonrası +10'a ilk kez ulaşıldığında animasyon başlat
            if (_depleted && !_reactivating && prev < ReactivationThreshold && currentShield >= ReactivationThreshold)
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
            if (this == null) return;
            _reactivating = false;
            _depleted     = false; // deplete döngüsü tamamlandı
        });
    }

    // ── Hasar emme ────────────────────────────────────────────────────────────

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
        _depleted     = true;
        TriggerDepletion();
        return remaining;
    }

    /// <summary>Uyumluluk için korundu — gecikme sayacı kaldırıldı.</summary>
    public void NotifyDamageTaken() { }

    // ── Statik yardımcılar ────────────────────────────────────────────────────

    /// <summary>Hasar emebilir aktif kalkan var mı? (generator veya orphan)</summary>
    public static bool AnyShieldActive()
    {
        if (s_orphanShield > 0f) return true;
        foreach (var sg in FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None))
            if (sg.IsShieldActive) return true;
        return false;
    }

    /// <summary>Toplam aktif kalkan HP'si (orphan dahil, negatif değerler hariç).</summary>
    public static float GetTotalShield()
    {
        float total = s_orphanShield;
        foreach (var sg in FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None))
            total += Mathf.Max(0f, sg.currentShield);
        return total;
    }

    /// <summary>Toplam max kalkan — generator yoksa orphan max kullanılır.</summary>
    public static float GetTotalMaxShield()
    {
        var gens = FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None);
        if (gens.Length > 0)
        {
            float total = 0f;
            foreach (var sg in gens) total += sg.maxShield;
            return total;
        }
        return s_orphanMaxShield; // generator yokken son bilinen max
    }

    /// <summary>Gelen hasarı tüm kalkan kaynaklarına (aktif generatorlar + orphan) dağıtır.</summary>
    public static float AbsorbDamageAll(float incomingDamage)
    {
        var generators = FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None);
        float remaining = incomingDamage;

        foreach (var sg in generators)
        {
            if (remaining <= 0f) break;
            remaining = sg.AbsorbDamage(remaining);
        }

        // Generator yoksa veya yetersizse orphan kalkanı kullan
        if (remaining > 0f && s_orphanShield > 0f)
        {
            if (s_orphanShield >= remaining)
            {
                s_orphanShield -= remaining;
                remaining       = 0f;
            }
            else
            {
                remaining      -= s_orphanShield;
                s_orphanShield  = 0f;
                s_orphanMaxShield = 0f;
                // Generator yokken orphan tükendiyse çöküş animasyonu
                var ship = FindFirstObjectByType<PlayerShip>();
                if (ship != null)
                    ShieldBubbleEffect.SpawnCollapse(ship.transform.position, ShieldEffect.ShieldRadius);
            }
        }

        return remaining;
    }
}
