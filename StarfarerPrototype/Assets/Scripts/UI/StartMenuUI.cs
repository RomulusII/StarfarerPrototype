using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun açılış ekranı. Oyun buradan başlar; menü kapanana kadar bölüm sistemi
/// kurulmaz, dolayısıyla arkada düşman spawn olmaz.
///
/// İki mod, her biri kendi satırında — solda BAŞLAT, sağda DEVAM ET:
///   Kampanya    — ChapterManager kurulur, normal dalga akışı
///   Serbest Mod — ChapterManager kurulmaz, EnemySpawner'ın test modu açılır
///
/// DEVAM ET kayıt yokken de ekranda durur, sönük ve basılamaz. Kayıt varken
/// ortaya çıkan bir düğme yerleşimi kaydırıyordu: aynı noktaya dokunan oyuncu
/// bir oturumda BAŞLA'ya, diğerinde DEVAM ET'e basıyordu.
///
/// Kayıt varken BAŞLAT iki dokunuş ister (bkz. <see cref="StartOrConfirm"/>).
/// Eskiden BAŞLA eski kaydı silmiyor, ilk level sonunda üstüne yazıyordu:
/// o ana kadar menüye dönen oyuncu "Devam Et"te hâlâ eski kampanyayı
/// buluyordu, yani gerçek anlamda YENİ bir oyuna başlamanın yolu yoktu.
///
/// Zorluk seçimi de buraya taşındı; daha önce yalnızca Game Over panelinde vardı
/// ve oyuncu zorluğu ancak öldükten sonra değiştirebiliyordu.
///
/// Canvas runtime'da kurulur (GameManager ile aynı desen) — ayrı sahne gerekmez.
/// </summary>
public class StartMenuUI : MonoBehaviour
{
    // Yeni değerler SONA eklenir — sıra numarası bir gün kayda girerse
    // ortaya eklenen bir değer eski kayıtları sessizce başka moda çevirir.
    public enum GameMode { Campaign, FreePlay, Continue, FreeContinue }

    /// <summary>
    /// Kampanyanın başlayacağı level. 100 levellik eğriyi baştan oynayarak test
    /// etmek imkânsız olduğu için var; ulaşılmış en yüksek levelle sınırlıdır.
    /// </summary>
    public static int SelectedStartLevel { get; private set; } = 1;

    /// <summary>Menü açıkken oyun girdileri (Tab / upgrade ekranı) kilitlidir.</summary>
    public static bool IsOpen { get; private set; }

    Action<GameMode> _onStart;
    GameObject       _canvasGO;
    GameObject       _panel;
    Button           _easyBtn, _normalBtn, _hardBtn;
    Text             _levelText;
    int              _startLevel = 1;
    Button           _startBtn, _freeBtn;

    // İki dokunuşlu "yeni oyun" onayı: bekleyen düğme ve özgün etiketi.
    const float ConfirmWindow = 4f;
    Button _confirmBtn;
    string _confirmLabel;
    int    _confirmFontSize;
    float  _confirmUntil;

    static readonly Color Selected   = new Color(1f, 0.85f, 0.25f);
    static readonly Color Unselected = new Color(0.30f, 0.30f, 0.34f);

    static readonly Color CampaignColor = new Color(0.13f, 0.42f, 0.22f);
    static readonly Color ContinueColor = new Color(0.15f, 0.28f, 0.45f);
    // Serbest modun devam düğmesi aynı rengi taşır: satırın iki düğmesi aynı
    // modun iki kapısı, renk hangi moda ait olduklarını söylüyor.
    static readonly Color FreeColor     = new Color(0.28f, 0.24f, 0.12f);
    static readonly Color ConfirmColor  = new Color(0.55f, 0.18f, 0.14f);

    /// <summary>Menüyü kurar ve gösterir. Seçim yapılınca onStart çağrılır.</summary>
    public static StartMenuUI Show(Action<GameMode> onStart)
    {
        var go   = new GameObject("StartMenuUI");
        var menu = go.AddComponent<StartMenuUI>();
        menu._onStart = onStart;

        // Son seçilen zorluk işaretli gelsin — Build düğmeleri boyarken okur.
        DifficultyManager.LoadPreference();
        menu.Build();

        // Menü açıkken oyun ilerlemesin. Projedeki pause protokolü SpeedController'da;
        // timeScale'i doğrudan ezmek hız sistemiyle çakışır.
        IsOpen = true;
        WebChrome.Refresh();
        if (SpeedController.Instance != null) SpeedController.Instance.Pause();
        else                                  Time.timeScale = 0f;

        return menu;
    }

    // ── Kurulum ───────────────────────────────────────────────────────────────

    void Build()
    {
        _canvasGO = new GameObject("StartMenuCanvas");
        _canvasGO.transform.SetParent(transform, false);

        var canvas       = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;   // her şeyin üstünde

        var scaler = _canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // Projedeki diğer sekiz canvas 0.5 kullanıyor; burada ayarlanmamıştı,
        // yani varsayılan 0'da (yalnızca GENİŞLİĞE göre) kalıyordu. Dar bir
        // ekranda menü, oyunun geri kalanındaki HUD'lardan daha çok küçülüyor
        // ve yazılar okunmuyordu — telefonda en çok şikâyet edilen ekran da
        // ilk açılan bu.
        scaler.matchWidthOrHeight  = 0.5f;

        _canvasGO.AddComponent<GraphicRaycaster>();

        _panel = new GameObject("Panel");
        _panel.transform.SetParent(_canvasGO.transform, false);

        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.03f, 0.06f, 0.96f);

        var pr = _panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero;
        pr.offsetMax = Vector2.zero;

        BuildLanguageButtons();

        // Oyunun adı çevrilmez.
        MakeText(_panel.transform, "Title", "STARFARER", 96,
                 new Color(0.85f, 0.92f, 1f),
                 new Vector2(0f, 0.74f), new Vector2(1f, 0.90f));

        MakeText(_panel.transform, "Subtitle",
                 Loc.T("menu.subtitle"), 24,
                 new Color(0.45f, 0.50f, 0.60f),
                 new Vector2(0f, 0.67f), new Vector2(1f, 0.73f));

        MakeText(_panel.transform, "DifficultyLabel", Loc.T("menu.difficulty"), 22,
                 new Color(0.55f, 0.55f, 0.60f),
                 new Vector2(0f, 0.58f), new Vector2(1f, 0.63f));

        BuildDifficultyButtons();

        BuildLevelSelect();

        BuildModeButtons();

        MakeText(_panel.transform, "FreePlayHint",
                 Loc.T("menu.freeplay.hint"),
                 17, new Color(0.40f, 0.42f, 0.48f),
                 new Vector2(0f, 0.06f), new Vector2(1f, 0.11f));

        BuildVersionLabel();

        RefreshDifficultyButtons();
    }

    /// <summary>
    /// İki satır: kampanya (BAŞLA · DEVAM ET) ve serbest mod (SERBEST MOD ·
    /// DEVAM ET). Satırın iki yarısı eşit genişlikte — DEVAM ET kayıttaki
    /// level/dalga numarasını taşıdığı için BAŞLA'dan uzun bir metin.
    /// </summary>
    void BuildModeButtons()
    {
        bool hasSave = SaveSystem.HasSave;
        bool hasFree = SaveSystem.HasFreeSave;

        _startBtn = MakeButton("StartButton", Loc.T("menu.start"), 32, CampaignColor,
                               new Vector2(0.25f, 0.25f), new Vector2(0.49f, 0.33f),
                               () => StartOrConfirm(_startBtn, GameMode.Campaign, SaveSystem.HasSave));

        var cont = MakeButton("ContinueButton",
                              hasSave ? Loc.T("menu.continue", SaveSystem.SavedLevel)
                                      : Loc.T("menu.continue.none"),
                              26, ContinueColor,
                              new Vector2(0.51f, 0.25f), new Vector2(0.75f, 0.33f),
                              () => Choose(GameMode.Continue));
        SetAvailable(cont, hasSave);

        _freeBtn = MakeButton("FreePlayButton", Loc.T("menu.freeplay"), 26, FreeColor,
                              new Vector2(0.25f, 0.14f), new Vector2(0.49f, 0.22f),
                              () => StartOrConfirm(_freeBtn, GameMode.FreePlay, SaveSystem.HasFreeSave));

        var freeCont = MakeButton("FreeContinueButton",
                                  hasFree ? Loc.T("menu.freeplay.continue", SaveSystem.SavedFreeWave)
                                          : Loc.T("menu.continue.none"),
                                  24, FreeColor,
                                  new Vector2(0.51f, 0.14f), new Vector2(0.75f, 0.22f),
                                  () => Choose(GameMode.FreeContinue));
        SetAvailable(freeCont, hasFree);
    }

    /// <summary>Kayıt yoksa düğme yerinde durur ama sönük ve basılamaz.</summary>
    static void SetAvailable(Button b, bool available)
    {
        b.interactable = available;
        if (available) return;

        var colors = b.colors;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        b.colors = colors;

        var label = b.GetComponentInChildren<Text>();
        if (label != null) label.color = new Color(1f, 1f, 1f, 0.35f);
    }

    /// <summary>
    /// Sürüm bilgisi sağ alt köşede. Testçi "hangi paketi oynuyorum" sorusuna
    /// ekrandan cevap verebilsin diye var: hata bildirimi sürümsüz geldiğinde
    /// hangi ayarın ölçüldüğü belirsizleşiyor.
    ///
    /// <c>Application.version</c> okunur, <see cref="GameVersion.Surum"/> DEĞİL:
    /// paket numarasını (1.0.0+b17) build sırasında <c>BuildStamp</c> oraya
    /// yazar ve bu, kayıttaki <c>build</c> alanıyla birebir aynı değerdir.
    /// Surum yalnızca "1.0.0" der, yani iki farklı paketi ayırt ettirmez.
    ///
    /// Denge revizyonu AYRI bir sayı olarak yanında: aynı sürüm altında
    /// onlarca ayar denemesi yapılıyor ve testçinin gördüğü tek numara
    /// bunları ayırmaya yetmez.
    ///
    /// Çevrilmez. "rev" üç dilde de aynı kısaltma (revizyon / revision /
    /// Revision), geri kalanı zaten sayı — oyunun adı gibi, Loc katmanına
    /// girmesi gereksiz bir bakım yükü olurdu.
    ///
    /// BAŞLIĞIN YANINDA, köşede DEĞİL. İki denemede de köşe yanlış çıktı:
    /// sağ alt köşede tarayıcının APK ve tam ekran düğmeleri (HTML katmanı
    /// canvas'ın her zaman üstünde) yazının üstünü örtüyordu; küçük punto ise
    /// telefonda hiç okunmuyordu. Sebep CanvasScaler'ın genişliğe göre
    /// ölçeklemesi: 1920 referansta 16 punto, 375 piksellik bir telefonda
    /// 3 piksele iner. Punto artık başlıkla aynı bantta ve ona oranlı, yani
    /// ekran küçüldükçe başlıkla birlikte küçülüyor — kaybolmuyor.
    ///
    /// Sola hizalı: kutu sabit, yazı uzunluğu ise sürüm numarasıyla değişiyor
    /// (1.0.0+b9 ile 1.0.0+b123 aynı değil). Ortalanmış olsaydı her dağıtımda
    /// birkaç piksel kayardı.
    /// </summary>
    void BuildVersionLabel()
    {
        var txt = MakeText(_panel.transform, "Version",
                           $"{Application.version} · rev {GameVersion.Denge}", 40,
                           new Color(0.45f, 0.52f, 0.66f),
                           new Vector2(0.66f, 0.745f), new Vector2(0.99f, 0.845f));
        txt.alignment = TextAnchor.MiddleLeft;
    }

    /// <summary>
    /// Dil seçimi sağ üst köşede durur — dikey yığının dışında olduğu için
    /// "Devam Et" ve level seçimi gibi koşullu satırlar geldiğinde kaymaz.
    /// Düğmeler dil adlarını kendi dillerinde yazar, o yüzden çevrilmezler.
    /// </summary>
    void BuildLanguageButtons()
    {
        float[] xMin = { 0.700f, 0.795f, 0.890f };
        float[] xMax = { 0.790f, 0.885f, 0.980f };

        for (int i = 0; i < Loc.All.Length && i < xMin.Length; i++)
        {
            var lang = Loc.All[i];
            MakeButton($"Lang_{lang}", Loc.NameOf(lang), 18,
                       lang == Loc.Language ? Selected : Unselected,
                       new Vector2(xMin[i], 0.92f), new Vector2(xMax[i], 0.97f),
                       () => SelectLanguage(lang));
        }
    }

    void BuildDifficultyButtons()
    {
        var defs = new[]
        {
            (name: "Easy",   label: Loc.T("menu.difficulty.easy"),   diff: Difficulty.Easy),
            (name: "Normal", label: Loc.T("menu.difficulty.normal"), diff: Difficulty.Normal),
            (name: "Hard",   label: Loc.T("menu.difficulty.hard"),   diff: Difficulty.Hard),
        };

        float[] xMin = { 0.34f, 0.44f, 0.54f };
        float[] xMax = { 0.43f, 0.53f, 0.63f };

        for (int i = 0; i < defs.Length; i++)
        {
            var d   = defs[i];
            // Nesne adı çeviriye bağlı olmamalı; hiyerarşi dilden dile değişmez.
            var btn = MakeButton($"Diff_{d.name}", d.label, 22, Unselected,
                                 new Vector2(xMin[i], 0.49f), new Vector2(xMax[i], 0.56f),
                                 () => SelectDifficulty(d.diff));

            if (d.diff == Difficulty.Easy)   _easyBtn   = btn;
            if (d.diff == Difficulty.Normal) _normalBtn = btn;
            if (d.diff == Difficulty.Hard)   _hardBtn   = btn;
        }
    }

    /// <summary>
    /// Başlangıç leveli seçimi. Ulaşılmış en yüksek levele kadar açıktır —
    /// istenen her levele atlamak testi kolaylaştırırdı ama ilerlemeyi
    /// anlamsız kılardı. Bölüm başlarına atlar (1, 11, 21 …) çünkü bölüm
    /// ortasından başlamak yeni düşman tipini tanıtan leveli atlamak demek.
    /// </summary>
    void BuildLevelSelect()
    {
        int maxLevel = SaveSystem.MaxReachedLevel;
        if (maxLevel <= 1) return;   // henüz seçilecek bir şey yok

        MakeText(_panel.transform, "LevelLabel", Loc.T("menu.startLevel"), 20,
                 new Color(0.55f, 0.55f, 0.60f),
                 new Vector2(0f, 0.42f), new Vector2(1f, 0.47f));

        MakeButton("LevelDown", "◀", 24, Unselected,
                   new Vector2(0.38f, 0.35f), new Vector2(0.44f, 0.41f),
                   () => StepLevel(-GameProgress.LevelsPerChapter));

        _levelText = MakeText(_panel.transform, "LevelValue", "", 26,
                              new Color(0.85f, 0.92f, 1f),
                              new Vector2(0.44f, 0.35f), new Vector2(0.56f, 0.41f));

        MakeButton("LevelUp", "▶", 24, Unselected,
                   new Vector2(0.56f, 0.35f), new Vector2(0.62f, 0.41f),
                   () => StepLevel(GameProgress.LevelsPerChapter));

        RefreshLevelText();
    }

    void StepLevel(int delta)
    {
        int per      = GameProgress.LevelsPerChapter;
        int maxStart = ((SaveSystem.MaxReachedLevel - 1) / per) * per + 1;
        _startLevel  = Mathf.Clamp(_startLevel + delta, 1, Mathf.Max(1, maxStart));
        RefreshLevelText();
    }

    void RefreshLevelText()
    {
        if (_levelText == null) return;
        int chapter = GameProgress.ChapterOf(_startLevel);
        _levelText.text = Loc.T("menu.levelValue", _startLevel, chapter);
    }

    // ── Etkileşim ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Dil değişince menü baştan kurulur. Metinleri tek tek güncellemek yerine
    /// bu tercih edildi: menü zaten koddan kuruluyor ve oyun henüz başlamadı,
    /// yani yeniden kurmanın görünür bir maliyeti yok.
    ///
    /// Menü dışındaki UI'lar bu yolu izleyemez: GameManager onları menüden ÖNCE
    /// kurar (BuildBoostHUD, BuildUpgradeUI …), yani dil değiştiğinde çoktan
    /// ekranda dururlar. Onlar <see cref="Loc.OnLanguageChanged"/>'a abone olup
    /// kendi metinlerini tazeler.
    /// </summary>
    void SelectLanguage(Lang lang)
    {
        if (Loc.Language == lang) return;

        // Bekleyen onay eski canvas'ın düğmesine bağlı; yeniden kurulumda kaybolur.
        _confirmBtn = null;

        Loc.Language = lang;

        // Destroy kare sonunda işler; eski canvas bir kare boyunca yenisiyle
        // üst üste çizilip tıklama yakalamasın diye önce kapatılır.
        if (_canvasGO != null)
        {
            _canvasGO.SetActive(false);
            Destroy(_canvasGO);
        }

        Build();
    }

    void SelectDifficulty(Difficulty d)
    {
        DifficultyManager.Current = d;
        DifficultyManager.SavePreference();
        RefreshDifficultyButtons();
    }

    void RefreshDifficultyButtons()
    {
        Tint(_easyBtn,   DifficultyManager.Current == Difficulty.Easy);
        Tint(_normalBtn, DifficultyManager.Current == Difficulty.Normal);
        Tint(_hardBtn,   DifficultyManager.Current == Difficulty.Hard);
    }

    static void Tint(Button b, bool selected)
    {
        if (b != null && b.targetGraphic != null)
            b.targetGraphic.color = selected ? Selected : Unselected;
    }

    /// <summary>
    /// Kayıt yoksa doğrudan başlatır. Kayıt varsa ilk dokunuş düğmeyi uyarıya
    /// çevirir ("kayıt silinecek — tekrar bas"), ikinci dokunuş başlatır.
    ///
    /// Ayrı bir "YENİ OYUN" düğmesi yerine bu seçildi: ekrana yeni bir düğme
    /// eklemek yerine var olan düğmenin ne yapacağını dürüstçe söylemesi. Tek
    /// dokunuşla silmek ise bir yanlış dokunuşun bütün kampanyayı götürmesi
    /// demekti — telefonda BAŞLA ile DEVAM ET yan yana.
    ///
    /// Onay <see cref="ConfirmWindow"/> saniye sonra kendiliğinden düşer.
    /// Menü açıkken timeScale 0, süre bu yüzden duvar saatiyle ölçülür.
    /// </summary>
    void StartOrConfirm(Button btn, GameMode mode, bool hasSave)
    {
        if (!hasSave || _confirmBtn == btn)
        {
            Choose(mode);
            return;
        }

        CancelConfirm();

        var label = btn.GetComponentInChildren<Text>();
        if (label == null) { Choose(mode); return; }

        _confirmBtn      = btn;
        _confirmLabel    = label.text;
        _confirmFontSize = label.fontSize;
        _confirmUntil    = Time.unscaledTime + ConfirmWindow;

        label.text     = Loc.T("menu.newConfirm");
        label.fontSize = 20;
        btn.targetGraphic.color = ConfirmColor;
    }

    void CancelConfirm()
    {
        if (_confirmBtn == null) return;

        var label = _confirmBtn.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.text     = _confirmLabel;
            label.fontSize = _confirmFontSize;
        }
        _confirmBtn.targetGraphic.color = _confirmBtn == _startBtn ? CampaignColor : FreeColor;
        _confirmBtn = null;
    }

    void Update()
    {
        if (_confirmBtn != null && Time.unscaledTime > _confirmUntil)
            CancelConfirm();
    }

    void Choose(GameMode mode)
    {
        IsOpen = false;
        WebChrome.Refresh();
        SelectedStartLevel = mode == GameMode.Campaign ? _startLevel : 1;

        // Oyun 1x hızda başlasın
        if (SpeedController.Instance != null) SpeedController.Instance.Reset();
        else                                  Time.timeScale = 1f;

        var cb = _onStart;
        _onStart = null;
        Destroy(gameObject);
        cb?.Invoke(mode);
    }

    void OnDestroy()
    {
        IsOpen = false;
        WebChrome.Refresh();
    }

    // ── UI yardımcıları ───────────────────────────────────────────────────────

    Button MakeButton(string objName, string label, int fontSize, Color color,
                      Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(_panel.transform, false);

        var img = go.AddComponent<Image>();
        img.color = color;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var r = go.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;

        MakeText(go.transform, "Label", label, fontSize, Color.white,
                 Vector2.zero, Vector2.one);

        return btn;
    }

    static Text MakeText(Transform parent, string objName, string content,
                         int fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax)
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
}
