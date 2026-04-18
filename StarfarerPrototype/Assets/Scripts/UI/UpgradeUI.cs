using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// Tab tuşuyla oyunu durduran ve world-space SlotVisual'ları aktif eden yönetici.
/// Canvas hiyerarşisi yoktur; durum takibi IsPaused ve _canvas.enabled üzerinden yapılır.
/// </summary>
public class UpgradeUI : MonoBehaviour
{
    public static UpgradeUI Instance { get; private set; }
    public static bool IsPaused = false;

    private Canvas      _canvas;
    private ShipLoadout _loadout;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    void Awake()
    {
        Instance = this;
        BuildCanvas();
        _canvas.enabled = false;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            _canvas.enabled = !_canvas.enabled;
            IsPaused        = _canvas.enabled;
            Time.timeScale  = _canvas.enabled ? 0f : 1f;
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void SetLoadout(ShipLoadout loadout) => _loadout = loadout;

    public void OnSlotClicked(int slotIndex) =>
        Debug.Log($"Slot {slotIndex} clicked");

    // -------------------------------------------------------------------------
    // Canvas Builder
    // -------------------------------------------------------------------------

    void BuildCanvas()
    {
        _canvas              = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 20;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
    }
}
