using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Oyun durumunu yönetir: PlayerShip'in HealthBar'ını takip eder,
/// currentHealth <= 0 olunca Game Over ekranı gösterir.
/// Sahneye boş GameObject olarak eklenir.
/// </summary>
public class GameManager : MonoBehaviour
{
    HealthBar _playerHealth;
    PlayerShip _playerShip;
    WeaponController _weaponController;
    WeaponMount _weaponMount;
    bool _gameOver = false;
    GameObject _gameOverPanel;

    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            _playerHealth     = player.GetComponent<HealthBar>();
            _playerShip       = player.GetComponent<PlayerShip>();
            _weaponController = player.GetComponentInChildren<WeaponController>();
            _weaponMount      = player.GetComponentInChildren<WeaponMount>();
        }

        EnsureEventSystem();
        BuildGameOverUI();
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();
    }

    void Update()
    {
        if (_gameOver) return;

        if (_playerShip != null && !_playerShip.IsAlive)
            TriggerGameOver();
    }

    void TriggerGameOver()
    {
        _gameOver = true;
        Time.timeScale = 0f;
        if (_weaponController != null) _weaponController.enabled = false;
        if (_weaponMount      != null) _weaponMount.enabled      = false;
        _gameOverPanel.SetActive(true);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ── UI Builder ─────────────────────────────────────────────────────────

    void BuildGameOverUI()
    {
        // Canvas — Screen Space Overlay
        var canvasGO = new GameObject("GameOverCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Koyu yarı saydam panel — ekran ortasında
        _gameOverPanel = new GameObject("GameOverPanel");
        _gameOverPanel.transform.SetParent(canvasGO.transform, false);

        var panelImg = _gameOverPanel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.78f);

        var panelRect = _gameOverPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot    = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(600f, 320f);
        panelRect.anchoredPosition = Vector2.zero;

        // GAME OVER yazısı
        MakeText(_gameOverPanel.transform, "GameOverLabel",
            "GAME OVER",
            fontSize: 80,
            color: new Color(0.92f, 0.12f, 0.12f),
            anchorMin: new Vector2(0f, 0.52f),
            anchorMax: new Vector2(1f, 1f));

        // RESTART butonu
        MakeRestartButton(_gameOverPanel.transform);

        _gameOverPanel.SetActive(false);
    }

    void MakeText(Transform parent, string objName, string content,
                  int fontSize, Color color,
                  Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(parent, false);

        var txt = go.AddComponent<Text>();
        txt.text      = content;
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.fontStyle = FontStyle.Bold;
        txt.color     = color;
        txt.alignment = TextAnchor.MiddleCenter;

        var r = go.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    void MakeRestartButton(Transform parent)
    {
        // Buton arka planı
        var btnGO = new GameObject("RestartButton");
        btnGO.transform.SetParent(parent, false);

        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        var cols = btn.colors;
        cols.normalColor      = new Color(0.18f, 0.18f, 0.18f, 1f);
        cols.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        cols.pressedColor     = new Color(0.10f, 0.10f, 0.10f, 1f);
        btn.colors = cols;

        btn.onClick.AddListener(Restart);

        var btnRect = btnGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.2f, 0.08f);
        btnRect.anchorMax = new Vector2(0.8f, 0.42f);
        btnRect.offsetMin = Vector2.zero;
        btnRect.offsetMax = Vector2.zero;

        // Buton yazısı
        MakeText(btnGO.transform, "Label",
            "RESTART",
            fontSize: 44,
            color: Color.white,
            anchorMin: Vector2.zero,
            anchorMax: Vector2.one);
    }
}
