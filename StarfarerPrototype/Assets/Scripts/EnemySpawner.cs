using UnityEngine;

/// <summary>
/// EnemyTypeData havuzundan ağırlıklı rastgele seçim yaparak düşman spawn eder.
/// typePool atanmamışsa built-in default tipler ve ağırlıkları kullanılır.
/// ChapterManager ileride spawnInterval ve typePool'u dinamik olarak güncelleyecek.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Tooltip("Editörde atanabilir. Boş bırakılırsa built-in default tipler kullanılır.")]
    public EnemyTypeData[] typePool;
    public float[]         typeWeights;
    public float           spawnInterval = 3f;
    public bool            spawning      = true;

    EnemyTypeData[] _defaultPool;
    float[]         _defaultWeights;
    float           _timer;

    void Awake()
    {
        _defaultPool = new[]
        {
            EnemyTypeData.CreateSwarm(),
            EnemyTypeData.CreateArmored(),
            EnemyTypeData.CreateShield(),
            EnemyTypeData.CreateBomber(),
        };
        _defaultWeights = new[] { 0.55f, 0.23f, 0.15f, 0.07f };
    }

    void Update()
    {
        if (!spawning || UpgradeUI.IsPaused) return;

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SpawnEnemy();
        }
    }

    void SpawnEnemy()
    {
        var data = RollType();
        var go   = new GameObject($"EnemyBot_{data.displayName}");
        go.transform.position = new Vector3(12f, Random.Range(-3f, 3f), 0f);
        go.AddComponent<HealthBar>();
        go.AddComponent<EnemyBot>().data = data;
    }

    EnemyTypeData RollType()
    {
        var pool    = (typePool != null && typePool.Length > 0) ? typePool    : _defaultPool;
        var weights = (typePool != null && typePool.Length > 0) ? typeWeights : _defaultWeights;

        float total = 0f;
        foreach (var w in weights) total += w;

        float r   = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < pool.Length; i++)
        {
            acc += weights[i];
            if (r < acc) return pool[i];
        }
        return pool[pool.Length - 1];
    }

    /// ChapterManager tarafından çağrılır: mevcut wave için havuz ve hızı günceller.
    public void SetWaveConfig(EnemyTypeData[] pool, float[] weights, float interval)
    {
        typePool      = pool;
        typeWeights   = weights;
        spawnInterval = interval;
    }
}
