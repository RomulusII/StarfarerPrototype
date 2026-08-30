using UnityEngine;

/// <summary>
/// Kaynak deposu. Kurulduğu sürece envanterin tavanını yükseltir.
/// "Kapasite" statı daha kompakt depolamadır — aynı slotta çok daha fazla yer.
///
/// Bu stat opsiyonel değil, ZORUNLU bir eksendir: geç seviyelerde tek bir stat
/// yükseltmesi binlerce kaynak tutuyor ve taban tavan (150 metal / 50 kristal)
/// o parayı tutamaz. Tier zincirleri kaldırılınca kapasitenin tek büyüme yolu
/// bu oldu.
///
/// Yok edilirse (veya Easy modda deaktive olursa) kapasite düşer; envanterdeki
/// fazlalık yeni tavana kırpılır, yani hasar almak biriktirdiğin kaynağı da yakar.
/// </summary>
public class StorageComponent : ShipComponentBase
{
    public float metalCapacity   = 250f;
    public float crystalCapacity =  50f;

    /// <summary>Stat anahtarı — UpgradeUI ve kayıt aynı adı kullanır.</summary>
    public const string CapacityKey = "capacity";

    public float EffMetalCapacity   => metalCapacity   * GetMultiplier(CapacityKey);
    public float EffCrystalCapacity => crystalCapacity * GetMultiplier(CapacityKey);

    // Kapasite toplamı sık okunuyor (her kaynak eklemede + HUD'da her kare).
    // FindObjectsByType'ı her seferinde çalıştırmamak için kısa ömürlü önbellek.
    static float _cachedMetal, _cachedCrystal, _cacheTime = -1f;
    const float CacheDuration = 0.25f;

    protected override void Awake()
    {
        base.Awake();
        componentName = "Depo";
        maxHP         = 14f;   // gövde deposu — jeneratörden dayanıklı, turretten kırılgan
        currentHP     = maxHP;
        Invalidate();
    }

    void OnDestroy() => Invalidate();

    public void Init(float metal, float crystal)
    {
        metalCapacity   = metal;
        crystalCapacity = crystal;
        Invalidate();
    }

    /// <summary>Önbelleği düşürür — kurulum, satış ve yıkımda çağrılır.</summary>
    public static void Invalidate() => _cacheTime = -1f;

    /// <summary>Kapasite yükseltmesi önbelleği geçersiz kılar — aksi halde yeni
    /// tavan çeyrek saniye boyunca görünmez ve oyuncu "işe yaramadı" sanır.</summary>
    public override void OnStatUpgraded(string key)
    {
        if (key == CapacityKey) Invalidate();
    }

    public static float TotalMetalCapacity   { get { Refresh(); return _cachedMetal; } }
    public static float TotalCrystalCapacity { get { Refresh(); return _cachedCrystal; } }

    static void Refresh()
    {
        if (_cacheTime >= 0f && Time.unscaledTime - _cacheTime < CacheDuration) return;

        _cachedMetal   = 0f;
        _cachedCrystal = 0f;

        foreach (var s in FindObjectsByType<StorageComponent>(FindObjectsSortMode.None))
        {
            if (!s.IsOperational) continue;   // yıkık/deaktif depo kapasite vermez
            _cachedMetal   += s.EffMetalCapacity;
            _cachedCrystal += s.EffCrystalCapacity;
        }

        _cacheTime = Time.unscaledTime;
    }
}
