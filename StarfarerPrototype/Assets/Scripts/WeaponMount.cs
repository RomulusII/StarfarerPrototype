using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Geminin ön tarafındaki silah noktası.
/// SpriteRenderer sahnede kayıtlıdır; Awake'te texture oluşturulup atanır.
/// Her karede mouse dünya pozisyonuna doğru döner.
/// </summary>
public class WeaponMount : MonoBehaviour
{
    void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();

        Texture2D tex = new Texture2D(4, 4);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f);
        sr.color = new Color(1f, 0.92f, 0f); // sarı
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
