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

    readonly List<EnemyTypeData> _pendingSpawns = new();

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
        BeginLevel();
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (_phase == Phase.WaitClear) UpdateWaitClear();
    }

    // ── Level kurulumu ────────────────────────────────────────────────────────

    ChapterData ChapterFor(int chapterNumber)
    {
        int idx = Mathf.Clamp(chapterNumber - 1, 0, _chapters.Length - 1);
        return _chapters[idx];
    }

    void BeginLevel()
    {
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
        foreach (int waveBudget in cfg.SplitWaveBudget(budget, WaveCountFor(level)))
            waves.Add(Wave(waveBudget, pool));

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

        if (wave.bossType != null)
            SpawnBossesFor(GameProgress.CurrentChapter, wave.bossType);

        var pool = (wave.allowedTypes != null && wave.allowedTypes.Length > 0)
            ? wave.allowedTypes
            : chapter.enemyPool;

        _pendingSpawns.Clear();
        if (pool != null && pool.Length > 0 && wave.budgetMax > 0)
        {
            FillByBudget(_pendingSpawns, pool,
                Random.Range(wave.budgetMin, wave.budgetMax + 1));

            // Bölümün tanıtılan tipi bütçeye sığmadıysa bir tane zorla eklenir
            // (bkz. BuildWaves). Bütçeyi bir tip kadar aşmak, bölümün kimliğini
            // hiç göstermemekten iyidir — boş dalga kuralıyla aynı gerekçe.
            if (wave.guaranteedType != null && !_pendingSpawns.Contains(wave.guaranteedType))
                _pendingSpawns.Add(wave.guaranteedType);

            var formation = wave.formation ?? PickFormation(_pendingSpawns, _formations);
            SortByFormation(_pendingSpawns, formation);

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
                      .End();

            SpawnFormation(_pendingSpawns, formation, wave.spawnSide);
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
    void SpawnFormation(List<EnemyTypeData> types, FormationTemplate formation, SpawnSide side)
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
    /// Geriye yalnızca siperler kaldığında onlara ÇEKİLME emri verilir: koruyacak
    /// bir filo kalmamışsa sahnede durmalarının bir anlamı yok, üstelik dalga
    /// dalga birikip oyuncunun ateş hattını kalıcı olarak kapatırlardı.
    /// </summary>
    void UpdateWaitClear()
    {
        if (FindFirstObjectByType<BossShip>() != null) return;

        var enemies = FindObjectsByType<EnemyBot>(FindObjectsSortMode.None);
        foreach (var e in enemies)
            if (e != null && e.data != null && e.data.BlocksWaveClear) return;

        foreach (var e in enemies)
            if (e != null) e.Withdraw();

        _waveIndex++;
        _phase = Phase.Transition;   // geçici duraksatma

        if (_waveIndex < _levelWaves.Count)
            StartCoroutine(DelayedBeginWave(2f));
        else
            CompleteLevel();
    }

    IEnumerator DelayedBeginWave(float delay)
    {
        yield return new WaitForSeconds(delay);
        BeginWave();
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
                _transitionUI.Show(next, BeginLevel);
            else
                StartCoroutine(DelayedBeginLevel(1f));
        }
        else
        {
            // Bölüm içi level geçişi sessizdir: her 10 levelde bir tam durak
            // yeterli, her levelde bir ekran akışı boğar.
            StartCoroutine(DelayedBeginLevel(2.5f));
        }
    }

    IEnumerator DelayedBeginLevel(float delay)
    {
        yield return new WaitForSeconds(delay);
        BeginLevel();
    }

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────

    static void FillByBudget(List<EnemyTypeData> list, EnemyTypeData[] pool, int budget)
    {
        int safety = 200;
        while (budget > 0 && safety-- > 0)
        {
            // Refakat gerektiren tipler (siper gemileri) ancak dalgada koruyacak
            // biri VARSA seçilebilir — yalnız gelen bir bariyer bir olay değil,
            // yalnızca bir gecikmedir.
            bool hasEscorted = false;
            foreach (var t in list)
                if (t != null && !t.RequiresEscort) { hasEscorted = true; break; }

            // Bütçeye sığan tipleri filtrele
            var affordable = new List<EnemyTypeData>();
            foreach (var t in pool)
            {
                if (t == null || t.threatScore > budget) continue;
                if (t.RequiresEscort && !hasEscorted) continue;
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
                        if (t != null && !t.RequiresEscort &&
                            (cheapest == null || t.threatScore < cheapest.threatScore))
                            cheapest = t;
                    if (cheapest != null) list.Add(cheapest);
                }
                break;
            }

            var chosen = affordable[Random.Range(0, affordable.Count)];
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
