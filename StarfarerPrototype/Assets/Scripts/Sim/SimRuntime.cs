using UnityEngine;

/// <summary>
/// Simülasyon koşusunun açılışı ve bitişi.
///
/// KOŞU BİR OYUN KOŞUSUDUR. Ayrı bir denge modeli yazılmadı: model, oyunun
/// kendisidir. Sahte oyuncu gerçek girdi yolundan nişan alıp ateş eder
/// (<see cref="PointerInput.Source"/>), gerçek mağazadan alışveriş yapar
/// (<c>ShipLoadout</c> + <c>ComponentCatalog</c>), gerçek dalgalarla dövüşür.
/// Ayrı bir model kurulsaydı ölçtüğümüz şey oyunun dengesi değil, modelin
/// dengesi olurdu — ve bugüne kadarki bütün sayılar zaten böyle bir kağıt
/// modelinden geliyor.
///
/// HIZLANDIRMA: <c>Time.captureDeltaTime</c>. Kare süresi duvar saatinden
/// koparılır; oyun CPU ne kadar hızlıysa o kadar hızlı koşar ve her karede
/// deltaTime TAM olarak sabittir. <c>Time.timeScale</c> ile hızlandırmak
/// determinizmi bozardı: kare sayısı makinenin hızına bağlı kalırdı.
///
/// Oyunun geri kalanı bu sınıfı yalnızca üç yerden görür: <c>GameManager</c>
/// (menüyü atla, geçiş ekranını kurma) ve buranın kendi kurduğu pilot.
/// </summary>
public static class SimRuntime
{
    public static bool      Active { get; private set; }
    public static SimConfig Config { get; private set; }

    /// <summary>Nişan hatası gibi koşuya özgü rastgelelik. Tohumdan türer.</summary>
    public static System.Random Rng { get; private set; }

    static float _wallStart;

    /// <summary>Koşunun duvar saati (saniye).</summary>
    public static float WallSeconds => Time.realtimeSinceStartup - _wallStart;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        var args = System.Environment.GetCommandLineArgs();
        if (!SimConfig.RequestedIn(args)) return;

        Config = SimConfig.Parse(args);
        Active = true;
        Rng    = new System.Random(Config.seed);
        _wallStart = Time.realtimeSinceStartup;

        // Rastgeleliğin İKİ akışı var ve ikisi de tohumlanmalı: oyunun kendi
        // UnityEngine.Random'ı (spawn, kaçamak fazı, drop) ve pilotun nişan
        // hatası. Ayrı tutuluyorlar ki pilotun politikasını değiştirmek
        // düşmanların doğduğu yeri kaydırmasın — yoksa iki koşu kıyaslanamaz.
        Random.InitState(Config.seed);

        Time.captureDeltaTime      = Config.frameStep;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        DifficultyManager.Current = Config.difficulty;

        ApplyOverrides();

        // Kayıt koşu dosyasına gitsin: ChapterManager'ın Begin("kampanya")
        // çağrısına dokunmadan yönlendirilir.
        if (!string.IsNullOrEmpty(Config.outPath))
            BalanceLog.PathOverride = Config.outPath;

        var go = new GameObject("SimDirector");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<SimDirector>();

        Debug.Log($"[Sim] koşu başlıyor — {Config.Describe()}");
    }

    /// <summary>
    /// <c>--set ad=değer</c> ile verilen parametre ezmeleri. Duyarlılık
    /// analizinin TEK kolu budur: aynı build, farklı sayı. Yeniden derlemek
    /// gerekseydi "her parametreyi ±%20 oynat" pratikte imkânsız olurdu.
    ///
    /// Ad önce <c>BalanceConfig</c>'te, sonra <c>LevelCurve</c>'de aranır.
    /// İkisinde de yoksa koşu HATAYLA durur: sessizce yok sayılsaydı bütün bir
    /// tarama, hiç uygulanmamış bir parametreyle "duyarsız" damgası yerdi.
    /// </summary>
    static void ApplyOverrides()
    {
        if (Config.overrides.Count == 0) return;

        // Ezme asıl asset'e YAZILMAZ — kopya üzerinde çalışılır.
        BalanceConfig.UseRuntimeCopy();
        LevelCurve.UseRuntimeCopy();

        foreach (var kv in Config.overrides)
        {
            if (SetField(BalanceConfig.Instance, kv.Key, kv.Value)) continue;
            if (SetField(LevelCurve.Instance,    kv.Key, kv.Value)) continue;

            Debug.LogError($"[Sim] bilinmeyen parametre: {kv.Key} — koşu durduruldu");
            Application.Quit(4);
            return;
        }
    }

    static bool SetField(object target, string name, float value)
    {
        var f = target.GetType().GetField(name);
        if (f == null) return false;

        if (f.FieldType == typeof(float)) { f.SetValue(target, value); }
        else if (f.FieldType == typeof(int)) { f.SetValue(target, Mathf.RoundToInt(value)); }
        else return false;

        Debug.Log($"[Sim] {target.GetType().Name}.{name} = {value}");
        return true;
    }
}

/// <summary>
/// Koşuyu izler: pilotu kurar, bitiş koşullarını bekler, özeti yazıp süreci
/// kapatır. Bitişi PROSESİN KENDİSİ bildirmeli — koşucu scripti "artık bitmiştir"
/// diye tahmin etseydi, ölçüm yarım kalan koşuları tam sayardı.
/// </summary>
public class SimDirector : MonoBehaviour
{
    SimPilot _pilot;
    bool     _ended;

    void Start() => StartCoroutine(Install());

    System.Collections.IEnumerator Install()
    {
        // Gemi ve donanım GameManager/ShipLoadout tarafından kuruluyor; pilot
        // ancak ondan sonra takılabilir.
        PlayerShip ship = null;
        while (ship == null)
        {
            ship = FindFirstObjectByType<PlayerShip>();
            yield return null;
        }

        _pilot = ship.gameObject.AddComponent<SimPilot>();
        ship.gameObject.AddComponent<SimShopper>();

        // Kayıt dosyası ChapterManager.Start tarafından açılıyor ve bu koroutin
        // ondan ÖNCE çalışabiliyor: ilk denemede sim_start satırı hiç yazılmadı,
        // çünkü henüz açık bir dosya yoktu. Pilot yine de bir kare bile
        // kaybetmez — kurulumu beklemiyoruz, yalnızca satırı bekletiyoruz.
        while (string.IsNullOrEmpty(BalanceLog.CurrentPath)) yield return null;

        BalanceLog.Event("sim_start")
                  .Num("seed",        SimRuntime.Config.seed)
                  .Str("profil",      SimRuntime.Config.profile)
                  .Num("levelBas",    SimRuntime.Config.startLevel)
                  .Num("levelSon",    SimRuntime.Config.endLevel)
                  .Num("nisanHata",   SimRuntime.Config.aimError)
                  .Num("nisanHiz",    SimRuntime.Config.aimErrorPerSpeed)
                  .Num("adim",        SimRuntime.Config.frameStep)
                  .Str("zorluk",      SimRuntime.Config.difficulty.ToString())
                  .End();
    }

    void Update()
    {
        if (_ended) return;
        var cfg = SimRuntime.Config;

        if (GameManager.IsGameOver)                        { Finish("oldu",   0); return; }
        if (GameProgress.CurrentLevel > cfg.endLevel)       { Finish("bitti",  0); return; }
        if (ChapterManager.CampaignFinished)               { Finish("bitti",  0); return; }
        if (Time.time > cfg.maxSimSeconds)                 { Finish("sure",   3); return; }
        if (SimRuntime.WallSeconds > cfg.maxWallSeconds)   { Finish("duvar",  3); return; }
    }

    void Finish(string reason, int exitCode)
    {
        _ended = true;

        var ship = FindFirstObjectByType<PlayerShip>();
        var inv  = ResourceInventory.Instance;

        BalanceLog.Event("sim_end")
                  .Str("sebep",   reason)
                  .Num("level",   GameProgress.CurrentLevel)
                  .Num("oyunSn",  Time.time)
                  .Num("duvarSn", SimRuntime.WallSeconds)
                  .Num("hp",      ship != null ? ship.currentHullHP : -1f)
                  .Num("metal",   inv != null ? inv.metal   : -1f)
                  .Num("kristal", inv != null ? inv.crystal : -1f)
                  .End();

        BalanceLog.Close();
        Debug.Log($"[Sim] koşu bitti — sebep={reason} level={GameProgress.CurrentLevel} " +
                  $"oyun={Time.time:F0}sn duvar={SimRuntime.WallSeconds:F0}sn");

        Application.Quit(exitCode);
    }
}
