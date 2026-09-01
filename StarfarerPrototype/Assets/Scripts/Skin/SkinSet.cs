using System.Collections.Generic;
using UnityEngine;

/// <summary>Hitbox'ın sprite'tan nasıl türetileceği.</summary>
public enum SkinColliderMode
{
    /// <summary>Sprite'ın physics shape'i varsa poligon, yoksa kutu.</summary>
    Auto,
    /// <summary>Her zaman kutu — sprite'ın sınırları × hitboxScale.</summary>
    Box,
    /// <summary>Her zaman poligon — sprite'ın physics shape'i × hitboxScale.</summary>
    Polygon,
}

/// <summary>
/// Tek bir görselin tanımı. Sprite şeklin TEK kaynağıdır; hitbox ondan türer
/// ve <see cref="hitboxScale"/> ile kasten daraltılır.
/// </summary>
[System.Serializable]
public class SkinEntry
{
    [Tooltip("SkinId sabitlerinden biri. Düşmanlar için \"enemy.<tip adı>\" (küçük harf).")]
    public string id;

    public Sprite sprite;

    [Tooltip("Auto: sprite'ın physics shape'i varsa poligon, yoksa kutu. Poligon " +
             "collider kutudan pahalıdır — basit siluetlerde Box tercih edilmeli.")]
    public SkinColliderMode colliderMode = SkinColliderMode.Auto;

    [Tooltip("Hitbox dikdörtgeni, KAYNAK GÖRSELİN PİKSELİ cinsinden (sol-alt orijin). " +
             "Genişlik veya yükseklik 0 ise sprite'ın tüm sınırları kullanılır. " +
             "Sivri burunlu gemilerde siluet, sınırlayıcı kutunun ancak yarısını " +
             "doldurur — kalan boşluğa atılan mermi hiçbir şeye çarpmaz. Bu alan " +
             "hitbox'ı kütlenin gerçekten bulunduğu bölgeye oturtur. Değerleri " +
             "Tools/SkinGen üreteci ölçüp yazdırır.")]
    public Rect hitboxRect;

    [Tooltip("Son daraltma kolu — hitboxRect verilmişse ONUN üzerine, verilmemişse " +
             "sprite'ın sınırları üzerine uygulanır. Dekoratif taşmaları (itki alevi, " +
             "anten, glow) hitbox dışında bırakmak için. Oyuncunun VURDUĞU hedeflerde " +
             "dikkatli kullan — fazla daraltma \"vurdum ama saymadı\" hissi yaratır. " +
             "hitboxRect zaten ölçülmüşse 1.0 bırakılabilir.")]
    [Range(0.4f, 1.2f)] public float hitboxScale = 0.85f;
}

/// <summary>
/// Tüm skin'lerin tek sahibi. <c>Assets/Resources/SkinSet.asset</c> olarak
/// aranır; asset yoksa veya <see cref="enabled"/> kapalıysa oyun bugünkü
/// prosedürel dikdörtgen görünümüne döner (bkz. <see cref="SkinLibrary"/>).
///
/// BalanceConfig ile aynı kalıp: asset opsiyonel, kod her durumda çalışır.
/// </summary>
[CreateAssetMenu(fileName = "SkinSet", menuName = "Starfarer/Skin Set")]
public class SkinSet : ScriptableObject
{
    [Header("Ana Anahtar")]
    [Tooltip("Kapatınca TÜM skin'ler devre dışı kalır ve oyun prosedürel " +
             "dikdörtgenlere döner. Bu bir asset alanıdır — değiştirmek derleme " +
             "TETİKLEMEZ, Play sırasında bile kapatılabilir.")]
    public bool enabled = true;

    [Tooltip("Collider sınırlarını sprite'ın üstüne çizer. Skin ile hitbox " +
             "örtüşmesini gözle doğrulamanın tek pratik yolu.")]
    public bool showHitboxOverlay = false;

    [Header("Görseller")]
    public List<SkinEntry> entries = new List<SkinEntry>();

    Dictionary<string, SkinEntry> _lookup;

    // ── Singleton ─────────────────────────────────────────────────────────────

    static SkinSet _instance;
    static bool    _loadAttempted;

    public static SkinSet Instance
    {
        get
        {
            if (_instance != null) return _instance;
            if (_loadAttempted)    return null;

            _loadAttempted = true;
            _instance      = Resources.Load<SkinSet>("SkinSet");
            return _instance;   // null olabilir — SkinLibrary bunu fallback sayar
        }
    }

    // ── Sorgu ─────────────────────────────────────────────────────────────────

    public bool TryGet(string id, out SkinEntry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(id)) return false;

        if (_lookup == null) BuildLookup();
        return _lookup.TryGetValue(id, out entry) && entry.sprite != null;
    }

    void BuildLookup()
    {
        _lookup = new Dictionary<string, SkinEntry>();
        if (entries == null) return;

        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.id)) continue;
            _lookup[e.id] = e;   // aynı id iki kez yazıldıysa sonuncu kazanır
        }
    }

    /// <summary>Editörde entries listesi değiştiğinde önbelleği düşür.</summary>
    void OnValidate() => _lookup = null;
}
