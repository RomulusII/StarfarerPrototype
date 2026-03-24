using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sol fare tuşuna basıldığında ateş eden silah kontrolcüsü.
/// Mermi sprite'ı runtime'da oluşturulur.
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
        {
            Texture2D tex = new Texture2D(4, 4);
            Color[] colors = new Color[16];
            for (int i = 0; i < 16; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            bulletSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f);
        }
    }

    void Update()
    {
        if (Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            SpawnBullet();
        }
    }

    void SpawnBullet()
    {
        GameObject obj = new GameObject("Bullet");
        obj.transform.SetPositionAndRotation(transform.position, transform.rotation);
        obj.transform.localScale = Vector3.one * 0.5f;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = bulletSprite;
        sr.color = Color.cyan;
        sr.sortingOrder = 2;

        Bullet b = obj.AddComponent<Bullet>();
        b.speed = bulletSpeed;
    }
}
