using UnityEngine;

/// <summary>
/// Sürekli derinlikli uzay tozu. Gemi sağa ilerliyor, tozlar sola kayıyor;
/// sol kenardan çıkan sağdan yeniden giriyor — sonsuz döngü.
///
/// DERİNLİK SÜREKLİDİR, iki katman değil. Her zerre 0 (uzak) .. 1 (yakın)
/// arasında bir derinlik çeker ve boyutu, parlaklığı, hızı, render sırası
/// hep o TEK sayıdan türer. İki ayrı katman "ön" ve "arka" diye ikiye
/// bölünmüş bir alan üretiyordu; sürekli dağılım gerçek bir hacim hissi verir.
///
/// Derinliğin üç etkisi birbirini destekler:
///   yakın  → daha BÜYÜK, çok daha SİLİK, daha HIZLI (odak dışı, göz önünden
///            geçip giden toz)
///   uzak   → çok küçük, daha yavaş; çoğu sönük ama bir kısmı belirgin parlak
///            (uzak yıldızlar)
///
/// Yakının silik olması bir stil tercihi değil zorunluluk: büyük ve opak
/// zerreler gemiyle mermilerin okunmasını bozar. Silik olunca hem derinlik
/// verir hem oynanışın önüne geçmez.
///
/// ALAN <see cref="ViewBounds"/>'tan gelir. Sabit 36×14'lük alan, zoom-out +
/// pan ile +32'ye kadar açılan kadrajın yalnızca yarısını kaplıyordu: sağa
/// bakınca toz aniden bitiyordu.
/// </summary>
public class StarField : MonoBehaviour
{
    [Tooltip("Birim kare başına zerre. Sayı DEĞİL yoğunluk yazılır — alan " +
             "kadrajdan türediği için sabit sayı, geniş ekranda alanı seyreltirdi.")]
    [SerializeField] private float density = 0.75f;

    [Tooltip("Emniyet tavanı: patolojik bir kadrajda GameObject sayısı patlamasın.")]
    [SerializeField] private int maxMotes = 1600;

    [Tooltip("Görünür alanın dışına taşan pay. Kenardan giren zerre kadrajın " +
             "içinde belirmesin diye.")]
    [SerializeField] private float areaPadding = 4f;

    [Header("Derinlik → boyut (dünya birimi, çap)")]
    [SerializeField] private float farSize  = 0.035f;
    [SerializeField] private float nearSize = 0.16f;

    [Header("Derinlik → hız (birim/sn)")]
    [SerializeField] private float farSpeed  = 0.22f;
    [SerializeField] private float nearSpeed = 1.5f;

    [Header("Parlaklık")]
    [Tooltip("Yakın zerrelerin alfası — kasten çok düşük.")]
    [SerializeField] private Vector2 nearAlpha = new Vector2(0.05f, 0.13f);
    [Tooltip("Uzak zerrelerin olağan alfası.")]
    [SerializeField] private Vector2 farAlpha  = new Vector2(0.18f, 0.45f);
    [Tooltip("Uzak zerrelerin bu kadarı 'parlak yıldız' olur.")]
    [Range(0f, 1f)] [SerializeField] private float brightStarChance = 0.14f;
    [SerializeField] private Vector2 brightAlpha = new Vector2(0.7f, 1f);

    struct Mote
    {
        public Transform tr;
        public float     speed;
    }

    Mote[] _motes;
    Rect   _area;

    void Awake()
    {
        var v = ViewBounds.Visible;
        _area = Rect.MinMaxRect(v.xMin - areaPadding, v.yMin - areaPadding,
                                v.xMax + areaPadding, v.yMax + areaPadding);

        int count = Mathf.Clamp(
            Mathf.RoundToInt(_area.width * _area.height * density), 1, maxMotes);

        var sprite = MoteSprite();
        _motes = new Mote[count];

        for (int i = 0; i < count; i++)
        {
            // Derinlik karesel dağıtılır: çoğu zerre UZAK olsun. Düz dağılımda
            // ekranın yarısı iri silik lekelerle doluyor ve gökyüzü sisleniyor.
            float t = Random.value * Random.value;

            var go = new GameObject("Mote");
            go.transform.SetParent(transform, false);
            go.transform.position   = new Vector3(Random.Range(_area.xMin, _area.xMax),
                                                  Random.Range(_area.yMin, _area.yMax), 0f);
            go.transform.localScale = Vector3.one * Mathf.Lerp(farSize, nearSize, t);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color  = new Color(1f, 1f, 1f, AlphaFor(t));

            // Gemi gövdesi -10'da. Toz her şeyin ARKASINDA kalmalı; eski
            // -1 / -2 değerleri tozu geminin ÖNÜNE çiziyordu.
            sr.sortingOrder = -100 + Mathf.RoundToInt(t * 20f);

            _motes[i] = new Mote { tr = go.transform, speed = Mathf.Lerp(farSpeed, nearSpeed, t) };
        }
    }

    /// <summary>
    /// Yakın zerre her zaman silik. Uzak zerre çoğunlukla sönük ama küçük bir
    /// kısmı parlak — bunlar toz değil, arkadaki yıldızlar.
    /// </summary>
    float AlphaFor(float depth)
    {
        if (depth > 0.5f) return Mathf.Lerp(farAlpha.x, nearAlpha.y, depth);

        bool bright = Random.value < brightStarChance;
        var  range  = bright ? brightAlpha : farAlpha;
        return Random.Range(range.x, range.y);
    }

    void Update()
    {
        float dt    = Time.deltaTime;
        float width = _area.width;

        foreach (ref var m in System.MemoryExtensions.AsSpan(_motes))
        {
            var pos = m.tr.position;
            pos.x -= m.speed * dt;
            if (pos.x < _area.xMin) pos.x += width;
            m.tr.position = pos;
        }
    }

    // ── Sprite ────────────────────────────────────────────────────────────────

    static Sprite _sprite;

    /// <summary>
    /// Yumuşak kenarlı YUVARLAK zerre. Eskiden 4×4 düz beyaz kareydi ve küçük
    /// boyutta okunaklı bir kare gibi görünüyordu. ppu = çözünürlük olduğu için
    /// sprite tam 1 dünya birimidir: localScale doğrudan ÇAP demektir.
    /// </summary>
    static Sprite MoteSprite()
    {
        if (_sprite != null) return _sprite;

        const int res = 16;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
                  { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px     = new Color32[res * res];
        float c    = res * 0.5f;
        float rad  = c - 0.5f;

        for (int i = 0; i < px.Length; i++)
        {
            float dx = (i % res) + 0.5f - c;
            float dy = (i / res) + 0.5f - c;
            float d  = Mathf.Sqrt(dx * dx + dy * dy) / rad;

            // Merkezde dolu, kenara doğru yumuşak sönüm — küçük ölçekte de
            // yuvarlak okunsun diye kare bir kesim yerine gradyan.
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a);   // smoothstep
            px[i] = new Color32(255, 255, 255, (byte)(a * 255));
        }

        tex.SetPixels32(px);
        tex.Apply();
        _sprite = Sprite.Create(tex, new Rect(0, 0, res, res), Vector2.one * 0.5f, res);
        return _sprite;
    }
}
