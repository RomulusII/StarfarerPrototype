using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Her level başlangıcında ekranın üstünde 2–3 saniye görünen bant:
/// level numarası, bölüm numarası ve sektör adı.
///
/// Neden var: bölüm içi level geçişi SESSİZDİ. Oyuncu 2.5 saniye bekleyip yeni
/// bir dalga görüyordu ama bir levelin bittiğini, kaçıncı levelde olduğunu ya da
/// hangi sektörde savaştığını hiçbir yerden okuyamıyordu — 100 levellik bir
/// kampanyada yer duygusu tamamen kayboluyordu. Bölüm geçişi (her 10 levelde
/// bir) tam ekran anlatımını sürdürüyor; bant onun yerini almaz, ARALARINI
/// doldurur.
///
/// Neden tam ekran değil bant: level geçişi bir olay değil bir ritim. Her
/// levelde akışı durdurmak 100 kez tekrarlanacak bir kesinti olurdu.
///
/// Bant oynanışa hiç dokunmaz: raycast almaz, girdi engellemez ve
/// <see cref="Time.unscaledDeltaTime"/> ile çalışır — hız kontrolü ×2'ye
/// alındığında bant yarı süre görünmemeli, süresi duvar saatidir.
/// </summary>
public class LevelBannerUI : MonoBehaviour
{
    const float FadeIn  = 0.35f;
    const float Hold    = 1.90f;
    const float FadeOut = 0.65f;   // toplam ~2.9 sn

    static LevelBannerUI _instance;

    CanvasGroup _group;
    Text        _levelText;
    Text        _sectorText;
    Coroutine   _running;

    /// <summary>
    /// Bandı gösterir. Sahnede bir örnek yoksa kendisi kurar — GameManager
    /// deseniyle aynı: geçiş ekranı için ayrı bir sahne nesnesi bağlamak
    /// gerekmesin.
    /// </summary>
    public static void Show(int level, int chapter, string sectorTitle, bool bossLevel)
    {
        if (_instance == null)
        {
            var go = new GameObject("LevelBannerUI");
            _instance = go.AddComponent<LevelBannerUI>();
        }
        _instance.Play(level, chapter, sectorTitle, bossLevel);
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Build();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── Kurulum ───────────────────────────────────────────────────────────────

    void Build()
    {
        var canvasGo = new GameObject("LevelBannerCanvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        // Bölüm geçiş ekranının (50) ALTINDA: ikisi üst üste gelirse anlatım kazanır
        canvas.sortingOrder = 45;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        // GraphicRaycaster YOK: bant tıklamayı yutmamalı. Upgrade ekranı ve
        // slotlar bandın arkasında çalışmaya devam eder.

        _group = canvasGo.AddComponent<CanvasGroup>();
        _group.alpha          = 0f;
        _group.blocksRaycasts = false;
        _group.interactable   = false;

        // Metnin arkasına yumuşak bir koyu şerit — yıldız alanının üstünde
        // beyaz yazı tek başına okunmuyor.
        var strip = new GameObject("Strip");
        strip.transform.SetParent(canvasGo.transform, false);
        var img = strip.AddComponent<Image>();
        img.color         = new Color(0f, 0f, 0f, 0.45f);
        img.raycastTarget = false;
        var sr = strip.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0.22f, 0.845f);
        sr.anchorMax = new Vector2(0.78f, 0.965f);
        sr.offsetMin = sr.offsetMax = Vector2.zero;

        _levelText  = MakeText("Level",  46, FontStyle.Bold,
                               new Vector2(0.22f, 0.895f), new Vector2(0.78f, 0.965f));
        _sectorText = MakeText("Sector", 22, FontStyle.Normal,
                               new Vector2(0.22f, 0.845f), new Vector2(0.78f, 0.900f));
        _sectorText.color = new Color(0.72f, 0.82f, 0.92f);

        Text MakeText(string name, int size, FontStyle style, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvasGo.transform, false);

            var txt           = go.AddComponent<Text>();
            txt.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize      = size;
            txt.fontStyle     = style;
            txt.color         = Color.white;
            txt.alignment     = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            var r = go.GetComponent<RectTransform>();
            r.anchorMin = aMin;
            r.anchorMax = aMax;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return txt;
        }
    }

    // ── Gösterim ──────────────────────────────────────────────────────────────

    void Play(int level, int chapter, string sectorTitle, bool bossLevel)
    {
        _levelText.text  = Loc.T(bossLevel ? "banner.levelBoss" : "banner.level", level);
        _levelText.color = bossLevel ? new Color(1f, 0.72f, 0.35f) : Color.white;

        // Büyük harf dile göre yapılır: ToUpperInvariant Türkçe'de "Devriye
        // Hattı"nı "DEVRIYE HATTI" yazıyordu.
        _sectorText.text = string.IsNullOrEmpty(sectorTitle)
                         ? Loc.T("banner.chapter", chapter)
                         : Loc.T("banner.chapterSector", chapter, Loc.ToUpper(sectorTitle));

        // Hızlı ilerleyen bir oyuncu bir önceki bandı görmeden yenisine geçebilir;
        // iki coroutine üst üste binerse alfa titrer.
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return Fade(0f, 1f, FadeIn);

        float t = 0f;
        while (t < Hold) { t += Time.unscaledDeltaTime; yield return null; }

        yield return Fade(1f, 0f, FadeOut);
        _running = null;
    }

    IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _group.alpha = to;
    }
}
