using UnityEngine;

/// <summary>
/// Her spawnInterval saniyede bir sağ kenarda rastgele Y pozisyonunda düşman spawn eder.
/// Tip ağırlıkları: %55 Swarm, %23 Armored, %15 Shield, %7 Bomber.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    public float spawnInterval = 3f;

    float _timer;

    void Update()
    {
        if (UpgradeUI.IsPaused) return;
        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SpawnEnemy();
        }
    }

    void SpawnEnemy()
    {
        var go = new GameObject("EnemyBot");
        go.transform.position = new Vector3(12f, Random.Range(-3f, 3f), 0f);
        go.AddComponent<HealthBar>();
        var bot = go.AddComponent<EnemyBot>();
        bot.enemyType = RollType();
    }

    static EnemyType RollType()
    {
        float r = Random.value;
        if (r < 0.55f) return EnemyType.Swarm;
        if (r < 0.78f) return EnemyType.Armored;
        if (r < 0.93f) return EnemyType.Shield;
        return EnemyType.Bomber;
    }
}
