using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Oyun açılış ekranı. Oyun buradan başlar; menü kapanana kadar bölüm sistemi
/// kurulmaz, dolayısıyla arkada düşman spawn olmaz.
///
/// İki mod:
///   Kampanya    — ChapterManager kurulur, normal dalga akışı
///   Serbest Mod — ChapterManager kurulmaz, EnemySpawner'ın test modu açılır
///
/// Zorluk seçimi de buraya taşındı; daha önce yalnızca Game Over panelinde vardı
/// ve oyuncu zorluğu ancak öldükten sonra değiştirebiliyordu.
///
/// Canvas runtime'da kurulur (GameManager ile aynı desen) — ayrı sahne gerekmez.
/// </summary>
public class StartMenuUI : MonoBehaviour
{
    public enum GameMode { Campaign, FreePlay, Continue }

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

    static readonly Color Selected   = new Color(1f, 0.85f, 0.25f);
    static readonly Color Unselected = new Color(0.30f, 0.30f, 0.34f);

    /// <summary>Menüyü kurar ve gösterir. Seçim yapılınca onStart çağrılır.</summary>
    public static StartMenuUI Show(Action<GameMode> onStart)
    {
        var go   = new GameObject("StartMenuUI");
        var menu = go.AddComponent<StartMenuUI>();
        menu._onStart = onStart;
        menu.Build();

        // Menü açıkken oyun ilerlemesin. Projedeki pause protokolü SpeedController'da;
        // timeScale'i doğrudan ezmek hız sistemiyle çakışır.
        IsOpen = true;
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

        MakeButton("StartButton", Loc.T("menu.start"), 32,
                   new Color(0.13f, 0.42f, 0.22f),
                   new Vector2(0.34f, 0.25f), new Vector2(0.66f, 0.33f),
                   () => Choose(GameMode.Campaign));

        // Kayıt varsa devam etmek asıl akıştır; yeni başlamak kaydı siler
        if (SaveSystem.HasSave)
        {
            MakeButton("ContinueButton", Loc.T("menu.continue", SaveSystem.SavedLevel), 24,
                       new Color(0.15f, 0.28f, 0.45f),
                       new Vector2(0.34f, 0.17f), new Vector2(0.66f, 0.24f),
                       () => Choose(GameMode.Continue));
        }

        MakeButton("FreePlayButton", Loc.T("menu.freeplay"), 22,
                   new Color(0.28f, 0.24f, 0.12f),
                   new Vector2(0.38f, 0.09f), new Vector2(0.62f, 0.15f),
                   () => Choose(GameMode.FreePlay));

        MakeText(_panel.transform, "FreePlayHint",
                 Loc.T("menu.freeplay.hint"),
                 17, new Color(0.40f, 0.42f, 0.48f),
                 new Vector2(0f, 0.04f), new Vector2(1f, 0.08f));

        RefreshDifficultyButtons();
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

    void Choose(GameMode mode)
    {
        IsOpen = false;
        SelectedStartLevel = mode == GameMode.Campaign ? _startLevel : 1;

        // Oyun 1x hızda başlasın
        if (SpeedController.Instance != null) SpeedController.Instance.Reset();
        else                                  Time.timeScale = 1f;

        var cb = _onStart;
        _onStart = null;
        Destroy(gameObject);
        cb?.Invoke(mode);
    }

    void OnDestroy() => IsOpen = false;

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
