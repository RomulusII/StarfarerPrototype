using UnityEngine;

/// <summary>
/// Kaynak deposu. Kurulduğu sürece envanterin tavanını yükseltir.
/// Upgrade'ler "daha kompakt depolama" — aynı slotta çok daha fazla kapasite.
///
/// Yok edilirse (veya Easy modda deaktive olursa) kapasite düşer; envanterdeki
/// fazlalık yeni tavana kırpılır, yani hasar almak biriktirdiğin kaynağı da yakar.
/// </summary>
public class StorageComponent : ShipComponentBase
{
    public float metalCapacity   = 250f;
    public float crystalCapacity =  50f;

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
            _cachedMetal   += s.metalCapacity;
            _cachedCrystal += s.crystalCapacity;
        }

        _cacheTime = Time.unscaledTime;
    }
}
