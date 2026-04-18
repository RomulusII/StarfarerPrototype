using UnityEngine;

/// <summary>
/// PlayerShip child'ı olan world-space slot göstergesi.
/// UpgradeUI açıkken daire sprite gösterir; renk, slot durumuna göre değişir.
/// </summary>
public class SlotVisual : MonoBehaviour
{
    public int  slotIndex;
    public bool isWeaponSlot;

    private SpriteRenderer  _sr;
    private ShipLoadout     _loadout;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        _sr             = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite      = CreateCircleSprite();
        _sr.sortingOrder = 5;
        _sr.enabled     = false;

        var col        = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger  = true;
        col.radius     = 0.3f;
    }

    void Update()
    {
        bool open = UpgradeUI.IsPaused;
        _sr.enabled = open;
        if (!open) return;

        if (_loadout == null)
            _loadout = FindFirstObjectByType<ShipLoadout>();

        bool isEmpty = _loadout == null || _loadout.IsSlotEmpty(slotIndex);

        if (isWeaponSlot)
            _sr.color = new Color(1f, 0.85f, 0f, 0.9f);       // sarı
        else if (isEmpty)
            _sr.color = new Color(1f, 1f, 1f, 0.4f);           // yarı transparan beyaz
        else
            _sr.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);    // yeşil
    }

    void OnMouseDown()
    {
        if (!UpgradeUI.IsPaused) return;
        if (UpgradeUI.Instance != null)
            UpgradeUI.Instance.OnSlotClicked(slotIndex);
    }

    // -------------------------------------------------------------------------
    // Sprite Builder
    // -------------------------------------------------------------------------

    static Sprite CreateCircleSprite()
    {
        const int size   = 32;
        var       tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var       pixels = new Color[size * size];
        var       center = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
        float     radius = size / 2f - 1f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dist = Vector2.Distance(new Vector2(x, y), center);
            pixels[y * size + x] = dist <= radius ? Color.white : Color.clear;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
