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
                                   int count = 6, Color? color = null, bool lethal = false)
    {
        if (_tex == null) _tex = BuildTex();

        Color baseColor = color ?? new Color(1f, 0.88f, 0.35f);

        // Lethal: ileri doğru patlama (geniş koni). Non-lethal: yüzeyden sekme.
        Vector2 bounce;
        float   spread;
        if (lethal)
        {
            bounce = incomingDir.normalized;
            spread = 90f;
        }
        else
        {
            bounce = Vector2.Reflect(incomingDir.normalized, surfaceNormal.normalized);
            if (bounce == Vector2.zero) bounce = surfaceNormal;
            spread = 60f;
        }

        for (int i = 0; i < count; i++)
        {
            float spreadAngle = Random.Range(-spread, spread);
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

    /// <summary>DeathEffect gibi dış sınıfların ortak texture'a erişimi için.</summary>
    public static Sprite SharedTex()
    {
        if (_tex == null) _tex = BuildTex();
        return Sprite.Create(_tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 100f);
    }

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
/// Düşman ölünce fırlayan gemi enkazı parçaları.
/// Dönerek uzaklaşır, alfa ile solar.
/// </summary>
public static class DeathEffect
{
    /// <param name="pos">Ölüm noktası</param>
    /// <param name="bodyColor">Geminin gövde rengi — parçalar bu renkten türer</param>
    /// <param name="bodyWidth">Gemi genişliği (px) — parça boyutunu ölçekler</param>
    /// <param name="bodyHeight">Gemi yüksekliği (px)</param>
    public static void Spawn(Vector2 pos, Color bodyColor, int bodyWidth, int bodyHeight)
    {
        int   count     = Random.Range(6, 12);
        float sizeScale = Mathf.Clamp(bodyWidth / 60f, 0.5f, 2.5f);

        for (int i = 0; i < count; i++)
        {
            float angle    = Random.Range(0f, 360f);
            float speed    = Random.Range(1.2f, 4.5f);
            Vector2 dir    = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                        Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;

            float lifetime = Random.Range(0.45f, 1.3f);
            float angVel   = Random.Range(-240f, 240f);

            // Gemi renginin hafif varyasyonları
            Color c = new Color(
                Mathf.Clamp01(bodyColor.r * Random.Range(0.75f, 1.25f)),
                Mathf.Clamp01(bodyColor.g * Random.Range(0.75f, 1.25f)),
                Mathf.Clamp01(bodyColor.b * Random.Range(0.75f, 1.25f)));

            // Dikdörtgen parça: non-uniform scale
            float sx = Random.Range(0.6f, 2.2f) * sizeScale * 0.06f;
            float sy = Random.Range(0.2f, 0.7f) * sizeScale * 0.06f;

            var go = new GameObject("DeathFragment");
            go.transform.position   = (Vector3)pos + (Vector3)Random.insideUnitCircle * 0.25f;
            go.transform.localScale = new Vector3(sx, sy, 1f);
            go.transform.rotation   = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite       = HitEffect.SharedTex();
            sr.sortingOrder = 22;
            sr.color        = c;

            var df              = go.AddComponent<DeathFragment>();
            df.velocity         = dir;
            df.lifetime         = lifetime;
            df.angularVelocity  = angVel;
            df.initialScale     = new Vector3(sx, sy, 1f);
        }
    }
}

/// <summary>
/// Tek bir enkaz parçası: dönerek hareket eder, yavaşlar, solar.
/// </summary>
public class DeathFragment : MonoBehaviour
{
    public Vector2 velocity;
    public float   lifetime;
    public float   angularVelocity;
    public Vector3 initialScale;

    float          _timer;
    SpriteRenderer _sr;

    void Awake() => _sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        _timer += Time.deltaTime;
        float t = Mathf.Clamp01(_timer / lifetime);

        if (t >= 1f) { Destroy(gameObject); return; }

        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity           *= Mathf.Max(0f, 1f - Time.deltaTime * 2.5f);
        transform.Rotate(0f, 0f, angularVelocity * Time.deltaTime);

        if (_sr != null)
        {
            Color c = _sr.color;
            c.a     = 1f - t;
            _sr.color = c;
        }

        transform.localScale = initialScale * (1f - t * 0.25f);
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
