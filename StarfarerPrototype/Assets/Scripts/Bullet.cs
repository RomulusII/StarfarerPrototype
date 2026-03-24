using UnityEngine;

/// <summary>
/// Ateşlenen mermi. Kendi yerel yukarı yönünde ileri gider,
/// 3 saniye sonra otomatik olarak yok olur.
/// </summary>
public class Bullet : MonoBehaviour
{
    public float speed = 8f;

    void Start()
    {
        Destroy(gameObject, 3f);
    }

    void Update()
    {
        transform.Translate(Vector3.up * speed * Time.deltaTime, Space.Self);
    }
}
