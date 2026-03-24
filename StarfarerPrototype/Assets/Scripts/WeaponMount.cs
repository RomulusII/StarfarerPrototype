using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Geminin ön tarafındaki silah noktası.
/// Her karede mouse dünya pozisyonuna doğru döner.
/// </summary>
public class WeaponMount : MonoBehaviour
{
    void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = new Color(1f, 0.92f, 0f); // sarı
    }

    void Update()
    {
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        Vector2 direction = (Vector2)(mouseWorld - transform.position);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    static Sprite CreateWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
    }
}
