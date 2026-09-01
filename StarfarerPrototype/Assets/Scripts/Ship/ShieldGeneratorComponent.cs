using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Küresel kalkan üreten gemi komponenti.
///
/// Davranış:
///   — Gecikme olmadan sürekli şarj olur.
///   — Tamamen boşaldığında currentShield = -10; +10'a ulaşınca genişleme animasyonu
///     oynar ve bittikten sonra kalkan aktif olur.
///   — SON generator satıldığında veya yok edildiğinde mevcut kalkan HP'si
///     s_orphanShield'e taşınır; yansıtılmış kabuk bu değerle hasar emmeye
///     devam eder. Yeni generator kurulunca oradan başlar.
///
/// YETİM HAVUZUN DEĞİŞMEZ KURALI: yalnızca HİÇ generator yokken var olabilir.
/// Bu kural çiğnendiğinde ortaya çıkan hata büyüktü ve üç ayrı belirti veriyordu:
///
///   1. HUD kalkan barı hiç azalmıyor (upgrade ekranı ise azaldığını gösteriyor).
///      GetTotalShield() yetim havuzu canlı generator'ın üstüne EKLİYOR, oran
///      1'i aşıp kırpılıyordu. Oysa AbsorbDamageAll önce generator'ı boşaltıyor,
///      yetim havuza hiç dokunmuyordu.
///   2. Kalkan bittikten, çöküş animasyonu oynadıktan SONRA bar azalmaya
///      başlıyor. Çünkü asıl kalkan biteli çoktan olmuş; barın gösterdiği
///      gizli yetim havuz ancak o noktadan sonra tüketilmeye başlıyor.
///   3. İkinci bir çöküş animasyonu. Yetim havuz da bitince AbsorbDamageAll
///      kendi çöküşünü oynatıyor.
///
/// İki kaynak vardı: (a) statik alan sahne yeniden yüklendiğinde (ölüm →
/// restart) SIFIRLANMIYORDU, yani yeni oyun önceki oyunun kalkanıyla
/// başlıyordu; (b) iki kalkan jeneratöründen biri yok edilince, diğeri
/// hayattayken yetim havuz açılıyordu.
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

    /// <summary>
    /// Sahnedeki canlı jeneratörler. Yetim havuzun değişmez kuralını (yalnız
    /// generator yokken var olur) uygulayabilmek için gerekiyor: OnDestroy
    /// sırasında FindObjectsByType, ölmekte olan nesneyi sayıp saymayacağı
    /// belirsiz olduğu için güvenilmez.
    /// </summary>
    static readonly List<ShieldGeneratorComponent> s_active = new();

    static bool HasGenerator => s_active.Count > 0;

    /// <summary>
    /// Statik durumu sıfırlar. Sahne yeniden yüklendiğinde (ölüm → restart)
    /// static alanlar HAYATTA KALIR — bu çağrı olmadan yeni oyun, önceki oyunun
    /// kalkan artığıyla başlıyor ve HUD barı ilk andan itibaren yalan söylüyordu.
    /// ShipLoadout.Awake çağırır.
    /// </summary>
    public static void ResetStatics()
    {
        s_active.Clear();
        s_orphanShield    = 0f;
        s_orphanMaxShield = 0f;
    }

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

    /// <summary>Stat yükseltmeleri uygulanmış kalkan tavanı — tek doğru kaynak.</summary>
    public float EffectiveMaxShield => maxShield * GetMultiplier("maxShield");

    public bool IsShieldFull => currentShield >= EffectiveMaxShield;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        componentName     = "Shield Generator";
        // currentShield ShipLoadout.InstallComponent tarafından Awake'den SONRA set edilir
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!s_active.Contains(this)) s_active.Add(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        s_active.Remove(this);
    }

    void OnDestroy()
    {
        s_active.Remove(this);   // OnDisable zaten çıkarmış olmalı; güvence

        // Başka generator hayattaysa yetim havuz AÇILMAZ. Açılsaydı canlı
        // jeneratörün üstüne binen ikinci, gizli bir havuz oluşurdu: bar
        // toplamı gösterir, hasar ise yalnızca jeneratörden düşerdi.
        if (HasGenerator) return;
        if (currentShield <= 0f) return;

        s_orphanShield    = currentShield;
        s_orphanMaxShield = EffectiveMaxShield;
    }

    void Update()
    {
        if (!IsOperational) return;
        if (BoostController.Mode == BoostMode.Weapon) return;
        if (IsShieldFull) return;

        float rechargeMulti = BoostController.Mode == BoostMode.Shield ? 3f : 1f;
        float energyMulti   = BoostController.Mode == BoostMode.Shield ? 5f : 1f;

        float effectiveRate = rechargeRate * rechargeMulti * GetMultiplier("rechargeRate");
        float effectiveMax  = EffectiveMaxShield;

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
        if (!HasGenerator) return s_orphanShield > 0f;
        foreach (var sg in s_active)
            if (sg != null && sg.IsShieldActive) return true;
        return false;
    }

    /// <summary>
    /// Toplam aktif kalkan HP'si. Yetim havuz yalnızca generator YOKKEN sayılır —
    /// ikisini toplamak, hasarın yalnızca birinden düştüğü gizli bir havuz
    /// yaratıyordu ve bar donuyordu.
    /// </summary>
    public static float GetTotalShield()
    {
        if (!HasGenerator) return Mathf.Max(0f, s_orphanShield);

        float total = 0f;
        foreach (var sg in s_active)
            if (sg != null) total += Mathf.Max(0f, sg.currentShield);
        return total;
    }

    /// <summary>
    /// Toplam max kalkan — generator yoksa orphan max kullanılır.
    ///
    /// Stat çarpanı BURADA da uygulanmak zorundadır. Uygulanmadığı sürece gemi
    /// üstündeki kalkan barı yalan söylüyordu: currentShield çarpanlı tavana
    /// (ör. 152) kadar doluyor, bar ise onu çarpansız tabana (50) bölüp oranı
    /// 1'e kırpıyordu. Sonuç: "Max Kalkan" yükseltmesi almış bir oyuncuda bar,
    /// kalkan TABANIN altına düşene kadar tam görünüyor — düşman ateş ediyor
    /// ama kalkan hiç azalmıyormuş gibi. Kalkan aslında hasarı emiyordu.
    /// </summary>
    public static float GetTotalMaxShield()
    {
        if (!HasGenerator) return s_orphanMaxShield; // generator yokken son bilinen max

        float total = 0f;
        foreach (var sg in s_active)
            if (sg != null) total += sg.EffectiveMaxShield;
        return total;
    }

    /// <summary>Gelen hasarı tüm kalkan kaynaklarına (aktif generatorlar + orphan) dağıtır.</summary>
    public static float AbsorbDamageAll(float incomingDamage)
    {
        float remaining = incomingDamage;

        foreach (var sg in s_active)
        {
            if (remaining <= 0f) break;
            if (sg != null) remaining = sg.AbsorbDamage(remaining);
        }

        // Yetim kabuk yalnızca hiç generator yokken devrededir (bkz. sınıf notu).
        if (remaining > 0f && !HasGenerator && s_orphanShield > 0f)
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
