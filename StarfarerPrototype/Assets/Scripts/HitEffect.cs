using UnityEngine;

/// <summary>
/// Çarpılan YÜZEY — kıvılcımın rengini o belirler.
///
/// Kural: renk çarpanı değil, ÇARPILANI anlatır. Asteroit kalkana çarptığında
/// mavi kıvılcım çıkar, gövdeye çarptığında taş rengi. Oyuncunun bir bakışta
/// okuması gereken şey "neye isabet etti" — kendi mermisinin ne olduğunu zaten
/// biliyor.
/// </summary>
public enum ImpactSurface
{
    Hull,       // metal gövde — sıcak sarı
    Shield,     // kalkan kabuğu — camgöbeği
    Rock,       // asteroit — tozlu kahve
    Component,  // gemi komponenti — mor (komponent mermileri kalkanı bypass eder)
}

/// <summary>
/// Çarpma efekti: bir şey bir şeye vurduğunda kıvılcım patlaması.
///
/// <see cref="SpawnImpact"/> TEK giriş noktasıdır ve oyundaki HER çarpışma
/// oradan geçer: ana silah, turret, savaşçı, düşman mermisi, boss mermisi,
/// bomba, asteroit çarpması. Eskiden yalnızca oyuncunun ana kinetik silahı
/// kıvılcım çıkarıyordu; turret mermisi, düşman mermisinin kalkana çarpması ve
/// asteroit çarpması sessizce hasar veriyordu — oyuncu vurulduğunu yalnızca
/// barın kısalmasından anlıyordu.
///
/// Patlamanın boyutu HASARDAN türer. Ayrı bir "büyüklük" parametresi olsaydı
/// her çağrı noktası kendi tahminini yazardı ve efekt hasarla ilgisini
/// kaybederdi; böyle olunca 3 hasarlı Swarm mermisi ile 60 hasarlı roket
/// kendiliğinden farklı görünür.
/// </summary>
public static class HitEffect
{
    static Texture2D _tex;
    static Sprite    _sprite;

    /// <summary>Bir çarpışmanın görsel karşılığı.</summary>
    /// <param name="hitPos">Çarpma noktası (world space)</param>
    /// <param name="travelDir">Merminin hareket yönü</param>
    /// <param name="targetCenter">Çarpılan nesnenin merkezi — yüzey normali buradan türer</param>
    /// <param name="surface">Neye çarpıldı (rengi belirler)</param>
    /// <param name="damage">Uygulanan hasar — patlamanın boyutunu belirler</param>
    /// <param name="lethal">Hedef bu vuruşla öldüyse: sekme yerine ileri doğru patlama</param>
    public static void SpawnImpact(Vector2 hitPos, Vector2 travelDir, Vector2 targetCenter,
                                   ImpactSurface surface, float damage, bool lethal = false)
    {
        Vector2 normal = hitPos - targetCenter;
        // Mermi hedefin tam merkezinde patlarsa normal sıfır olur; o durumda
        // geldiği yöne geri saçılsın.
        if (normal.sqrMagnitude < 0.0001f) normal = -travelDir;

        Burst(hitPos, travelDir, normal, SparkCount(damage), SurfaceColor(surface),
              SizeScale(damage), lethal);
    }

    /// <summary>
    /// Hasardan kıvılcım sayısı. 10 hasar = 6 kıvılcım — eski sabit değerin ta
    /// kendisi, yani ana silahın bugünkü görüntüsü birebir korunur.
    /// </summary>
    static int SparkCount(float damage)
        => Mathf.Clamp(Mathf.RoundToInt(3f + damage * 0.3f), 3, 16);

    static float SizeScale(float damage)
        => Mathf.Clamp(0.85f + damage * 0.012f, 0.85f, 1.7f);

    static Color SurfaceColor(ImpactSurface s) => s switch
    {
        ImpactSurface.Shield    => new Color(0.45f, 0.82f, 1f),
        ImpactSurface.Rock      => new Color(0.78f, 0.70f, 0.56f),
        ImpactSurface.Component => new Color(0.85f, 0.55f, 1f),
        _                       => new Color(1f,   0.88f, 0.35f),
    };

    /// <summary>Kıvılcım patlamasının çekirdeği. Dışarıdan SpawnImpact ile çağrılır.</summary>
    static void Burst(Vector2 pos, Vector2 incomingDir, Vector2 surfaceNormal,
                      int count, Color baseColor, float sizeScale, bool lethal)
    {
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
            if (bounce == Vector2.zero) bounce = surfaceNormal.normalized;
            spread = 60f;
        }

        for (int i = 0; i < count; i++)
        {
            float spreadAngle = Random.Range(-spread, spread);
            float speed       = Random.Range(3.5f, 9f);
            float lifetime    = Random.Range(0.18f, 0.42f);
            float sizeMult    = Random.Range(0.7f, 1.4f) * sizeScale;

            Vector2 dir = Rotate(bounce, spreadAngle) * speed;

            var go  = new GameObject("HitSpark");
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * sizeMult * 2.5f;

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite       = SharedSprite();
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
            sr.sprite       = SharedSprite();
            sr.sortingOrder = 25;
            sr.color        = sparkColor;

            var sp       = go.AddComponent<Spark>();
            sp.velocity  = dir;
            sp.lifetime  = lifetime;
            sp.baseSize  = sizeMult * 1.8f;
        }
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tüm parçacıkların paylaştığı 4×4 beyaz sprite. Rengi SpriteRenderer verir,
    /// o yüzden paylaşmak güvenli.
    ///
    /// Eskiden HER kıvılcım için ayrı bir Sprite.Create çağrılıyordu — tek bir
    /// çarpışma 6 tane demekti. Artık her mermi tipi kıvılcım çıkardığına göre
    /// yoğun bir dalgada yüzlerce ayrı Sprite nesnesi doğardı.
    /// </summary>
    public static Sprite SharedSprite()
    {
        if (_sprite != null) return _sprite;
        if (_tex == null) _tex = BuildTex();
        _sprite = Sprite.Create(_tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 100f);
        return _sprite;
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
            sr.sprite       = HitEffect.SharedSprite();
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
