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
    [Tooltip("Metin tablosu anahtarı (chapter.N.title), ekrana yazılacak başlık değil.")]
    public string chapterTitle;
    [Tooltip("Metin tablosu anahtarı (chapter.N.story).")]
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
        var barrier     = EnemyTypeData.CreateBarrier();
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
            Make(1,
                introduced: swarm,
                pool: new[] { swarm },
                asteroids: 2),

            Make(2,
                introduced: armored,
                pool: new[] { swarm, armored, barrier },
                asteroids: 2),

            Make(3,
                introduced: shield,
                pool: new[] { swarm, armored, shield, barrier },
                asteroids: 3),

            Make(4,
                introduced: bomber,
                pool: new[] { swarm, armored, shield, barrier, bomber, bombRunner },
                asteroids: 3),

            Make(5,
                introduced: interceptor,
                pool: new[] { swarm, armored, shield, barrier, bomber, interceptor },
                asteroids: 3),

            Make(6,
                introduced: artillery,
                pool: new[] { swarm, armored, shield, barrier, interceptor, artillery },
                asteroids: 4),

            Make(7,
                introduced: jammer,
                pool: new[] { swarm, armored, interceptor, artillery, jammer, phantom },
                asteroids: 4),

            Make(8,
                introduced: regenerator,
                pool: new[] { swarm, armored, shield, barrier, interceptor, jammer, regenerator, leech },
                asteroids: 4),

            Make(9,
                introduced: splitter,
                pool: new[] { swarm, armored, interceptor, artillery, phantom, regenerator, splitter },
                asteroids: 4),

            Make(10,
                introduced: juggernaut,
                pool: new[] { armored, shield, barrier, interceptor, artillery, jammer,
                              phantom, regenerator, splitter, juggernaut },
                asteroids: 4),
        };
    }

    /// <summary>
    /// Başlık ve hikaye metni bölüm numarasından türeyen tablo anahtarlarıdır
    /// (<c>chapter.N.title</c> / <c>chapter.N.story</c>); metinler
    /// Resources/Locale/strings.txt'te durur. Burada tutulsalardı bölümler
    /// statik olarak bir kez kurulduğu için dil değişimini göremezlerdi.
    /// </summary>
    static ChapterData Make(int number,
        EnemyTypeData introduced, EnemyTypeData[] pool, int asteroids)
    {
        var c = CreateInstance<ChapterData>();
        c.chapterNumber        = number;
        c.chapterTitle         = $"chapter.{number}.title";
        c.storyText            = $"chapter.{number}.story";
        c.introducedType       = introduced;
        c.enemyPool            = pool;
        c.boss                 = BossShipData.CreateForChapter(number);
        c.asteroidCount        = asteroids;
        c.asteroidInterval     = 14f;
        c.dialogue             = new DialogueLine[0];
        return c;
    }
}
