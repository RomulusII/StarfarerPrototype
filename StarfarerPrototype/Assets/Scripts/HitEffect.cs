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
    /// Alan hasarı patlaması — roket ve flak mermisi (bkz. DamageUtil.AreaDamage).
    ///
    /// Çarpma kıvılcımından iki yönden ayrılır ve ikisi de oyuncuya BİLGİ verir:
    ///
    /// 1. **360° saçılır**, yüzeyden sekmez. Patlamanın bir çarpma yönü yok.
    /// 2. **Kıvılcım hızı yarıçaptan türer** (`radius / ömür`), yani saçılma tam
    ///    patlama kenarında söner. Yarıçap oyuncuya başka hiçbir yerde
    ///    GÖSTERİLMİYOR: efektin boyutu ile hasarın boyutu aynı şeyi anlatmazsa
    ///    oyuncu nişan almayı öğrenemez — "şuraya atarsam üçünü birden yakalarım"
    ///    ancak yarıçap görünürse düşünülebilir bir şey olur.
    ///
    /// Kıvılcım sayısı yarıçapla büyür ama HASARLA değil: küçük yarıçaplı ağır
    /// bir patlama ile geniş yarıçaplı hafif bir patlama farklı görünmeli.
    /// </summary>
    /// <summary>
    /// Tek bir şarapnel kıymığının izi (bkz. DamageUtil.Shrapnel).
    ///
    /// Kıymık hasarı anlıktır ama görseli uçar. Her kıymığın hızı rastgele
    /// (ortalama menzil / 0.5 sn, ±%35) — hepsi aynı hızda giderse dağılım
    /// genişleyen bir halka gibi okunuyordu. Ömür yol / hız: hedefe çarpan
    /// kıymık tam çarptığı noktada söner, boşa giden menzilin ucunda.
    /// Kıvılcım sürüklemesi kapalı — sürüklenseydi kıymık gösterilen yerden
    /// önce dururdu ve oyuncu menzili yanlış okurdu.
    ///
    /// Bedeli: hasar anlık, görsel yavaş. 2 birimdeki hedefe kıymık ~0.25 sn
    /// sonra VARIR ama hasar o anda yazılmıştır. Göz bunu bir "vurdu" gecikmesi
    /// olarak değil, patlamanın parçası olarak okuyor olmalı — oyunda bakılacak.
    /// </summary>
    public static void SpawnShrapnel(Vector2 origin, Vector2 dir, float length, float range)
    {
        if (length <= 0f || range <= 0f) return;

        const float FullLife = 0.5f;
        float speed = range / FullLife * Random.Range(0.65f, 1.35f);
        float life  = Mathf.Max(0.03f, length / speed);

        var go = new GameObject("ShrapnelSpark");
        go.transform.position   = origin;
        go.transform.localScale = Vector3.one * 1.6f;

        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = SharedSprite();
        sr.sortingOrder = 25;
        sr.color        = new Color(1f, 0.78f, 0.35f);

        var sp      = go.AddComponent<Spark>();
        sp.velocity = dir.normalized * speed;
        sp.lifetime = life;
        sp.baseSize = 1.6f;
        sp.drag     = 0f;
    }

    // ── Flak bulutu ───────────────────────────────────────────────────────────

    static Sprite _puffSprite;

    /// <summary>
    /// Flak patlamasının geride bıraktığı küçük gri bulut — flak'in klasik
    /// imzası: gökyüzünde asılı kalan patlama izleri. Birkaç yumuşak leke üst
    /// üste binerek düzensiz bir bulut çizer; her biri hafifçe büyüyüp yavaşça
    /// solar. Oynanışa dokunmaz, collider'ı yok; kayda girmez (bkz. "Tam Kayıt":
    /// görsel efektler kaydedilmez).
    /// </summary>
    public static void SpawnFlakCloud(Vector2 at)
    {
        int blobs = Random.Range(3, 5);
        for (int i = 0; i < blobs; i++)
        {
            var go = new GameObject("FlakPuff");
            go.transform.position = at + Random.insideUnitCircle * 0.18f;

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite       = PuffSprite();
            sr.sortingOrder = 23;   // kıvılcımların ve şok dalgasının altında
            float g         = Random.Range(0.45f, 0.62f);
            sr.color        = new Color(g, g, g * 1.04f, 0f);

            var p        = go.AddComponent<SmokePuff>();
            p.startSize  = Random.Range(0.28f, 0.42f);
            p.endSize    = p.startSize * Random.Range(1.6f, 2.0f);
            p.peakAlpha  = Random.Range(0.35f, 0.5f);
            p.lifetime   = Random.Range(2.2f, 3.0f);
            p.drift      = Random.insideUnitCircle * 0.08f;
        }
    }

    /// <summary>Kenarı yumuşak gri disk — önbellekli, bütün bulutlar paylaşır.</summary>
    static Sprite PuffSprite()
    {
        if (_puffSprite != null) return _puffSprite;

        const int res = 32;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px  = new Color32[res * res];
        float c = (res - 1) * 0.5f;
        for (int i = 0; i < px.Length; i++)
        {
            float dx = (i % res - c) / c, dy = (i / res - c) / c;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);
            float a  = Mathf.Clamp01(1f - d);
            px[i] = new Color32(255, 255, 255, (byte)(a * a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply();
        _puffSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        return _puffSprite;
    }

    public static void SpawnBlast(Vector2 center, float radius, float damage)
    {
        if (radius <= 0f) return;

        SpawnShockWave(center, radius);

        int count = Mathf.Clamp(Mathf.RoundToInt(10f + radius * 14f), 10, 48);
        Color color = new Color(1f, 0.72f, 0.28f);
        float size  = SizeScale(damage);

        for (int i = 0; i < count; i++)
        {
            // Düzgün dağılım + küçük bir sapma: tam eşit aralık bir çark gibi
            // okunuyor, tamamen rastgele ise kümelenip delik bırakıyor.
            float angle    = i * 360f / count + Random.Range(-8f, 8f);
            float lifetime = Random.Range(0.22f, 0.40f);
            float reach    = radius * Random.Range(0.65f, 1.0f);
            float sizeMult = Random.Range(0.7f, 1.3f) * size;

            var go = new GameObject("BlastSpark");
            go.transform.position   = center;
            go.transform.localScale = Vector3.one * sizeMult * 2.5f;

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.sprite       = SharedSprite();
            sr.sortingOrder = 25;
            sr.color        = color;

            var sp      = go.AddComponent<Spark>();
            sp.velocity = Rotate(Vector2.right, angle) * (reach / lifetime);
            sp.lifetime = lifetime;
            sp.baseSize = sizeMult * 2.5f;
        }
    }

    /// <summary>
    /// Patlamanın şok dalgası: merkezden dışa doğru çok hızlı büyüyen, büyüdükçe
    /// silikleşen bir halka.
    ///
    /// Kıvılcımlarla AYNI işi yapar ama daha okunur biçimde: yarıçap oyuncuya
    /// başka hiçbir yerde gösterilmiyor ve saçılan kıvılcımlardan sınırın tam
    /// nerede bittiğini okumak kalabalıkta zor. Halkanın kenarı o sınırı tek bir
    /// çizgi olarak söyler — "şuraya atarsam üçünü birden yakalarım" ancak
    /// yarıçap görünürse düşünülebilir bir şey olur.
    ///
    /// Bu yüzden yarıçap hasar yarıçapının TA KENDİSİDİR; ayrı bir "görsel
    /// büyüklük" parametresi yok. Efektin sınırı ile hasarın sınırı ayrılsaydı
    /// oyuncu yanlış bir nişan alma dersi öğrenirdi.
    ///
    /// Büyüme YAVAŞLAYARAK ilerler (1−(1−t)³): şok dalgası ilk anda fırlar,
    /// sonra kenarda durulur. Doğrusal büyüme bir patlama değil, açılan bir
    /// çember gibi okunuyor. Alfa aynı sürede tepe değerinin %10'una iner ve
    /// halka orada yok edilir — sıfıra indirmek son karelerde görünmeyen bir
    /// nesneyi beklemek demekti.
    ///
    /// Halka sprite'ı <see cref="BubbleShield.Shell"/>'den gelir: şekil birebir
    /// aynı (içi neredeyse boş, kenarı parlak bir daire) ve o üreteç zaten
    /// önbellekli. İkinci bir halka üreteci yazmak, zamanla birbirinden sapan
    /// iki kopya demekti — bu proje o hatayı kalkan kabuğunda bir kez yaşadı.
    /// </summary>
    static void SpawnShockWave(Vector2 center, float radius)
    {
        var go = new GameObject("ShockWave");
        go.transform.position   = center;
        go.transform.localScale = Vector3.one * (radius * ShockWave.StartRatio);

        var sr          = go.AddComponent<SpriteRenderer>();
        sr.sprite       = BubbleShield.Shell();
        // Kıvılcımların (25) ALTINDA: dalga sınırı çizer, kıvılcım olayı anlatır.
        sr.sortingOrder = 24;
        sr.color        = new Color(1f, 0.85f, 0.55f, ShockWave.PeakAlpha);

        var w        = go.AddComponent<ShockWave>();
        w.maxRadius  = radius;
        // Geniş patlama biraz daha uzun yaşar ama fark küçük: şok dalgası her
        // boyutta ANİ olmalı, yoksa bir patlama değil bir animasyon olur.
        //
        // Üç sayı da 1.2 ile çarpıldı (0.16/0.05/0.36 → 0.192/0.06/0.432):
        // ilk hâli gözle fazla çabuk bitiyordu. Katsayının da ölçeklenmesi şart,
        // yoksa uzama yalnızca küçük yarıçaplarda hissedilir ve eğrinin şekli
        // yarıçapa göre değişirdi; böyle her boyutta TAM %20 uzuyor.
        w.duration   = Mathf.Clamp(0.192f + radius * 0.06f, 0.192f, 0.432f);
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
/// Alan hasarı patlamasının şok dalgası: hızla büyüyen ve silikleşen halka.
/// <see cref="HitEffect.SpawnBlast"/> tarafından kurulur.
///
/// Ölçek UNIFORM'dur ve doğrudan dünya yarıçapıdır — <c>BubbleShield.Shell</c>
/// sprite'ının dış kenarı 1 birimde (bkz. projenin uniform-ölçek kuralı).
/// </summary>
public class ShockWave : MonoBehaviour
{
    /// <summary>Halkanın doğduğu yarıçap, son yarıçapın oranı olarak.</summary>
    public const float StartRatio = 0.12f;

    /// <summary>Doğuştaki alfa. Kalkan kabuğundan daha belirgin: bu bir OLAY.</summary>
    public const float PeakAlpha = 0.65f;

    /// <summary>Sönerken inilen alfa oranı — orada yok edilir.</summary>
    public const float EndAlphaRatio = 0.10f;

    public float maxRadius;
    public float duration;

    float          _timer;
    SpriteRenderer _sr;

    void Awake() => _sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        _timer += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(_timer / duration) : 1f;

        if (t >= 1f) { Destroy(gameObject); return; }

        // 1−(1−t)³ : ilk anda fırlar, kenarda durulur.
        float inv  = 1f - t;
        float ease = 1f - inv * inv * inv;

        transform.localScale = Vector3.one * Mathf.Lerp(maxRadius * StartRatio, maxRadius, ease);

        if (_sr != null)
        {
            Color c   = _sr.color;
            c.a       = PeakAlpha * Mathf.Lerp(1f, EndAlphaRatio, t);
            _sr.color = c;
        }
    }
}

/// <summary>
/// Tek bir kıvılcım parçacığı: hareket eder, yavaşlar, solar, küçülür.
/// </summary>
/// <summary>
/// Flak bulutunun bir lekesi: hızla belirir (ömrün ilk %10'u), sonra büyüyerek
/// yavaşça solar. Upgrade ekranı açıkken donar — oyun da duruyor.
/// </summary>
public class SmokePuff : MonoBehaviour
{
    public float   startSize, endSize, peakAlpha, lifetime;
    public Vector2 drift;

    float          _t;
    SpriteRenderer _sr;

    void Awake() => _sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        _t += Time.deltaTime;
        float k = Mathf.Clamp01(_t / lifetime);
        if (k >= 1f) { Destroy(gameObject); return; }

        const float FadeIn = 0.1f;
        float a = k < FadeIn ? k / FadeIn : 1f - (k - FadeIn) / (1f - FadeIn);

        var col = _sr.color;
        col.a     = peakAlpha * a * a;   // kare: sonlara doğru daha da yavaş söner gibi okunur
        _sr.color = col;

        // Büyüme yavaşlayarak (1−(1−k)²): bulut önce açılır, sonra asılı kalır
        float grow = 1f - (1f - k) * (1f - k);
        transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, grow);
        transform.position  += (Vector3)(drift * Time.deltaTime);
    }
}

public class Spark : MonoBehaviour
{
    public Vector2 velocity;
    public float   lifetime;
    public float   baseSize;

    /// <summary>Saniyedeki hız kaybı katsayısı. 0 = sürüklenmez (şarapnel).</summary>
    public float   drag = 6f;

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
        velocity           *= Mathf.Max(0f, 1f - Time.deltaTime * drag);

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
