using UnityEngine;

/// <summary>
/// Vurulabilir mühimmatın etrafında yanıp sönen köşe parantezleri.
///
/// Neden var: oyuncunun ekranda gördüğü mermilerin çoğu vurulamaz — düşman
/// mermisinin HP'si yok, <see cref="ITurretTarget"/> uygulamıyor, yalnızca
/// çarptığı şeye hasar veren bir trigger. Bomba ise vurulabilir ve Point
/// Defence'in bütün varlık sebebi o. İkisi de sıcak turuncu bir yumru olarak
/// çizildiği için oyuncu hangisinin durdurulabileceğini göremiyordu.
///
/// İşaret sprite'ın kendisine gömülmez, AYRI bir çocuk nesnedir: yanıp sönme
/// merminin kendi görselini karartmamalı ve yeni bir vurulabilir tip
/// eklendiğinde tek satırla ona da takılabilmeli.
///
/// Halka değil PARANTEZ kullanılır — halka kalkan/aura okuması yaratıyor,
/// köşe parantezi evrensel "nişan alınabilir" dili.
/// </summary>
public class ShootableMarker : MonoBehaviour
{
    /// <summary>Saniyedeki yanıp sönme sayısı.</summary>
    const float BlinkRate = 2.6f;

    const float MinAlpha = 0.20f;
    const float MaxAlpha = 0.85f;

    /// <summary>Çerçeve nefes alır — sabit boyut, yanıp sönerken donuk duruyor.</summary>
    const float PulseScale = 0.06f;

    SpriteRenderer _sr;
    Color          _color;
    float          _baseScale;
    float          _phase;

    /// <summary>
    /// Hedefin etrafına işareti takar. Skin yoksa HİÇBİR ŞEY yapmaz: prosedürel
    /// yedek dolu bir dikdörtgendir ve mühimmatın üstünü tamamen kapatırdı —
    /// "işaret yok" o dikdörtgenden iyidir.
    /// </summary>
    public static void Attach(Transform target, float worldSize, Color color, int sortingOrder)
    {
        if (target == null || !SkinLibrary.Has(SkinId.ShootableFrame)) return;

        var go = new GameObject("ShootableMarker");
        go.transform.SetParent(target, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.ShootableFrame, 30, 30, Color.white);
        sr.sortingOrder = sortingOrder;

        // Sprite beyaz üretilir, rengi buradan gelir — aynı çerçeve ileride
        // farklı mühimmat tiplerinde farklı renkte kullanılabilsin diye.
        var m = go.AddComponent<ShootableMarker>();
        m._sr        = sr;
        m._color     = color;
        m._baseScale = worldSize / Mathf.Max(0.0001f, sr.bounds.size.x);
        m._phase     = Random.Range(0f, Mathf.PI * 2f);   // aynı anda doğanlar senkron yanmasın

        m.Apply(0f);
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;
        _phase += Time.deltaTime * BlinkRate * Mathf.PI * 2f;
        Apply(_phase);
    }

    void Apply(float phase)
    {
        float t = (Mathf.Sin(phase) + 1f) * 0.5f;

        Color c = _color;
        c.a       = Mathf.Lerp(MinAlpha, MaxAlpha, t);
        _sr.color = c;

        // Parlaklıkla birlikte hafifçe açılıp kapanır: alfa tek başına küçük
        // bir nesnede zayıf bir sinyal, ölçek değişimi çevresel görüşte de okunur.
        transform.localScale = Vector3.one * (_baseScale * (1f + PulseScale * t));
    }
}
