using UnityEngine;

/// <summary>
/// Sahnede 200 adet rastgele sabit yıldız oluşturur.
/// Küçük beyaz noktalar; bir kısmı transparan olarak derinlik hissi verir.
/// </summary>
public class StarField : MonoBehaviour
{
    [SerializeField] private int starCount = 400;
    [SerializeField] private float areaWidth = 30f;
    [SerializeField] private float areaHeight = 30f;

    void Awake()
    {
        Sprite starSprite = CreateStarSprite();

        for (int i = 0; i < starCount; i++)
        {
            float x = Random.Range(-areaWidth * 0.5f, areaWidth * 0.5f);
            float y = Random.Range(-areaHeight * 0.5f, areaHeight * 0.5f);
            float size = Random.Range(0.02f, 0.05f);

            // Yıldızların %40'ı transparan (derinlik hissi), geri kalanı tam görünür
            float alpha = Random.value < 0.4f ? Random.Range(0.3f, 0.6f) : 1f;

            GameObject star = new GameObject("Star");
            star.transform.SetParent(transform, false);
            star.transform.position = new Vector3(x, y, 0f);
            star.transform.localScale = Vector3.one * size;

            SpriteRenderer sr = star.AddComponent<SpriteRenderer>();
            sr.sprite = starSprite;
            sr.color = new Color(1f, 1f, 1f, alpha);
            sr.sortingOrder = -1;
        }
    }

    static Sprite CreateStarSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f);
    }
}
