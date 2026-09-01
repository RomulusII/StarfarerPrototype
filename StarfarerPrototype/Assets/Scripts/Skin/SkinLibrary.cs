using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tüm görsel üretiminin TEK giriş noktası. Skin varsa gerçek sprite'ı,
/// yoksa bugünkü prosedürel dikdörtgeni döner — çağıran taraf farkı bilmez.
///
/// Neden tek nokta:
///   1. Skin'ler tek bir bool ile açılıp kapanır (SkinSet.enabled). Bu bir asset
///      alanıdır, derleme tetiklemez.
///   2. Görsel değişikliği oynanış koduna dokunmaz.
///   3. Dokular önbelleğe alınır. Eskiden her düşman/mermi doğuşunda yeni bir
///      Texture2D ayrılıyordu; artık aynı (boyut, renk, pivot) tek sprite paylaşır.
///      Renk animasyonu SpriteRenderer.color üzerinden yapıldığı için paylaşım güvenli.
/// </summary>
public static class SkinLibrary
{
    public static bool Enabled => SkinSet.Instance != null && SkinSet.Instance.enabled;

    public static bool OverlayOn => SkinSet.Instance != null && SkinSet.Instance.showHitboxOverlay;

    // ── Sprite alma ───────────────────────────────────────────────────────────

    /// <summary>
    /// Skin varsa gerçek sprite, yoksa <paramref name="w"/>×<paramref name="h"/>
    /// boyutunda düz renkli dikdörtgen. Fallback parametreleri skin kapalıyken
    /// bugünkü görünümü BİREBİR korur.
    /// </summary>
    public static Sprite Get(string id, int w, int h, Color c,
                             Vector2? pivot = null, float ppu = 100f)
    {
        if (TryGetEntry(id, out var entry)) return entry.sprite;
        return Rect(w, h, c, pivot, ppu);
    }

    /// <summary>
    /// Önce tipe özel anahtarı, sonra ortak anahtarı dener; ikisi de yoksa
    /// prosedürel dikdörtgene düşer. Örn. "enemy.swarm.barrel" -> "enemy.barrel".
    /// </summary>
    public static Sprite Get(string id, string fallbackId, int w, int h, Color c,
                             Vector2? pivot = null, float ppu = 100f)
    {
        if (TryGetEntry(id, out var entry))          return entry.sprite;
        if (TryGetEntry(fallbackId, out var shared)) return shared.sprite;
        return Rect(w, h, c, pivot, ppu);
    }

    /// <summary>Skin aranmadan doğrudan prosedürel dikdörtgen (önbellekli).</summary>
    public static Sprite Rect(int w, int h, Color c, Vector2? pivot = null, float ppu = 100f)
    {
        Vector2 p   = pivot ?? new Vector2(0.5f, 0.5f);
        var     key = new RectKey(w, h, c, p, ppu);

        if (_rectCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var tex = new Texture2D(w, h);
        var px  = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        tex.SetPixels(px);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), p, ppu);
        _rectCache[key] = sprite;
        return sprite;
    }

    /// <summary>
    /// Skin varken beyaz döner. Prosedürel dokular düz beyaz olup rengi
    /// SpriteRenderer'dan aldığı için, gerçek sprite'a aynı tint uygulanırsa
    /// görsel boyanır. Renk veren her çağrı bunu kullanmalı.
    /// </summary>
    public static Color Tint(string id, Color fallback)
    {
        if (!TryGetEntry(id, out _)) return fallback;
        return new Color(1f, 1f, 1f, fallback.a);
    }

    public static bool Has(string id) => TryGetEntry(id, out _);

    /// <summary>
    /// Skin sprite'ı varsa onu, yoksa null döner. Prosedürel fallback'i çağıranın
    /// kendisi ürettiği durumlar için (halka, daire gibi dikdörtgen olmayan sekiller).
    /// </summary>
    public static Sprite GetOrNull(string id) =>
        TryGetEntry(id, out var e) ? e.sprite : null;

    /// <summary>
    /// Paylaşılan bir sprite'ı istenen piksel boyutuna gerer. Kalkan kabuğu gibi
    /// tek sprite'ın farklı gemi boyutlarına uyması gereken yerler için.
    /// Prosedürel fallback zaten tam boyutta üretildiği için orada ölçek 1 kalır.
    /// </summary>
    public static void FitToSize(Transform t, Sprite sprite, int w, int h)
    {
        if (t == null || sprite == null) return;
        Vector2 size = sprite.bounds.size;
        if (size.x <= 0.0001f || size.y <= 0.0001f) return;
        t.localScale = new Vector3((w / 100f) / size.x, (h / 100f) / size.y, 1f);
    }

    static bool TryGetEntry(string id, out SkinEntry entry)
    {
        entry = null;
        if (!Enabled) return false;
        return SkinSet.Instance.TryGet(id, out entry);
    }

    // ── Collider türetme ──────────────────────────────────────────────────────

    /// <summary>
    /// Hitbox'ı sprite'tan türetir. Sprite şeklin tek kaynağıdır; hitbox onun
    /// <c>hitboxScale</c> ile daraltılmış halidir — bağımsız değil, TÜREVDİR.
    ///
    /// Skin yoksa hiçbir şey yapmaz ve <c>false</c> döner; çağıran taraf kendi
    /// fallback kutusunu kurar. Yani skin ve hitbox BİRLİKTE açılıp kapanır,
    /// yarı yolda kalmış bir durum oluşmaz.
    /// </summary>
    /// <param name="isTrigger">Kurulan collider trigger mı.</param>
    /// <returns>Collider skin'den türetildiyse true.</returns>
    public static bool TryApplyCollider(GameObject go, string id, bool isTrigger = false)
    {
        if (go == null)                    return false;
        if (!TryGetEntry(id, out var e))   return false;

        var sprite = e.sprite;
        float s    = Mathf.Max(0.01f, e.hitboxScale);

        bool wantPolygon = e.colliderMode switch
        {
            SkinColliderMode.Polygon => true,
            SkinColliderMode.Box     => false,
            _                        => sprite.GetPhysicsShapeCount() > 0,
        };

        // Fallback kutusu Awake'de eklenmiş olabilir — çift trigger olmasın diye
        // yok etmek yerine kapatıyoruz (Destroy bir frame gecikir, o frame'de
        // iki collider birden vurulurdu).
        var existingBox = go.GetComponent<BoxCollider2D>();

        if (wantPolygon && sprite.GetPhysicsShapeCount() > 0)
        {
            var pts = new List<Vector2>();
            sprite.GetPhysicsShape(0, pts);
            for (int i = 0; i < pts.Count; i++) pts[i] *= s;

            if (existingBox != null) existingBox.enabled = false;

            var poly = go.GetComponent<PolygonCollider2D>();
            if (poly == null) poly = go.AddComponent<PolygonCollider2D>();
            poly.pathCount = 1;
            poly.SetPath(0, pts.ToArray());
            poly.isTrigger = isTrigger;
            return true;
        }

        // Kutu modu
        var box = existingBox != null ? existingBox : go.AddComponent<BoxCollider2D>();
        box.enabled   = true;
        box.isTrigger = isTrigger;

        if (e.hitboxRect.width > 0f && e.hitboxRect.height > 0f)
        {
            // Ölçülmüş dikdörtgen: kütlenin gerçekten bulunduğu bölge.
            // Sivri burunlu gemilerde sınırlayıcı kutunun yarısı boş kalır;
            // oraya konan hitbox mermiyi boşlukta yakalar.
            float ppu = sprite.pixelsPerUnit;
            box.size   = new Vector2(e.hitboxRect.width, e.hitboxRect.height) / ppu * s;
            box.offset = (e.hitboxRect.center - sprite.pivot) / ppu;
        }
        else
        {
            box.size   = (Vector2)sprite.bounds.size * s;
            box.offset = (Vector2)sprite.bounds.center;
        }
        return true;
    }

    // ── Doku önbelleği ────────────────────────────────────────────────────────

    static readonly Dictionary<RectKey, Sprite> _rectCache = new Dictionary<RectKey, Sprite>();

    readonly struct RectKey : System.IEquatable<RectKey>
    {
        readonly int     _w, _h;
        readonly Color   _c;
        readonly Vector2 _pivot;
        readonly float   _ppu;

        public RectKey(int w, int h, Color c, Vector2 pivot, float ppu)
        { _w = w; _h = h; _c = c; _pivot = pivot; _ppu = ppu; }

        public bool Equals(RectKey o) =>
            _w == o._w && _h == o._h && _c == o._c && _pivot == o._pivot && _ppu == o._ppu;

        public override bool Equals(object o) => o is RectKey k && Equals(k);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _w * 397 ^ _h;
                hash = hash * 397 ^ _c.GetHashCode();
                hash = hash * 397 ^ _pivot.GetHashCode();
                hash = hash * 397 ^ _ppu.GetHashCode();
                return hash;
            }
        }
    }
}
