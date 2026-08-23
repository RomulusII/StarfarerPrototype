using UnityEngine;

/// <summary>
/// Düşman gemisi kurar. Oyundaki TEK düşman inşa yolu burasıdır — bölüm
/// çarpanları da burada uygulanır, böylece ikinci bir yol sessizce ondan sapamaz.
///
/// Sorumluluk ayrımı:
///   ChapterManager — NE spawn edilecek (bütçe, dalga, formasyon, hangi bölüm)
///   EnemySpawner   — NASIL kurulacak (GameObject, HealthBar, çarpanlar)
///
/// Serbest mod (debugFreeSpawn) dalga sistemini beklemeden düşman akıtır; test
/// içindir, varsayılan kapalıdır. O da aynı Spawn() metodunu çağırır — gerçek
/// oyundan farklı bir düşman üretmesi mümkün değildir.
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

    public float spawnInterval = 3f;

    [Tooltip("Editörde atanabilir. Boş bırakılırsa built-in default tipler kullanılır.")]
    public EnemyTypeData[] typePool;
    public float[]         typeWeights;

    EnemyTypeData[] _defaultPool;
    float[]         _defaultWeights;
    float           _timer;

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
        if (!debugFreeSpawn || UpgradeUI.IsPaused) return;

        _timer += Time.deltaTime;
        if (_timer < spawnInterval) return;

        _timer = 0f;
        Spawn(RollType(),
              new Vector3(15f, Random.Range(-4.5f, 4.5f), 0f),
              debugChapter);
    }

    EnemyTypeData RollType()
    {
        bool custom  = typePool != null && typePool.Length > 0
                    && typeWeights != null && typeWeights.Length == typePool.Length;
        var  pool    = custom ? typePool    : _defaultPool;
        var  weights = custom ? typeWeights : _defaultWeights;

        float total = 0f;
        foreach (var w in weights) total += w;
        if (total <= 0f) return pool[0];

        float r   = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < pool.Length; i++)
        {
            acc += weights[i];
            if (r < acc) return pool[i];
        }
        return pool[pool.Length - 1];
    }

    /// <summary>ChapterManager sahnedeyse serbest modu kapatır.</summary>
    public void DisableFreeSpawn() => debugFreeSpawn = false;
}
