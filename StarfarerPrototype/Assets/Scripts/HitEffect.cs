using UnityEngine;

/// <summary>
/// Çarpma efekti: mermi çarptığında kıvılcım parçacıkları oluşturur.
/// SpawnSparks() statik çağrı — herhangi bir script'ten kullanılabilir.
/// Parçacıklar merminin geliş açısına ve çarpılan yüzeyin normaline göre yönlenir.
/// </summary>
public static class HitEffect
{
    static Texture2D _tex;

    /// <param name="pos">Çarpma noktası (world space)</param>
    /// <param name="incomingDir">Merminin hareket yönü (normalize edilmemiş olabilir)</param>
    /// <param name="surfaceNormal">Çarpılan yüzeyin normali (merkeze göre yaklaşık)</param>
    /// <param name="count">Kıvılcım sayısı</param>
    /// <param name="color">Ana renk (varsayılan: sıcak sarı)</param>
    public static void SpawnSparks(Vector2 pos, Vector2 incomingDir, Vector2 surfaceNormal,
                                   int count = 6, Color? color = null)
    {
        if (_tex == null) _tex = BuildTex();

        Color baseColor = color ?? new Color(1f, 0.88f, 0.35f);

        // Geliş yönünü yüzey normaline göre yansıt → ana sekme yönü
        Vector2 bounce = Vector2.Reflect(incomingDir.normalized, surfaceNormal.normalized);
        if (bounce == Vector2.zero) bounce = surfaceNormal;

        for (int i = 0; i < count; i++)
        {
            float spreadAngle = Random.Range(-60f, 60f);
            float speed       = Random.Range(3.5f, 9f);
            float lifetime    = Random.Range(0.18f, 0.42f);
            float sizeMult    = Random.Range(0.7f, 1.4f);

            Vector2 dir = Rotate(bounce, spreadAngle) * speed;

            var go  = new GameObject("HitSpark");
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * sizeMult * 2.5f;

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite       = Sprite.Create(_tex, new Rect(0, 0, 4, 4),
                                            Vector2.one * 0.5f, 100f);
            sr.sortingOrder = 25;
            sr.color        = baseColor;

            var sp       = go.AddComponent<Spark>();
            sp.velocity  = dir;
            sp.lifetime  = lifetime;
            sp.baseSize  = sizeMult * 2.5f;
        }
    }

    /// <summary>
    /// Lazer temas noktası için sürekli, az sayıda elektrik kıvılcımı.
    /// LaserBeam.Update() içinden periyodik olarak çağrılır.
    /// </summary>
    public static void SpawnLaserSparks(Vector2 pos, Vector2 beamDir, Vector2 surfaceNormal, Color sparkColor)
    {
        if (_tex == null) _tex = BuildTex();

        int count = Random.Range(1, 3); // 1-2 parçacık / emit

        for (int i = 0; i < count; i++)
        {
            // Laser kıvılcımı: ışından saçılır, ±80° geniş koni, kısa ömür
            Vector2 bounce    = Vector2.Reflect(beamDir.normalized, surfaceNormal.normalized);
            float   angle     = Random.Range(-80f, 80f);
            float   speed     = Random.Range(4f, 12f);
            float   lifetime  = Random.Range(0.08f, 0.22f);
            float   sizeMult  = Random.Range(0.5f, 1.0f);

            Vector2 dir = Rotate(bounce, angle) * speed;

            var go          = new GameObject("LaserSpark");
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * sizeMult * 1.8f;

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite       = Sprite.Create(_tex, new Rect(0, 0, 4, 4),
                                            Vector2.one * 0.5f, 100f);
            sr.sortingOrder = 25;
            sr.color        = sparkColor;

            var sp       = go.AddComponent<Spark>();
            sp.velocity  = dir;
            sp.lifetime  = lifetime;
            sp.baseSize  = sizeMult * 1.8f;
        }
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    static Texture2D BuildTex()
    {
        var t   = new Texture2D(4, 4) { filterMode = FilterMode.Point };
        var px  = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        t.SetPixels(px);
        t.Apply();
        return t;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}

/// <summary>
/// Tek bir kıvılcım parçacığı: hareket eder, yavaşlar, solar, küçülür.
/// </summary>
public class Spark : MonoBehaviour
{
    public Vector2 velocity;
    public float   lifetime;
    public float   baseSize;

    float          _timer;
    SpriteRenderer _sr;

    void Awake() => _sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / lifetime);

        if (t >= 1f) { Destroy(gameObject); return; }

        // Hareket + sürükleme
        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity           *= Mathf.Max(0f, 1f - Time.deltaTime * 6f);

        // Alfa ve boyut azalır
        if (_sr != null)
        {
            Color c = _sr.color;
            c.a     = 1f - t;
            _sr.color = c;
        }

        float scale = baseSize * (1f - t * 0.6f);
        transform.localScale = Vector3.one * scale;
    }
}
