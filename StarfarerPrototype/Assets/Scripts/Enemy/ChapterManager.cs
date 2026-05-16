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
/// EnemySpawner'a SetWaveConfig() ile komut verir; spawn adedi tükenince
/// veya sahnede hiç EnemyBot kalmazsa wave tamamlanmış sayılır.
/// </summary>
public class ChapterManager : MonoBehaviour
{
    // ── Bağımlılıklar ─────────────────────────────────────────────────────────

    EnemySpawner          _spawner;
    ChapterTransitionUI   _transitionUI;

    // ── Veriler ───────────────────────────────────────────────────────────────

    ChapterData[]  _chapters;
    FormationTemplate[] _formations;

    int _chapterIndex;
    int _waveIndex;

    // ── Durum ─────────────────────────────────────────────────────────────────

    enum Phase { Spawning, WaitClear, Transition, Done }
    Phase _phase = Phase.Spawning;

    // Wave spawn sayacı
    List<EnemyTypeData> _pendingSpawns = new();
    float               _spawnTimer;
    float               _currentSpawnInterval;
    bool                _allSpawned;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        _spawner = FindFirstObjectByType<EnemySpawner>();
        if (_spawner != null) _spawner.spawning = false;   // kendi yönetiyoruz

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

        _chapterIndex = index;
        _waveIndex    = 0;
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

        // Düşman listesini bütçeye göre doldur
        var pool = (wave.allowedTypes != null && wave.allowedTypes.Length > 0)
            ? wave.allowedTypes
            : chapter.defaultEnemyPool;

        _pendingSpawns.Clear();
        FillByBudget(_pendingSpawns, pool,
            Random.Range(wave.budgetMin, wave.budgetMax + 1));

        // Formasyona göre sırala (aynı roller yan yana spawn olsun)
        var formation = wave.formation ?? PickFormation(_pendingSpawns, _formations);
        SortByFormation(_pendingSpawns, formation);

        _currentSpawnInterval = wave.spawnInterval > 0f
            ? wave.spawnInterval
            : chapter.defaultSpawnInterval;

        _spawnTimer  = 0f;
        _allSpawned  = false;
        _phase       = Phase.Spawning;
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
    }

    void SpawnEnemy(EnemyTypeData data, WaveData wave)
    {
        var chapter  = _chapters[_chapterIndex];

        // HP ve hasar çarpanları uygulanır (ScriptableObject asset'ini bozmamak için
        // runtime'da geçici bir kopya oluşturulur)
        var d = Instantiate(data);
        d.maxHP         = data.maxHP         * chapter.enemyHpMultiplier;
        d.maxShield     = data.maxShield     * chapter.enemyHpMultiplier;
        d.fireDamage    = data.fireDamage    * chapter.enemyDamageMultiplier;
        d.contactDamage = data.contactDamage * chapter.enemyDamageMultiplier;

        var pos  = SpawnPosition(wave.spawnSide);
        var go   = new GameObject($"EnemyBot_{data.displayName}");
        go.transform.position = pos;
        go.AddComponent<HealthBar>();
        go.AddComponent<EnemyBot>().data = d;
    }

    // ── Dalga temizlenme bekleme ──────────────────────────────────────────────

    void UpdateWaitClear()
    {
        if (FindFirstObjectByType<EnemyBot>() != null) return;

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
