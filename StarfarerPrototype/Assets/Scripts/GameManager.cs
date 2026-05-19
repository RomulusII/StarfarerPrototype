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
    public static bool IsGameOver { get; private set; } = false;
    bool _gameOver = false;
    GameObject _gameOverPanel;
    Button _easyBtn, _normalBtn, _hardBtn;

    void Awake()
    {
        if (FindFirstObjectByType<EnergyBar>() == null)
        {
            var go = new GameObject("EnergyBarHUD");
            go.AddComponent<EnergyBar>();
        }
    }

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

        if (FindFirstObjectByType<EnergyBus>() == null)
        {
            var go = new GameObject("EnergyBus");
            go.AddComponent<EnergyBus>();
        }

        if (FindFirstObjectByType<ResourceInventory>() == null)
        {
            var go = new GameObject("ResourceInventory");
            go.AddComponent<ResourceInventory>();
        }

        EnsureEventSystem();
        BuildGameOverUI();
        BuildUpgradeUI();
        BuildBoostHUD();
        BuildSpeedHUD();
        BuildChapterSystem();
    }

    void BuildSpeedHUD()
    {
        // SpeedController singleton
        var scGO = new GameObject("SpeedController");
        var sc   = scGO.AddComponent<SpeedController>();

        // Canvas — sağ alt köşe
        var canvasGO = new GameObject("SpeedHUDCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        string[] labels  = { "1x", "3x", "10x" };
        var      buttons = new Button[labels.Length];
        float    btnW    = 70f;
        float    btnH    = 36f;
        float    pad     = 6f;
        float    startX  = -(labels.Length * (btnW + pad) - pad) * 0.5f;

        for (int i = 0; i < labels.Length; i++)
        {
            var btnGO  = new GameObject($"Speed_{labels[i]}");
            btnGO.transform.SetParent(canvasGO.transform, false);

            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.15f, 0.18f, 0.22f);

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = img;

            var r = btnGO.GetComponent<RectTransform>();
            r.anchorMin        = new Vector2(1f, 0f);
            r.anchorMax        = new Vector2(1f, 0f);
            r.pivot            = new Vector2(1f, 0f);
            r.sizeDelta        = new Vector2(btnW, btnH);
            r.anchoredPosition = new Vector2(
                -10f - (labels.Length - 1 - i) * (btnW + pad),
                10f);

            MakeText(btnGO.transform, "Label", labels[i], 22, Color.white,
                     Vector2.zero, Vector2.one);

            int captured = i;
            btn.onClick.AddListener(() => sc.SetSpeed(captured));
            buttons[i] = btn;
        }

        sc.RegisterButtons(buttons);
    }

    void BuildChapterSystem()
    {
        if (FindFirstObjectByType<ChapterTransitionUI>() == null)
        {
            var go = new GameObject("ChapterTransitionUI");
            go.AddComponent<ChapterTransitionUI>();
        }

        if (FindFirstObjectByType<ChapterManager>() == null)
        {
            var go = new GameObject("ChapterManager");
            go.AddComponent<ChapterManager>();
        }
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
        _gameOver  = true;
        IsGameOver = true;

        // UpgradeUI açıksa zorla kapat (Tab ile resume'u engellemek için)
        if (UpgradeUI.IsPaused)
        {
            UpgradeUI.IsPaused = false;
            if (UpgradeUI.Instance != null)
                UpgradeUI.Instance.ForceClose();
        }

        SpeedController.Instance?.Pause();
        if (_weaponController != null) _weaponController.enabled = false;
        if (_weaponMount      != null) _weaponMount.enabled      = false;
        _gameOverPanel.SetActive(true);
    }

    public void Restart()
    {
        IsGameOver = false;
        SpeedController.Instance?.Reset();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ── UI Builder ─────────────────────────────────────────────────────────

    void BuildUpgradeUI()
    {
        var upgradeGO = new GameObject("UpgradeCanvas");
        var upgradeUI = upgradeGO.AddComponent<UpgradeUI>();

        if (_playerShip != null && _playerShip.TryGetComponent<ShipLoadout>(out var loadout))
            upgradeUI.SetLoadout(loadout);
    }

    void BuildBoostHUD()
    {
        var go = new GameObject("BoostHUD");
        go.AddComponent<BoostHUD>();
    }

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
        panelRect.sizeDelta = new Vector2(600f, 420f);
        panelRect.anchoredPosition = Vector2.zero;

        // GAME OVER yazısı
        MakeText(_gameOverPanel.transform, "GameOverLabel",
            "GAME OVER",
            fontSize: 80,
            color: new Color(0.92f, 0.12f, 0.12f),
            anchorMin: new Vector2(0f, 0.76f),
            anchorMax: new Vector2(1f, 1f));

        // Zorluk başlığı
        MakeText(_gameOverPanel.transform, "DifficultyLabel",
            "ZORLUK SEÇ",
            fontSize: 22,
            color: new Color(0.7f, 0.7f, 0.7f),
            anchorMin: new Vector2(0f, 0.60f),
            anchorMax: new Vector2(1f, 0.74f));

        // Zorluk butonları
        BuildDifficultyButtons(_gameOverPanel.transform);

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

    void BuildDifficultyButtons(Transform parent)
    {
        var defs = new[]
        {
            (label: "KOLAY",  diff: Difficulty.Easy,   col: new Color(0.12f, 0.55f, 0.18f)),
            (label: "NORMAL", diff: Difficulty.Normal, col: new Color(0.15f, 0.35f, 0.65f)),
            (label: "ZOR",    diff: Difficulty.Hard,   col: new Color(0.65f, 0.12f, 0.12f)),
        };

        float[] xMins = { 0.04f, 0.36f, 0.68f };
        float[] xMaxs = { 0.32f, 0.64f, 0.96f };

        for (int i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            int idx = i;

            var go = new GameObject($"Diff_{d.label}");
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(xMins[idx], 0.38f);
            r.anchorMax = new Vector2(xMaxs[idx], 0.58f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            MakeText(go.transform, "Label", d.label, 28, Color.white,
                     Vector2.zero, Vector2.one);

            var capturedDiff = d.diff;
            var capturedCol  = d.col;
            btn.onClick.AddListener(() => SelectDifficulty(capturedDiff));

            if (d.diff == Difficulty.Easy)   _easyBtn   = btn;
            if (d.diff == Difficulty.Normal) _normalBtn = btn;
            if (d.diff == Difficulty.Hard)   _hardBtn   = btn;
        }

        RefreshDifficultyColors();
    }

    void SelectDifficulty(Difficulty diff)
    {
        DifficultyManager.Current = diff;
        RefreshDifficultyColors();
    }

    static readonly Color ColEasy   = new Color(0.12f, 0.55f, 0.18f);
    static readonly Color ColNormal = new Color(0.15f, 0.35f, 0.65f);
    static readonly Color ColHard   = new Color(0.65f, 0.12f, 0.12f);
    static readonly Color ColInactive = new Color(0.18f, 0.18f, 0.22f);

    void RefreshDifficultyColors()
    {
        if (_easyBtn   == null) return;
        _easyBtn  .GetComponent<Image>().color = DifficultyManager.Current == Difficulty.Easy   ? ColEasy   : ColInactive;
        _normalBtn.GetComponent<Image>().color = DifficultyManager.Current == Difficulty.Normal ? ColNormal : ColInactive;
        _hardBtn  .GetComponent<Image>().color = DifficultyManager.Current == Difficulty.Hard   ? ColHard   : ColInactive;
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
        btnRect.anchorMin = new Vector2(0.2f, 0.06f);
        btnRect.anchorMax = new Vector2(0.8f, 0.32f);
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
