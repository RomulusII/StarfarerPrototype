using UnityEngine;

/// <summary>
/// Kalkan boşalınca (Collapse) ve yeniden aktive olunca (Expand) oynayan çember animasyonu.
///
/// Collapse : kalkan yüzeyinden merkeze hızla çöker (ease-in, ~0.28sn).
/// Expand   : merkezden yavaş başlayıp hızlanarak kalkan yarıçapına ulaşır,
///            2-3 bounce yaparak fade out olur (~1.1sn). Animasyon bitince onComplete çağrılır.
///
/// Sprite: dış kenar opak, içe doğru transparan tam çember.
/// PPU = OutR → dış kenar = 1 birim; localScale = ShieldRadius ile doğru boyuta getirilir.
/// </summary>
public class ShieldBubbleEffect : MonoBehaviour
{
    public enum AnimMode { Collapse, Expand }

    AnimMode      _mode;
    SpriteRenderer _sr;
    float          _timer;
    float          _radius;
    System.Action  _onComplete;

    const float CollapseTime = 0.28f;
    const float ExpandTime   = 1.1f;

    // ── Spawn ─────────────────────────────────────────────────────────────────

    public static void SpawnCollapse(Vector2 center, float radius)
        => Create(center, radius, AnimMode.Collapse, null);

    public static void SpawnExpand(Vector2 center, float radius, System.Action onComplete)
        => Create(center, radius, AnimMode.Expand, onComplete);

    static void Create(Vector2 center, float radius, AnimMode mode, System.Action onComplete)
    {
        var go = new GameObject("ShieldBubble");
        go.transform.position   = center;
        go.transform.localScale = Vector3.one * (mode == AnimMode.Collapse ? radius : 0.001f);

        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = GetRingSprite();
        sr.sortingOrder = 11;
        sr.color        = new Color(0.82f, 0.93f, 1f, mode == AnimMode.Collapse ? 0.85f : 0f);

        var fx         = go.AddComponent<ShieldBubbleEffect>();
        fx._mode       = mode;
        fx._sr         = sr;
        fx._timer      = 0f;
        fx._radius     = radius;
        fx._onComplete = onComplete;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (UpgradeUI.IsPaused) return;
        _timer += Time.deltaTime;

        if (_mode == AnimMode.Collapse) UpdateCollapse();
        else                            UpdateExpand();
    }

    void UpdateCollapse()
    {
        float t = Mathf.Clamp01(_timer / CollapseTime);

        // Ease-in cubic: yavaş başlar, merkeze yaklaşırken hızlanır
        float ease  = t * t * t;
        float scale = _radius * (1f - ease);
        float alpha = 0.85f * (1f - t);

        Apply(scale, alpha);
        if (t >= 1f) Destroy(gameObject);
    }

    void UpdateExpand()
    {
        float t = Mathf.Clamp01(_timer / ExpandTime);

        const float GrowEnd = 0.62f; // büyüme fazı

        float scale, alpha;

        if (t < GrowEnd)
        {
            // Ease-in cubic — önce yavaş, hızla hedefe koşar
            float tt   = t / GrowEnd;
            float ease = tt * tt * tt;
            scale = ease * _radius;
            alpha = Mathf.Lerp(0f, 0.85f, Mathf.Sqrt(tt));
        }
        else
        {
            // Bounce + fade
            float bt     = (t - GrowEnd) / (1f - GrowEnd); // 0 → 1
            float bounce = Mathf.Sin(bt * Mathf.PI * 2.8f) * (1f - bt) * 0.10f;
            scale = _radius * (1f + bounce);
            alpha = 0.85f * (1f - bt * bt);
        }

        Apply(scale, alpha);

        if (t >= 1f)
        {
            _onComplete?.Invoke();
            Destroy(gameObject);
        }
    }

    void Apply(float scale, float alpha)
    {
        transform.localScale = Vector3.one * Mathf.Max(scale, 0.001f);
        var c = _sr.color;
        _sr.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, alpha));
    }

    // ── Sprite üretimi ────────────────────────────────────────────────────────

    static Sprite _ringSprite;

    static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        const int   Sz   = 256;
        const float OutR = 120f;         // PPU = OutR → dış kenar = 1 birim
        const float InR  = OutR * 0.72f; // yüzey kalınlığı ~%28
        const float Cx   = Sz * 0.5f;

        var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[Sz * Sz];

        for (int y = 0; y < Sz; y++)
        for (int x = 0; x < Sz; x++)
        {
            float r = Mathf.Sqrt((x - Cx) * (x - Cx) + (y - Cx) * (y - Cx));
            if (r < InR || r > OutR) { px[y * Sz + x] = Color.clear; continue; }

            float fade = (r - InR) / (OutR - InR); // 0 içte → 1 dışta (dış opak)
            px[y * Sz + x] = new Color(1f, 1f, 1f, fade);
        }

        tex.SetPixels(px);
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, Sz, Sz), new Vector2(0.5f, 0.5f), OutR);
        return _ringSprite;
    }
}
