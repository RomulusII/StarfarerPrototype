using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Geminin sağ tarafında mouse'a dönen silah noktası.
/// Görsel: "WeaponVisual" child objesinde 20x80px texture (ince uzun namlu).
/// Uniform scale — proporsiyon texture boyutundan gelir.
/// </summary>
public class WeaponMount : MonoBehaviour
{
    void Awake()
    {
        // 20x80 px → ppu 100 → dünya boyutu 0.2 x 0.8 birim (ince, uzun namlu)
        Texture2D tex = new Texture2D(20, 80);
        Color[] pixels = new Color[20 * 80];
        Color barrelColor = new Color(1f, 0.92f, 0f); // sarı
        for (int i = 0; i < pixels.Length; i++) pixels[i] = barrelColor;
        tex.SetPixels(pixels);
        tex.Apply();

        GameObject visual = new GameObject("WeaponVisual");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one;

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        // Pivot alt-merkez: namlu WeaponMount noktasından yukarı uzanır
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 20, 80), new Vector2(0.5f, 0f), 100f);
        sr.sortingOrder = 1;
    }

    void Update()
    {
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(
            new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        Vector2 direction = (Vector2)(mouseWorld - transform.position);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
