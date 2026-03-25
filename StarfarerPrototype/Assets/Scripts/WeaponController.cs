using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sol fare tuşuna basıldığında mermi ateşler.
/// Bullet görsel: 10x30px texture (ince uzun mermi), uniform scale (1,1,1).
/// </summary>
public class WeaponController : MonoBehaviour
{
    [SerializeField] private float bulletSpeed = 10f;
    [SerializeField] private float fireRate = 0.15f;

    private float nextFireTime;
    private Sprite bulletSprite;

    void Start()
    {
        if (bulletSprite == null)
        {
            // 10x30 px → ppu 100 → dünya boyutu 0.1 x 0.3 birim
            Texture2D tex = new Texture2D(10, 30);
            Color[] pixels = new Color[10 * 30];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            bulletSprite = Sprite.Create(tex, new Rect(0, 0, 10, 30), new Vector2(0.5f, 0.5f), 100f);
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
        obj.transform.localScale = Vector3.one;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = bulletSprite;
        sr.color = Color.cyan;
        sr.sortingOrder = 2;

        Bullet b = obj.AddComponent<Bullet>();
        b.speed = bulletSpeed;
    }
}
