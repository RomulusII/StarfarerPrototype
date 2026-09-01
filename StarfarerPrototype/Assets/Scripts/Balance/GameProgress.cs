using UnityEngine;

/// <summary>
/// Oyuncunun kampanyadaki yeri. 100 level, 10 bölüm, her bölümün 10. leveli boss.
///
/// Tek gerçek sayı <see cref="CurrentLevel"/>'dır; bölüm ondan türer. Ölçekleme
/// ve gelir formülleri bölümü değil leveli okur — böylece zorluk bölüm sınırında
/// sıçramaz, sürekli akar.
///
/// ChapterManager bu değeri ilerletir. Serbest mod dokunmaz (kendi rampası var).
/// </summary>
public static class GameProgress
{
    public static int LevelsPerChapter => Mathf.Max(1, LevelCurve.Instance.levelsPerChapter);
    public static int TotalLevels      => Mathf.Max(1, LevelCurve.Instance.totalLevels);

    static int _currentLevel = 1;

    /// <summary>1..TotalLevels arası. Ölçekleme ve gelir bunu okur.</summary>
    public static int CurrentLevel
    {
        get => _currentLevel;
        set => _currentLevel = Mathf.Clamp(value, 1, TotalLevels);
    }

    /// <summary>1..10.</summary>
    public static int CurrentChapter => (_currentLevel - 1) / LevelsPerChapter + 1;

    /// <summary>Bölüm içindeki sıra: 1..10.</summary>
    public static int LevelInChapter => (_currentLevel - 1) % LevelsPerChapter + 1;

    /// <summary>Bölümün son leveli mi? Boss burada gelir.</summary>
    public static bool IsBossLevel => LevelInChapter == LevelsPerChapter;

    public static bool IsLastLevel => _currentLevel >= TotalLevels;

    public static void Reset()   => _currentLevel = 1;
    public static void Advance() => CurrentLevel  = _currentLevel + 1;

    /// <summary>Verilen levelin ait olduğu bölüm (1 tabanlı).</summary>
    public static int ChapterOf(int level) => (Mathf.Max(1, level) - 1) / LevelsPerChapter + 1;
}
