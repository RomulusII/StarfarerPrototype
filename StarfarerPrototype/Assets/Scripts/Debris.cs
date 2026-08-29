using UnityEngine;

/// <summary>
/// Düşman gemisi veya asteroit parçalandığında geride kalan enkaz.
/// CollectorShip tarafından toplanır.
///
/// Kurallar:
///   - Hız iki bileşenlidir: saçılma (sönümlenir) + sabit sola sürüklenme (kalıcı).
///     Patlama itmesi hızla söner ama enkaz durmaz — yavaşça sola kayar ve
///     vaktinde toplanmazsa sahneden çıkar. Tamamen dursaydı ekranın sağında
///     kalan enkaz toplayıcının menzili dışında sonsuza dek asılı kalırdı.
///   - Kaynak tipine göre renklenir: ham madde kahverengi, kristal parlak camgöbeği.
///     Kristal daha nadir düştüğü için daha da iri çizilir.
///   - İki şekilde kaybolur: soldan çıkarak (asıl yol) veya ömrü dolarak (emniyet).
///     Görsel uyarı hangisi önce gelecekse ona göre işler — enkaz kaybolmasına
///     yaklaşırken solar, son saniyelerde yanıp söner.
/// </summary>
public class Debris : MonoBehaviour
{
    public ResourceType resourceType   = ResourceType.RawMaterial;
    public float        resourceAmount = 10f;

    Vector2        _scatter;   // patlama itmesi — sönümlenir
    float          _life;
    SpriteRenderer _sr;

    const float LifeTime   = 180f;  // toplanmazsa kaybolma süresi
    const float FadeStart  = 25f;   // kaybolmaya bu kadar kala solmaya başlar
    const float BlinkStart = 8f;    // bu kadar kala yanıp söner
    const float MinAlpha   = 0.3f;  // solmanın indiği taban
    const float BlinkRate  = 4f;    // saniyedeki yanıp sönme sayısı
    const float Drag       = 0.9f;  // saçılmanın sönümlenme hızı (birim/sn²)
    const float DriftSpeed = 0.3f;  // sabit sola kayma (birim/sn) — asla durmaz
    const float DespawnX   = -17f;  // soldan çıkınca yok olur

    static readonly Color MetalColor   = new Color(0.55f, 0.45f, 0.30f);

    // Kristal PARLAK camgöbeği, mavimsi gri değil. Eski ton (0.52, 0.70, 0.85)
    // ekranda ~7 pikselken kahverengi metalden ayırt edilemiyordu; oyuncu kristal
    // düştüğünü göremiyordu. Skin'lerdeki sensör lensiyle aynı renk dili — oyunda
    // camgöbeği "enerji" demek.
    static readonly Color CrystalColor = new Color(0.35f, 0.88f, 1.00f);

    // Kristal daha nadir düşer, o yüzden daha iri çizilir — kaçırılmamalı.
    int PxW => resourceType == ResourceType.EnergyCrystal ? 16 : 12;
    int PxH => resourceType == ResourceType.EnergyCrystal ? 14 : 10;

    public bool IsEmpty => resourceAmount <= 0f;

    /// <summary>Toplam hız — CollectorShip toplarken enkazla birlikte sürüklenir.</summary>
    public Vector2 Velocity => _scatter + Vector2.left * DriftSpeed;

    /// <summary>Kaynak tipine göre skin anahtarı — ham madde ve kristal ayrı görsel.</summary>
    string SkinKey => resourceType == ResourceType.EnergyCrystal
                    ? SkinId.DebrisCrystal : SkinId.DebrisMetal;

    public void Init(Vector2 velocity, float amount, ResourceType type = ResourceType.RawMaterial)
    {
        _scatter       = velocity;
        resourceAmount = amount;
        resourceType   = type;

        // resourceType Awake'den SONRA belli olur — sprite burada kesinleşir
        if (_sr != null) _sr.sprite = SkinLibrary.Get(SkinKey, PxW, PxH, Color.white);
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

        // Saçılma sönümlenir, sola kayma kalıcıdır
        if (_scatter.sqrMagnitude > 0.000001f)
            _scatter = Vector2.MoveTowards(_scatter, Vector2.zero, Drag * Time.deltaTime);

        transform.position += (Vector3)(Velocity * Time.deltaTime);

        if (transform.position.x < DespawnX) { Destroy(gameObject); return; }

        _life -= Time.deltaTime;
        if (_life <= 0f) { Destroy(gameObject); return; }

        ApplyTint(AlphaForRemaining(SecondsLeft()));
    }

    /// <summary>
    /// Kaybolmasına kalan süre: ömür sayacı ile soldan çıkışa kalan süreden
    /// hangisi yakınsa o. Sürüklenme yüzünden enkaz genelde ömrü dolmadan
    /// sahneden çıkar; uyarı bu yüzden yalnızca sayaca bakamaz.
    /// </summary>
    float SecondsLeft()
    {
        float toEdge = (transform.position.x - DespawnX) / Mathf.Max(DriftSpeed, 0.001f);
        return Mathf.Min(_life, toEdge);
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
        // Skin varken sprite kendi rengini taşır; tip rengi yerine yalnızca alfa geçer
        _sr.color = SkinLibrary.Tint(SkinKey, c);
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
        // Prosedürel doku beyazdır; renk sr.color ile verilir — tip rengi ve solma
        // tek yerden yönetilir. Skin varken sprite kendi rengini taşır.
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite       = SkinLibrary.Get(SkinKey, PxW, PxH, Color.white);
        _sr.sortingOrder = -1;
        ApplyTint(1f);
    }
}
