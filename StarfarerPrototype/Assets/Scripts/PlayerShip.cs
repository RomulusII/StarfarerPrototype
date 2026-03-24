using UnityEngine;

/// <summary>
/// Ekranın ortasında sabit duran ana gemi.
/// </summary>
public class PlayerShip : MonoBehaviour
{
    void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.sprite = CreateWhiteSprite();
        sr.color = Color.white;
    }

    static Sprite CreateWhiteSprite()
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
    }
}
