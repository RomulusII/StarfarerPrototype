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

    /// <summary>
    /// Düşman kalkanları soluk turuncu — oyuncunun mavi kalkanından bir bakışta
    /// ayrılsın. Yarı saydam: arkasındaki gemi ve mermiler görünmeli, kalkan
    /// bir duvar değil bir yüzey olarak okunmalı.
    /// </summary>
    public static readonly Color ArcColor = new Color(1f, 0.62f, 0.28f, 0.42f);

    /// <summary>Çarpma hilalinin yarı genişliği (derece).</summary>
    const float FlashHalfAngle = 20f;

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
                              Mathf.Lerp(0.12f, ArcColor.a, ratio));
    }

    /// <summary>
    /// Çarpma parlaması. Ana geminin kalkanındaki hilalin aynısı, iki farkla:
    /// dar (20°) ve YAYIN SINIRLARI İÇİNE KIRPILIR. Kırpılmasaydı yayın ucuna
    /// yakın bir isabet, kalkan olmayan boşlukta parlardı.
    /// </summary>
    public void Flash(Vector2 worldHitPos)
    {
        if (_sr == null || !_sr.enabled) return;

        Vector2 center  = transform.position;
        float   facing  = transform.eulerAngles.z;
        float   hitDeg  = Mathf.Atan2(worldHitPos.y - center.y, worldHitPos.x - center.x) * Mathf.Rad2Deg;

        // Yayın içinde kalacak şekilde kırp
        float limit  = Mathf.Max(0f, _halfAngle - FlashHalfAngle);
        float offset = Mathf.Clamp(Mathf.DeltaAngle(facing, hitDeg), -limit, limit);
        float rad    = (facing + offset) * Mathf.Deg2Rad;

        Vector2 onArc = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _radius;
        ShieldEffect.Spawn(onArc, center, _radius, ArcColor, FlashHalfAngle);
    }

    // ── Prosedürel yay ────────────────────────────────────────────────────────

    static readonly Dictionary<int, Sprite> _cache = new();

    /// <summary>
    /// HİLAL — yeni ay. Dış kenar sabit yarıçapta (kalkan yüzeyi), iç kenar
    /// ortada içeri girip UÇLARDA dış kenarla BİRLEŞİR. Böylece yay ortada
    /// kalın, uçlarda sivridir.
    ///
    /// Eskiden sabit kalınlıkta bir şeritti: her iki ucu da küt bitiyor ve
    /// kalkandan çok bir boru parçası gibi duruyordu. Kalınlığın kosinüsle
    /// sönmesi, şekli tek bir satırla gerçek bir hilale çevirir.
    /// </summary>
    static Sprite ArcSprite(float halfAngle)
    {
        int key = Mathf.RoundToInt(halfAngle);
        if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

        const int   Sz        = 256;
        const float OutR      = 122f;       // piksel; ppu olarak da kullanılır
        const float MaxThick  = 26f;        // ortadaki en kalın yer (piksel)
        const float C         = Sz * 0.5f;

        var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false)
                  { filterMode = FilterMode.Bilinear };
        var px = new Color[Sz * Sz];

        for (int y = 0; y < Sz; y++)
        for (int x = 0; x < Sz; x++)
        {
            float dx = x - C, dy = y - C;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);
            if (r > OutR) { px[y * Sz + x] = Color.clear; continue; }

            float deg = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg); // 0 = +X
            if (deg > halfAngle) { px[y * Sz + x] = Color.clear; continue; }

            // Kalınlık ortada MaxThick, uçlarda 0 — hilalin sivri uçları
            float taper = Mathf.Cos(deg / halfAngle * Mathf.PI * 0.5f);
            float thick = MaxThick * taper;
            if (thick < 0.5f || r < OutR - thick) { px[y * Sz + x] = Color.clear; continue; }

            // Dış kenar parlak, iç kenara doğru sönümlenir
            float t = (r - (OutR - thick)) / thick;   // içte 0, dışta 1
            px[y * Sz + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.25f + 0.75f * t));
        }

        tex.SetPixels(px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, Sz, Sz), Vector2.one * 0.5f, OutR);
        _cache[key] = sprite;
        return sprite;
    }
}
