using UnityEngine;

/// <summary>
/// Bir bölümü tamamen tanımlar: dalgalar, zorluk skalası, hikaye ve diyalog.
/// </summary>
[CreateAssetMenu(fileName = "Chapter_01", menuName = "Starfarer/Chapter Data")]
public class ChapterData : ScriptableObject
{
    [Header("Kimlik")]
    public int    chapterNumber;
    public string chapterTitle;
    [TextArea(2, 5)]
    public string storyText;

    [Header("Geçiş Diyalogu (wave bitmeden önce gösterilir)")]
    public DialogueLine[] dialogue;

    [Header("Dalgalar")]
    public WaveData[] waves;

    [Header("Zorluk Parametreleri")]
    [Tooltip("Wave arası varsayılan spawn aralığı (saniye).")]
    public float defaultSpawnInterval = 3f;
    [Tooltip("Tüm düşman HP'lerine uygulanan çarpan.")]
    public float enemyHpMultiplier    = 1f;
    [Tooltip("Tüm düşman hasarına uygulanan çarpan.")]
    public float enemyDamageMultiplier = 1f;

    [Header("Genel Düşman Havuzu (wave'de allowedTypes boşsa kullanılır)")]
    public EnemyTypeData[] defaultEnemyPool;

    // ── Built-in factory: editör olmadan test için ────────────────────────────

    public static ChapterData[] CreateDefaultChapters()
    {
        var swarm   = EnemyTypeData.CreateSwarm();
        var armored = EnemyTypeData.CreateArmored();
        var shield  = EnemyTypeData.CreateShield();
        var bomber  = EnemyTypeData.CreateBomber();

        return new ChapterData[]
        {
            MakeChapter(1,  "Sektör 1: İlk Temas",
                "Araştırma gemimiz sistemleri yeni aktive etti. İlk botlar yaklaşıyor.",
                new[] { swarm }, interval: 4f, hpMult: 1.0f,
                waves: new[]
                {
                    MakeWave(4, 6, new[] { swarm }),
                    MakeWave(4, 6, new[] { swarm }),
                }),

            MakeChapter(2,  "Sektör 2: Devriye",
                "Bot sinyalleri yoğunlaşıyor. Savunmayı güçlendirme zamanı.",
                new[] { swarm }, interval: 3.5f, hpMult: 1.0f,
                waves: new[]
                {
                    MakeWave(5, 7, new[] { swarm }),
                    MakeWave(5, 8, new[] { swarm }),
                    MakeWave(6, 8, new[] { swarm }),
                }),

            MakeChapter(3,  "Sektör 3: Ağır Metaller",
                "Zırhlı birimler tespit edildi. Kinetik silahlarınızı gözden geçirin.",
                new[] { swarm, armored }, interval: 3.5f, hpMult: 1.1f,
                waves: new[]
                {
                    MakeWave(6, 8,  new[] { swarm }),
                    MakeWave(6, 9,  new[] { swarm, armored }),
                    MakeWave(8, 10, new[] { swarm, armored }),
                }),

            MakeChapter(4,  "Sektör 4: Baskı",
                "Kaynaklar kıt. Her kararınız önemli.",
                new[] { swarm, armored }, interval: 3f, hpMult: 1.2f,
                waves: new[]
                {
                    MakeWave(8,  10, new[] { swarm, armored }),
                    MakeWave(9,  11, new[] { swarm, armored }),
                    MakeWave(10, 13, new[] { swarm, armored }),
                }),

            MakeChapter(5,  "Sektör 5: Kalkan Duvarı",
                "Yeni bir bot varyantı — enerji kalkanıyla donatılmış. Lazer işe yaramıyor.",
                new[] { swarm, armored, shield }, interval: 3f, hpMult: 1.3f,
                waves: new[]
                {
                    MakeWave(8,  11, new[] { swarm, shield }),
                    MakeWave(10, 13, new[] { swarm, armored, shield }),
                    MakeWave(12, 15, new[] { swarm, armored, shield }),
                }),

            MakeChapter(6,  "Sektör 6: Karma Saldırı",
                "Tüm bot tipleri koordineli hareket ediyor.",
                new[] { swarm, armored, shield }, interval: 2.8f, hpMult: 1.4f,
                waves: new[]
                {
                    MakeWave(10, 14, new[] { swarm, armored, shield }),
                    MakeWave(12, 16, new[] { swarm, armored, shield }),
                    MakeWave(13, 17, new[] { swarm, armored, shield }),
                    MakeWave(14, 18, new[] { swarm, armored, shield }),
                }),

            MakeChapter(7,  "Sektör 7: Bomba Yağmuru",
                "Yakın mesafe intihar droneları tespit edildi. Bileşenleri koruyun!",
                new[] { swarm, armored, shield, bomber }, interval: 2.5f, hpMult: 1.5f,
                waves: new[]
                {
                    MakeWave(12, 16, new[] { swarm, bomber }),
                    MakeWave(14, 18, new[] { swarm, armored, bomber }),
                    MakeWave(16, 20, new[] { swarm, armored, shield, bomber }),
                    MakeWave(18, 22, new[] { swarm, armored, shield, bomber }),
                }),

            MakeChapter(8,  "Sektör 8: Tam Baskı",
                "Tüm sistemler kritik. Geri çekilme yok.",
                new[] { swarm, armored, shield, bomber }, interval: 2.2f, hpMult: 1.7f,
                waves: new[]
                {
                    MakeWave(18, 22, new[] { swarm, armored, shield, bomber }),
                    MakeWave(20, 25, new[] { swarm, armored, shield, bomber }),
                    MakeWave(22, 27, new[] { swarm, armored, shield, bomber }),
                    MakeWave(24, 28, new[] { swarm, armored, shield, bomber }),
                }),

            MakeChapter(9,  "Sektör 9: Son Hazırlık",
                "Büyük bir sinyal yaklaşıyor. Bu bölümde hazırlıklarınızı tamamlayın.",
                new[] { swarm, armored, shield, bomber }, interval: 2f, hpMult: 1.8f,
                waves: new[]
                {
                    MakeWave(20, 26, new[] { swarm, armored, shield, bomber }),
                    MakeWave(22, 28, new[] { swarm, armored, shield, bomber }),
                    MakeWave(24, 30, new[] { swarm, armored, shield, bomber }),
                    MakeWave(26, 32, new[] { swarm, armored, shield, bomber }),
                    MakeWave(28, 34, new[] { swarm, armored, shield, bomber }),
                }),
        };
    }

    static ChapterData MakeChapter(int num, string title, string story,
        EnemyTypeData[] pool, float interval, float hpMult, WaveData[] waves)
    {
        var c = CreateInstance<ChapterData>();
        c.chapterNumber          = num;
        c.chapterTitle           = title;
        c.storyText              = story;
        c.defaultEnemyPool       = pool;
        c.defaultSpawnInterval   = interval;
        c.enemyHpMultiplier      = hpMult;
        c.enemyDamageMultiplier  = 1f;
        c.waves                  = waves;
        c.dialogue               = new DialogueLine[0];
        return c;
    }

    static WaveData MakeWave(int budMin, int budMax, EnemyTypeData[] types,
        SpawnSide side = SpawnSide.Right)
    {
        return new WaveData
        {
            budgetMin    = budMin,
            budgetMax    = budMax,
            allowedTypes = types,
            spawnSide    = side,
        };
    }
}
