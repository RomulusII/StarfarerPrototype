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

    enum Phase { Spawning, WaitClear, Transition, Done }
    Phase _phase = Phase.Spawning;

    List<WaveData> _levelWaves = new();
    int            _waveIndex;

    List<EnemyTypeData> _pendingSpawns = new();
    float               _spawnTimer;
    float               _currentSpawnInterval;
    bool                _allSpawned;

    FormationTemplate _currentFormation;
    Vector3           _baseSpawnPos;
    int               _spawnSlotIndex;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        // Serbest mod test aracı; dalga sistemi devredeyken kapalı olmalı
        foreach (var s in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
            s.DisableFreeSpawn();

        _asteroids    = gameObject.AddComponent<AsteroidSpawner>();
        _transitionUI = FindFirstObjectByType<ChapterTransitionUI>();

        _formations = new[]
        {
            FormationTemplate.CreateArrow(),
            FormationTemplate.CreateColumn(),
            FormationTemplate.CreateBroadFront(),
            FormationTemplate.CreateEscort(),
            FormationTemplate.CreateScattered(),
        };

        _chapters = ChapterData.CreateDefaultChapters();

        GameProgress.Reset();
        BeginLevel();
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        switch (_phase)
        {
            case Phase.Spawning:  UpdateSpawning();  break;
            case Phase.WaitClear: UpdateWaitClear(); break;
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
        int level   = GameProgress.CurrentLevel;
        var chapter = ChapterFor(GameProgress.CurrentChapter);
        CurrentChapter = chapter;

        _asteroids?.Configure(chapter.asteroidCount, chapter.asteroidInterval);

        _levelWaves = BuildWaves(level, chapter);
        _waveIndex  = 0;

        BeginWave();
    }

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

        if (GameProgress.IsBossLevel)
        {
            // Escort dalgası, sonra boss + küçük refakat
            int escort = Mathf.RoundToInt(budget * 0.6f);
            waves.Add(Wave(escort, pool, chapter.defaultSpawnInterval));

            var bossWave = Wave(Mathf.RoundToInt(budget * 0.3f), pool,
                                chapter.defaultSpawnInterval);
            bossWave.bossType = chapter.boss;
            waves.Add(bossWave);
            return waves;
        }

        // Normal level: bütçe 2–4 dalgaya bölünür. Geç leveller daha çok dalga
        // görür — tek seferde 40 tehdit puanı boca etmek yığılma yaratır.
        int waveCount = Mathf.Clamp(2 + level / 34, 2, 4);
        for (int i = 0; i < waveCount; i++)
        {
            // Son dalga biraz daha ağır: level kendi zirvesiyle bitsin
            float share = (i == waveCount - 1) ? 1.25f : 1f;
            int   share_ = Mathf.Max(1, Mathf.RoundToInt(budget / waveCount * share));
            waves.Add(Wave(share_, pool, chapter.defaultSpawnInterval));
        }
        return waves;
    }

    static WaveData Wave(int budget, EnemyTypeData[] pool, float interval)
    {
        budget = Mathf.Max(1, budget);
        return new WaveData
        {
            budgetMin     = budget,
            budgetMax     = budget,
            allowedTypes  = pool,
            spawnSide     = SpawnSide.Right,
            spawnInterval = interval,
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

            _currentFormation = wave.formation ?? PickFormation(_pendingSpawns, _formations);
            SortByFormation(_pendingSpawns, _currentFormation);
        }
        else
        {
            _currentFormation = null;
        }

        _baseSpawnPos   = SpawnPosition(wave.spawnSide);
        _spawnSlotIndex = 0;

        _currentSpawnInterval = wave.spawnInterval > 0f
            ? wave.spawnInterval
            : chapter.defaultSpawnInterval;

        _spawnTimer = 0f;
        _allSpawned = _pendingSpawns.Count == 0;
        _phase      = Phase.Spawning;
    }

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
            go.transform.position = new Vector3(14f + i * 2f, count == 1 ? 0f : (i == 0 ? 2f : -2f), 0f);
            var boss = go.AddComponent<BossShip>();
            boss.data = bossData;
        }
    }

    // ── Spawning güncellemesi ─────────────────────────────────────────────────

    void UpdateSpawning()
    {
        if (_allSpawned)
        {
            _phase = Phase.WaitClear;
            return;
        }

        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer > 0f) return;

        _spawnTimer = _currentSpawnInterval;

        if (_pendingSpawns.Count == 0)
        {
            _allSpawned = true;
            return;
        }

        var data = _pendingSpawns[0];
        _pendingSpawns.RemoveAt(0);

        SpawnEnemy(data, _levelWaves[_waveIndex]);
        _spawnSlotIndex++;
    }

    void SpawnEnemy(EnemyTypeData data, WaveData wave)
    {
        Vector3 pos;
        if (_currentFormation != null && _currentFormation.slots.Length > 0)
        {
            int   si   = _spawnSlotIndex % _currentFormation.slots.Length;
            float yOff = _currentFormation.slots[si].offset.y * 2.5f; // -1..1 → ±2.5 birim
            pos = new Vector3(_baseSpawnPos.x, Mathf.Clamp(_baseSpawnPos.y + yOff, -4f, 4f), 0f);
        }
        else
        {
            pos = SpawnPosition(wave.spawnSide);
        }

        // Ölçekleme dahil kurulum EnemySpawner'ın işi — tek inşa yolu orası
        EnemySpawner.Spawn(data, pos);
    }

    // ── Dalga temizlenme bekleme ──────────────────────────────────────────────

    void UpdateWaitClear()
    {
        if (FindFirstObjectByType<EnemyBot>() != null) return;
        if (FindFirstObjectByType<BossShip>() != null) return;

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

        if (GameProgress.IsLastLevel)
        {
            _transitionUI?.ShowCredits();
            _phase = Phase.Done;
            return;
        }

        bool chapterEnds = GameProgress.IsBossLevel;
        GameProgress.Advance();

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
            // Bütçeye sığan tipleri filtrele
            var affordable = new List<EnemyTypeData>();
            foreach (var t in pool)
                if (t != null && t.threatScore <= budget) affordable.Add(t);

            if (affordable.Count == 0) break;

            var chosen = affordable[Random.Range(0, affordable.Count)];
            list.Add(chosen);
            budget -= chosen.threatScore;
        }
    }

    static FormationTemplate PickFormation(List<EnemyTypeData> enemies,
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

    static void SortByFormation(List<EnemyTypeData> list, FormationTemplate formation)
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
            case SpawnSide.Top:    return new Vector3(Random.Range(-8f,  8f),  6f, 0f);
            case SpawnSide.Bottom: return new Vector3(Random.Range(-8f,  8f), -6f, 0f);
            case SpawnSide.Left:   return new Vector3(-14f, Random.Range(-3f, 3f), 0f);
            default:               return new Vector3( 12f, Random.Range(-3f, 3f), 0f);
        }
    }
}
