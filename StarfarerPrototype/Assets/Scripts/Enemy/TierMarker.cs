using UnityEngine;

/// <summary>
/// Tier işareti: geminin altında tier − 1 kadar küçük chevron (T2: 1, T3: 2,
/// T4: 3), tier renginde. Zırh plakasının rengi tier'ı zaten söylüyor; işaret
/// iki durumu kapatır — renk körlüğü, ve kendi rengi tier rengine yakın tipler
/// (altın T4 ile sarı Avcı/Muhafız). Rütbe işareti dili: sayılabilir, renge
/// bağlı değil.
///
/// Gövdenin ALTINDA durur (HP/kalkan barları üstte) ve dönmez — barlarla aynı
/// kural: gemi döndükçe dönen bir işaret okunmaz. Oynanışa dokunmaz.
/// </summary>
public class TierMarker : MonoBehaviour
{
    const float ChevronWidth = 0.20f;   // dünya birimi
    const float Spacing      = 0.075f;
    const float Gap          = 0.10f;   // gövdenin alt kenarından

    Transform _root;
    float     _offsetY;

    static Sprite _chevron;

    public void Init(int tier, float bodyHeight)
    {
        _offsetY = -(bodyHeight * 0.5f + Gap);

        var rootGO = new GameObject("TierMarker");
        rootGO.transform.SetParent(transform, false);
        _root = rootGO.transform;

        var color = EnemyTier.ColorOf(tier);
        int count = Mathf.Clamp(tier - 1, 1, 3);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Chevron");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(0f, -i * Spacing, 0f);
            go.transform.localScale    = Vector3.one * ChevronWidth;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = Chevron();
            sr.color        = color;
            sr.sortingOrder = 30;
        }

        LateUpdate();
    }

    void LateUpdate()
    {
        if (_root == null) return;
        _root.SetPositionAndRotation(transform.position + new Vector3(0f, _offsetY, 0f),
                                     Quaternion.identity);
        // Ebeveyn ölçeği işareti büyütmesin — uniform ölçek kuralı, ama güvence
        var s = transform.lossyScale.x;
        _root.localScale = Vector3.one / Mathf.Max(0.0001f, s);
    }

    /// <summary>Yukarı bakan "^" — paylaşılan, beyaz; rengi SpriteRenderer verir.</summary>
    static Sprite Chevron()
    {
        if (_chevron != null) return _chevron;

        const int W = 32, H = 14;
        const float thick = 4.2f;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px  = new Color32[W * H];
        float cx = (W - 1) * 0.5f;
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            // İki kol: tepe (cx, H-1)'den köşelere. Kolun eksenine uzaklık.
            float dx   = Mathf.Abs(x - cx);
            float armY = (H - 1) - dx * ((H - 1) / cx);   // kolun bu x'teki y'si
            float d    = Mathf.Abs(y - armY) * 0.8f;
            float a    = Mathf.Clamp01(thick * 0.5f - d + 0.5f);
            px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply();
        _chevron = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);
        return _chevron;
    }
}
