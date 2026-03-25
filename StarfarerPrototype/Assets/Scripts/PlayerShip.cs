using UnityEngine;

/// <summary>
/// Ekranın alt bölgesinde sabit duran ana gemi.
/// Görsel: "Body" child objesinde 400x100px texture (4:1 yatay gemi gövdesi).
/// Uniform scale — proporsiyon texture boyutundan gelir.
/// </summary>
public class PlayerShip : MonoBehaviour
{
    void Awake()
    {
        // 400x100 px → ppu 100 → dünya boyutu 4 x 1 birim
        Texture2D tex = new Texture2D(400, 100);
        Color[] pixels = new Color[400 * 100];
        Color shipColor = new Color(0.3f, 0.3f, 0.4f);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = shipColor;
        tex.SetPixels(pixels);
        tex.Apply();

        GameObject body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = Vector3.one;

        SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 400, 100), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 0;
    }
}
