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

    [Header("Serbest Mod — Dalgalar")]
    [Tooltip("İki dalga arasındaki süre (sn). SABİTTİR: zorluk dalga BÜYÜKLÜĞÜNDEN " +
             "gelsin diye. İki kadranı (sıklık ve büyüklük) aynı anda açmak, " +
             "log'dan hangisinin fazla geldiğini okumayı imkânsız kılardı — önce " +
             "tek kadranla ölçüp sonra karar vereceğiz.")]
    public float waveInterval = 20f;

    [Tooltip("İlk dalganın tehdit bütçesi. 1 = tek Swarm — oyuncu ısınsın.")]
    public float startWaveBudget = 1f;

    [Tooltip("İKİNCİ dalganın bütçesi. Formülden DEĞİL, elle konur: %10 büyüme " +
             "1'i 1.1 yapar ve ikinci dalga birincinin aynısı olurdu. Açılış bir " +
             "eğri değil, iki elle konmuş adımdır — oyuncu ilkinde tek gemiyi " +
             "tanır, ikincisinde kalabalığın geldiğini anlar. Üçüncü dalgadan " +
             "itibaren normal büyüme devralır.")]
    public float secondWaveBudget = 3f;

    [Tooltip("İlk iki dalga arasındaki süre. Normalden kısa: açılışta tanıma " +
             "anı kısa olmalı, bekleme anı değil.")]
    public float openingInterval = 10f;

    [Tooltip("Her dalga bir öncekinden bu kadar büyük. %10 BİLEŞİKTİR: 10. dalga " +
             "2.4×, 20. dalga 6.1×, 30. dalga 15.9× bütçe taşır — yani 10 " +
             "dakikalık bir koşu kampanyanın ucuna kadar tırmanır. Doğru oran " +
             "ölçümle bulunacak; bu bir başlangıç tahmini.")]
    public float waveBudgetGrowth = 1.10f;

    [Tooltip("Bütçe bunu aşınca dalgaya boss girebilir.")]
    public float bossMinBudget = 40f;

    [Tooltip("Boss eşiğini aşan her dilim için boss gelme olasılığı.")]
    [Range(0f, 1f)] public float bossChance = 0.35f;

    [Tooltip("Tek dalgada en fazla kaç boss.")]
    public int maxBossesPerWave = 2;

    [Header("Serbest Mod Zorluk Rampası (düşman GÜCÜ)")]
    [Tooltip("Rampanın tamamlandığı seviye — bundan sonrası tam zorluktur.")]
    public float rampFullLevel = 20f;

    [Tooltip("Sahada aynı anda bulunabilecek TEHDİT PUANI. Aşılmışsa zamanlı " +
             "dalga ertelenir — oyuncu boğulmasın. Saha temizlendiğinde sınır " +
             "aranmaz: yeni dalga zaten hemen gelir.\n\n" +
             "Ölçü GEMİ SAYISI DEĞİL TEHDİT: 4 Swarm (tehdit 4) ile 4 Kaleci " +
             "(tehdit 108) aynı sayılamaz. Sayıyla ölçülürken valf erken oyunda " +
             "boğuyor, geç oyunda hiçbir şey ifade etmiyordu.")]
    public float baseMaxThreat     = 6f;
    public float maxThreatPerLevel = 2f;
    public float maxThreatCap      = 60f;

    [Tooltip("Valf bir dalgayı EN FAZLA bu kadar geciktirebilir (sn). Süre " +
             "dolunca dalga sahada ne olursa olsun gelir.\n\n" +
             "Farm etmenin bir süresi olmalı ama sınırsız olmamalı: valf tek " +
             "başınayken oyuncu asteroit kazarken sahadaki gemileri görmezden " +
             "geliyor, valf de onun adına oyunu duraklatıyordu. Ölçülen ilk " +
             "oturumda bu 40 ve 61 saniyelik iki boşluk üretti.")]
    public float maxWaveDelay = 15f;

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

    bool  _freeRunning;
    int   _waveIndex;
    float _waveBudget;
    float _scanTimer;

    /// <summary>Sahne taramaları arası süre (sn) — bkz. Update.</summary>
    const float ScanInterval = 0.25f;

    /// <summary>Sonraki dalganın bütçesi — HUD/log için okunur.</summary>
    public float NextWaveBudget => _waveBudget;

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

    /// <summary>
    /// Serbest mod artık DALGA gönderir, tek tek gemi değil.
    ///
    /// Eskisi sabit bir aralıkta bir gemi doğuruyordu: sahne ne doluyor ne
    /// boşalıyordu — ne bir dalganın gerilimi ne de aralardaki nefes vardı,
    /// yalnızca düz bir sızıntı. Kampanyanın dalga ritmi zaten doğru olan
    /// taraftı; serbest mod ondan yalnızca BEKLEME KURALINDA ayrılır:
    ///
    ///   Kampanya  — dalga TEMİZLENMEDEN sonraki başlamaz.
    ///   Serbest   — dalga bir SAATE göre gelir; saha erken temizlenirse
    ///               sonraki dalga beklemeden gelir.
    ///
    /// "Bitmeden başlamaz" kuralı kampanyada anlamlı (level bir bütündür),
    /// burada değildi: oyuncu son bir gemiyi kovalarken oyun duruyordu.
    ///
    /// TEK İSTİSNA BOSS'tur. Boss sahnedeyken yeni dalga gelmez — boss dövüşü
    /// zaten sahnenin tamamını istiyor, üstüne dalga bindirmek onu bir dövüş
    /// değil bir kalabalık yapardı.
    /// </summary>
    void Update()
    {
        if (!debugFreeSpawn) { _freeRunning = false; return; }

        if (!_freeRunning) { _freeRunning = true; BeginFreeRun(); }

        // Duraklatma rampayı sıfırlamamalı — yalnızca ilerlemeyi durdurur
        if (UpgradeUI.IsPaused) return;

        _timer += Time.deltaTime;

        // Sahne taraması KARE BAŞINA yapılmaz. FindObjectsByType bütün sahneyi
        // gezer; "saha temizlendi mi" sorusunun saniyede 60 kez sorulmasının
        // hiçbir karşılığı yok — 0.25 sn'lik gecikme fark edilmez, maliyeti ise
        // dörtte bire iner.
        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return;
        _scanTimer = ScanInterval;

        // Boss dövüşü sahnenin tamamını ister: saat işlemez, dalga gelmez.
        if (FindFirstObjectByType<BossShip>() != null) { _timer = 0f; return; }

        var   alive  = FindObjectsByType<EnemyBot>(FindObjectsSortMode.None);
        float onField = BlockingThreat(alive);

        // Saha temizlendiyse saati bekleme. "Son gemiyi kovala" ölü zamanı
        // buradan çıkar; oyuncu erken bitirdiği için ÖDÜLLENDİRİLİR.
        bool clear = onField <= 0f;
        bool due   = _timer >= CurrentInterval;
        if (!clear && !due) return;

        // Valf: saha doluysa dalga ERTELENİR — ama süresiz değil. Gecikme payı
        // dolunca dalga her hâlükârda gelir; yoksa oyuncu sahadaki gemileri
        // görmezden gelerek oyunu süresiz duraklatabiliyordu.
        float level = RampLevel;
        if (!clear
            && onField >= MaxThreatAt(level)
            && _timer < CurrentInterval + Mathf.Max(0f, maxWaveDelay))
            return;

        _timer = 0f;
        SendWave(level, alive);
    }

    /// <summary>
    /// Dalgayı kurar ve sahneye koyar. Bütçe her dalgada
    /// <see cref="waveBudgetGrowth"/> kadar büyür; düşmanların GÜCÜ ise ayrı bir
    /// koldan, oyuncunun temizlediği tehditten gelir (bkz. <see cref="RampLevel"/>).
    /// İkisi ayrı tutulur: bütçe "kaç tane", rampa "ne kadar sert" sorusudur —
    /// aynı anda ikisini birden artırmak, log'da hangisinin fazla geldiğini
    /// okumayı imkânsız kılardı.
    /// </summary>
    void SendWave(float level, EnemyBot[] alive)
    {
        var scaling = debugLevel > 0
            ? EnemyScaling.ForLevel(debugLevel)
            : CurrentRamp(level);

        int   budget = Mathf.Max(1, Mathf.RoundToInt(_waveBudget));
        float left   = budget;

        int bosses = RollBosses(budget, ref left);
        var types  = BuildWaveTypes(level, left, alive);

        int kadroTehdit = 0;
        foreach (var t in types) if (t != null) kadroTehdit += t.threatScore;

        BalanceLog.Event("wave")
                  .Num("index",  _waveIndex)
                  .Num("butce",  budget)
                  .Num("kadro",  types.Count)
                  .Num("tehdit", kadroTehdit)
                  .Num("boss",   bosses)
                  .Num("rampa",  level)
                  .End();

        if (types.Count > 0)
        {
            var formation = ChapterManager.PickFormation(types, _formations);
            ChapterManager.SortByFormation(types, formation);
            SpawnFormation(types, formation,
                           new Vector3(ViewBounds.SpawnX, 0f, 0f), scaling);
        }

        for (int i = 0; i < bosses; i++) SpawnFreeBoss(level, i, bosses);

        _waveIndex++;

        // Açılış elle konur, formül üçüncü dalgadan itibaren devralır.
        _waveBudget = _waveIndex == 1
            ? Mathf.Max(startWaveBudget, secondWaveBudget)
            : _waveBudget * Mathf.Max(1f, waveBudgetGrowth);
    }

    /// <summary>
    /// Dalgaya kaç boss girecek. Boss bütçeden ÖDENİR (tehdit değeri kadar),
    /// yani boss gelen dalgada refakat kadrosu kendiliğinden küçülür: boss
    /// dalganın üstüne eklenen bir bonus değil, içindeki en pahalı kalemdir.
    ///
    /// Boss bütçenin tamamını yiyemez (1.5× pay şartı) — yalnız gelen bir boss
    /// hedef bölme sınavı olmaktan çıkıp tek hedefli bir bekleyişe döner.
    /// </summary>
    int RollBosses(int budget, ref float left)
    {
        if (budget < bossMinBudget || maxBossesPerWave <= 0) return 0;

        float bossThreat = Mathf.Max(1f, BalanceConfig.Instance.bossThreatValue);
        int   slots      = Mathf.Min(maxBossesPerWave,
                                     Mathf.FloorToInt(budget / Mathf.Max(1f, bossMinBudget)));
        int   count      = 0;

        for (int i = 0; i < slots; i++)
        {
            if (left < bossThreat * 1.5f) break;
            if (Random.value >= bossChance) continue;
            left -= bossThreat;
            count++;
        }
        return count;
    }

    /// <summary>Serbest modun boss'u: rampanın karşılık geldiği bölümün boss'u.</summary>
    void SpawnFreeBoss(float level, int index, int total)
    {
        int chapter = Mathf.Clamp(GameProgress.ChapterOf(EquivalentLevel(level)), 1, 10);
        var data    = BossShipData.CreateForChapter(chapter);
        if (data == null) return;

        var go = new GameObject($"Boss_{data.displayName}");
        go.transform.position = new Vector3(ViewBounds.SpawnX + index * 2f,
                                            total == 1 ? 0f : (index == 0 ? 2f : -2f), 0f);
        go.AddComponent<BossShip>().data = data;
    }

    /// <summary>
    /// Bütçeyi tiplere çevirir. Aynı iş kampanyada
    /// <c>ChapterManager.FillByBudget</c> ile yapılıyor ama oradaki havuz
    /// bölümün sabit listesidir; buradaki havuz SEVİYEYE GÖRE açılır ve siper /
    /// ağır tip kuralları sahnenin o anki durumuna bakar. İki kural kümesi
    /// birleştirilemedi, o yüzden dolum burada kendi yolundan gider.
    /// </summary>
    List<EnemyTypeData> BuildWaveTypes(float level, float budget, EnemyBot[] alive)
    {
        SnapshotAlive(alive);

        var   list   = new List<EnemyTypeData>();
        float left   = budget;
        int   safety = 200;

        while (left >= 1f && safety-- > 0)
        {
            var t = RollUnlockedType(level, left);
            if (t == null) break;
            list.Add(t);
            NoteChosen(t);
            left -= Mathf.Max(1, t.threatScore);
        }

        // BOŞ DALGA OLMAZ. Bütçe küçükken tek uygun tip bile pahalı kalabilir;
        // dalganın hiç gelmemesi, bütçeyi bir tip kadar aşmaktan kötüdür —
        // kampanyadaki taşma kuralıyla aynı gerekçe.
        if (list.Count == 0)
        {
            var t = RollUnlockedType(level, float.MaxValue);
            if (t != null) { list.Add(t); NoteChosen(t); }
        }
        return list;
    }

    /// <summary>Dalganın gemilerini formasyon düzeninde doğurur.</summary>
    public static void SpawnFormation(List<EnemyTypeData> types, FormationTemplate formation,
                                      Vector3 basePos, EnemyScaling scaling)
    {
        var group = FormationGroup.Create(basePos, FormationTarget());

        int slotCount = formation != null && formation.slots != null && formation.slots.Length > 0
            ? formation.slots.Length : 1;

        for (int i = 0; i < types.Count; i++)
        {
            Vector2 offset = Vector2.zero;
            if (formation != null && slotCount > 1)
            {
                offset = formation.slots[i % slotCount].offset;
                offset.x -= (i / slotCount) * RankSpacing;
            }

            Vector3 pos = basePos + new Vector3(offset.x * FormationGroup.SpreadX,
                                                offset.y * FormationGroup.SpreadY, 0f);

            var bot = Spawn(types[i], pos, scaling);
            if (bot != null) group.Add(bot, offset);
        }

        group.Seal();
    }

    /// <summary>Taşan sıralar arası mesafe (normalize ofset biriminde).</summary>
    public const float RankSpacing = 0.55f;

    static Vector2 FormationTarget()
    {
        var ship = FindFirstObjectByType<PlayerShip>();
        return ship != null ? (Vector2)ship.transform.position : Vector2.zero;
    }

    /// <summary>
    /// Sahadaki TEHDİT PUANI toplamı. Silahsız siper gemileri sayılmaz — onları
    /// beklemek sahneyi hiçbir şeyin olmadığı bir bekleyişte kilitler
    /// (kampanyadaki UpdateWaitClear ile aynı kural).
    ///
    /// Tehdit puanı, dalga bütçesiyle AYNI para birimidir. Valfin de onunla
    /// ölçülmesi tesadüf değil: "sahada ne kadar iş var" ile "dalgada ne kadar
    /// iş gönderiyorum" aynı soru.
    /// </summary>
    static float BlockingThreat(EnemyBot[] alive)
    {
        if (alive == null) return 0f;
        float t = 0f;
        foreach (var b in alive)
            if (b != null && b.data != null && b.data.BlocksWaveClear)
                t += Mathf.Max(1, b.data.threatScore);
        return t;
    }

    FormationTemplate[] _formations;

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
        _waveIndex      = 0;
        _waveBudget     = Mathf.Max(1f, startWaveBudget);

        _formations = new[]
        {
            FormationTemplate.CreateArrow(),
            FormationTemplate.CreateColumn(),
            FormationTemplate.CreateBroadFront(),
            FormationTemplate.CreateEscort(),
            FormationTemplate.CreateShieldWall(),
            FormationTemplate.CreateScattered(),
        };

        // Asteroit yoksa serbest modda hiç kaynak akmaz — küçük bir alan kur
        if (FindFirstObjectByType<AsteroidSpawner>() == null)
            gameObject.AddComponent<AsteroidSpawner>().Configure(3, 12f);
    }

    // ── Zorluk rampası ────────────────────────────────────────────────────────

    /// <summary>Sahada aynı anda taşınabilecek tehdit puanı — valfin eşiği.</summary>
    float MaxThreatAt(float level)
        => Mathf.Min(maxThreatCap, baseMaxThreat + level * maxThreatPerLevel);

    /// <summary>
    /// Bu dalganın beklenme süresi. İlk iki dalga arası kısadır: açılışta
    /// oyuncu tek gemiyi tanır, hemen ardından kalabalığın geldiğini görür.
    /// </summary>
    float CurrentInterval => _waveIndex <= 1 ? openingInterval : waveInterval;

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
    /// <param name="budgetLeft">
    /// Dalganın kalan tehdit bütçesi. Bundan pahalı tip seçilemez — dolum
    /// döngüsü bu sayı tükenince durur.
    /// </param>
    EnemyTypeData RollUnlockedType(float level, float budgetLeft)
    {
        bool custom  = typePool != null && typePool.Length > 0
                    && typeWeights != null && typeWeights.Length == typePool.Length;
        var  pool    = custom ? typePool    : _defaultPool;
        var  weights = custom ? typeWeights : _defaultWeights;

        bool  barriersAllowed = _hasEscorted && _barriers < Mathf.Max(0, maxBarriersAlive);
        float cap             = ThreatCapAt(level);

        // Taban 2: tehdit 1 olan tip (Swarm) hiçbir zaman "ağır" sayılmamalı,
        // yoksa oyunun ilk dakikasında sahada tek bir Swarm kalırdı.
        float heavyAt = Mathf.Max(2f, cap * heavySoloRatio);

        float total = 0f;
        for (int i = 0; i < pool.Length; i++)
            if (Allowed(pool[i], cap, heavyAt, barriersAllowed, budgetLeft)) total += weights[i];

        // Hiçbir tip gönderilemiyorsa (ör. yalnızca siper açık ve sahada refakat
        // yok) bu turu ATLA. Eskiden pool[0]'a düşülüyordu — kilit boşa çıkardı.
        if (total <= 0f) return null;

        float r   = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < pool.Length; i++)
        {
            if (!Allowed(pool[i], cap, heavyAt, barriersAllowed, budgetLeft)) continue;
            acc += weights[i];
            if (r < acc) return pool[i];
        }
        return null;
    }

    // ── Saha durumu anlık görüntüsü ───────────────────────────────────────────
    //
    // Siper ve "ağır tip yalnız gelir" kuralları sahnedeki duruma bakar. Dalga
    // TEK SEFERDE kurulduğu için bu durum dolum sırasında da güncellenmeli:
    // yoksa aynı dalgaya iki Kaleci birden girerdi — kural yalnızca ZATEN
    // sahnede olanlara bakıyor, aynı turda seçilenlere değil.

    readonly Dictionary<string, int> _aliveByType = new();
    bool _hasEscorted;
    int  _barriers;

    /// <summary>Dalga kurulmaya başlarken sahnenin durumunu okur.</summary>
    void SnapshotAlive(EnemyBot[] alive)
    {
        _aliveByType.Clear();
        _hasEscorted = false;
        _barriers    = 0;

        if (alive == null) return;
        foreach (var b in alive)
        {
            if (b == null || b.data == null) continue;
            NoteChosen(b.data);
        }
    }

    /// <summary>
    /// Bir tipi "sahada var" sayar. Tip kimliği ADdır — ApplyScaling runtime
    /// kopyasında adı korur (skin anahtarı da oradan türer).
    /// </summary>
    void NoteChosen(EnemyTypeData t)
    {
        if (t == null) return;
        if (t.RequiresEscort) _barriers++;
        else                  _hasEscorted = true;

        _aliveByType.TryGetValue(t.name, out int c);
        _aliveByType[t.name] = c + 1;
    }

    bool Allowed(EnemyTypeData t, float threatCap, float heavyAt, bool barriersAllowed,
                 float budgetLeft)
    {
        if (t == null || t.threatScore > threatCap) return false;
        if (t.threatScore > budgetLeft)             return false;
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
