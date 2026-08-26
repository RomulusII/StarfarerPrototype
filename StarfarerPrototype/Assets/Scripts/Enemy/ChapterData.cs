using UnityEngine;

/// <summary>
/// Bir bölümün TEMASI: hangi düşmanlar sahnede, hangi yeni tip tanıtılıyor,
/// hangi boss kapatıyor, hikâye ne.
///
/// ZORLUK TAŞIMAZ. Eskiden her bölümde elle yazılmış wave dizileri ve sabit
/// HP/hasar çarpanları vardı; 10 bölüm için idare edilebilirdi, 100 level için
/// edilemez. Sayısal ölçekleme artık <see cref="LevelCurve"/>'den, wave'ler
/// <see cref="BalanceConfig.ThreatBudget"/>'ten üretilir.
///
/// Bölüm = 10 level. Bölümün son leveli boss levelidir.
/// </summary>
[CreateAssetMenu(fileName = "Chapter_01", menuName = "Starfarer/Chapter Data")]
public class ChapterData : ScriptableObject
{
    [Header("Kimlik")]
    public int    chapterNumber;
    public string chapterTitle;
    [TextArea(2, 5)]
    public string storyText;

    [Header("Geçiş Diyalogu")]
    public DialogueLine[] dialogue;

    [Header("İçerik")]
    [Tooltip("Bu bölümde sahaya çıkabilecek tipler.")]
    public EnemyTypeData[] enemyPool;

    [Tooltip("Bu bölümün tanıttığı yeni tip. İlk levelde tek başına gelir ki " +
             "oyuncu davranışını öğrenebilsin.")]
    public EnemyTypeData introducedType;

    [Tooltip("Bölümün son levelinde spawn olan boss.")]
    public BossShipData boss;

    [Header("Tempo")]
    [Tooltip("Wave içindeki spawn aralığı (saniye).")]
    public float defaultSpawnInterval = 3f;

    [Tooltip("Sahada aynı anda tutulmaya çalışılan asteroit sayısı.")]
    public int   asteroidCount    = 3;
    public float asteroidInterval = 14f;

    // ── Built-in bölümler ─────────────────────────────────────────────────────

    /// <summary>
    /// On bölüm. Her biri bir oyuncu sistemine baskı yapan yeni bir tip getirir;
    /// boss o sistemin sınavıdır.
    /// </summary>
    public static ChapterData[] CreateDefaultChapters()
    {
        var swarm       = EnemyTypeData.CreateSwarm();
        var armored     = EnemyTypeData.CreateArmored();
        var shield      = EnemyTypeData.CreateShield();
        var bomber      = EnemyTypeData.CreateBomber();
        var bombRunner  = EnemyTypeData.CreateBombRunner();
        var interceptor = EnemyTypeData.CreateInterceptor();
        var artillery   = EnemyTypeData.CreateArtillery();
        var jammer      = EnemyTypeData.CreateJammer();
        var phantom     = EnemyTypeData.CreatePhantom();
        var regenerator = EnemyTypeData.CreateRegenerator();
        var leech       = EnemyTypeData.CreateLeech();
        var splitter    = EnemyTypeData.CreateSplitter();
        var juggernaut  = EnemyTypeData.CreateJuggernaut();

        return new[]
        {
            Make(1, "Sektör 1: İlk Temas",
                "Araştırma gemimiz sistemleri yeni aktive etti. İlk botlar yaklaşıyor.",
                introduced: swarm,
                pool: new[] { swarm },
                interval: 4f, asteroids: 2),

            Make(2, "Sektör 2: Devriye Hattı",
                "Zırhlı birimler tespit edildi. Raylı toplar bu gövdeleri delmiyor.",
                introduced: armored,
                pool: new[] { swarm, armored },
                interval: 3.6f, asteroids: 2),

            Make(3, "Sektör 3: Kalkan Duvarı",
                "Enerji kalkanıyla donatılmış yeni bir varyant. Lazer kalkanda eriyor.",
                introduced: shield,
                pool: new[] { swarm, armored, shield },
                interval: 3.4f, asteroids: 3),

            Make(4, "Sektör 4: Bomba Yağmuru",
                "Yakın mesafe bombardımanı. Komponentleriniz doğrudan hedef alınıyor — " +
                "Point Defence artık lüks değil.",
                introduced: bomber,
                pool: new[] { swarm, armored, shield, bomber, bombRunner },
                interval: 3.2f, asteroids: 3),

            Make(5, "Sektör 5: Avcı Sürüsü",
                "Küçük, hızlı, kaçamak. Turretleriniz nişan alamıyor.",
                introduced: interceptor,
                pool: new[] { swarm, armored, shield, bomber, interceptor },
                interval: 3f, asteroids: 3),

            Make(6, "Sektör 6: Uzun Menzil",
                "Menzil dışından dövülüyoruz. Beklemek ölüm, ilerlemek şart.",
                introduced: artillery,
                pool: new[] { swarm, armored, shield, interceptor, artillery },
                interval: 2.9f, asteroids: 4),

            Make(7, "Sektör 7: Karartma",
                "Sinyal karışıyor, reaktör düşüyor. Bazı gemiler hiç vurulmuyor.",
                introduced: jammer,
                pool: new[] { swarm, armored, interceptor, artillery, jammer, phantom },
                interval: 2.8f, asteroids: 4),

            Make(8, "Sektör 8: Onarım Kovanı",
                "Vurduğunuz her şey geri geliyor. Yetersiz hasar, hiç hasar demek.",
                introduced: regenerator,
                pool: new[] { swarm, armored, shield, interceptor, jammer, regenerator, leech },
                interval: 2.7f, asteroids: 4),

            Make(9, "Sektör 9: Bölünen Sürü",
                "Öldürdükçe çoğalıyorlar. Tek hedefe odaklanmak artık işe yaramıyor.",
                introduced: splitter,
                pool: new[] { swarm, armored, interceptor, artillery, phantom, regenerator, splitter },
                interval: 2.6f, asteroids: 4),

            Make(10, "Sektör 10: Kovan Zihni",
                "Sinyalin kaynağı. Bütün bu tasarımlar tek bir yerden çıkıyordu.",
                introduced: juggernaut,
                pool: new[] { armored, shield, interceptor, artillery, jammer,
                              phantom, regenerator, splitter, juggernaut },
                interval: 2.5f, asteroids: 4),
        };
    }

    static ChapterData Make(int number, string title, string story,
        EnemyTypeData introduced, EnemyTypeData[] pool, float interval, int asteroids)
    {
        var c = CreateInstance<ChapterData>();
        c.chapterNumber        = number;
        c.chapterTitle         = title;
        c.storyText            = story;
        c.introducedType       = introduced;
        c.enemyPool            = pool;
        c.boss                 = BossShipData.CreateForChapter(number);
        c.defaultSpawnInterval = interval;
        c.asteroidCount        = asteroids;
        c.asteroidInterval     = 14f;
        c.dialogue             = new DialogueLine[0];
        return c;
    }
}
