using UnityEngine;

/// <summary>
/// Düşman gemisi veya asteroit parçalandığında geride kalan enkaz.
/// CollectorShip tarafından toplanır.
///
/// Kurallar:
///   - Kısa bir sürüklenmeden sonra durur (Drag) — sahadan kaçıp gitmez,
///     toplayıcının yetişebileceği yerde kalır.
///   - Kaynak tipine göre renklenir: ham madde kahverengi, kristal mavimsi gri.
///   - Ömrü dolmadan toplanmazsa kaybolur. Son saniyelerde önce solar,
///     son 10 saniyede yanıp söner — oyuncu kaçırdığını görebilsin.
/// </summary>
public class Debris : MonoBehaviour
{
    public ResourceType resourceType   = ResourceType.RawMaterial;
    public float        resourceAmount = 10f;

    Vector2        _velocity;
    float          _life;
    SpriteRenderer _sr;

    const float LifeTime   = 180f;  // toplanmazsa kaybolma süresi
    const float FadeStart  = 60f;   // kalan bu sürenin altında solmaya başlar
    const float BlinkStart = 10f;   // kalan bu sürenin altında yanıp söner
    const float MinAlpha   = 0.3f;  // solmanın indiği taban
    const float BlinkRate  = 4f;    // saniyedeki yanıp sönme sayısı
    const float Drag       = 0.9f;  // sürüklenmenin sönümlenme hızı (birim/sn²)

    static readonly Color MetalColor   = new Color(0.55f, 0.45f, 0.30f);
    static readonly Color CrystalColor = new Color(0.52f, 0.70f, 0.85f);

    public bool    IsEmpty  => resourceAmount <= 0f;
    public Vector2 Velocity => _velocity;

    public void Init(Vector2 velocity, float amount, ResourceType type = ResourceType.RawMaterial)
    {
        _velocity      = velocity;
        resourceAmount = amount;
        resourceType   = type;
        ApplyTint(1f);
    }

    void Awake()
    {
        _life = LifeTime;
        BuildVisual();
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        // Sürüklenme sönümlenir — enkaz biraz savrulur, sonra durur
        if (_velocity.sqrMagnitude > 0.000001f)
        {
            _velocity = Vector2.MoveTowards(_velocity, Vector2.zero, Drag * Time.deltaTime);
            transform.position += (Vector3)(_velocity * Time.deltaTime);
        }

        _life -= Time.deltaTime;
        if (_life <= 0f) { Destroy(gameObject); return; }

        ApplyTint(AlphaForRemaining(_life));
    }

    /// <summary>Kalan süreye göre görünürlük: önce solar, sonunda yanıp söner.</summary>
    static float AlphaForRemaining(float remaining)
    {
        if (remaining > FadeStart) return 1f;

        if (remaining > BlinkStart)
        {
            float t = Mathf.InverseLerp(BlinkStart, FadeStart, remaining);
            return Mathf.Lerp(MinAlpha, 1f, t);
        }

        // Son saniyeler: MinAlpha etrafında yanıp sönme
        float pulse = (Mathf.Sin(Time.time * BlinkRate * Mathf.PI * 2f) + 1f) * 0.5f;
        return Mathf.Lerp(MinAlpha * 0.4f, MinAlpha * 2f, pulse);
    }

    void ApplyTint(float alpha)
    {
        if (_sr == null) return;
        Color c = resourceType == ResourceType.EnergyCrystal ? CrystalColor : MetalColor;
        c.a     = alpha;
        _sr.color = c;
    }

    /// <summary>İstenen miktarı tüketir; gerçekte tüketilen miktarı döner.</summary>
    public float Collect(float amount)
    {
        float actual = Mathf.Min(amount, resourceAmount);
        resourceAmount -= actual;
        if (resourceAmount <= 0f)
            Destroy(gameObject);
        return actual;
    }

    void BuildVisual()
    {
        // Doku beyaz; renk sr.color ile verilir — tip rengi ve solma tek yerden yönetilir
        var tex = new Texture2D(12, 10);
        var px  = new Color[12 * 10];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite       = Sprite.Create(tex, new Rect(0, 0, 12, 10), new Vector2(0.5f, 0.5f), 100f);
        _sr.sortingOrder = -1;
        ApplyTint(1f);
    }
}
