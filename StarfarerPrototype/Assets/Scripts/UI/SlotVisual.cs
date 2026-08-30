using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// PlayerShip child'ı olan world-space slot göstergesi.
/// UpgradeUI açıkken görünür; slot durumuna göre iki farklı görünüm alır.
///
/// Neden dolu slotlar dolu daire DEĞİL:
///   Kurulu komponent kendi sprite'ını zaten aynı noktada çiziyor
///   (ShipComponentBase.SpawnVisual, sortingOrder -5). Slot göstergesi bunun
///   üstünde (sortingOrder 5) opak yeşil bir daire çizince komponent ikonu
///   tamamen kayboluyordu — bütün slotlar birbirinin aynı yeşil düğmeye
///   dönüyordu. Dolu slot artık yalnızca İNCE BİR HALKA çizer: ikon okunur
///   kalır, halka da tıklanabilirliği ve durumu anlatır.
/// </summary>
public class SlotVisual : MonoBehaviour, IPointerClickHandler
{
    public int  slotIndex;
    public bool isWeaponSlot;

    private SpriteRenderer _sr;
    private ShipLoadout    _loadout;

    // Boş slot: yumuşak dolu daire (buraya bir şey konabilir)
    // Dolu slot: yalnızca halka — ortası şeffaf, komponent sprite'ı görünür
    static readonly Color ColEmpty  = new Color(1f,   1f,    1f,   0.28f);
    static readonly Color ColFilled = new Color(0.35f, 0.95f, 0.40f, 0.55f);
    static readonly Color ColWeapon = new Color(1f,   0.85f, 0f,   0.60f);

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        _sr              = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite       = DiscSprite();
        _sr.sortingOrder = 5;
        _sr.enabled      = false;

        // Tıklama alanı ÇİZİLEN halkayla aynı yarıçapta (64 px / ppu 160 = 0.4
        // birim çap). Eskiden 0.3'tü, yani 0.6 birim çap; slot 1 ile slot 4
        // arası tam 0.6 birim olduğu için iki dairenin kenarları birbirine
        // değiyor ve aradaki piksellerde hangisinin tıklandığı sıralamaya
        // kalıyordu. Gördüğün şey tıkladığın şey olmalı.
        var col        = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger  = true;
        col.radius     = 0.2f;

        // OnMouseDown için Main Camera'da Physics2DRaycaster gerekir
        var cam = Camera.main;
        if (cam != null && cam.GetComponent<PhysicsRaycaster>() == null
                        && cam.GetComponent<Physics2DRaycaster>() == null)
            cam.gameObject.AddComponent<Physics2DRaycaster>();
    }

    void Update()
    {
        bool open = UpgradeUI.IsPaused;
        _sr.enabled = open;
        if (!open) return;

        if (_loadout == null)
            _loadout = FindFirstObjectByType<ShipLoadout>();

        bool isEmpty = !isWeaponSlot &&
                       (_loadout == null || _loadout.IsSlotEmpty(slotIndex));

        // Dolu slotta halka, boş slotta dolu daire. Halkanın ortası şeffaf
        // olduğu için altındaki komponent sprite'ı olduğu gibi görünür.
        _sr.sprite = isEmpty ? DiscSprite() : RingSprite();
        _sr.color  = isWeaponSlot ? ColWeapon
                   : isEmpty      ? ColEmpty
                                  : ColFilled;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!UpgradeUI.IsPaused) return;
        UpgradeUI.Instance?.OnSlotClicked(slotIndex);
    }

    // -------------------------------------------------------------------------
    // Sprite Builder — ikisi de paylaşılır, slot başına doku ayrılmaz
    // -------------------------------------------------------------------------

    static Sprite _disc, _ring;

    static Sprite DiscSprite() => _disc ??= BuildCircle(innerRatio: 0f);

    /// <summary>İçi boş halka — kenarı %72'den başlar, ortası tamamen şeffaf.</summary>
    static Sprite RingSprite() => _ring ??= BuildCircle(innerRatio: 0.72f);

    static Sprite BuildCircle(float innerRatio)
    {
        const int size   = 64;
        var       tex    = new Texture2D(size, size, TextureFormat.RGBA32, false)
                           { filterMode = FilterMode.Bilinear };
        var       px     = new Color32[size * size];
        float     centre = size * 0.5f;
        float     outer  = centre - 1f;
        float     inner  = outer * innerRatio;

        for (int i = 0; i < px.Length; i++)
        {
            float x = (i % size) + 0.5f - centre;
            float y = (i / size) + 0.5f - centre;
            float d = Mathf.Sqrt(x * x + y * y);

            // Dış kenar ve (halka ise) iç kenar 1.5px yumuşatılır
            float a = Mathf.Clamp01(outer - d + 1.5f);
            if (innerRatio > 0f) a = Mathf.Min(a, Mathf.Clamp01(d - inner + 1.5f));
            px[i] = new Color32(255, 255, 255, (byte)(a * 255));
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 160f);
    }
}
