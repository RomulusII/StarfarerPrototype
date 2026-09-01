using System.Collections.Generic;
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
    [Tooltip("Bir rampa seviyesi için YOK EDİLMESİ gereken tehdit puanı. " +
             "Zorluk saatten değil, oyuncunun temizlediği düşmandan ilerler.")]
    public float threatPerRampLevel = 20f;

    [Tooltip("Spawn aralığı: başlangıç → seviye rampFullLevel'e ulaşınca varılan değer.")]
    public float startInterval = 3.5f;
    public float minInterval   = 1.5f;

    [Tooltip("Rampanın tamamlandığı seviye — bundan sonrası tam zorluktur.")]
    public float rampFullLevel = 20f;

    [Tooltip("Aynı anda sahada olabilecek düşman: taban ve seviye başına artış. " +
             "SAYI hızlı büyür, TİP yavaş açılır — ikisi ayrı kollardır: bir " +
             "Swarm daha eklemek tempoyu artırır, yeni bir tip açmak duvar örer.")]
    public int   baseMaxAlive    = 2;
    public float maxAlivePerLevel = 0.5f;
    public int   maxAliveCap      = 8;

    [Tooltip("Tip kilidi: seviye başına açılan tehdit puanı. threatScore'u bu " +
             "eşiğin üstünde olan tipler henüz gelmez.")]
    public float threatPerLevel = 0.7f;

    [Tooltip("Bir rampa seviyesi kaç kampanya leveline denk sayılır? " +
             "Ölçekleme çarpanları bu levelden okunur.")]
    public float campaignLevelsPerRamp = 1.5f;

    [Tooltip("Aynı anda sahada olabilecek EN FAZLA siper gemisi. Siper hiç hasar " +
             "vermez, yalnızca ateş hattını kapatır — ikisi bir duvar eder.")]
    public int maxBarriersAlive = 1;

    [Tooltip("Bir tip, o anki tehdit tavanının bu oranından pahalıysa aynı anda " +
             "yalnızca BİR tane bulunabilir. Yeni açılan ağır tip YALNIZ gelir; " +
             "havuz büyüyüp o tip sıradanlaşınca çoğalabilir.")]
    public float heavySoloRatio = 0.5f;

    [Tooltip("Editörde atanabilir. Boş bırakılırsa built-in default tipler kullanılır.")]
    public EnemyTypeData[] typePool;
    public float[]         typeWeights;

    EnemyTypeData[] _defaultPool;
    float[]         _defaultWeights;
    float           _timer;

    bool _freeRunning;

    // ── Serbest modun ilerlemesi: YOK EDİLEN TEHDİT ──────────────────────────
    //
    // Zorluk eskiden GEÇEN SÜREDEN geliyordu (`_freeElapsed += Time.deltaTime`).
    // Bu, oyuncunun yaptığı hiçbir şeye bakmayan bir saatti: gemileri
    // öldürebilsen de öldüremesen de, kaynak toplasan da toplamasan da düşmanlar
    // güçleniyordu. Bir kez geride kalan bir daha asla toparlayamıyordu — üstelik
    // saha dolduğu için enkaz da düşmüyor, yani gerekli kaynak da akmıyordu.
    //
    // Şimdi ölçü, oyuncunun TEMİZLEDİĞİ tehdit puanıdır — kampanyanın dalga
    // kurarken kullandığı para birimiyle AYNI (`ThreatBudget`). Kaleci öldürmek
    // 20, Swarm öldürmek 1 ilerletir. Sonuç kendini dengeler: takılan oyuncuda
    // zorluk durur, hızlı temizleyende hızlı yükselir. Saat yerine SKOR.
    static float s_clearedThreat;

    /// <summary>
    /// Bir düşman GERÇEKTEN yok edildiğinde çağrılır (bkz. EnemyBot.ApplyHullDamage).
    /// Ekrandan çıkarak kaybolan gemi sayılmaz — o oyuncunun kazanımı değil.
    /// </summary>
    public static void ReportKill(EnemyTypeData data)
    {
        if (data != null) s_clearedThreat += Mathf.Max(1, data.threatScore);
    }

    /// <summary>Serbest modun anlık zorluk seviyesi — temizlenen tehditten türer.</summary>
    public float RampLevel =>
        threatPerRampLevel > 0.01f ? s_clearedThreat / threatPerRampLevel : 0f;

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

        // Ölçekli kopya kaydedilir, asset'teki taban değil: bu levelde gerçekten
        // geçerli olan sayı budur (düşman bilgi kutusuyla aynı gerekçe).
        BalanceLog.Event("enemy_spawn")
                  .Str("tip",    data.name)
                  .Num("tehdit", data.threatScore)
                  .Num("maxHP",  bot.data.maxHP)
                  .Num("kalkan", bot.data.maxShield)
                  .Num("zirh",   bot.data.armor)
                  .Num("hasar",  bot.data.fireDamage)
                  .Num("manevra", bot.data.maneuverScale)
                  .End();

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
        // Şarj hızı kalkanla AYNI çarpanı alır: yoksa geç levellerde kalkan
        // 10 katına çıkarken doldurma süresi de 10 katına çıkar ve "boşalt,
        // pencereyi kullan" mekaniği tek seferlik bir olaya dönerdi.
        d.shieldRechargeRate = src.shieldRechargeRate * s.hp;
        d.fireDamage    = src.fireDamage    * s.damage;
        d.evasionAngle  = src.evasionAngle  * s.evasion;
        d.escapeAngle   = src.escapeAngle   * s.evasion;
        d.armor         = src.armor         + s.armor;

        // Manevra çarpanı AYRI bir alanda taşınır, agility'nin üstüne yazılmaz:
        // agility aynı zamanda tipin kimliğidir (PursuesFighters onu okur).
        d.maneuverScale = s.mobility;
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

        // Seviye TEMİZLENEN TEHDİTTEN gelir; burada yalnızca okunur. Zaman
        // yalnızca spawn ARALIĞINI ölçer — "ne sıklıkta" bir tempo sorusudur,
        // "ne kadar güçlü" ise bir kazanım sorusu.
        float level = RampLevel;

        _timer += Time.deltaTime;
        if (_timer < IntervalAt(level)) return;
        _timer = 0f;

        var alive = FindObjectsByType<EnemyBot>(FindObjectsSortMode.None);

        // Sahada yeterince düşman varsa yenisini gönderme — oyuncu boğulmasın
        if (alive.Length >= MaxAliveAt(level)) return;

        var type = RollUnlockedType(level, alive);
        if (type == null) return;

        var scaling = debugLevel > 0
            ? EnemyScaling.ForLevel(debugLevel)
            : CurrentRamp(level);

        Spawn(type, new Vector3(ViewBounds.SpawnX, Random.Range(-4.5f, 4.5f), 0f), scaling);
    }

    /// <summary>
    /// Serbest mod açıldığında sayaçları sıfırlar, asteroit alanını kurar.
    /// Temizlenen tehdit STATİK tutulur (ölüm anında ulaşılabilir olmalı), bu
    /// yüzden burada açıkça sıfırlanır — yoksa sahne yeniden yüklendiğinde yeni
    /// oyun bir önceki oyunun skoruyla, yani ortasından başlardı.
    /// </summary>
    void BeginFreeRun()
    {
        s_clearedThreat = 0f;
        _timer          = 0f;

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
    /// Serbest modun bu andaki kampanya levelİ karşılığı. Rampa artık kendi
    /// çarpanlarını üretmiyor, yalnızca bir LEVEL seçiyor.
    /// </summary>
    public int EquivalentLevel(float level)
        => Mathf.Clamp(1 + Mathf.RoundToInt(level * campaignLevelsPerRamp),
                       1, LevelCurve.Instance.totalLevels);

    /// <summary>
    /// Rampanın ölçekleme çarpanları — doğrudan <see cref="LevelCurve"/>'den.
    ///
    /// Eskiden burada AYRI bir formül vardı ve tehlikeli olan zırhtı:
    /// <c>armor = level × 1.2</c>, yani 5 dakikada 6 zırh. Kampanyada 6 zırha
    /// ancak ~level 45'te ulaşılır; oyuncu ise serbest modda başlangıç
    /// donanımıyla oynuyor. Raylı topun 10 hasarı zırh eşiğinden 4'e, sonra
    /// tabana (%10 = 1) düşüyordu: düşmanlar birkaç dakikada VURULAMAZ hâle
    /// geliyordu. "Serbest mod da kampanyayla aynı ölçekleme yolunu kullanır"
    /// kuralı yazılıydı ama yalnızca yapıya (EnemyScaling) uyuluyordu, sayılara
    /// değil — tek formül olunca zırh eğrisi de kendiliğinden doğru yerde.
    /// </summary>
    EnemyScaling CurrentRamp(float level) => EnemyScaling.ForLevel(EquivalentLevel(level));

    /// <summary>
    /// Yalnızca bu seviyede açılmış VE şu an gönderilebilir tipler arasından
    /// ağırlıklı seçim.
    ///
    /// Siper gemisi (<see cref="EnemyTypeData.RequiresEscort"/>) iki ek kurala
    /// tabidir ve ikisi de sahnedeki duruma bakar:
    ///
    /// 1. **Yalnız gelmez.** Kampanya bu kuralı `FillByBudget` içinde zaten
    ///    uyguluyordu, serbest mod HİÇ uygulamıyordu — oysa gerekçe modun
    ///    değil gemi tipinin kendisine ait: siperin bütün anlamı ARKASINDAKİNE
    ///    siper olmaktır. Yalnız gelen bariyer baskı üretmeyen, sadece ateş
    ///    hattını kapatan bir engeldir.
    /// 2. **Aynı anda en fazla `maxBarriersAlive` tane.** Siper hiç hasar
    ///    vermediği ve kalkanı boşalınca çekilip şarj olduğu için ölmeden
    ///    birikebiliyordu: üç siper bir DUVAR eder, oyuncunun yapabileceği
    ///    hiçbir şey kalmaz.
    ///
    /// Ayrıca **AĞIR tipler yalnız gelir**: tehdit puanı o anki tavanın
    /// `heavySoloRatio` katından yüksek olan tipten sahada en fazla bir tane
    /// bulunabilir. Ölçü mutlak değil GÖRELİDİR — yeni açılan tip tanım gereği
    /// tavanın tepesindedir, yani hep yalnız gelir; havuz büyüyüp o tip
    /// "sıradan"laşınca kendiliğinden çoğalır. Ağır bir tipin açıldığı ANDA
    /// çifter gelmesi, oyuncunun eline yeni bir cevap geçmeden önce iki katı
    /// duvar demekti.
    /// </summary>
    EnemyTypeData RollUnlockedType(float level, EnemyBot[] alive)
    {
        bool custom  = typePool != null && typePool.Length > 0
                    && typeWeights != null && typeWeights.Length == typePool.Length;
        var  pool    = custom ? typePool    : _defaultPool;
        var  weights = custom ? typeWeights : _defaultWeights;

        // Sahnenin durumu: refakat edecek biri var mı, kaç siper duruyor,
        // hangi tipten kaç tane var? Tip kimliği ADdır — ApplyScaling runtime
        // kopyasında adı korur (skin anahtarı da oradan türer).
        bool hasEscorted = false;
        int  barriers    = 0;
        _aliveByType.Clear();

        if (alive != null)
            foreach (var b in alive)
            {
                if (b == null || b.data == null) continue;
                if (b.data.RequiresEscort) barriers++;
                else                       hasEscorted = true;

                _aliveByType.TryGetValue(b.data.name, out int c);
                _aliveByType[b.data.name] = c + 1;
            }

        bool  barriersAllowed = hasEscorted && barriers < Mathf.Max(0, maxBarriersAlive);
        float cap             = ThreatCapAt(level);

        // Taban 2: tehdit 1 olan tip (Swarm) hiçbir zaman "ağır" sayılmamalı,
        // yoksa oyunun ilk dakikasında sahada tek bir Swarm kalırdı.
        float heavyAt = Mathf.Max(2f, cap * heavySoloRatio);

        float total = 0f;
        for (int i = 0; i < pool.Length; i++)
            if (Allowed(pool[i], cap, heavyAt, barriersAllowed)) total += weights[i];

        // Hiçbir tip gönderilemiyorsa (ör. yalnızca siper açık ve sahada refakat
        // yok) bu turu ATLA. Eskiden pool[0]'a düşülüyordu — kilit boşa çıkardı.
        if (total <= 0f) return null;

        float r   = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!Allowed(pool[i], cap, heavyAt, barriersAllowed)) continue;
            acc += weights[i];
            if (r < acc) return pool[i];
        }
        return null;
    }

    readonly Dictionary<string, int> _aliveByType = new();

    bool Allowed(EnemyTypeData t, float threatCap, float heavyAt, bool barriersAllowed)
    {
        if (t == null || t.threatScore > threatCap) return false;
        if (t.RequiresEscort && !barriersAllowed)   return false;

        if (t.threatScore >= heavyAt)
        {
            _aliveByType.TryGetValue(t.name, out int c);
            if (c >= 1) return false;
        }
        return true;
    }

    /// <summary>ChapterManager sahnedeyse serbest modu kapatır.</summary>
    public void DisableFreeSpawn() => debugFreeSpawn = false;
}
