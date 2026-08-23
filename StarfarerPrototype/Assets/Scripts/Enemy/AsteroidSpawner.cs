using UnityEngine;

/// <summary>
/// Asteroit alanının yoğunluğunu korur. Sahada hedeflenen sayıdan az asteroit
/// varsa aralıklarla sağ kenardan yenisini gönderir.
///
/// Sorumluluk ayrımı:
///   ChapterManager  — bölümün yoğunluğu ne olsun (Configure ile bildirir)
///   AsteroidSpawner — o yoğunluğu nasıl koruyacak
///
/// Sayım parçaları da kapsar: bir asteroit bölününce alan zaten dolduğu için
/// yenisi gönderilmez. Asteroitler dalga ilerlemesini engellemez —
/// ChapterManager'ın wave temizlik kontrolü onları saymaz.
/// </summary>
public class AsteroidSpawner : MonoBehaviour
{
    [Tooltip("Sahada aynı anda tutulmaya çalışılan max asteroit (parçalar dahil). 0 = kapalı.")]
    public int targetCount;

    [Tooltip("Yeni büyük asteroit gönderme aralığı (saniye).")]
    public float interval = 12f;

    float _timer;

    // Sahne kenarı ve sürüklenme aralığı — kadrajın sağından gelip sola süzülürler
    const float SpawnX      = 15f;
    const float SpawnYRange = 4.5f;

    /// <summary>Bölüm başında çağrılır. targetCount 0 ise spawner boşta bekler.</summary>
    public void Configure(int count, float spawnInterval)
    {
        targetCount = count;
        interval    = spawnInterval;
    }

    void Update()
    {
        if (targetCount <= 0 || UpgradeUI.IsPaused) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = interval;

        if (FindObjectsByType<Asteroid>(FindObjectsSortMode.None).Length >= targetCount)
            return;

        var pos = new Vector3(SpawnX, Random.Range(-SpawnYRange, SpawnYRange), 0f);
        var drift = new Vector2(Random.Range(-1.3f, -0.6f), Random.Range(-0.25f, 0.25f));
        Asteroid.Spawn(pos, Asteroid.Size.Large, drift);
    }
}
