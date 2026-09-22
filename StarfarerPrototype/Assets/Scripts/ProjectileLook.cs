using UnityEngine;

/// <summary>
/// Mermi görünümünün tek sahibi: boyut, parlaklık ve hâle.
///
/// Mermiler oyun ölçeğinde neredeyse görünmüyordu — turret mermisi 0.08 × 0.04
/// birim, yani zoom-out'ta bir-iki piksel. Oyuncu kendi turretinin ateş ettiğini
/// ve kendisine ne geldiğini göremiyordu.
///
/// GÖRSEL AYRI BİR ÇOCUK NESNEDİR, collider kökte kalır. Görseli büyütmek
/// isabet alanını büyütmemeli: bu bir okunabilirlik değişikliği, denge değil.
/// Kökün ölçeği (boost ×1.5 / ×0.6) collider'la birlikte görsele de işler —
/// o zaten bir denge kuralı ve öyle kalıyor.
///
/// BOYUT HASARDAN TÜRER (<see cref="PowerScale"/>): güçlenen mermi büyür.
/// Ayrı bir "boyut" parametresi olsaydı her çağrı noktası kendi tahminini
/// yazardı — HitEffect'in kıvılcım kuralıyla aynı gerekçe.
/// </summary>
public static class ProjectileLook
{
    /// <summary>Bütün mermilerin genel büyütmesi.</summary>
    public const float BaseScale = 1.7f;

    /// <summary>Mermi renginin beyaza ne kadar yaklaştırılacağı — parlaklık.</summary>
    const float Brighten = 0.35f;

    /// <summary>Hâlenin çapı, merminin uzun kenarına göre.</summary>
    const float GlowSize  = 2.2f;
    const float GlowAlpha = 0.45f;

    const string VisualName = "Visual";
    const string GlowName   = "Glow";

    static Sprite _glow;

    /// <summary>
    /// Hasara göre boyut çarpanı. 10 hasar = 1 (başlangıç raylı topu).
    ///
    ///     çarpan = clamp((hasar / 10)^0.3, 0.85, 2.0)
    ///
    /// Üs 1'in çok altında: Sv10 raylı top (93 hasar) iki kat büyür, dokuz
    /// kat değil — yoksa geç oyunda mermiler sahneyi kaplardı. Taban 0.85:
    /// 3 hasarlı Swarm mermisi yine GÖRÜNMELİ.
    ///
    /// Mutlak hasara bakar, silahın kendi tabanına değil: nükleer başlık
    /// (110) bir gatling mermisinden (8) iri görünmeli, yükseltme de aynı
    /// kuralla büyütür. Kayıttan kurulan mermi de yalnızca hasarından aynı
    /// boyutu bulur — ayrı bir alan kaydetmek gerekmez.
    /// </summary>
    public static float PowerScale(float damage)
        => Mathf.Clamp(Mathf.Pow(Mathf.Max(damage, 0.01f) / 10f, 0.3f), 0.85f, 2.0f);

    /// <summary>
    /// Mermiye görselini kurar; zaten kuruluysa günceller (EnemyBullet hasarını
    /// ve tipini Start'ta öğreniyor).
    /// </summary>
    /// <param name="tint">SpriteRenderer.color — gri tonlamalı sprite'larda rengin kendisi.
    /// Kendi renginde çizilmiş sprite'larda beyaz geçilir.</param>
    /// <param name="glowColor">Hâlenin rengi — merminin okunduğu renk.</param>
    /// <param name="cancelRootScale">Kökün ölçeği görselden düşülsün mü. Düşman mermisinin kökü
    /// hasara göre ölçekleniyor (EnemyBot, collider'la birlikte — bir isabet alanı kuralı);
    /// görsel boyut da hasardan türediği için o ölçek düşülmezse hasar iki kez sayılırdı.</param>
    public static SpriteRenderer Apply(GameObject bullet, Sprite sprite, Color tint, Color glowColor,
                                       int sortingOrder, float damage, bool cancelRootScale = false)
    {
        var visual = bullet.transform.Find(VisualName);
        SpriteRenderer sr, glowSr;
        if (visual == null)
        {
            visual = new GameObject(VisualName).transform;
            visual.SetParent(bullet.transform, false);
            sr = visual.gameObject.AddComponent<SpriteRenderer>();

            var glow = new GameObject(GlowName).transform;
            glow.SetParent(visual, false);
            glowSr = glow.gameObject.AddComponent<SpriteRenderer>();
            glowSr.sprite = GlowSprite();
        }
        else
        {
            sr     = visual.GetComponent<SpriteRenderer>();
            glowSr = visual.Find(GlowName).GetComponent<SpriteRenderer>();
        }

        float root = cancelRootScale ? Mathf.Max(bullet.transform.localScale.x, 0.01f) : 1f;
        visual.localScale = Vector3.one * (BaseScale * PowerScale(damage) / root);

        sr.sprite       = sprite;
        sr.color        = Color.Lerp(tint, Color.white, Brighten);
        sr.sortingOrder = sortingOrder;

        // Hâle sprite'ın MERKEZİNE oturur, pivotuna değil: turret mermisinin
        // pivotu arka ucunda (0, 0.5).
        var b = sprite != null ? sprite.bounds : new Bounds(Vector3.zero, Vector3.one * 0.08f);
        glowSr.transform.localPosition = b.center;
        glowSr.transform.localScale    = Vector3.one * (Mathf.Max(b.size.x, b.size.y) * GlowSize);
        glowSr.color        = new Color(glowColor.r, glowColor.g, glowColor.b, GlowAlpha);
        glowSr.sortingOrder = sortingOrder - 1;
        return sr;
    }

    /// <summary>Yumuşak radyal hâle, 1 birim çaplı. Tek doku, bütün mermiler paylaşır.</summary>
    static Sprite GlowSprite()
    {
        if (_glow != null) return _glow;
        const int res = 32;
        var tex = new Texture2D(res, res) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px  = new Color[res * res];
        float c = (res - 1) / 2f;
        for (int i = 0; i < px.Length; i++)
        {
            float dx = (i % res - c) / c, dy = (i / res - c) / c;
            float d  = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
            float a  = (1f - d) * (1f - d);   // merkezde dolgun, kenarda sıfır
            px[i] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        _glow = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        return _glow;
    }
}
