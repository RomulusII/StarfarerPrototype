using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Geminin sağ tarafında mouse'a dönen silah noktası.
/// Görsel: "WeaponVisual" child objesinde 20x80px texture (ince uzun namlu).
/// Uniform scale — proporsiyon texture boyutundan gelir.
/// </summary>
public class WeaponMount : MonoBehaviour
{
    const int BarrelPx  = 80;
    const int BarrelPpu = 100;

    /// <summary>
    /// Namlu ucunun mount noktasına uzaklığı (dünya birimi). Mermiler ve ışın
    /// buradan çıkar — namlunun içinden değil. Sprite de bu değerden türetilir,
    /// böylece görsel ile çıkış noktası birbirinden ayrı düşemez.
    /// </summary>
    public const float BarrelLength = (float)BarrelPx / BarrelPpu;

    void Awake()
    {
        // 20x80 px → ppu 100 → dünya boyutu 0.2 x 0.8 birim (ince, uzun namlu)
        GameObject visual = new GameObject("WeaponVisual");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one;

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        // Pivot alt-merkez: namlu WeaponMount noktasından yukarı uzanır.
        // Skin gelirse de bu pivotu korumalı — BarrelLength buna bağlı.
        sr.sprite = SkinLibrary.Get(SkinId.PlayerBarrel, 20, BarrelPx,
                        new Color(1f, 0.92f, 0f), new Vector2(0.5f, 0f), BarrelPpu);
        sr.sortingOrder = 1;
    }

    void Update()
    {
        if (PointerInput.Locked) return;

        // Fare YOKSA (Android) namlu son yönünde kalır — eskiden Mouse.current
        // kontrolsüz okunuyordu ve telefonda ilk karede patlıyordu.
        if (!PointerInput.TryPosition(out Vector2 pointerScreen)) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(pointerScreen.x, pointerScreen.y, 0f));
        Vector2 direction = (Vector2)(mouseWorld - transform.position);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
