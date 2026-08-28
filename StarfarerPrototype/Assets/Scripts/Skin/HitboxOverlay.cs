using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collider sınırlarını sprite'ın üstüne çizer — skin ile hitbox örtüşmesini
/// gözle doğrulamak için. Tabloda oran hesaplamak yerine farkı doğrudan görürsün.
///
/// Kendi kendini kurar: <c>SkinSet.showHitboxOverlay</c> açıksa sahnedeki her
/// Collider2D'ye bir çerçeve iliştirir. Çağrı noktalarına hiç dokunmaz, bu yüzden
/// asteroit ve oyuncu gemisi gibi skin sistemine henüz girmemiş nesnelerde de çalışır.
///
/// Yalnızca teşhis aracıdır; kapalıyken hiçbir maliyeti yoktur.
/// </summary>
[DefaultExecutionOrder(1000)]
public class HitboxOverlay : MonoBehaviour
{
    const float ScanInterval = 0.5f;
    const float LineWidth    = 0.02f;
    const int   CircleSteps  = 20;

    static readonly Color BoxColor     = new Color(0f, 1f, 0.35f, 0.9f);
    static readonly Color PolygonColor = new Color(1f, 0.85f, 0f, 0.9f);

    readonly HashSet<Collider2D> _tracked = new HashSet<Collider2D>();
    float     _timer;
    Material  _mat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        // SkinSet asset'i varsa her zaman kurulur; açma/kapama Update'te yaşar.
        // Kapalıyken maliyeti kare başına tek bool kontrolüdür — böylece kutu
        // Play SIRASINDA işaretlenince de çerçeveler anında belirir.
        if (SkinSet.Instance == null) return;

        var go = new GameObject("~HitboxOverlay");
        go.AddComponent<HitboxOverlay>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader != null) _mat = new Material(shader);
    }

    void Update()
    {
        // Asset alanı Play sırasında değiştirilebilir; her iki yön de anında işler
        if (!SkinLibrary.OverlayOn)
        {
            if (_tracked.Count > 0) Clear();
            return;
        }

        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;
        _timer = ScanInterval;

        _tracked.RemoveWhere(c => c == null);

        foreach (var col in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (col == null || _tracked.Contains(col)) continue;
            _tracked.Add(col);
            Attach(col);
        }
    }

    void Clear()
    {
        foreach (var col in _tracked)
        {
            if (col == null) continue;
            var frame = col.transform.Find("~hitbox");
            if (frame != null) Destroy(frame.gameObject);
        }
        _tracked.Clear();
    }

    void Attach(Collider2D col)
    {
        Vector2[] pts   = OutlineFor(col);
        if (pts == null || pts.Length < 2) return;

        Color color = col is PolygonCollider2D ? PolygonColor : BoxColor;

        var go = new GameObject("~hitbox");
        go.transform.SetParent(col.transform, false);   // transform'u takip eder

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace   = false;
        lr.loop            = true;
        lr.positionCount   = pts.Length;
        lr.widthMultiplier = LineWidth;
        lr.sortingOrder    = 100;                       // her şeyin üstünde
        lr.numCapVertices  = 0;
        if (_mat != null) lr.material = _mat;
        lr.startColor = lr.endColor = color;

        for (int i = 0; i < pts.Length; i++)
            lr.SetPosition(i, pts[i]);
    }

    static Vector2[] OutlineFor(Collider2D col)
    {
        switch (col)
        {
            case BoxCollider2D box:
            {
                Vector2 h = box.size * 0.5f;
                Vector2 o = box.offset;
                return new[]
                {
                    o + new Vector2(-h.x, -h.y), o + new Vector2( h.x, -h.y),
                    o + new Vector2( h.x,  h.y), o + new Vector2(-h.x,  h.y),
                };
            }

            case PolygonCollider2D poly:
                return poly.pathCount > 0 ? poly.GetPath(0) : null;

            case CircleCollider2D circle:
            {
                var pts = new Vector2[CircleSteps];
                for (int i = 0; i < CircleSteps; i++)
                {
                    float a = i / (float)CircleSteps * Mathf.PI * 2f;
                    pts[i] = circle.offset +
                             new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * circle.radius;
                }
                return pts;
            }

            case CapsuleCollider2D cap:
            {
                Vector2 h = cap.size * 0.5f;
                Vector2 o = cap.offset;
                return new[]
                {
                    o + new Vector2(-h.x, -h.y), o + new Vector2( h.x, -h.y),
                    o + new Vector2( h.x,  h.y), o + new Vector2(-h.x,  h.y),
                };
            }

            default:
                return null;
        }
    }
}
