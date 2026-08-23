using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bölüm ve dalga akışını yönetir.
///
/// Akış:
///   BeginChapter → wave döngüsü (SpawnWave → WaitWaveClear) → BeginTransition
///   → diyalog + başlık göster → sonraki bölüme geç
///
/// NE spawn edileceğine bu sınıf karar verir: bütçe, dalga, formasyon, bölüm
/// çarpanları. NASIL kurulacağını bilmez — düşmanı EnemySpawner.Spawn(),
/// asteroit alanını AsteroidSpawner kurar.
///
/// Spawn adedi tükenince veya sahnede hiç EnemyBot/BossShip kalmazsa wave
/// tamamlanmış sayılır. Asteroitler bu sayıma girmez, ilerlemeyi engellemezler.
/// </summary>
public class ChapterManager : MonoBehaviour
{
    // ── Bağımlılıklar ─────────────────────────────────────────────────────────

    AsteroidSpawner       _asteroids;
    ChapterTransitionUI   _transitionUI;

    // ── Veriler ───────────────────────────────────────────────────────────────

    ChapterData[]  _chapters;
    FormationTemplate[] _formations;

    int _chapterIndex;
    int _waveIndex;

    /// <summary>
    /// Oynanmakta olan bölüm. EnemySpawner çarpanları buradan okur, böylece
    /// bölümü bilmeyen çağıranlar (ör. boss drone üretimi) da doğru ölçekte
    /// düşman kurar. Bölüm sistemi yoksa null.
    /// </summary>
    public static ChapterData CurrentChapter { get; private set; }

    // ── Durum ─────────────────────────────────────────────────────────────────

    enum Phase { Spawning, WaitClear, Transition, Done }
    Phase _phase = Phase.Spawning;

    // Wave spawn sayacı
    List<EnemyTypeData> _pendingSpawns = new();
    float               _spawnTimer;
    float               _currentSpawnInterval;
    bool                _allSpawned;

    // Formasyon konumlandırması
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
        BeginChapter(0);
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        switch (_phase)
        {
            case Phase.Spawning:   UpdateSpawning();  break;
            case Phase.WaitClear:  UpdateWaitClear(); break;
        }
    }

    // ── Bölüm başlangıcı ─────────────────────────────────────────────────────

    void BeginChapter(int index)
    {
        if (index >= _chapters.Length) { _phase = Phase.Done; return; }

        _chapterIndex  = index;
        _waveIndex     = 0;

        var chapter    = _chapters[index];
        CurrentChapter = chapter;
        _asteroids?.Configure(chapter.asteroidCount, chapter.asteroidInterval);

        BeginWave();
    }

    // ── Dalga başlangıcı ─────────────────────────────────────────────────────

    void BeginWave()
    {
        var chapter = _chapters[_chapterIndex];

        if (_waveIndex >= chapter.waves.Length)
        {
            BeginTransition();
            return;
        }

        var wave = chapter.waves[_waveIndex];

        // Boss wave ise boss'u hemen spawn et
        if (wave.bossType != null)
            SpawnBoss(wave.bossType);

        // Normal düşman bütçesini doldur (boss wave'de escort olabilir)
        var pool = (wave.allowedTypes != null && wave.allowedTypes.Length > 0)
            ? wave.allowedTypes
            : chapter.defaultEnemyPool;

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

    void SpawnBoss(BossShipData bossData)
    {
        var go = new GameObject($"Boss_{bossData.displayName}");
        go.transform.position = new Vector3(14f, 0f, 0f);
        var boss = go.AddComponent<BossShip>();
        boss.data = bossData;
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

        var data    = _pendingSpawns[0];
        _pendingSpawns.RemoveAt(0);

        SpawnEnemy(data, _chapters[_chapterIndex].waves[_waveIndex]);
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

        // Çarpanlar dahil kurulum EnemySpawner'ın işi — tek inşa yolu orası
        EnemySpawner.Spawn(data, pos, _chapters[_chapterIndex]);
    }

    // ── Dalga temizlenme bekleme ──────────────────────────────────────────────

    void UpdateWaitClear()
    {
        if (FindFirstObjectByType<EnemyBot>()  != null) return;
        if (FindFirstObjectByType<BossShip>()  != null) return;

        _waveIndex++;
        if (_waveIndex < _chapters[_chapterIndex].waves.Length)
        {
            // Waveler arasında kısa nefes (2 saniye)
            StartCoroutine(DelayedBeginWave(2f));
        }
        else
        {
            BeginTransition();
        }

        _phase = Phase.Transition; // geçici olarak duraksatır
    }

    IEnumerator DelayedBeginWave(float delay)
    {
        yield return new WaitForSeconds(delay);
        _phase = Phase.Spawning;
        BeginWave();
    }

    // ── Bölüm geçişi ─────────────────────────────────────────────────────────

    void BeginTransition()
    {
        _phase = Phase.Transition;
        var chapter = _chapters[_chapterIndex];

        // Bir sonraki bölümün başlık ve diyalogunu göster
        int nextIndex = _chapterIndex + 1;
        if (nextIndex >= _chapters.Length)
        {
            _transitionUI?.ShowCredits();
            return;
        }

        var nextChapter = _chapters[nextIndex];
        _transitionUI?.Show(nextChapter, () => BeginChapter(nextIndex));
    }

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────

    static void FillByBudget(List<EnemyTypeData> list, EnemyTypeData[] pool, int budget)
    {
        int safety = 50;
        while (budget > 0 && safety-- > 0)
        {
            // Bütçeye sığan tipleri filtrele
            var affordable = new List<EnemyTypeData>();
            foreach (var t in pool)
                if (t.threatScore <= budget) affordable.Add(t);

            if (affordable.Count == 0) break;

            var chosen = affordable[Random.Range(0, affordable.Count)];
            list.Add(chosen);
            budget -= chosen.threatScore;
        }
    }

    static FormationTemplate PickFormation(List<EnemyTypeData> enemies,
                                           FormationTemplate[] formations)
    {
        // Düşman rollerini topla
        var roles = new HashSet<EnemyRole>();
        foreach (var e in enemies) roles.Add(e.role);

        // En çok tercih edilen rolü kapsayan şablonu seç
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
        // Slot sırasına göre düşmanları sırala: rol önceliği şablondan gelir
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
