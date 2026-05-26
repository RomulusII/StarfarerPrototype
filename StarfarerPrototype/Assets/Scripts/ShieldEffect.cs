using UnityEngine;

/// <summary>
/// Kalkana mermi çarptığında merminin açısında hilal şeklinde parlar, 0.5sn'de fade out yapar.
/// Her çarpmada bağımsız bir instance oluşur — aynı anda birden fazla görünebilir.
///
/// Sprite: +X yönüne bakan 110° yaylı hilal.
///   - Dış kenar (kalkan yüzeyi) opak, iç kenar transparan.
///   - Yay kenarlarına doğru smooth fade.
/// Rotation: hit yönüne göre çevrilir (Atan2).
/// Scale:    ShieldRadius kadar, dış kenar = 1 birim @ scale=1.
/// </summary>
public class ShieldEffect : MonoBehaviour
{
    /// <summary>Kalkan küresinin dünya-uzayı yarıçapı — PlayerShip Awake ile senkron tutulmalı.</summary>
    public const float ShieldRadius = 2.5f;

    SpriteRenderer _sr;
    float          _timer;
    const float    FadeDuration = 0.5f;

    static Sprite _sprite;

    // ── Spawn ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Çarpma noktasında hilal efekti oluşturur.
    /// hitPos: merminin konumu (kalkan yüzeyinde).
    /// center: kalkan küresinin merkezi (genellikle PlayerShip.transform.position).
    /// </summary>
    public static void Spawn(Vector2 hitPos, Vector2 center)
    {
        var go = new GameObject("ShieldHit");
        go.transform.position = center;

        // Çarpma yönüne döndür — hilal dışa baksın
        float angle           = Mathf.Atan2(hitPos.y - center.y, hitPos.x - center.x) * Mathf.Rad2Deg;
        go.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
        go.transform.localScale = Vector3.one * ShieldRadius;

        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = GetSprite();
        sr.sortingOrder = 12;
        sr.color        = new Color(0.45f, 0.88f, 1f, 1f); // açık mavi

        var fx    = go.AddComponent<ShieldEffect>();
        fx._sr    = sr;
        fx._timer = FadeDuration;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        _timer -= Time.deltaTime;
        if (_timer <= 0f) { Destroy(gameObject); return; }

        var c     = _sr.color;
        _sr.color = new Color(c.r, c.g, c.b, _timer / FadeDuration);
    }

    // ── Sprite üretimi ────────────────────────────────────────────────────────

    static Sprite GetSprite()
    {
        if (_sprite != null) return _sprite;

        const int   Sz    = 256;
        const float OutR  = 118f;          // piksel — PPU olarak da kullanılır → dış kenar = 1 birim
        const float InR   = OutR * 0.60f;  // iç kenar, buradan içe doğru transparan
        const float Half  = 55f;           // yarı yay genişliği (derece) — toplam 110°
        const float Cx    = Sz * 0.5f;

        var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[Sz * Sz];

        for (int y = 0; y < Sz; y++)
        for (int x = 0; x < Sz; x++)
        {
            float dx = x - Cx, dy = y - Cx;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);

            if (r < InR || r > OutR) { px[y * Sz + x] = Color.clear; continue; }

            float deg = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg); // 0 = sağ (+X)
            if (deg > Half)          { px[y * Sz + x] = Color.clear; continue; }

            float radFade = (r - InR) / (OutR - InR); // 0 içte → 1 dışta
            float angFade = 1f - (deg / Half);         // 1 ortada → 0 kenarda
            angFade       = angFade * angFade;          // smooth

            px[y * Sz + x] = new Color(1f, 1f, 1f, radFade * angFade);
        }

        tex.SetPixels(px);
        tex.Apply();

        // PPU = OutR → dış kenar tam 1 birim @ scale 1
        _sprite = Sprite.Create(tex, new Rect(0, 0, Sz, Sz), new Vector2(0.5f, 0.5f), OutR);
        return _sprite;
    }
}
