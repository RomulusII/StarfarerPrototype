using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Destek gemilerinin (Muhafız, Besleyici) görseli — YALNIZCA görsel. Etkinin
/// kendisi <see cref="EnemyBot"/>'ta hesaplanır ve bu sınıfa ne çizileceği
/// söylenir; ikisi ayrı yaşasaydı "görünen dalga" ile "dolduran dalga" zamanla
/// birbirinden saparlardı.
///
/// İki gemi iki AYRI görsel dil konuşur, çünkü ikisi de aynı soruya cevap
/// verir — "bu gemi neden ölmüyor" — ve cevaplar karışmamalı:
///
///   Muhafız (zırh)     — auranın içindeki her gemiye uzanan soluk bir ışın
///                        huzmesi, huzmenin içinde hedefe doğru akan soluk zırh
///                        ikonları. Kimin korunduğunu TEK TEK gösterir.
///   Besleyici (kalkan) — merkezden dışa yayılan bir dalga (radyasyon gibi).
///                        Etki dalganın cephesi gemiye VARDIĞI anda uygulanır;
///                        oyuncu dolumu dalgayla aynı anda görür.
///
/// Menzil halkası ikisinde de çok soluk: aura bir KALKAN değildir ve kalkan
/// kabuğunun diliyle (parlak kenarlı daire) konuşmamalı. Menzili asıl anlatan
/// ışınlar ve dalga.
///
/// Kayda girmez: bağlar bir sonraki aura taramasında (0.25 sn) yeniden kurulur,
/// dalganın hâli EnemyBot'un kaydedilen sayacından türer.
/// </summary>
public class SupportAuraFx : MonoBehaviour
{
    // ── Renkler ve alfalar ───────────────────────────────────────────────────

    /// <summary>Muhafız — soluk altın. Düşman kalkanlarının turuncusundan sarıya kayık.</summary>
    public static readonly Color ArmorColor = new Color(0.95f, 0.80f, 0.34f);

    /// <summary>Besleyici — düşman kalkanlarının tonu: doldurduğu şeyin rengi.</summary>
    public static Color PulseColor =>
        new Color(BarrierShield.ArcColor.r, BarrierShield.ArcColor.g, BarrierShield.ArcColor.b);

    const float ArmorRingAlpha = 0.05f;   // eskiden 0.22 — küre kalkan gibi okunuyordu
    const float PulseRingAlpha = 0.04f;

    const float BeamAlpha      = 0.10f;
    const float BeamWidthStart = 0.035f;
    const float BeamWidthEnd   = 0.018f;
    const float BeamFadeIn     = 0.35f;   // yeni bağlanan gemiye ışın yavaşça açılır

    const int   IconsPerBeam   = 2;
    const float IconTravelTime = 1.1f;    // ikonun gemiden hedefe yolculuğu (sn)
    const float IconAlpha      = 0.40f;
    const float IconWorldSize  = 0.16f;

    const float PulsePeakAlpha  = 0.30f;
    const float PulseTrailRatio = 0.80f;  // ikinci, daha silik halka öncünün bu oranında
    const float PulseTrailAlpha = 0.45f;  // öncüye göre

    // ── Durum ────────────────────────────────────────────────────────────────

    class Beam
    {
        public EnemyBot         target;
        public LineRenderer     line;
        public SpriteRenderer[] icons;
        public float            age;
    }

    readonly List<Beam> _beams = new();
    readonly HashSet<EnemyBot> _wanted = new();

    Transform      _owner;
    Color          _color;
    int            _order;
    SpriteRenderer _pulseLead, _pulseTrail;

    static Material s_lineMat;
    static Sprite   s_armorIcon;

    // ── Kurulum ──────────────────────────────────────────────────────────────

    /// <summary>Muhafız: soluk halka + ışın/ikon bağları.</summary>
    public static SupportAuraFx ForArmor(EnemyBot owner, float range, int sortingOrder)
        => Attach(owner, ArmorColor, ArmorRingAlpha, range, sortingOrder, pulse: false);

    /// <summary>Besleyici: soluk halka + yayılan dalga.</summary>
    public static SupportAuraFx ForShieldPulse(EnemyBot owner, float range, int sortingOrder)
        => Attach(owner, PulseColor, PulseRingAlpha, range, sortingOrder, pulse: true);

    static SupportAuraFx Attach(EnemyBot owner, Color color, float ringAlpha, float range,
                                int sortingOrder, bool pulse)
    {
        var go = new GameObject("SupportAura");
        go.transform.SetParent(owner.transform, false);

        var fx    = go.AddComponent<SupportAuraFx>();
        fx._owner = owner.transform;
        fx._color = color;
        fx._order = sortingOrder;

        // Halkanın dış kenarı 1 birim (BubbleShield.Shell) → ölçek doğrudan yarıçap
        var ring = fx.Ring("Range", ringAlpha);
        ring.transform.localScale = Vector3.one * range;

        if (pulse)
        {
            fx._pulseLead  = fx.Ring("PulseLead",  0f);
            fx._pulseTrail = fx.Ring("PulseTrail", 0f);
            fx.HidePulse();
        }
        return fx;
    }

    SpriteRenderer Ring(string name, float alpha)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = BubbleShield.Shell();
        sr.color        = WithAlpha(_color, alpha);
        sr.sortingOrder = _order - 1;
        return sr;
    }

    // ── Kalkan dalgası ───────────────────────────────────────────────────────

    /// <summary>
    /// Dalganın o anki hâli. <paramref name="radius"/> etkinin cephesiyle AYNI
    /// sayıdır (EnemyBot hesaplar); <paramref name="t"/> yolculuğun oranı (0–1).
    /// </summary>
    public void ShowPulse(float radius, float t)
    {
        if (_pulseLead == null) return;
        float a = PulsePeakAlpha * (1f - t);

        _pulseLead.gameObject.SetActive(true);
        _pulseLead.transform.localScale = Vector3.one * Mathf.Max(radius, 0.01f);
        _pulseLead.color = WithAlpha(_color, a);

        _pulseTrail.gameObject.SetActive(true);
        _pulseTrail.transform.localScale = Vector3.one * Mathf.Max(radius * PulseTrailRatio, 0.01f);
        _pulseTrail.color = WithAlpha(_color, a * PulseTrailAlpha);
    }

    public void HidePulse()
    {
        if (_pulseLead  != null) _pulseLead.gameObject.SetActive(false);
        if (_pulseTrail != null) _pulseTrail.gameObject.SetActive(false);
    }

    // ── Zırh bağları ─────────────────────────────────────────────────────────

    /// <summary>
    /// Auranın o an zırh verdiği gemiler. Aura taraması (0.25 sn) çağırır;
    /// listede olmayan bağ kopar, yeni gelene ışın açılır.
    /// </summary>
    public void SetLinks(List<EnemyBot> targets)
    {
        _wanted.Clear();
        foreach (var t in targets) if (t != null) _wanted.Add(t);

        for (int i = _beams.Count - 1; i >= 0; i--)
        {
            var b = _beams[i];
            if (b.target != null && _wanted.Remove(b.target)) continue;   // sürüyor
            Drop(i);
        }

        foreach (var t in _wanted) _beams.Add(NewBeam(t));
    }

    Beam NewBeam(EnemyBot target)
    {
        var go = new GameObject("ArmorBeam");
        go.transform.SetParent(transform, false);

        var line = go.AddComponent<LineRenderer>();
        line.positionCount     = 2;
        line.useWorldSpace     = true;
        line.startWidth        = BeamWidthStart;
        line.endWidth          = BeamWidthEnd;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows    = false;
        line.sortingOrder      = _order - 2;
        line.material          = LineMaterial;

        var icons = new SpriteRenderer[IconsPerBeam];
        for (int i = 0; i < IconsPerBeam; i++)
        {
            var ic = new GameObject("ArmorIcon");
            ic.transform.SetParent(go.transform, false);
            var sr = ic.AddComponent<SpriteRenderer>();
            sr.sprite       = ArmorIcon;
            sr.sortingOrder = _order - 1;
            sr.color        = WithAlpha(_color, 0f);
            icons[i] = sr;
        }

        return new Beam { target = target, line = line, icons = icons };
    }

    void Drop(int i)
    {
        if (_beams[i].line != null) Destroy(_beams[i].line.gameObject);
        _beams.RemoveAt(i);
    }

    void Update()
    {
        if (UpgradeUI.IsPaused || _beams.Count == 0) return;

        Vector3 from = _owner.position;

        for (int i = _beams.Count - 1; i >= 0; i--)
        {
            var b = _beams[i];
            if (b.target == null) { Drop(i); continue; }

            b.age += Time.deltaTime;
            float   fade = Mathf.Clamp01(b.age / BeamFadeIn);
            Vector3 to   = b.target.transform.position;

            b.line.SetPosition(0, from);
            b.line.SetPosition(1, to);
            b.line.startColor = WithAlpha(_color, BeamAlpha * fade);
            b.line.endColor   = WithAlpha(_color, BeamAlpha * 0.5f * fade);

            // İkonlar huzmenin içinde gemiden hedefe akar. Faz bağın YAŞINDAN
            // gelir: yeni açılan bağ ilk ikonunu gemiden çıkarır, ortadan değil.
            for (int k = 0; k < b.icons.Length; k++)
            {
                float p = Mathf.Repeat(b.age / IconTravelTime + k / (float)b.icons.Length, 1f);
                var   sr = b.icons[k];
                sr.transform.position = Vector3.Lerp(from, to, p);
                sr.transform.rotation = Quaternion.identity;   // ikon hep dik
                sr.color = WithAlpha(_color, IconAlpha * Mathf.Sin(p * Mathf.PI) * fade);
            }
        }
    }

    // ── Paylaşılan kaynaklar ─────────────────────────────────────────────────

    static Material LineMaterial =>
        s_lineMat != null ? s_lineMat : (s_lineMat = new Material(Shader.Find("Sprites/Default")));

    /// <summary>
    /// Zırh ikonu: küçük bir arma kalkanı (düz üst, sivri alt), kenarı parlak,
    /// içi yarı saydam. Beyaz çizilir, renk SpriteRenderer'dan gelir. Tek sprite
    /// bütün Muhafızlar arasında paylaşılır.
    /// </summary>
    static Sprite ArmorIcon
    {
        get
        {
            if (s_armorIcon != null) return s_armorIcon;

            const int W = 24, H = 28;
            const float Edge = 2.2f;   // kenar kalınlığı (piksel)

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                      { filterMode = FilterMode.Bilinear };
            var px = new Color[W * H];

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // v: üstten aşağı 0..1, u: merkezden kenara piksel
                float v    = 1f - (y + 0.5f) / H;
                float u    = Mathf.Abs(x + 0.5f - W * 0.5f);
                float half = W * 0.5f - 1f;

                // Üst %45 düz, sonra sivriye kavisle daralır
                float w = v < 0.45f ? half
                                    : half * (1f - Mathf.Pow((v - 0.45f) / 0.55f, 1.4f));

                float inside = Mathf.Min(w - u, (v * H) - 1f);   // yan kenar ve üst kenar
                if (inside <= 0f) { px[y * W + x] = Color.clear; continue; }

                float a = inside < Edge ? 1f : 0.45f;
                px[y * W + x] = new Color(1f, 1f, 1f, a * Mathf.Clamp01(inside));
            }

            tex.SetPixels(px);
            tex.Apply();
            s_armorIcon = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f),
                                        H / IconWorldSize);
            return s_armorIcon;
        }
    }

    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
}
