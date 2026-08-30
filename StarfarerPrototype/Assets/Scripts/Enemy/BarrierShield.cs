using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Yön duyarlı yay kalkanı — Bariyer tipinin tek silahı.
///
/// Küresel kalkandan farkı YÖNLÜ olmasıdır ve bu fark oyunun içine geometriyle
/// girer, ekstra bir parametreyle değil: kalkan geminin ÖNÜNDE ayrı bir
/// collider'dır. Önden gelen mermi ona çarpar, yandan veya arkadan gelen mermi
/// onu ıskalayıp gövde collider'ına ulaşır. Yani "kalkanı kandırmak" bir
/// tasarım kuralı değil, sahnedeki şeklin doğal sonucudur.
///
/// Collider dilim (pasta) şeklindedir, ince bir şerit değil: hızlı mermi ince
/// bir şeridi bir karede atlayıp içeri girebilir. Dilim, sektörün TAMAMINI
/// kapladığı için tünelleme olmaz.
///
/// Kalkan boşalınca collider kapanır ve gövde açığa çıkar; dolunca geri gelir.
/// </summary>
public class BarrierShield : MonoBehaviour
{
    /// <summary>Kalkanı taşıyan gemi. Hasar buraya yönlendirilir.</summary>
    public EnemyBot owner;

    float _radius;
    float _halfAngle;

    SpriteRenderer    _sr;
    PolygonCollider2D _col;

    static readonly Color ArcColor = new Color(0.35f, 0.8f, 1f, 0.75f);

    /// <param name="radius">Yayın dış yarıçapı (dünya birimi).</param>
    /// <param name="arcDegrees">Yayın toplam açısı.</param>
    public static BarrierShield Attach(EnemyBot bot, float radius, float arcDegrees)
    {
        var go = new GameObject("BarrierShield");
        go.transform.SetParent(bot.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var b = go.AddComponent<BarrierShield>();
        b.owner      = bot;
        b._radius    = radius;
        b._halfAngle = Mathf.Clamp(arcDegrees, 20f, 340f) * 0.5f;
        b.Build();
        return b;
    }

    void Build()
    {
        _sr = gameObject.AddComponent<SpriteRenderer>();
        // Skin varsa o kullanılır; yoksa prosedürel yay. SkinLibrary deseni.
        _sr.sprite       = SkinLibrary.GetOrNull(SkinId.EnemyBarrierArc) ?? ArcSprite(_halfAngle);
        _sr.color        = ArcColor;
        _sr.sortingOrder = 9;
        // Sprite dış kenarı 1 birim (ppu = dış yarıçap) → ölçek doğrudan yarıçap
        transform.localScale = Vector3.one * _radius;

        _col           = gameObject.AddComponent<PolygonCollider2D>();
        _col.isTrigger = true;
        _col.SetPath(0, WedgePath(_halfAngle));
    }

    /// <summary>
    /// Dilim collider'ın noktaları. LOCAL uzayda, yarıçap 1 — GameObject'in
    /// ölçeği zaten gerçek yarıçapı veriyor.
    /// </summary>
    static Vector2[] WedgePath(float halfAngle)
    {
        const int seg = 14;
        var pts = new List<Vector2>(seg + 2) { Vector2.zero };
        for (int i = 0; i <= seg; i++)
        {
            float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)seg) * Mathf.Deg2Rad;
            pts.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
        }
        return pts.ToArray();
    }

    /// <summary>Kalkan durumuna göre görünürlük ve collider. Oran 0 = kapalı.</summary>
    public void Refresh(float ratio)
    {
        bool up = ratio > 0f;
        if (_col != null) _col.enabled = up;
        if (_sr  == null) return;

        _sr.enabled = up;
        // Zayıflayan kalkan solar — oyuncu ne zaman kırılacağını görebilmeli
        _sr.color = new Color(ArcColor.r, ArcColor.g, ArcColor.b,
                              Mathf.Lerp(0.18f, ArcColor.a, ratio));
    }

    // ── Prosedürel yay ────────────────────────────────────────────────────────

    static readonly Dictionary<int, Sprite> _cache = new();

    /// <summary>
    /// İçi boş yay: dış kenar opak, içe doğru sönümlenir; uçlara doğru da solar.
    /// ShieldEffect'in çarpma hilaliyle aynı fikir, farklı ölçüde — o anlık bir
    /// parlama, bu duran bir yapı.
    /// </summary>
    static Sprite ArcSprite(float halfAngle)
    {
        int key = Mathf.RoundToInt(halfAngle);
        if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int   Sz   = 256;
        const float OutR = 120f;           // piksel; ppu olarak da kullanılır
        const float InR  = OutR * 0.78f;   // yayın kalınlığı
        const float C    = Sz * 0.5f;

        var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false)
                  { filterMode = FilterMode.Bilinear };
        var px = new Color[Sz * Sz];

        for (int y = 0; y < Sz; y++)
        for (int x = 0; x < Sz; x++)
        {
            float dx = x - C, dy = y - C;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);
            if (r < InR || r > OutR) { px[y * Sz + x] = Color.clear; continue; }

            float deg = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg); // 0 = +X
            if (deg > halfAngle) { px[y * Sz + x] = Color.clear; continue; }

            float radial = (r - InR) / (OutR - InR);          // içte 0, dışta 1
            float ang    = 1f - deg / halfAngle;              // ortada 1, uçta 0
            px[y * Sz + x] = new Color(1f, 1f, 1f,
                Mathf.Max(radial * 0.55f + 0.45f, 0f) * Mathf.Sqrt(ang));
        }

        tex.SetPixels(px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, Sz, Sz), Vector2.one * 0.5f, OutR);
        _cache[key] = sprite;
        return sprite;
    }
}
