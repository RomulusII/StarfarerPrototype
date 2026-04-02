using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas tabanlı enerji HUD barı. Screen Space Overlay.
/// Awake()'te tüm hiyerarşiyi kod ile oluşturur.
/// EnergyBus.Instance üzerinden enerji oranını okur.
/// </summary>
public class EnergyBar : MonoBehaviour
{
    RectTransform _fillRect;

    void Awake()
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        // ── EnergyBarBg ───────────────────────────────────────────────────────
        var bgGO  = new GameObject("EnergyBarBg");
        bgGO.transform.SetParent(transform, false);

        var bgImg  = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.2f, 0f, 0.8f);

        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin        = new Vector2(0f, 1f);
        bgRect.anchorMax        = new Vector2(0f, 1f);
        bgRect.pivot            = new Vector2(0f, 1f);
        bgRect.anchoredPosition = new Vector2(20f, -20f);
        bgRect.sizeDelta        = new Vector2(300f, 20f);

        // ── EnergyBarFill ─────────────────────────────────────────────────────
        var fillGO = new GameObject("EnergyBarFill");
        fillGO.transform.SetParent(bgGO.transform, false);

        var fillImg  = fillGO.AddComponent<Image>();
        fillImg.color = new Color(1f, 0.7f, 0.1f, 1f);

        _fillRect               = fillGO.GetComponent<RectTransform>();
        _fillRect.anchorMin     = new Vector2(0f, 0f);
        _fillRect.anchorMax     = new Vector2(0f, 1f);
        _fillRect.pivot         = new Vector2(0f, 0.5f);
        _fillRect.anchoredPosition = Vector2.zero;
        _fillRect.sizeDelta     = new Vector2(300f, 0f);
    }

    void Update()
    {
        float ratio = 0f;
        if (EnergyBus.Instance != null && EnergyBus.Instance.maxEnergy > 0f)
            ratio = Mathf.Clamp01(EnergyBus.Instance.currentEnergy / EnergyBus.Instance.maxEnergy);

        _fillRect.sizeDelta = new Vector2(300f * ratio, 0f);
    }
}
