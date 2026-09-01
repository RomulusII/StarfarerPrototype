using UnityEngine;

/// <summary>
/// Enkazın nereden koptuğu. Kaynak tipinden (ham madde / kristal) BAĞIMSIZ
/// bir eksen: köken ŞEKLİ, kaynak tipi RENGİ belirler. Bir gemi hem metal hem
/// kristal enkaz bırakabilir ve ikisi de gemi parçasına benzemelidir.
/// </summary>
public enum DebrisOrigin
{
    Ship,   // gemi parçası — çizgiler, plakalar, kirişler
    Rock,   // kaya parçası — yalnızca şekilsiz silik lekeler
}

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
    public DebrisOrigin origin         = DebrisOrigin.Ship;

    Vector2        _scatter;   // patlama itmesi — sönümlenir
    float          _life;
    SpriteRenderer _sr;
    int            _variant;  // kökenin hangi görseli — doğarken seçilir

    const float LifeTime   = 180f;  // toplanmazsa kaybolma süresi
    const float FadeStart  = 25f;   // kaybolmaya bu kadar kala solmaya başlar
    const float BlinkStart = 8f;    // bu kadar kala yanıp söner
    const float MinAlpha   = 0.3f;  // solmanın indiği taban
    const float BlinkRate  = 4f;    // saniyedeki yanıp sönme sayısı
    const float Drag       = 0.9f;  // saçılmanın sönümlenme hızı (birim/sn²)
    const float DriftSpeed = 0.3f;  // sabit sola kayma (birim/sn) — asla durmaz
    const float DespawnX   = -17f;  // soldan çıkınca yok olur

    /// <summary>
    /// Skin sprite'ı tek boyutta üretilir (ham madde ölçüsü). Kristalin daha
    /// iri çizilmesi prosedürel yolda PxW/PxH farkından geliyordu; skin yolunda
    /// ölçekten gelmek zorunda. Uniform tutulur — proje kuralı.
    /// </summary>
    const float CrystalScale = 1.35f;

    static readonly Color MetalColor   = new Color(0.55f, 0.45f, 0.30f);

    // Kristal PARLAK camgöbeği, mavimsi gri değil. Eski ton (0.52, 0.70, 0.85)
    // ekranda ~7 pikselken kahverengi metalden ayırt edilemiyordu; oyuncu kristal
    // düştüğünü göremiyordu. Skin'lerdeki sensör lensiyle aynı renk dili — oyunda
    // camgöbeği "enerji" demek.
    static readonly Color CrystalColor = new Color(0.35f, 0.88f, 1.00f);

    // Kristal daha nadir düşer, o yüzden daha iri çizilir — kaçırılmamalı.
    //
    // Ölçüler %50 büyütüldü (12x10 -> 18x15): 0.12 birim ekranda ~9 piksel,
    // yani enkazın silueti okunmadan toplanıp gidiyordu. Skin tarafındaki
    // DebrisScale ile aynı oran — iki yol aynı boyu vermeli.
    int PxW => resourceType == ResourceType.EnergyCrystal ? 24 : 18;
    int PxH => resourceType == ResourceType.EnergyCrystal ? 21 : 15;

    public bool IsEmpty => resourceAmount <= 0f;

    /// <summary>Toplam hız — CollectorShip toplarken enkazla birlikte sürüklenir.</summary>
    public Vector2 Velocity => _scatter + Vector2.left * DriftSpeed;

    /// <summary>Kökene ve doğarken seçilen varyanta göre skin anahtarı.</summary>
    string SkinKey => SkinId.ForDebris(origin, _variant);

    public void Init(Vector2 velocity, float amount,
                     ResourceType type   = ResourceType.RawMaterial,
                     DebrisOrigin source = DebrisOrigin.Ship)
    {
        _scatter       = velocity;
        resourceAmount = amount;
        resourceType   = type;
        origin         = source;

        // Gelirin KAYNAĞI. resource/toplandi ile farkı, toplayıcıların
        // yetişemediği (soldan çıkan veya ömrü dolan) kaynağı verir.
        BalanceLog.Event("resource")
                  .Str("tip",    type.ToString())
                  .Str("olay",   "dustu")
                  .Str("koken",  source.ToString())
                  .Num("miktar", amount)
                  .End();
        _variant       = Random.Range(0, SkinId.DebrisVariantCount(source));

        // Köken ve resourceType Awake'den SONRA belli olur — görsel burada kesinleşir
        RefreshVisual();
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
        // Enkaz sprite'ları GRİ TONLAMALIDIR — rengi kaynak tipi verir.
        // SkinLibrary.Tint KULLANILMAZ: o, skin varken beyaza düşürür ve
        // metal/kristal ayrımını silerdi. Şekil kökenden, renk kaynaktan gelir;
        // iki eksen birbirine karışmamalı.
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
        // Doku beyaz/gri; renk sr.color ile verilir — tip rengi ve solma tek
        // yerden yönetilir. Skin varken de öyle: enkaz sprite'ları gri tonlamalı.
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = -1;
        RefreshVisual();
    }

    /// <summary>Sprite'ı, ölçeği ve rengi mevcut köken/varyant/kaynak tipinden kurar.</summary>
    void RefreshVisual()
    {
        if (_sr == null) return;

        string key = SkinKey;
        _sr.sprite = SkinLibrary.Get(key, PxW, PxH, Color.white);

        bool crystalSkin = SkinLibrary.Has(key) &&
                           resourceType == ResourceType.EnergyCrystal;
        transform.localScale = Vector3.one * (crystalSkin ? CrystalScale : 1f);

        ApplyTint(1f);
    }
}
