using UnityEngine;

/// <summary>
/// Her spawnInterval saniyede bir sağ kenarda rastgele Y pozisyonunda EnemyBot spawn eder.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    public float spawnInterval = 3f;

    float _timer;

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SpawnEnemy();
        }
    }

    void SpawnEnemy()
    {
        GameObject go = new GameObject("EnemyBot");
        go.transform.position = new Vector3(12f, Random.Range(-3f, 3f), 0f);
        go.AddComponent<HealthBar>();
        go.AddComponent<EnemyBot>();
    }
}
