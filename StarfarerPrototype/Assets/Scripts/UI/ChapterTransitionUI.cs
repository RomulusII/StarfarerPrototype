using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bölüm geçiş ekranı:
///   1. Mürettebat konuşma balonları (sırayla, slot pozisyonlarına yakın)
///   2. Bölüm başlığı + hikaye metni (fade-in → bekle → fade-out)
///   3. onComplete callback → ChapterManager bir sonraki bölümü başlatır
///
/// Konuşma balonları gemi slot'larının world→screen pozisyonuna anchor edilir:
///   Kaptan  → Slot 5 (Ana silah)
///   Mühendis → Slot 0-4'teki ilk Generator
///   Pilot   → Slot 6 (Hangar)
/// </summary>
public class ChapterTransitionUI : MonoBehaviour
{
    // Slot referansları
    ShipLoadout _loadout;

    // Canvas
    Canvas     _canvas;
    RectTransform _canvasRect;
    Camera     _cam;

    // Başlık paneli
    GameObject _titlePanel;
    Text       _titleText;
    Text       _storyText;
    CanvasGroup _titleGroup;

    // Konuşma balonu (tek instance, sırayla yeniden kullanılır)
    RectTransform _bubbleRect;
    Text          _bubbleText;
    Text          _bubbleSpeaker;
    CanvasGroup   _bubbleGroup;

    Action _onComplete;

    // ── Kurulum ───────────────────────────────────────────────────────────────

    void Awake()
    {
        _cam     = Camera.main;
        _loadout = FindFirstObjectByType<ShipLoadout>();

        BuildCanvas();
        BuildTitlePanel();
        BuildBubble();
    }

    void BuildCanvas()
    {
        var go = new GameObject("ChapterTransitionCanvas");
        go.transform.SetParent(transform, false);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        _canvasRect = go.GetComponent<RectTransform>();
    }

    void BuildTitlePanel()
    {
        var panel = new GameObject("TitlePanel");
        panel.transform.SetParent(_canvas.transform, false);

        _titleGroup = panel.AddComponent<CanvasGroup>();
        _titleGroup.alpha = 0f;

        // Yarı saydam arka plan — ekranın üst %30'u
        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0.05f, 0.82f);
        var pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0f, 0.68f);
        pr.anchorMax = new Vector2(1f, 1.00f);
        pr.offsetMin = pr.offsetMax = Vector2.zero;

        // Bölüm başlığı
        _titleText        = MakeText(panel.transform, "TitleText",
            fontSize: 56, bold: true,
            anchorMin: new Vector2(0.05f, 0.58f), anchorMax: new Vector2(0.95f, 0.96f));
        _titleText.color  = new Color(0.85f, 0.90f, 1.00f);
        _titleText.alignment = TextAnchor.MiddleLeft;

        // Hikaye metni
        _storyText        = MakeText(panel.transform, "StoryText",
            fontSize: 26, bold: false,
            anchorMin: new Vector2(0.05f, 0.06f), anchorMax: new Vector2(0.95f, 0.52f));
        _storyText.color  = new Color(0.72f, 0.78f, 0.85f);
        _storyText.alignment = TextAnchor.UpperLeft;

        _titlePanel = panel;
        _titlePanel.SetActive(false);
    }

    void BuildBubble()
    {
        var bubble = new GameObject("SpeechBubble");
        bubble.transform.SetParent(_canvas.transform, false);

        _bubbleGroup       = bubble.AddComponent<CanvasGroup>();
        _bubbleGroup.alpha = 0f;

        var bg = bubble.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.15f, 0.92f);

        _bubbleRect = bubble.GetComponent<RectTransform>();
        _bubbleRect.sizeDelta = new Vector2(320f, 90f);
        _bubbleRect.pivot     = new Vector2(0f, 0f);

        // Konuşan isim
        _bubbleSpeaker        = MakeText(bubble.transform, "Speaker",
            fontSize: 20, bold: true,
            anchorMin: new Vector2(0.03f, 0.60f), anchorMax: new Vector2(0.97f, 0.96f));
        _bubbleSpeaker.color  = new Color(0.55f, 0.85f, 1.00f);
        _bubbleSpeaker.alignment = TextAnchor.UpperLeft;

        // Metin
        _bubbleText           = MakeText(bubble.transform, "BubbleText",
            fontSize: 18, bold: false,
            anchorMin: new Vector2(0.03f, 0.04f), anchorMax: new Vector2(0.97f, 0.58f));
        _bubbleText.color     = new Color(0.90f, 0.92f, 0.95f);
        _bubbleText.alignment = TextAnchor.UpperLeft;

        bubble.SetActive(false);
    }

    // ── Dış API ───────────────────────────────────────────────────────────────

    public void Show(ChapterData chapter, Action onComplete)
    {
        _onComplete = onComplete;
        StartCoroutine(RunTransition(chapter));
    }

    public void ShowCredits()
    {
        // Placeholder — tüm bölümler bitti
        StartCoroutine(RunSimpleText("Tebrikler!", "Tüm sektörler temizlendi.", null));
    }

    // ── Coroutine akışı ───────────────────────────────────────────────────────

    IEnumerator RunTransition(ChapterData chapter)
    {
        // 1. Diyaloglar
        if (chapter.dialogue != null)
        {
            foreach (var line in chapter.dialogue)
                yield return StartCoroutine(ShowDialogueLine(line));
        }

        // 2. Başlık + hikaye
        yield return StartCoroutine(RunSimpleText(chapter.chapterTitle, chapter.storyText, null));

        // 3. Callback
        _onComplete?.Invoke();
    }

    IEnumerator ShowDialogueLine(DialogueLine line)
    {
        var bubbleGO = _bubbleGroup.gameObject;
        bubbleGO.SetActive(true);

        _bubbleSpeaker.text = SpeakerName(line.speaker);
        _bubbleText.text    = line.text;

        // Slot dünya pozisyonunu → canvas pozisyonuna çevir
        PositionBubble(line.speaker);

        yield return StartCoroutine(FadeGroup(_bubbleGroup, 0f, 1f, 0.3f));
        yield return new WaitForSeconds(line.displayDuration > 0f ? line.displayDuration : 2.5f);
        yield return StartCoroutine(FadeGroup(_bubbleGroup, 1f, 0f, 0.3f));

        bubbleGO.SetActive(false);
    }

    IEnumerator RunSimpleText(string title, string story, Action callback)
    {
        _titleText.text = title;
        _storyText.text = story ?? "";
        _titlePanel.SetActive(true);

        yield return StartCoroutine(FadeGroup(_titleGroup, 0f, 1f, 0.6f));
        yield return new WaitForSeconds(3.5f);
        yield return StartCoroutine(FadeGroup(_titleGroup, 1f, 0f, 0.8f));

        _titlePanel.SetActive(false);
        callback?.Invoke();
    }

    // ── Konuşma balonu konumlama ───────────────────────────────────────────────

    void PositionBubble(CrewMember speaker)
    {
        var worldPos = GetCrewWorldPosition(speaker);
        if (_cam == null || worldPos == Vector3.zero) return;

        var screenPos = _cam.WorldToScreenPoint(worldPos);

        // Screen → Canvas local koordinatına çevir
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect, screenPos, null, out var localPoint);

        // Balonu biraz yukarı kaydır (gemi üstünde görünsün)
        localPoint += new Vector2(10f, 40f);
        _bubbleRect.anchoredPosition = localPoint;
    }

    Vector3 GetCrewWorldPosition(CrewMember speaker)
    {
        if (_loadout == null) return Vector3.zero;

        int slotIndex = speaker switch
        {
            CrewMember.Captain  => 5,
            CrewMember.Pilot    => 6,
            CrewMember.Engineer => FindGeneratorSlot(),
            _                   => 5,
        };

        var slotVisuals = _loadout.GetComponentsInChildren<SlotVisual>();
        foreach (var sv in slotVisuals)
            if (sv.slotIndex == slotIndex)
                return sv.transform.position;

        return _loadout.transform.position;
    }

    int FindGeneratorSlot()
    {
        if (_loadout == null) return 0;
        var slotVisuals = _loadout.GetComponentsInChildren<SlotVisual>();
        // Generator en küçük slot indeksinde aranır (0–4 arası)
        foreach (var sv in slotVisuals)
            if (sv.slotIndex < 5) return sv.slotIndex;
        return 0;
    }

    static string SpeakerName(CrewMember m) => m switch
    {
        CrewMember.Captain  => "Kaptan",
        CrewMember.Engineer => "Mühendis",
        CrewMember.Pilot    => "Pilot",
        _                   => "?",
    };

    // ── Fade yardımcısı ───────────────────────────────────────────────────────

    static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        float t = 0f;
        group.alpha = from;
        while (t < duration)
        {
            t           += Time.deltaTime;
            group.alpha  = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    // ── UI yardımcısı ─────────────────────────────────────────────────────────

    static Text MakeText(Transform parent, string name, int fontSize, bool bold,
                         Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var txt       = go.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = fontSize;
        txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        txt.color     = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;

        var r = go.GetComponent<RectTransform>();
        r.anchorMin = anchorMin;
        r.anchorMax = anchorMax;
        r.offsetMin = r.offsetMax = Vector2.zero;

        return txt;
    }
}
