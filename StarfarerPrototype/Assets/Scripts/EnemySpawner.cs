using UnityEngine;

/// <summary>
/// Düşman gemisi kurar. Oyundaki TEK düşman inşa yolu burasıdır — bölüm
/// çarpanları da burada uygulanır, böylece ikinci bir yol sessizce ondan sapamaz.
///
/// Sorumluluk ayrımı:
///   ChapterManager — NE spawn edilecek (bütçe, dalga, formasyon, hangi bölüm)
///   EnemySpawner   — NASIL kurulacak (GameObject, HealthBar, çarpanlar)
///
/// Serbest mod (debugFreeSpawn) dalga sistemini beklemeden düşman akıtır. O da
/// aynı Spawn() metodunu çağırır — gerçek oyundan farklı bir düşman üretmesi
/// mümkün değildir.
///
/// Serbest modun kendi zorluk rampası vardır: baştan her tipi boca etmez.
/// Geçen süreye göre bir "seviye" hesaplanır ve dört şey birlikte artar:
/// hangi tiplerin açık olduğu, spawn sıklığı, aynı anda sahada olabilecek
/// düşman sayısı ve istatistik çarpanları. Bkz. RampLevel / CurrentRamp.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Serbest Mod (test)")]
    [Tooltip("Açıksa dalga sistemi olmadan sürekli düşman akıtır. ChapterManager " +
             "sahnedeyse bunu kapatır — normal oyunda devre dışıdır.")]
    public bool debugFreeSpawn = false;

    [Tooltip("Serbest modda hangi bölümün çarpanlarıyla spawn edilsin. " +
             "null ise çarpansız (ham) düşman gelir.")]
    public ChapterData debugChapter;

    [Header("Serbest Mod Zorluk Rampası")]
    [Tooltip("Bir zorluk seviyesinin süresi (saniye). Küçük = hızlı sertleşir.")]
    public float levelDuration = 40f;

    [Tooltip("Spawn aralığı: başlangıç → seviye rampFullLevel'e ulaşınca varılan değer.")]
    public float startInterval = 4.5f;
    public float minInterval   = 1.0f;

    [Tooltip("Rampanın tamamlandığı seviye — bundan sonrası tam zorluktur.")]
    public float rampFullLevel = 8f;

    [Tooltip("Aynı anda sahada olabilecek düşman: taban ve seviye başına artış.")]
    public int   baseMaxAlive    = 3;
    public float maxAlivePerLevel = 1.2f;
    public int   maxAliveCap      = 14;

    [Tooltip("Tip kilidi: seviye başına açılan tehdit puanı. threatScore'u bu " +
             "eşiğin üstünde olan tipler henüz gelmez.")]
    public float threatPerLevel = 1.5f;

    [Tooltip("Editörde atanabilir. Boş bırakılırsa built-in default tipler kullanılır.")]
    public EnemyTypeData[] typePool;
    public float[]         typeWeights;

    EnemyTypeData[] _defaultPool;
    float[]         _defaultWeights;
    float           _timer;

    float       _freeElapsed;      // serbest modda geçen süre
    bool        _freeRunning;
    ChapterData _rampChapter;      // rampanın çarpanlarını taşıyan sentetik bölüm

    /// <summary>Serbest modun anlık zorluk seviyesi (0'dan başlar, sürekli artar).</summary>
    public float RampLevel => levelDuration > 0.01f ? _freeElapsed / levelDuration : 0f;

    // ── Tek inşa yolu ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verilen tipten bir düşman kurar; çarpanlar oynanmakta olan bölümden alınır.
    /// Çağıranın hangi bölümde olduğunu bilmesi gerekmez.
    /// </summary>
    public static EnemyBot Spawn(EnemyTypeData data, Vector3 position)
        => Spawn(data, position, ChapterManager.CurrentChapter);

    /// <summary>
    /// Bölümü açıkça vererek kurar. chapter null ise çarpansız (ham) düşman gelir.
    /// ScriptableObject asset'i bozulmaz — çarpanlar kopya üzerinde uygulanır.
    /// </summary>
    public static EnemyBot Spawn(EnemyTypeData data, Vector3 position, ChapterData chapter)
    {
        if (data == null) return null;

        var go = new GameObject($"EnemyBot_{data.displayName}");
        go.transform.position = position;
        go.AddComponent<HealthBar>();

        var bot = go.AddComponent<EnemyBot>();
        bot.data = ApplyChapterScaling(data, chapter);
        return bot;
    }

    /// <summary>
    /// Bölüm zorluk çarpanlarını uygular. Orijinal asset'e dokunmaz — runtime
    /// kopyası döner. chapter null ise veri olduğu gibi kullanılır.
    /// </summary>
    static EnemyTypeData ApplyChapterScaling(EnemyTypeData src, ChapterData chapter)
    {
        if (chapter == null) return src;

        var d = Instantiate(src);
        d.maxHP         = src.maxHP         * chapter.enemyHpMultiplier;
        d.maxShield     = src.maxShield     * chapter.enemyHpMultiplier;
        d.fireDamage    = src.fireDamage    * chapter.enemyDamageMultiplier;
        d.contactDamage = src.contactDamage * chapter.enemyDamageMultiplier;
        d.evasionAngle  = src.evasionAngle  * chapter.enemyEvasionMultiplier;
        d.escapeAngle   = src.escapeAngle   * chapter.enemyEvasionMultiplier;
        return d;
    }

    // ── Serbest mod ───────────────────────────────────────────────────────────

    void Awake()
    {
        _defaultPool = new[]
        {
            EnemyTypeData.CreateSwarm(),
            EnemyTypeData.CreateArmored(),
            EnemyTypeData.CreateShield(),
            EnemyTypeData.CreateBomber(),
            EnemyTypeData.CreateBombRunner(),
        };
        _defaultWeights = new[] { 0.50f, 0.20f, 0.15f, 0.07f, 0.08f };
    }

    void Update()
    {
        if (!debugFreeSpawn) { _freeRunning = false; return; }

        if (!_freeRunning) { _freeRunning = true; BeginFreeRun(); }

        // Duraklatma rampayı sıfırlamamalı — yalnızca ilerlemeyi durdurur
        if (UpgradeUI.IsPaused) return;

        _freeElapsed += Time.deltaTime;

        float level = RampLevel;

        _timer += Time.deltaTime;
        if (_timer < IntervalAt(level)) return;
        _timer = 0f;

        // Sahada yeterince düşman varsa yenisini gönderme — oyuncu boğulmasın
        if (FindObjectsByType<EnemyBot>(FindObjectsSortMode.None).Length >= MaxAliveAt(level))
            return;

        var type = RollUnlockedType(level);
        if (type == null) return;

        Spawn(type,
              new Vector3(15f, Random.Range(-4.5f, 4.5f), 0f),
              debugChapter != null ? debugChapter : CurrentRamp(level));
    }

    /// <summary>Serbest mod açıldığında sayaçları sıfırlar, asteroit alanını kurar.</summary>
    void BeginFreeRun()
    {
        _freeElapsed = 0f;
        _timer       = 0f;

        // Asteroit yoksa serbest modda hiç kaynak akmaz — küçük bir alan kur
        if (FindFirstObjectByType<AsteroidSpawner>() == null)
            gameObject.AddComponent<AsteroidSpawner>().Configure(3, 12f);
    }

    // ── Zorluk rampası ────────────────────────────────────────────────────────

    float IntervalAt(float level)
        => Mathf.Lerp(startInterval, minInterval, Mathf.Clamp01(level / rampFullLevel));

    int MaxAliveAt(float level)
        => Mathf.Min(maxAliveCap, baseMaxAlive + Mathf.FloorToInt(level * maxAlivePerLevel));

    /// <summary>Bu seviyede açık olan max tehdit puanı — tipler buna göre kilitlenir.</summary>
    float ThreatCapAt(float level) => 1f + level * threatPerLevel;

    /// <summary>
    /// Rampanın istatistik çarpanlarını taşıyan sentetik bölüm. Böylece serbest mod
    /// da kampanyayla aynı ölçekleme yolunu kullanır — ayrı bir formül yoktur.
    /// Kaçamak eğrisi kampanyadakiyle aynı biçimde 0'dan 1'e çıkar.
    /// </summary>
    ChapterData CurrentRamp(float level)
    {
        if (_rampChapter == null)
        {
            _rampChapter = ScriptableObject.CreateInstance<ChapterData>();
            _rampChapter.chapterNumber = 0;
            _rampChapter.chapterTitle  = "Serbest Mod";
        }

        _rampChapter.enemyHpMultiplier      = 1f + 0.10f * level;
        _rampChapter.enemyDamageMultiplier  = 1f + 0.07f * level;
        _rampChapter.enemyEvasionMultiplier = Mathf.Clamp01(level / (rampFullLevel - 1f));
        return _rampChapter;
    }

    /// <summary>Yalnızca bu seviyede açılmış tipler arasından ağırlıklı seçim.</summary>
    EnemyTypeData RollUnlockedType(float level)
    {
        bool custom  = typePool != null && typePool.Length > 0
                    && typeWeights != null && typeWeights.Length == typePool.Length;
        var  pool    = custom ? typePool    : _defaultPool;
        var  weights = custom ? typeWeights : _defaultWeights;

        float cap   = ThreatCapAt(level);
        float total = 0f;
        for (int i = 0; i < pool.Length; i++)
            if (pool[i] != null && pool[i].threatScore <= cap) total += weights[i];

        if (total <= 0f) return pool.Length > 0 ? pool[0] : null;

        float r   = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < pool.Length; i++)
        {
            if (pool[i] == null || pool[i].threatScore > cap) continue;
            acc += weights[i];
            if (r < acc) return pool[i];
        }
        return pool[0];
    }

    /// <summary>ChapterManager sahnedeyse serbest modu kapatır.</summary>
    public void DisableFreeSpawn() => debugFreeSpawn = false;
}
