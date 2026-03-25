using UnityEngine;

/// <summary>
/// Her 3 saniyede bir sağ kenarda rastgele Y pozisyonunda EnemyBot spawn eder.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyBotPrefab;
    public float spawnInterval = 3f;

    float _timer;

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            if (enemyBotPrefab != null)
            {
                float y = Random.Range(-3f, 3f);
                Instantiate(enemyBotPrefab, new Vector3(12f, y, 0f), Quaternion.identity);
            }
        }
    }
}
