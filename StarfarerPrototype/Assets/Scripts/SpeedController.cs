using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun hızını yönetir (1x / 3x / 10x).
/// Time.timeScale'i doğrudan ayarlar — tüm deltaTime tabanlı sistemler otomatik ölçeklenir.
/// Pause (UpgradeUI / GameOver) sırasında hız sıfırlanır; pause bitince seçili hız geri gelir.
/// </summary>
public class SpeedController : MonoBehaviour
{
    public static SpeedController Instance { get; private set; }

    static readonly float[] Speeds = { 1f, 3f, 10f };

    int  _speedIndex = 0;
    bool _paused     = false;

    public float CurrentSpeed => Speeds[_speedIndex];

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── API ───────────────────────────────────────────────────────────────────

    public void SetSpeed(int index)
    {
        if (GameManager.IsGameOver) return;
        _speedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
        if (!_paused) Apply();
        RefreshButtons();
    }

    /// Pause başlarken çağır (UpgradeUI / GameOver)
    public void Pause()
    {
        _paused         = true;
        Time.timeScale  = 0f;
    }

    /// Pause biterken çağır
    public void Resume()
    {
        _paused = false;
        Apply();
    }

    /// Restart sonrası hızı 1x'e sıfırla
    public void Reset()
    {
        _speedIndex    = 0;
        _paused        = false;
        Time.timeScale = 1f;
        RefreshButtons();
    }

    void Apply() => Time.timeScale = Speeds[_speedIndex];

    // ── HUD butonları ─────────────────────────────────────────────────────────

    Button[] _buttons;

    public void RegisterButtons(Button[] buttons)
    {
        _buttons = buttons;
        RefreshButtons();
    }

    void RefreshButtons()
    {
        if (_buttons == null) return;
        for (int i = 0; i < _buttons.Length; i++)
        {
            var img = _buttons[i].GetComponent<Image>();
            if (img != null)
                img.color = i == _speedIndex
                    ? new Color(0.20f, 0.65f, 0.30f)
                    : new Color(0.15f, 0.18f, 0.22f);
        }
    }
}
