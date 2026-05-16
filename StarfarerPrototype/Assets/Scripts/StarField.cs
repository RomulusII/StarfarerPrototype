using UnityEngine;

/// <summary>
/// İki katmanlı paralaks yıldız alanı. Gemi sağa ilerliyor, yıldızlar sola kayıyor.
/// Yıldızlar sol kenardan çıkınca sağdan yeniden girer — sonsuz döngü.
/// Arka katman (sönük, yavaş) → derinlik, ön katman (parlak, hızlı) → yakınlık hissi.
/// </summary>
public class StarField : MonoBehaviour
{
    [SerializeField] private int   starCount    = 400;
    [SerializeField] private float areaWidth    = 36f;
    [SerializeField] private float areaHeight   = 14f;
    [SerializeField] private float backSpeed    = 0.4f;   // arka katman
    [SerializeField] private float frontSpeed   = 1.1f;   // ön katman

    struct StarData
    {
        public Transform       tr;
        public SpriteRenderer  sr;
        public float           speed;
        public float           baseAlpha;
    }

    StarData[] _stars;
    float      _halfW;
    float      _halfH;

    void Awake()
    {
        _halfW = areaWidth  * 0.5f;
        _halfH = areaHeight * 0.5f;

        var sprite = CreateStarSprite();
        _stars = new StarData[starCount];

        for (int i = 0; i < starCount; i++)
        {
            bool isFront = i >= starCount / 2;

            float x     = Random.Range(-_halfW, _halfW);
            float y     = Random.Range(-_halfH, _halfH);
            float size  = isFront
                ? Random.Range(0.03f, 0.06f)
                : Random.Range(0.015f, 0.035f);
            float alpha = isFront
                ? Random.Range(0.6f, 1.0f)
                : Random.Range(0.2f, 0.5f);

            var go = new GameObject("Star");
            go.transform.SetParent(transform, false);
            go.transform.position   = new Vector3(x, y, 0f);
            go.transform.localScale = Vector3.one * size;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.color        = new Color(1f, 1f, 1f, alpha);
            sr.sortingOrder = isFront ? -1 : -2;

            _stars[i] = new StarData
            {
                tr        = go.transform,
                sr        = sr,
                speed     = isFront ? frontSpeed : backSpeed,
                baseAlpha = alpha,
            };
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        foreach (ref var s in System.MemoryExtensions.AsSpan(_stars))
        {
            var pos = s.tr.position;
            pos.x -= s.speed * dt;

            // Sol sınırı geçince sağdan girer
            if (pos.x < -_halfW)
                pos.x += areaWidth;

            s.tr.position = pos;
        }
    }

    static Sprite CreateStarSprite()
    {
        var tex     = new Texture2D(4, 4);
        var colors  = new Color[16];
        for (int i = 0; i < 16; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 1f);
    }
}
