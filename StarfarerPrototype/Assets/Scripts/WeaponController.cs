using UnityEngine;

/// <summary>
/// Sol fare tuşuna basıldığında ateş eden silah kontrolcüsü.
/// Mermi sprite'ı Unity'nin built-in "Sprites/Default" kaynağından alınır.
/// </summary>
public class WeaponController : MonoBehaviour
{
    [SerializeField] private float bulletSpeed = 8f;
    [SerializeField] private float fireRate = 0.15f;

    private float nextFireTime;
    private static Sprite bulletSprite;

    void Start()
    {
        if (bulletSprite == null)
            bulletSprite = Resources.GetBuiltinResource<Sprite>("Sprites/Default");
    }

    void Update()
    {
        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            SpawnBullet();
        }
    }

    void SpawnBullet()
    {
        GameObject obj = new GameObject("Bullet");
        obj.transform.SetPositionAndRotation(transform.position, transform.rotation);
        obj.transform.localScale = Vector3.one * 0.15f;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = bulletSprite;
        sr.color = Color.cyan;
        sr.sortingOrder = 2;

        Bullet b = obj.AddComponent<Bullet>();
        b.speed = bulletSpeed;
    }
}
