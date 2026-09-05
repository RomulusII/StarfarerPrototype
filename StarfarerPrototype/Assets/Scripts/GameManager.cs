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

    void Awake()
    {
        // Kadraj önbelleği statiktir; sahne yeniden yüklenince (ölüm → restart)
        // hayatta kalır ve eski en-boy oranıyla hesaplanmış kalırdı.
        ViewBounds.Invalidate();

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
        BuildEnemyInfoHUD();

        // Simülasyon koşusunda menü YOK: koşunun bütün seçimleri komut
        // satırından geldi (bkz. SimConfig) ve batchmode'da tıklayacak kimse
        // yok. Menüyü kurup programatik tıklamak, ölçülen şeye menü akışını
        // da katardı.
        if (SimRuntime.Active) { StartCoroutine(BeginSimRun()); return; }

        // Oyun açılış menüsünden başlar; seçim yapılana kadar bölüm sistemi
        // kurulmaz, dolayısıyla arkada düşman spawn olmaz.
        StartMenuUI.Show(BeginGame);
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

    /// <summary>Menüdeki seçime göre oyunu başlatır.</summary>
    void BeginGame(StartMenuUI.GameMode mode)
    {
        if (mode == StartMenuUI.GameMode.FreePlay)
        {
            BeginFreePlay();
            return;
        }

        // Kaydı uygulamak ShipLoadout.Start()'tan SONRA olmalı — yoksa
        // başlangıç donanımı kaydın üstüne kurulur. Bir kare beklemek yeterli.
        StartCoroutine(BeginCampaign(mode));
    }

    /// <summary>
    /// Simülasyon koşusu: kayıt yüklenmez (koşu temiz bir gemiyle başlamalı,
    /// yoksa iki koşu farklı donanımla kıyaslanır) ve başlangıç leveli
    /// menüden değil koşu yapılandırmasından gelir.
    /// </summary>
    System.Collections.IEnumerator BeginSimRun()
    {
        yield return null;   // ShipLoadout.Start() bu karede çalışır

        GameProgress.CurrentLevel = SimRuntime.Config.startLevel;
        BuildChapterSystem();
    }

    System.Collections.IEnumerator BeginCampaign(StartMenuUI.GameMode mode)
    {
        yield return null;   // ShipLoadout.Start() bu karede çalışır

        if (mode == StartMenuUI.GameMode.Continue)
        {
            if (!SaveSystem.Apply(SaveSystem.Load()))
                GameProgress.Reset();   // kayıt bozuksa baştan başla
        }
        else
        {
            // Yeni oyun: seçilen levelden başla, eski kaydın üstüne yazılacak
            GameProgress.CurrentLevel = StartMenuUI.SelectedStartLevel;
        }

        BuildChapterSystem();
    }

    void BuildChapterSystem()
    {
        // Bölüm geçiş ekranı simülasyonda KURULMAZ: mürettebat diyaloğu
        // saniyelerce akan bir anlatım ve koşuya ölçülecek hiçbir şey katmaz.
        // Kurulmadığında ChapterManager 1 saniyelik sade gecikmeye düşer.
        if (!SimRuntime.Active && FindFirstObjectByType<ChapterTransitionUI>() == null)
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

    /// <summary>
    /// Serbest mod: bölüm sistemi kurulmaz, EnemySpawner'ın test modu açılır.
    /// Bölüm çarpanı olmadığı için düşmanlar ham (ölçeklenmemiş) gelir;
    /// belirli bir levelin zorluğunu test etmek için spawner'ın debugLevel
    /// alanı Inspector'dan doldurulabilir.
    /// </summary>
    void BeginFreePlay()
    {
        var spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null)
        {
            var go  = new GameObject("EnemySpawner");
            spawner = go.AddComponent<EnemySpawner>();
        }

        spawner.debugFreeSpawn = true;
        BalanceLog.Begin("serbest");
        BalanceUploader.EnsureExists();
    }

    // Kayıt tamponu diske ancak kapanışta boşalır. Editörde Play'den çıkmak
    // OnApplicationQuit tetikler; bu olmadan son satırlar kaybolurdu.
    void OnApplicationQuit() => BalanceLog.Close();
    void OnDisable()         => BalanceLog.Close();

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

    void BuildEnemyInfoHUD()
    {
        var go = new GameObject("EnemyInfoHUD");
        go.AddComponent<EnemyInfoHUD>();
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
        // Zorluk seçimi kaldırıldıktan sonra panel kısaldı (420 -> 280).
        panelRect.sizeDelta = new Vector2(600f, 280f);
        panelRect.anchoredPosition = Vector2.zero;

        // GAME OVER yazısı
        MakeText(_gameOverPanel.transform, "GameOverLabel",
            "GAME OVER",
            fontSize: 80,
            color: new Color(0.92f, 0.12f, 0.12f),
            anchorMin: new Vector2(0f, 0.52f),
            anchorMax: new Vector2(1f, 1f));

        // ZORLUK SEÇİMİ BURADA YOK. RESTART açılış menüsüne döner ve zorluk
        // orada zaten seçiliyor: aynı kararı iki ekranda sormak, ikincisinin
        // seçimini bir sonraki ekranda tekrar değiştirilebilir kılıyordu.
        // Zorluğun tek sahibi StartMenuUI'dır.

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
        btnRect.anchorMin = new Vector2(0.2f, 0.10f);
        btnRect.anchorMax = new Vector2(0.8f, 0.45f);
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
