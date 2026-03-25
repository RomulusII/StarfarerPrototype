using UnityEngine;

/// <summary>
/// Ekranın ortasında sabit duran ana gemi.
/// SpriteRenderer sahnede kayıtlıdır; Awake'te texture oluşturulup atanır.
/// </summary>
public class PlayerShip : MonoBehaviour
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
        sr.color = new Color(0.3f, 0.3f, 0.4f); // koyu mavi-gri, uzay gemisi hissi
        sr.sortingOrder = 0;
    }
}
