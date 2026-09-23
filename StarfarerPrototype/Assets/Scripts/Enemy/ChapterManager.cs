using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kampanya akışını yönetir: 100 level, 10 bölüm, her bölümün 10. leveli boss.
///
/// Akış:
///   BeginLevel → wave döngüsü (SpawnWave → WaitWaveClear) → level biter
///   → aynı bölümdeyse kısa nefes, bölüm bittiyse geçiş ekranı
///
/// BİR DALGANIN TÜM GEMİLERİ AYNI ANDA doğar ve formasyon hâlinde gelir.
/// Eskiden spawnInterval (3 sn) arayla teker teker doğuyorlardı: altı gemilik
/// bir dalga 18 saniyeye yayılıyor, ilk gelen ölmeden sonuncusu doğmuyor ve
/// formasyonun var olduğu bir an hiç oluşmuyordu. Dalga artık tek bir olaydır.
///
/// NE spawn edileceğine bu sınıf karar verir. NASIL kurulacağını bilmez —
/// düşmanı EnemySpawner.Spawn(), asteroit alanını AsteroidSpawner kurar.
///
/// Wave'ler ELLE YAZILMAZ. Level bütçesi BalanceConfig.ThreatBudget(n)'den
/// gelir ve wave'lere bölünür; 100 levelin her birini elle ayarlamak
/// sürdürülebilir değildi ve zorluk eğrisini bölüm sınırlarında sıçratıyordu.
/// </summary>
public class ChapterManager : MonoBehaviour
{
    // ── Bağımlılıklar ─────────────────────────────────────────────────────────

    AsteroidSpawner     _asteroids;
    ChapterTransitionUI _transitionUI;

    // ── Veriler ───────────────────────────────────────────────────────────────

    ChapterData[]       _chapters;
    FormationTemplate[] _formations;

    /// <summary>Oynanmakta olan bölüm. UI ve boss üretimi buradan okur.</summary>
    public static ChapterData CurrentChapter { get; private set; }

    // ── Durum ─────────────────────────────────────────────────────────────────

    enum Phase { WaitClear, Transition, Done }
    Phase _phase = Phase.WaitClear;

    /// <summary>
    /// Kampanya tamamlandı mı? Simülasyon koşusu bitişi buradan anlar — kendi
    /// başına "artık bitmiştir" diye tahmin etseydi yarım koşuyu tam sayardı.
    /// Statik: koşuyu izleyen SimDirector sahnedeki yöneticiyi aramak zorunda
    /// kalmasın (bölüm sistemi menü seçiminden SONRA kuruluyor).
    /// </summary>
    public static bool CampaignFinished { get; private set; }

    List<WaveData> _levelWaves = new();
    int            _waveIndex;

    /// <summary>
    /// Açık dalganın seri numarası — doğan her gemi bununla etiketlenir
    /// (<see cref="EnemyBot.waveTag"/>). Kampanya boyunca yalnızca artar; level
    /// içi indeks (_waveIndex) yetmezdi, çünkü önceki levelden kalan bir
    /// etiketle çakışabilirdi.
    /// </summary>
    int   _waveSerial;

    /// <summary>Açık dalganın doğurduğu, dalgayı ENGELLEYEN tehdit (%90 eşiğinin paydası).</summary>
    float _waveThreat;

    float _clearScanTimer;

    /// <summary>
    /// Temizlik taraması aralığı. FindObjectsByType bütün sahneyi gezer ve
    /// dalgalar onlarca gemiye çıktı; soruyu saniyede 60 kez sormanın karşılığı
    /// yok (serbest moddaki ScanInterval ile aynı gerekçe). Kayda yazılmaz:
    /// yüklemede sıfırdan başlaması en fazla bir taramayı öne çeker.
    /// </summary>
    const float ClearScanInterval = 0.25f;

    // ── Akış: büyük dalga KOLLAR hâlinde gelir ────────────────────────────────
    //
    // "Bir dalganın bütün gemileri aynı anda doğar" kuralı dalgalar 3-6 gemiyken
    // yazıldı. Düz statla geç levellerde dalga 46 gemiye, cümbüşte ~190'a çıkıyor:
    // hepsini tek noktada tek formasyonda doğurmak üst üste binmiş upuzun bir
    // kolon üretirdi. Kuralın ruhu (dalga bir OLAYDIR, damla damla sızmaz)
    // korunur: gemiler MaxFormationSize'lık formasyonlar hâlinde, kısa aralıkla
    // art arda gelir. Bu eşiğin altındaki her dalga eskisi gibi tek seferde doğar.

    /// <summary>Tek formasyonun en fazla gemisi. Şablonlar 5-8 yuvalı; 12 iki sıra eder.</summary>
    const int   MaxFormationSize = 12;

    /// <summary>Kollar arası süre. Kısa: sızıntı değil, art arda gelen bir akın.</summary>
    const float StreamInterval   = 1.5f;

    /// <summary>Dalgalar arası nefes; cümbüşten önce daha uzun (bkz. SurgeBreath).</summary>
    const float WaveBreath  = 2f;

    /// <summary>
    /// Cümbüşten önceki nefes. Uyarı bandı çıkar ve oyuncunun upgrade ekranını
    /// açıp bir karar vermesine (flak kurmak, kalkanı yükseltmek) yetecek süre
    /// tanınır — habersiz gelen bir cümbüş bir oyun değil, bir ölüm olurdu.
    /// </summary>
    const float SurgeBreath = 5f;

    /// <summary>Henüz doğmamış kolların kadrosu, doğacağı sırayla.</summary>
    readonly List<EnemyTypeData> _stream = new();
    float     _streamTimer;
    SpawnSide _streamSide;

    readonly List<EnemyTypeData> _pendingSpawns = new();

    // Bekleyen geçiş. Eskiden dalga ve level arası gecikmeler coroutine'di;
    // bir coroutine'in ilerlemesi kaydedilemez, bir sayaç kaydedilir.
    enum Pending { None, BeginWave, BeginLevel, ChapterTransition }
    Pending _pending;
    float   _pendingTimer;

    /// <summary>
    /// Kayıttan devam: GameManager bölüm sistemini kurmadan ÖNCE doldurur,
    /// Start leveli baştan kurmak yerine kayıttaki ana döner.
    /// </summary>
    public static ChapterRunState    PendingRestore;
    public static AsteroidFieldState PendingField;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        // Serbest mod test aracı; dalga sistemi devredeyken kapalı olmalı
        foreach (var s in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
            s.DisableFreeSpawn();

        CampaignFinished = false;   // sahne yeniden yüklenince (ölüm → restart) sıfırlanmalı
        BalanceLog.Begin("kampanya");
        BalanceUploader.EnsureExists();
        PerfSampler.EnsureExists();

        _asteroids    = gameObject.AddComponent<AsteroidSpawner>();
        _transitionUI = FindFirstObjectByType<ChapterTransitionUI>();

        _formations = new[]
        {
            FormationTemplate.CreateArrow(),
            FormationTemplate.CreateColumn(),
            FormationTemplate.CreateBroadFront(),
            FormationTemplate.CreateEscort(),
            FormationTemplate.CreateShieldWall(),
            FormationTemplate.CreateScattered(),
        };

        _chapters = ChapterData.CreateDefaultChapters();

        // GameProgress burada SIFIRLANMAZ: level, GameManager tarafından menüden
        // (yeni oyun = seçilen level, devam = kayıttaki level) zaten ayarlandı.
        if (PendingRestore != null)
        {
            var s = PendingRestore;
            PendingRestore = null;
            RestoreRun(s, PendingField);
            PendingField = null;
            return;
        }

        BeginLevel();
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (_pending == Pending.BeginWave || _pending == Pending.BeginLevel)
        {
            _pendingTimer -= Time.deltaTime;
            if (_pendingTimer > 0f) return;

            var next = _pending;
            _pending = Pending.None;
            if (next == Pending.BeginWave) BeginWave();
            else                           BeginLevel();
            return;
        }

        if (_phase == Phase.WaitClear)
        {
            UpdateStream();
            UpdateWaitClear();
        }
    }

    void Schedule(Pending what, float delay)
    {
        _pending      = what;
        _pendingTimer = delay;
    }

    // ── Kayıt ─────────────────────────────────────────────────────────────────

    public ChapterRunState CaptureState() => new ChapterRunState
    {
        waveIndex    = _waveIndex,
        phase        = (int)_phase,
        pending      = (int)_pending,
        pendingTimer = _pendingTimer,
        levelElapsed = Time.time - _levelStartedAt,
        waveSerial   = _waveSerial,
        waveThreat   = _waveThreat,
        stream       = StreamNames(),
        streamTimer  = _streamTimer,
        streamSide   = (int)_streamSide,
    };

    List<string> StreamNames()
    {
        var names = new List<string>(_stream.Count);
        foreach (var t in _stream) if (t != null) names.Add(t.name);
        return names;
    }

    /// <summary>
    /// Levelin dalga PLANI yeniden kurulur — BuildWaves rastgelelik içermez,
    /// aynı level aynı planı verir. Dalganın KADROSU (hangi gemiler) ise
    /// yeniden çekilmez: o gemiler zaten sahnede, kayıttan geldiler.
    ///
    /// Bölüm geçişi sürerken kaydedildiyse anlatım baştan oynar; anlatımın
    /// içindeki yer bir oyun durumu değil.
    /// </summary>
    void RestoreRun(ChapterRunState s, AsteroidFieldState field)
    {
        int level   = GameProgress.CurrentLevel;
        var chapter = ChapterFor(GameProgress.CurrentChapter);
        CurrentChapter = chapter;

        _asteroids.Configure(chapter.asteroidCount, chapter.asteroidInterval);
        _asteroids.RestoreState(field);

        _levelWaves     = BuildWaves(level, chapter);
        _waveIndex      = s.waveIndex;
        _phase          = (Phase)s.phase;
        _pending        = (Pending)s.pending;
        _pendingTimer   = s.pendingTimer;
        _levelStartedAt = Time.time - s.levelElapsed;
        _waveSerial     = s.waveSerial;
        _waveThreat     = s.waveThreat;

        // Doğmamış kollar ADIYLA yazıldı; tip verisi fabrikadan kurulur. Ölçekleme
        // doğarken uygulandığı için ölçeklenmemiş şablon doğru olandır.
        _stream.Clear();
        if (s.stream != null)
            foreach (var n in s.stream)
            {
                var t = EnemyTypeData.ByName(n);
                if (t != null) _stream.Add(t);
            }
        _streamTimer = s.streamTimer;
        _streamSide  = (SpawnSide)s.streamSide;

        if (_pending == Pending.ChapterTransition)
        {
            if (_transitionUI != null) _transitionUI.Show(chapter, BeginLevel);
            else                       Schedule(Pending.BeginLevel, 1f);
        }

        if (_phase == Phase.Done)
        {
            _transitionUI?.ShowCredits();
            CampaignFinished = true;
        }
    }

    // ── Level kurulumu ────────────────────────────────────────────────────────

    ChapterData ChapterFor(int chapterNumber)
    {
        int idx = Mathf.Clamp(chapterNumber - 1, 0, _chapters.Length - 1);
        return _chapters[idx];
    }

    void BeginLevel()
    {
        _pending = Pending.None;

        int level   = GameProgress.CurrentLevel;
        var chapter = ChapterFor(GameProgress.CurrentChapter);
        CurrentChapter = chapter;

        // Nerede olduğunu söyle. Bölüm geçişi (her 10 levelde bir) tam ekran
        // anlatımını sürdürüyor; bant onun ARALARINI dolduruyor — bölüm içi
        // level geçişi tamamen sessizdi ve 100 levellik kampanyada oyuncu
        // kaçıncı levelde olduğunu hiçbir yerden okuyamıyordu.
        LevelBannerUI.Show(level, GameProgress.CurrentChapter,
                           chapter != null ? Loc.T(chapter.chapterTitle) : null,
                           GameProgress.IsBossLevel);

        _asteroids?.Configure(chapter.asteroidCount, chapter.asteroidInterval);

        _levelWaves     = BuildWaves(level, chapter);
        _waveIndex      = 0;
        _levelStartedAt = Time.time;

        BalanceLog.Event("level_start")
                  .Num("butce", BalanceConfig.Instance.ThreatBudget(level))
                  .Num("dalga", _levelWaves.Count)
                  .End();

        BeginWave();
    }

    /// <summary>Level süresini ölçmek için — bkz. CompleteLevel.</summary>
    float _levelStartedAt;

    /// <summary>
    /// Levelin tehdit bütçesini wave'lere böler.
    ///
    /// İki özel durum var:
    ///   — Bölümün İLK leveli yalnızca yeni tipi getirir. Oyuncu bir tipin
    ///     davranışını kalabalık içinde öğrenemez.
    ///   — Bölümün SON leveli boss levelidir: escort dalgası önce gelir,
    ///     boss ikinci dalgada girer.
    /// </summary>
    List<WaveData> BuildWaves(int level, ChapterData chapter)
    {
        var cfg    = BalanceConfig.Instance;
        var waves  = new List<WaveData>();
        float budget = cfg.ThreatBudget(level);

        int inChapter = GameProgress.LevelInChapter;
        var pool      = chapter.enemyPool;

        // Tanıtım leveli — yeni tip yalnız gelsin
        if (inChapter == 1 && chapter.introducedType != null)
            pool = new[] { chapter.introducedType };

        // OYUNUN İLK LEVELİ ELLE YAZILIR. Havuz zaten tek tip (Swarm) olduğu
        // için guaranteedType da gereksiz; bütçe formülü hiç çalışmaz.
        if (level == 1)
        {
            foreach (int b in OpeningWaveBudgets) waves.Add(Wave(b, pool));
            return waves;
        }

        if (GameProgress.IsBossLevel)
        {
            // Escort dalgası, sonra boss + küçük refakat
            int escort = Mathf.RoundToInt(budget * 0.6f);
            waves.Add(Wave(escort, pool));

            var bossWave = Wave(Mathf.RoundToInt(budget * 0.3f), pool);
            bossWave.bossType = chapter.boss;
            waves.Add(bossWave);
            return waves;
        }

        // Bütçe dalgalara GEOMETRİK bölünür: her dalga bir öncekinden %25 daha
        // ağır. Eskiden eşit bölüşüm + son dalgaya sabit bir zam vardı, yani
        // level düz gidip sonunda tek bir sıçrama yapıyordu; şimdi baştan sona
        // tırmanıyor.
        int[] split = cfg.SplitWaveBudget(budget, WaveCountFor(level));

        // CÜMBÜŞ LEVELİ: orta dalga bütçenin büyük payını alır ve ucuz tiplerle
        // doldurulur. Toplam DEĞİŞMEZ — diğer dalgalar kalanı eski oranlarıyla
        // paylaşır; cümbüş bütçe eklemez, levelin şeklini değiştirir.
        //
        // Neden ORTA dalga: son dalga tam temizlenme bekler (bkz. UpdateWaitClear).
        // Cümbüş sonda olsaydı %90 kuralı onun için hiç işlemez ve oyuncu ~190
        // geminin son birkaçını kovalardı. Ortada olunca kalanları son dalganın
        // üstüne biner — cümbüşün asıl hissi o üst üste binme.
        int surgeIndex = -1;
        if (inChapter == cfg.surgeLevelInChapter && split.Length >= 3)
        {
            surgeIndex = split.Length / 2;
            ApplySurgeShare(split, surgeIndex, budget, cfg.surgeBudgetShare);
        }

        for (int i = 0; i < split.Length; i++)
        {
            var w = Wave(split[i], pool);
            w.isSurge = i == surgeIndex;
            waves.Add(w);
        }

        // Bölümün KİMLİĞİ her levelde en az bir kez görünmeli. Dalga bütçesi
        // levelin bütçesinin ~%40'ı olduğu için ağır bir tip (Armored 7,
        // Bomber 10, Jammer 11) bütçeye uzun süre HİÇ sığmaz: "Zırhlı birimler
        // tespit edildi" diyen bölüm 2, tanıtım levelinden sonra tek bir zırhlı
        // göstermeden bitiyordu. Garanti son dalgaya konur — level bir tırmanış,
        // bölümün imzası da zirvesinde durmalı.
        if (waves.Count > 0 && chapter.introducedType != null)
            waves[waves.Count - 1].guaranteedType = chapter.introducedType;

        return waves;
    }

    /// <summary>
    /// Oyunun İLK levelinin dalgaları — tehdit puanı cinsinden, elle yazılmış.
    ///
    /// Level 1 bir denge eğrisi değil bir TANIŞMADIR: oyuncu ilk dalgada tek
    /// gemiyi tanır, ikincisinde kalabalığın geldiğini anlar, üçüncüsünde
    /// levelin zirvesini görür.
    ///
    /// Neden formülden gelmiyor: geometrik bölüşümde ikinci dalganın birinciye
    /// ORANI, büyüme katsayısının kendisidir. Level 1 bütçesi 7 iken yuvarlama
    /// ikinci dalgayı 2'ye çakılı tutuyor — büyümeyi 1.6'dan 2.5'e çıkarmak bile
    /// 1/2/4 veriyor. "1 sonra 3" için taban bütçeyi 7'den 10'a çıkarmak
    /// gerekirdi ve o değişiklik level 1'i değil YÜZ LEVELİN HEPSİNİ %43
    /// kaydırırdı (üstelik geliri sabit tutmak için `dropPerThreat`i de
    /// düşürmek gerekirdi).
    ///
    /// Onboarding zaten özel bir andır. Bedeli üç elle yazılmış sayıdır,
    /// kampanyanın tamamı değil.
    ///
    /// TOPLAM TAM 7 — levelin bütçesinin AYNISI. Bu bir tesadüf değil, kısıt:
    /// 1/3/5 denendi ve level 1'i 9'a çıkarıyordu, oysa level 2 formülden
    /// 2/2/3 = 7 geliyor. Yani oyuncu level 1'i beş gemilik bir dalgayla
    /// bitirip level 2'ye iki gemiyle başlıyordu — bir TESTERE DİŞİ. Açılışın
    /// levelden taşmaması, sonraki levelin daha hafif hissettirmemesi demek.
    ///
    /// Son iki dalganın eşit olması (3 ve 3) "level tırmanır" kuralından bir
    /// ödün, ama karşılığında iki level ARASINDAKİ akış korunuyor: bir levelin
    /// içindeki tırmanış, levellerin arasındaki düşüşten daha az önemli.
    ///
    /// Serbest moddaki karşılığı: `EnemySpawner.startWaveBudget` /
    /// `secondWaveBudget`. Aynı gerekçe, aynı çözüm.
    /// </summary>
    static readonly int[] OpeningWaveBudgets = { 1, 3, 3 };

    /// <summary>
    /// Bir levelde kaç dalga var. Bütçe büyüdükçe dalga sayısı da bir artar;
    /// yoksa geç levellerde tek dalga 30+ tehdit puanı taşır ve sahneye sığmaz.
    /// </summary>
    static int WaveCountFor(int level) => level < 50 ? 3 : 4;

    /// <summary>
    /// Cümbüş dalgasına levelin bütçesinden <paramref name="share"/> kadar pay
    /// verir; diğer dalgalar kalanı AYNI ORANLARLA paylaşır, yani level hâlâ
    /// tırmanır ve toplam değişmez.
    /// </summary>
    static void ApplySurgeShare(int[] split, int surgeIndex, float levelBudget, float share)
    {
        float othersBefore = 0f;
        for (int i = 0; i < split.Length; i++)
            if (i != surgeIndex) othersBefore += split[i];
        if (othersBefore <= 0f) return;

        float rest = levelBudget * (1f - share);
        for (int i = 0; i < split.Length; i++)
            split[i] = i == surgeIndex
                ? Mathf.Max(1, Mathf.RoundToInt(levelBudget * share))
                : Mathf.Max(1, Mathf.RoundToInt(split[i] * rest / othersBefore));
    }

    static WaveData Wave(int budget, EnemyTypeData[] pool)
    {
        budget = Mathf.Max(1, budget);
        return new WaveData
        {
            budgetMin    = budget,
            budgetMax    = budget,
            allowedTypes = pool,
            spawnSide    = SpawnSide.Right,
        };
    }

    // ── Dalga başlangıcı ─────────────────────────────────────────────────────

    void BeginWave()
    {
        if (_waveIndex >= _levelWaves.Count)
        {
            CompleteLevel();
            return;
        }

        var wave    = _levelWaves[_waveIndex];
        var chapter = CurrentChapter;

        _waveSerial++;
        _waveThreat = 0f;

        if (wave.bossType != null)
            SpawnBossesFor(GameProgress.CurrentChapter, wave.bossType);

        var pool = (wave.allowedTypes != null && wave.allowedTypes.Length > 0)
            ? wave.allowedTypes
            : chapter.enemyPool;

        _pendingSpawns.Clear();
        if (pool != null && pool.Length > 0 && wave.budgetMax > 0)
        {
            FillByBudget(_pendingSpawns, pool,
                Random.Range(wave.budgetMin, wave.budgetMax + 1),
                wave.isSurge ? BalanceConfig.Instance.surgeAlpha : float.NaN);

            // Bölümün tanıtılan tipi bütçeye sığmadıysa bir tane zorla eklenir
            // (bkz. BuildWaves). Bütçeyi bir tip kadar aşmak, bölümün kimliğini
            // hiç göstermemekten iyidir — boş dalga kuralıyla aynı gerekçe.
            if (wave.guaranteedType != null && !_pendingSpawns.Contains(wave.guaranteedType))
                _pendingSpawns.Add(wave.guaranteedType);

            // Dalganın GERÇEKTEN ne ürettiği: bütçe küçük ve tipler pahalı
            // olduğu için kadro çoğu zaman bütçenin söylediği şey değildir
            // (boş-dalga taşması ve guaranteedType). Kağıt üstündeki bütçeyle
            // sahneye çıkan kadroyu ancak yan yana koyunca görebiliriz.
            int kadroTehdit = 0;
            foreach (var t in _pendingSpawns) if (t != null) kadroTehdit += t.threatScore;
            BalanceLog.Event("wave")
                      .Num("index",  _waveIndex)
                      .Num("butce",  wave.budgetMax)
                      .Num("kadro",  _pendingSpawns.Count)
                      .Num("tehdit", kadroTehdit)
                      .Bool("cumbus", wave.isSurge)
                      .End();

            // Payda KADRONUN TAMAMIDIR, doğan gemiler değil: kollar hâlinde gelen
            // bir dalgada ilk kol hızla ölürse, henüz doğmamış kollar sayılmadan
            // dalga "temizlendi" sanılırdı.
            foreach (var t in _pendingSpawns)
                if (t != null && t.BlocksWaveClear) _waveThreat += Mathf.Max(1, t.threatScore);

            _stream.Clear();
            _streamSide = wave.spawnSide;

            if (_pendingSpawns.Count <= MaxFormationSize)
                SpawnChunk(new List<EnemyTypeData>(_pendingSpawns), wave.spawnSide, wave.formation);
            else
            {
                // Kadro önce KOLLARA dağıtılır (sırayla, kart dağıtır gibi), sonra
                // kollar uç uca eklenir. Böylece her kol dalganın küçük bir örneği
                // olur; sırayla kesilseydi ilk kol bütün öncüleri, son kol bütün
                // ağırları taşırdı.
                int k = Mathf.CeilToInt(_pendingSpawns.Count / (float)MaxFormationSize);
                var buckets = new List<EnemyTypeData>[k];
                for (int i = 0; i < k; i++) buckets[i] = new List<EnemyTypeData>();
                for (int i = 0; i < _pendingSpawns.Count; i++) buckets[i % k].Add(_pendingSpawns[i]);

                SpawnChunk(buckets[0], wave.spawnSide, wave.formation);
                for (int i = 1; i < k; i++) _stream.AddRange(buckets[i]);
                _streamTimer = StreamInterval;
            }
        }

        _phase = Phase.WaitClear;
    }

    /// <summary>
    /// Dalganın TÜM gemilerini aynı anda, formasyon düzeninde doğurur ve tek bir
    /// <see cref="FormationGroup"/>'a bağlar.
    ///
    /// Ofsetin İKİ ekseni de kullanılır. Eskiden yalnızca y okunuyordu; ok
    /// formasyonunun tamamı x ekseninde tanımlı olduğu için (0.6 / 0.2 / 0 /
    /// -0.4) düzen dikey bir çizgiye çöküyor ve hangi şablon seçilirse seçilsin
    /// aynı görünüyordu.
    ///
    /// Gemi sayısı yuva sayısını aşarsa formasyon ARKAYA doğru sıralar hâlinde
    /// uzatılır. Eskiden indeks yuva sayısına göre mod alınıyordu, yani fazla
    /// gemiler öndekilerin tam üstüne doğuyordu.
    /// </summary>
    /// <summary>Bir kolu formasyonla doğurur ve dalganın etiketini basar.</summary>
    void SpawnChunk(List<EnemyTypeData> chunk, SpawnSide side, FormationTemplate forced)
    {
        if (chunk.Count == 0) return;
        var formation = forced ?? PickFormation(chunk, _formations);
        SortByFormation(chunk, formation);
        foreach (var bot in SpawnFormation(chunk, formation, side))
            bot.waveTag = _waveSerial;
    }

    /// <summary>Sıradaki kolu zamanı gelince doğurur (bkz. MaxFormationSize).</summary>
    void UpdateStream()
    {
        if (_stream.Count == 0) return;

        _streamTimer -= Time.deltaTime;
        if (_streamTimer > 0f) return;
        _streamTimer = StreamInterval;

        int n     = Mathf.Min(MaxFormationSize, _stream.Count);
        var chunk = _stream.GetRange(0, n);
        _stream.RemoveRange(0, n);
        SpawnChunk(chunk, _streamSide, null);
    }

    List<EnemyBot> SpawnFormation(List<EnemyTypeData> types, FormationTemplate formation, SpawnSide side)
        => EnemySpawner.SpawnFormation(types, formation, SpawnPosition(side),
                                       EnemyScaling.ForLevel(GameProgress.CurrentLevel));

    /// <summary>
    /// Boss'u sahneye koyar. 9. bölümde İKİ tane gelir — tek hedefe kilitlenen
    /// build'i cezalandıran "hedef bölme" sınavı odur.
    /// </summary>
    void SpawnBossesFor(int chapter, BossShipData bossData)
    {
        int count = chapter == 9 ? 2 : 1;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Boss_{bossData.displayName}");
            go.transform.position = new Vector3(ViewBounds.SpawnX + i * 2f,
                                                count == 1 ? 0f : (i == 0 ? 2f : -2f), 0f);
            var boss = go.AddComponent<BossShip>();
            boss.data = bossData;
        }
    }

    // ── Dalga temizlenme bekleme ──────────────────────────────────────────────

    /// <summary>
    /// Dalga temizlendi mi? Yalnızca TEHDİT üreten düşmanlara bakılır
    /// (bkz. EnemyTypeData.BlocksWaveClear): silahsız bir siper gemisinin
    /// ölmesini beklemek, leveli hiçbir şeyin olmadığı bir bekleyişte kilitler.
    ///
    /// İKİ KURAL VAR:
    ///
    /// **Ara dalgalar %90'da geçer** (<see cref="BalanceConfig.waveClearRatio"/>).
    /// Yalnızca BU dalganın etiketli gemileri sayılır; tehditlerinin %90'ı yok
    /// edilince sonraki dalga gelir. Geride kalanlar KAÇMAZ — savaşmaya devam
    /// eder, yeni dalga onların üstüne biner. Son üç gemiyi kovalamak leveli
    /// dakikalarca uzatıyordu; üstelik üst üste binen dalgalar kalabalığın
    /// asıl hissi.
    ///
    /// **Levelin son dalgası TAM temizlenme bekler** — ve o an sahnedeki HER
    /// engelleyici gemi sayılır, hangi dalgadan kalmış olursa olsun. Level
    /// sınırı temiz olmalı: orada kayıt alınıyor, bant çıkıyor, bölüm sonunda
    /// diyalog ekranı açılıyor. Level başı kaydı gemileri tutmadığı için sınırda
    /// sağ kalan bir gemi, kapatıp açarak silinebilen bir kaçış yolu olurdu.
    ///
    /// Etiketsiz gemiler (Bölünen'in parçaları, boss dronları) ara dalga
    /// eşiğine GİRMEZ: parçası doğarken ölen gövde zaten sayımdan düştü, parçalar
    /// da diğer artıklar gibi savaşmaya devam eder. Son dalgada ise hepsi sayılır.
    ///
    /// Geriye yalnızca siperler kaldığında onlara ÇEKİLME emri verilir: koruyacak
    /// bir filo kalmamışsa sahnede durmalarının bir anlamı yok, üstelik dalga
    /// dalga birikip oyuncunun ateş hattını kalıcı olarak kapatırlardı.
    /// </summary>
    void UpdateWaitClear()
    {
        // Geri yükleme sürerken gemiler henüz kuruluyor; yarım bir sahneyi
        // "temiz" saymak dalgayı erkenden geçirirdi.
        if (WorldSave.IsRestoring) return;

        // Kollar hâlâ geliyorsa dalga bitmemiştir — sahnedeki boşluk yalnızca
        // iki kol arasındaki andır.
        if (_stream.Count > 0) return;

        _clearScanTimer -= Time.deltaTime;
        if (_clearScanTimer > 0f) return;
        _clearScanTimer = ClearScanInterval;

        if (FindFirstObjectByType<BossShip>() != null) return;

        var  enemies  = FindObjectsByType<EnemyBot>(FindObjectsSortMode.None);
        bool lastWave = _waveIndex >= _levelWaves.Count - 1;

        if (lastWave)
        {
            foreach (var e in enemies)
                if (e != null && e.data != null && e.data.BlocksWaveClear) return;

            foreach (var e in enemies)
                if (e != null) e.Withdraw();
        }
        else
        {
            float remaining = 0f;
            foreach (var e in enemies)
                if (e != null && e.waveTag == _waveSerial && e.data != null && e.data.BlocksWaveClear)
                    remaining += Mathf.Max(1, e.data.threatScore);

            // Pay kayan noktaya karşı: 10 × (1 − 0.9) float'ta 1'in hemen altına
            // ya da üstüne düşebilir ve kalan TEK Swarm'ın sayılıp sayılmaması
            // yuvarlama hatasına kalırdı.
            float allowed = _waveThreat * (1f - BalanceConfig.Instance.waveClearRatio);
            if (remaining > allowed + 0.001f) return;

            // Biten dalganın SİPERLERİ çekilir, savaşan gemileri değil. Eskiden
            // her dalga sonunda sahne boşaldığı için siperler de çekiliyordu;
            // artık ara dalgalarda kimse çekilmediğinden bir level içinde
            // birikirlerdi — ve üç siper bir DUVAR eder, oyuncunun ateş hattı
            // tamamen kapanır. Yeni dalga kendi siperini getirir. Geride kalan
            // savaşçıların siperini kaybetmesi de %90'a ulaşmanın ödülü.
            foreach (var e in enemies)
                if (e != null && e.waveTag == _waveSerial && e.data != null && !e.data.BlocksWaveClear)
                    e.Withdraw();
        }

        _waveIndex++;
        _phase = Phase.Transition;   // geçici duraksatma

        if (_waveIndex < _levelWaves.Count)
        {
            bool surge = _levelWaves[_waveIndex].isSurge;
            if (surge) LevelBannerUI.ShowSurge();
            Schedule(Pending.BeginWave, surge ? SurgeBreath : WaveBreath);
        }
        else
            CompleteLevel();
    }

    // ── Level / bölüm geçişi ──────────────────────────────────────────────────

    void CompleteLevel()
    {
        _phase = Phase.Transition;

        // Level SÜRESİ ölçülmemiş en pahalı varsayım: asteroit geliri ~3.5
        // dakikalık bir level varsayımına dayanıyor (bkz. Gelir Eğrisi). Gerçek
        // süre saparsa asteroit payı da sapar.
        var ship = FindFirstObjectByType<PlayerShip>();
        var inv  = ResourceInventory.Instance;
        BalanceLog.Event("level_end")
                  .Num("sure",    Time.time - _levelStartedAt)
                  .Num("dalga",   _levelWaves != null ? _levelWaves.Count : 0)
                  .Num("hp",      ship != null ? ship.currentHullHP : -1f)
                  .Num("metal",   inv != null ? inv.metal   : -1f)
                  .Num("kristal", inv != null ? inv.crystal : -1f)
                  .End();

        // Level sınırı doğal gönderim noktası: oyun zaten duruyor ve kayıt
        // tutarlı bir yerde kesiliyor.
        BalanceUploader.Flush();

        if (GameProgress.IsLastLevel)
        {
            _transitionUI?.ShowCredits();
            _phase = Phase.Done;
            CampaignFinished = true;
            return;
        }

        bool chapterEnds = GameProgress.IsBossLevel;
        GameProgress.Advance();

        // Kayıt yalnızca level sınırlarında alınır — savaş ortasında kaydetmek
        // yarım kalmış bir dalgayı geri yüklemeye çalışmak demek olurdu.
        SaveSystem.Save();

        if (chapterEnds)
        {
            // Bölüm değişti — hikâye ve yeni tip tanıtımı için geçiş ekranı
            var next = ChapterFor(GameProgress.CurrentChapter);
            if (_transitionUI != null)
            {
                _pending = Pending.ChapterTransition;
                _transitionUI.Show(next, BeginLevel);
            }
            else
                Schedule(Pending.BeginLevel, 1f);
        }
        else
        {
            // Bölüm içi level geçişi sessizdir: her 10 levelde bir tam durak
            // yeterli, her levelde bir ekran akışı boğar.
            Schedule(Pending.BeginLevel, 2.5f);
        }
    }

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────

    /// <summary>
    /// Dalga kompozisyonunu kuran asıl kural. PUBLIC olmasının tek sebebi
    /// çevrimdışı denge modeli (bkz. CurveModel): model kompozisyonu yeniden
    /// yazsaydı oyunla sessizce ayrışırdı ve tam da ayrıştığı yerde yanlış
    /// sayı üretirdi. Metot rastgelelik içerdiği için model onu defalarca
    /// örnekleyip ortalamasını alır.
    ///
    /// Tip seçimi AĞIRLIKLIDIR: ağırlık = tehdit^(−alfa), bkz.
    /// <see cref="BalanceConfig.compositionAlpha"/>. Eskiden bütçeye sığan
    /// tipler arasından düzgün rastgele seçiliyordu — Swarm ile Kaleci eşit
    /// sıklıkta geliyor, dalganın dokusu yalnızca bütçenin tükenme sırasından
    /// doğuyordu. Düşman statı düzleşip sayı tek para birimi hâline gelince bu
    /// seçim dalganın KARAKTERİNİ belirleyen şey oldu: alfa ucuzların kalabalık
    /// gelmesini sağlarken bütçenin çoğunu pahalı tiplerde tutar.
    /// </summary>
    /// <param name="alpha">
    /// Kompozisyon alfası; NaN = <see cref="BalanceConfig.compositionAlpha"/>.
    /// Cümbüş dalgası kendi alfasını geçer.
    /// </param>
    public static void FillByBudget(List<EnemyTypeData> list, EnemyTypeData[] pool, int budget,
                                    float alpha = float.NaN)
    {
        var cfg = BalanceConfig.Instance;
        if (float.IsNaN(alpha)) alpha = cfg.compositionAlpha;

        // Emniyet sayacı bütçeden TÜREYECEK, sabit olmayacak. En ucuz tip 1
        // tehdit ettiği için bir dalga en fazla `budget` tane gemi üretebilir;
        // sabit 200, düşman statı düzleşip bütçeler yüzlere çıkınca (Lv100'de
        // ~955) dalgayı sessizce kırpardı — hata değil, EKSİK dalga olarak
        // görünürdü. +16 pay, boş-dalga taşması gibi kenar durumlar için.
        int safety = budget + 16;

        while (budget > 0 && safety-- > 0)
        {
            // Refakat gerektiren tipler (siper ve destek gemileri) ancak dalgada
            // koruyacak biri VARSA seçilebilir — yalnız gelen bir bariyer bir
            // olay değil, yalnızca bir gecikmedir. Besleyici ayrıca KALKANLI
            // birini ister (bkz. EnemyTypeData.EscortSatisfied).
            bool hasEscorted = false, hasShielded = false;
            foreach (var t in list)
            {
                if (t == null) continue;
                if (t.CountsAsEscort) hasEscorted = true;
                if (t.maxShield > 0f && !t.IsSupport) hasShielded = true;
            }

            // Bütçeye sığan tipleri filtrele
            var affordable = new List<EnemyTypeData>();
            foreach (var t in pool)
            {
                if (t == null || t.threatScore > budget) continue;
                if (!t.EscortSatisfied(hasEscorted, hasShielded)) continue;
                affordable.Add(t);
            }

            if (affordable.Count == 0)
            {
                // Hiçbir tip bütçeye sığmıyor. Dalga BOŞ kalmamalı: bölümün
                // tanıtım levelinde havuz tek tipe indiriliyor ve o tip
                // bütçeden pahalıysa level hiç düşman üretmiyordu.
                //
                // Bölüm 4 ("Bomba Yağmuru", level 31) tam olarak böyleydi:
                // tanıtılan tip Bomber (tehdit 10), level 31'in en büyük
                // dalgası 6 — üç dalga da boş geçiyor, oyuncu boş bir sahnede
                // bekliyordu ve level kendiliğinden bitiyordu.
                //
                // Bütçeyi bir tip kadar aşmak, boş dalgadan iyidir. Yalnızca
                // dalga HÂLÂ boşken yapılır; içi dolu bir dalgaya taşma eklemek
                // bütçe kavramını anlamsızlaştırırdı.
                if (list.Count == 0)
                {
                    EnemyTypeData cheapest = null;
                    foreach (var t in pool)
                        if (t != null && t.CountsAsEscort &&
                            (cheapest == null || t.threatScore < cheapest.threatScore))
                            cheapest = t;
                    if (cheapest != null) list.Add(cheapest);
                }
                break;
            }

            // AĞIRLIKLI SEÇİM: ağırlık = tehdit^(−alfa) (bkz.
            // BalanceConfig.compositionAlpha). Eskiden düzgün rastgeleydi, yani
            // Swarm ile Kaleci eşit sıklıkta geliyordu ve dalganın dokusu
            // yalnızca bütçenin tükenme sırasından doğuyordu.
            //
            // Ağırlıklar her turda YENİDEN hesaplanır: bütçe azaldıkça uygun
            // küme daralır, bir kez hesaplanmış tablo yanlış kümeye ait olurdu.
            float total = 0f;
            for (int i = 0; i < affordable.Count; i++)
                total += cfg.CompositionWeight(affordable[i].threatScore, alpha);

            var chosen = affordable[affordable.Count - 1];
            float roll = Random.value * total;
            for (int i = 0; i < affordable.Count; i++)
            {
                roll -= cfg.CompositionWeight(affordable[i].threatScore, alpha);
                if (roll <= 0f) { chosen = affordable[i]; break; }
            }

            list.Add(chosen);
            budget -= chosen.threatScore;
        }
    }

    public static FormationTemplate PickFormation(List<EnemyTypeData> enemies,
                                           FormationTemplate[] formations)
    {
        var roles = new HashSet<EnemyRole>();
        foreach (var e in enemies) roles.Add(e.role);

        FormationTemplate best      = formations[0];
        int               bestScore = -1;

        foreach (var f in formations)
        {
            int score = 0;
            foreach (var r in f.preferredRoles)
                if (roles.Contains(r)) score++;
            if (score > bestScore) { bestScore = score; best = f; }
        }

        return best;
    }

    public static void SortByFormation(List<EnemyTypeData> list, FormationTemplate formation)
    {
        var slotRoles = new List<EnemyRole>();
        foreach (var s in formation.slots) slotRoles.Add(s.role);

        list.Sort((a, b) =>
        {
            int ia = slotRoles.IndexOf(a.role);
            int ib = slotRoles.IndexOf(b.role);
            if (ia < 0) ia = 999;
            if (ib < 0) ib = 999;
            return ia.CompareTo(ib);
        });
    }

    static Vector3 SpawnPosition(SpawnSide side)
    {
        switch (side)
        {
            // Sabit sayılar kadrajla ilgisizdi: zoom-out + pan ile görünür alan
            // x ekseninde +32'ye kadar açılıyor, yani 12'de doğan düşman ekranın
            // ORTASINDA yoktan var oluyordu. Kenarlar artık ViewBounds'tan gelir.
            case SpawnSide.Top:    return new Vector3(Random.Range(-8f, 8f), ViewBounds.SpawnYTop,    0f);
            case SpawnSide.Bottom: return new Vector3(Random.Range(-8f, 8f), ViewBounds.SpawnYBottom, 0f);
            case SpawnSide.Left:   return new Vector3(ViewBounds.SpawnXLeft, Random.Range(-3f, 3f),   0f);
            default:               return new Vector3(ViewBounds.SpawnX,     Random.Range(-3f, 3f),   0f);
        }
    }
}
