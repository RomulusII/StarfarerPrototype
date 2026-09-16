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

    // Oyunun hangi moddan başlatıldığı: menüye dönüşte ve ölümde HANGİ kaydın
    // yazılacağını/silineceğini belirler.
    StartMenuUI.GameMode _mode;
    bool                 _modeChosen;

    public bool IsFreePlay =>
        _modeChosen && (_mode == StartMenuUI.GameMode.FreePlay ||
                        _mode == StartMenuUI.GameMode.FreeContinue);

    // Panel açılış menüsünden ÖNCE kurulur, yani metinleri dil seçilmeden
    // yazılır; gösterildiği anda tazelenirler (bkz. TriggerGameOver).
    Text _gameOverLabel, _restartLabel;

    void Awake()
    {
        // Mobilde Unity kare hızını varsayılan olarak 30'a sabitler. Bu bir
        // shmup: nişan almak ve kaçamak manevra kare hızına doğrudan bağlı,
        // 30'da oyun ağır hissediyor. Değer AÇIKÇA yazılır — "varsayılan neyse
        // o" hâli, ölçülen performans sayılarını (bkz. PerfSampler) hangi
        // hedefe göre okuyacağımızı da belirsiz bırakıyordu.
        //
        // Masaüstünde vSync bunu zaten ezer; orada etkisi yoktur.
        Application.targetFrameRate = 60;

        // Kadraj önbelleği statiktir; sahne yeniden yüklenince (ölüm → restart)
        // hayatta kalır ve eski en-boy oranıyla hesaplanmış kalırdı.
        ViewBounds.Invalidate();

        // Boost modu da statik. Menüye dönüp yeni oyuna başlayan oyuncu önceki
        // oyunun boost'uyla başlıyordu; kayıttan devam eden ise onu zaten
        // kayıttan alır.
        BoostController.Restore(BoostMode.None);

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
        _mode       = mode;
        _modeChosen = true;

        if (IsFreePlay)
        {
            StartCoroutine(BeginFreePlay(mode));
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
        // Kaydet/yükle testi (bkz. SaveRoundTrip): sahne yeniden yüklendiğinde
        // koşu kayıttan DEVAM eder — menüdeki "Devam Et" ile aynı yoldan, ki
        // test oyuncunun kullandığı kodu sınasın.
        bool free = SimRuntime.Config.saveTest == "serbest";
        if (SaveRoundTrip.AwaitingRestore)
        {
            BeginGame(free ? StartMenuUI.GameMode.FreeContinue : StartMenuUI.GameMode.Continue);
            yield break;
        }
        if (free)
        {
            BeginGame(StartMenuUI.GameMode.FreePlay);
            yield break;
        }

        yield return null;   // ShipLoadout.Start() bu karede çalışır

        GameProgress.CurrentLevel = SimRuntime.Config.startLevel;
        BuildChapterSystem();
    }

    System.Collections.IEnumerator BeginCampaign(StartMenuUI.GameMode mode)
    {
        yield return null;   // ShipLoadout.Start() bu karede çalışır

        if (mode == StartMenuUI.GameMode.Continue)
        {
            // Önce dünya kaydı (level ortası, tam sahne), yoksa level başı kaydı.
            // Dünya kaydı varsa her zaman daha yenidir: level sınırında yazılan
            // kayıt onu siliyor.
            var world = WorldSave.Load(WorldSave.Slot.Campaign);
            if (world != null && SaveSystem.ApplyShip(world.ship))
            {
                GameProgress.CurrentLevel = world.level;
                WorldSave.RestoreWorld(world);

                // Bölüm sistemi kaydın ANINA döner, leveli baştan kurmaz
                ChapterManager.PendingRestore = world.chapter;
                ChapterManager.PendingField   = world.asteroidField;
            }
            else if (!SaveSystem.Apply(SaveSystem.Load()))
                GameProgress.Reset();   // kayıt bozuksa baştan başla
        }
        else
        {
            // Yeni oyun: eski kayıt menüde onaylanarak bırakıldı. Hemen silinir,
            // ilk level sonunu beklemez — yoksa o ana kadar menüye dönen oyuncu
            // "Devam Et"te sildiğini sandığı eski kampanyayı bulurdu.
            // Ulaşılmış en yüksek level (level seçimi) SİLİNMEZ.
            SaveSystem.Delete();
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
    System.Collections.IEnumerator BeginFreePlay(StartMenuUI.GameMode mode)
    {
        var spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null)
        {
            var go  = new GameObject("EnemySpawner");
            spawner = go.AddComponent<EnemySpawner>();
        }

        if (mode == StartMenuUI.GameMode.FreeContinue)
        {
            // Gemi kaydı ShipLoadout.Start()'tan SONRA uygulanmalı — kampanyadaki
            // devamla aynı gerekçe (bkz. BeginCampaign).
            yield return null;

            var world = WorldSave.Load(WorldSave.Slot.Free);
            if (world != null && SaveSystem.ApplyShip(world.ship))
            {
                WorldSave.RestoreWorld(world);
                spawner.ResumeFreeRun(world.free, world.asteroidField);
            }
        }
        else
        {
            // Yeni serbest oyun: eski kayıt menüde onaylanarak bırakıldı.
            SaveSystem.DeleteFree();
        }

        // Rampa geri yüklemesi bu bayrak açılmadan ÖNCE verilmiş olmalı:
        // spawner koşuyu bayrağı gördüğü ilk karede kurar.
        spawner.debugFreeSpawn = true;
        BalanceLog.Begin("serbest");
        BalanceUploader.EnsureExists();
        PerfSampler.EnsureExists();
    }

    // Kayıt tamponu diske ancak kapanışta boşalır. Editörde Play'den çıkmak
    // OnApplicationQuit tetikler; bu olmadan son satırlar kaybolurdu.
    void OnApplicationQuit()
    {
        SaveWorldIfPlaying();
        BalanceLog.Close();
    }
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

        // Ölüm DÜNYA kaydını siler. Kalsaydı "Devam Et" ölümden hemen önceki ana
        // döndürür, ölüm de bedeli olmayan bir geri sarmaya dönerdi. Serbest
        // koşu burada biter; kampanyada level BAŞI kaydı yerinde kalır — oradaki
        // ceza "son tamamlanan levele dön" olarak tasarlandı.
        if (_modeChosen)
            WorldSave.Delete(IsFreePlay ? WorldSave.Slot.Free : WorldSave.Slot.Campaign);

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

        if (_gameOverLabel != null) _gameOverLabel.text = Loc.T("gameover.title");
        if (_restartLabel  != null) _restartLabel.text  = Loc.T("gameover.restart");

        _gameOverPanel.SetActive(true);
    }

    public void Restart()
    {
        IsGameOver = false;
        SpeedController.Instance?.Reset();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Upgrade ekranındaki ANA MENÜ düğmesi. Sahne yeniden yüklenir, yani
    /// açılış menüsüne ölümdeki RESTART ile AYNI yoldan dönülür — ikinci bir
    /// "oyunu sök" yolu yazılsaydı sıfırlanmayı unutulan bir statik (yetim
    /// kalkan havuzu hatası tam böyle doğmuştu) er geç sızardı.
    ///
    /// Dünya olduğu gibi kaydedilir (bkz. WorldSave) — her iki modda.
    /// </summary>
    public void ReturnToMenu()
    {
        if (IsGameOver) return;

        SaveWorldIfPlaying();

        // IsPaused statiktir ve sahne yüklemesinden sağ çıkar; kapatılmazsa
        // yeni oyunda spawner "upgrade ekranı açık" sanıp hiç dalga göndermezdi.
        if (UpgradeUI.IsPaused)
        {
            UpgradeUI.IsPaused = false;
            if (UpgradeUI.Instance != null) UpgradeUI.Instance.ForceClose();
        }

        Restart();
    }

    // Telefonda uygulama arka plana atılıp oradan kapatılabilir; o yolda ANA
    // MENÜ düğmesine hiç basılmaz. Dünya burada da yazılır.
    void OnApplicationPause(bool paused)
    {
        if (paused) SaveWorldIfPlaying();
    }

    /// <summary>
    /// Oynanan modun dünyasını yazar. Menü açıkken, oyun bittikten sonra ve
    /// simülasyonda yazmaz — simülasyonun kaydını yalnızca kaydet/yükle testi
    /// yönetir.
    /// </summary>
    void SaveWorldIfPlaying()
    {
        if (!_modeChosen || IsGameOver || SimRuntime.Active) return;
        WorldSave.Save(IsFreePlay ? WorldSave.Slot.Free : WorldSave.Slot.Campaign);
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

        // GAME OVER yazısı — metni gösterim anında yazılır
        _gameOverLabel = MakeText(_gameOverPanel.transform, "GameOverLabel",
            "",
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

    Text MakeText(Transform parent, string objName, string content,
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
        return txt;
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

        // Buton yazısı — metni gösterim anında yazılır
        _restartLabel = MakeText(btnGO.transform, "Label",
            "",
            fontSize: 44,
            color: Color.white,
            anchorMin: Vector2.zero,
            anchorMax: Vector2.one);
    }
}
