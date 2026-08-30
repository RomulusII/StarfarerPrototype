using UnityEngine;

/// <summary>
/// Düşman gemisi kurar. Oyundaki TEK düşman inşa yolu burasıdır — ölçekleme
/// çarpanları da burada uygulanır, böylece ikinci bir yol sessizce ondan sapamaz.
///
/// Sorumluluk ayrımı:
///   ChapterManager — NE spawn edilecek (bütçe, dalga, formasyon, hangi level)
///   EnemySpawner   — NASIL kurulacak (GameObject, HealthBar, çarpanlar)
///
/// Ölçekleme artık BÖLÜM değil LEVEL bazlıdır. 100 level elle ayarlanamaz;
/// HP, hasar, zırh ve kaçamak <see cref="LevelCurve"/> formüllerinden gelir.
/// Bölüm sınırı yalnızca tema ve yeni düşman tipi getirir — zorluk orada
/// sıçramaz, sürekli akar.
///
/// Serbest mod kendi rampasını üretir ama AYNI <see cref="EnemyScaling"/>
/// yolundan geçer; ayrı bir formül yoktur.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Serbest Mod (test)")]
    [Tooltip("Açıksa dalga sistemi olmadan sürekli düşman akıtır. ChapterManager " +
             "sahnedeyse bunu kapatır — normal oyunda devre dışıdır.")]
    public bool debugFreeSpawn = false;

    [Tooltip("Serbest modda rampa yerine bu levelin çarpanlarını kullan. " +
             "0 = rampa çalışsın.")]
    public int debugLevel = 0;

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

    float _freeElapsed;      // serbest modda geçen süre
    bool  _freeRunning;

    /// <summary>Serbest modun anlık zorluk seviyesi (0'dan başlar, sürekli artar).</summary>
    public float RampLevel => levelDuration > 0.01f ? _freeElapsed / levelDuration : 0f;

    // ── Tek inşa yolu ─────────────────────────────────────────────────────────

    /// <summary>
    /// Verilen tipten bir düşman kurar; çarpanlar oynanmakta olan levelden alınır.
    /// Çağıranın hangi levelde olduğunu bilmesi gerekmez.
    /// </summary>
    public static EnemyBot Spawn(EnemyTypeData data, Vector3 position)
        => Spawn(data, position, EnemyScaling.ForLevel(GameProgress.CurrentLevel));

    /// <summary>Ölçeklemeyi açıkça vererek kurar (serbest mod, testler).</summary>
    public static EnemyBot Spawn(EnemyTypeData data, Vector3 position, EnemyScaling scaling)
    {
        if (data == null) return null;

        var go = new GameObject($"EnemyBot_{data.displayName}");
        go.transform.position = position;
        go.AddComponent<HealthBar>();

        var bot = go.AddComponent<EnemyBot>();
        bot.data = ApplyScaling(data, scaling);
        return bot;
    }

    /// <summary>
    /// Ölçekleme çarpanlarını uygular. Orijinal asset'e dokunmaz — runtime
    /// kopyası döner.
    ///
    /// Zırh ÇARPILMAZ, EKLENİR: levelin taban zırhı tipin kendi zırhının
    /// üstüne biner. Çarpılsaydı zırhsız tipler sonsuza dek zırhsız kalır ve
    /// eşiğin geç bölümlerdeki işlevi kaybolurdu.
    /// </summary>
    static EnemyTypeData ApplyScaling(EnemyTypeData src, EnemyScaling s)
    {
        var d = Instantiate(src);

        // Unity kopyanın adına "(Clone)" ekler; ad ise tipin kimliğidir —
        // skin anahtarı (SkinId.ForEnemy) buradan türetilir. Geri yazılmazsa
        // kopya kendi skin'ini bulamaz ve sessizce dikdörtgene döner.
        d.name = src.name;

        d.maxHP         = src.maxHP         * s.hp;
        d.maxShield     = src.maxShield     * s.hp;
        d.fireDamage    = src.fireDamage    * s.damage;
        d.evasionAngle  = src.evasionAngle  * s.evasion;
        d.escapeAngle   = src.escapeAngle   * s.evasion;
        d.armor         = src.armor         + s.armor;
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
            EnemyTypeData.CreateBarrier(),
            EnemyTypeData.CreateBomber(),
            EnemyTypeData.CreateBombRunner(),
            EnemyTypeData.CreateInterceptor(),
            EnemyTypeData.CreateArtillery(),
            EnemyTypeData.CreatePhantom(),
            EnemyTypeData.CreateJammer(),
            EnemyTypeData.CreateSplitter(),
            EnemyTypeData.CreateRegenerator(),
            EnemyTypeData.CreateJuggernaut(),
        };
        _defaultWeights = new[]
        {
            0.28f, 0.12f, 0.09f, 0.07f, 0.05f, 0.05f,
            0.08f, 0.06f, 0.05f, 0.05f, 0.05f, 0.03f, 0.02f,
        };
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

        var scaling = debugLevel > 0
            ? EnemyScaling.ForLevel(debugLevel)
            : CurrentRamp(level);

        Spawn(type, new Vector3(ViewBounds.SpawnX, Random.Range(-4.5f, 4.5f), 0f), scaling);
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
    /// Rampanın ölçekleme çarpanları. Kampanyayla aynı EnemyScaling yapısını
    /// kullanır — tek fark çarpanların süreden türemesi.
    /// </summary>
    EnemyScaling CurrentRamp(float level)
    {
        return new EnemyScaling
        {
            hp      = 1f + 0.10f * level,
            damage  = 1f + 0.07f * level,
            evasion = Mathf.Clamp01(level / Mathf.Max(1f, rampFullLevel - 1f)),
            armor   = Mathf.Min(level * 1.2f, LevelCurve.Instance.maxArmor),
        };
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
