using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Bir simülasyon koşusunun BÜTÜN girdileri. Komut satırından okunur, çünkü
/// koşu başına değişen her şey burada olmalı: aynı build binlerce farklı koşu
/// yapacak ve her koşu için yeniden derlemek duyarlılık analizini imkânsız
/// kılardı (bkz. CLAUDE.md "GÖREV — Headless denge simülasyonu").
///
/// Örnek:
///   Starfarer-sim.exe -batchmode -nographics ^
///       --sim --seed 7 --profil ucuz --level 1-10 ^
///       --cikti runs/s7.jsonl --set statStep=1.5
///
/// TÜRKÇE BAYRAKLAR: oyunun geri kalanı (log alan adları, komponent adları)
/// Türkçe; koşucu scripti de bu adları basıyor. Tek dil, tek sözlük.
/// </summary>
public class SimConfig
{
    /// <summary>Rastgeleliğin tek tohumu. Aynı tohum aynı koşuyu vermeli.</summary>
    public int seed = 1;

    /// <summary>Sahte oyuncunun satın alma politikası — bkz. SimShopper.</summary>
    public string profile = "ucuz";

    public int startLevel = 1;
    public int endLevel   = 10;

    /// <summary>
    /// Kaç OYUN saniyesi sonra koşu zorla kesilir. Emniyet kemeri: dengesi
    /// bozuk bir parametre setinde oyuncu ne ölür ne kazanır, koşu sonsuza
    /// kadar sürerdi.
    /// </summary>
    public float maxSimSeconds = 5400f;

    /// <summary>Duvar saati sınırı — asıl kilitlenme emniyeti.</summary>
    public float maxWallSeconds = 900f;

    /// <summary>Koşunun JSONL çıktısı. Boşsa normal kayıt yolu kullanılır.</summary>
    public string outPath;

    /// <summary>
    /// Sabit kare adımı. <c>Time.captureDeltaTime</c>'a yazılır: kare süresi
    /// duvar saatinden KOPARILIR, yani oyun CPU ne kadar hızlıysa o kadar hızlı
    /// koşar ve deltaTime her karede tam olarak bu değerdir.
    ///
    /// Determinizmin yarısı budur (öbür yarısı tohum): değişken deltaTime ile
    /// aynı tohum aynı sonucu VERMEZ.
    /// </summary>
    public float frameStep = 1f / 60f;

    /// <summary>
    /// Nişan hatasının sabit bileşeni (derece, standart sapma). Işınlar hariç
    /// her atışta yeniden çekilir.
    ///
    /// DEĞER ÖLÇÜLDÜ, tahmin edilmedi (2026-09-02, level 1–5, 8 koşu):
    ///
    ///   nişan  0.0° → ana silah isabeti %58.8   ← yapısal tavan
    ///   nişan  0.4° → %50.3                     ← insandan ölçülen %52
    ///   nişan  1.2° → %42.3
    ///   nişan  3.0° → %37.4
    ///
    /// Sıfır gürültüde bile %41 ıskalanıyor: mermi uçarken hedef ölüyor,
    /// kaçamak manevra yapıyor ya da öngörü hedefin dönüşünü kaçırıyor.
    /// Yani insanın %52'si neredeyse tamamen YAPISAL; nişan hatası küçük bir
    /// düzeltmedir. Bu, gürültüyü büyük seçseydik göremeyeceğimiz bir sonuç.
    /// </summary>
    public float aimError = 0.4f;

    /// <summary>
    /// Hedefin hızına bağlı bileşen (derece / (birim/sn)). Hızlı ve kaçamak
    /// hedef daha çok ıskalanır — ölçülen %52'lik isabet oranının tek bir sabite
    /// sıkıştırılması, hangi TİPİN ıskalandığı bilgisini yok ederdi.
    /// </summary>
    public float aimErrorPerSpeed = 0.5f;

    /// <summary>Nişan hatasının yeniden çekilme sıklığı (sn).</summary>
    public float aimJitterPeriod = 0.2f;

    public Difficulty difficulty = Difficulty.Normal;

    /// <summary>
    /// <c>BalanceConfig</c> alanlarının koşuya özgü ezmeleri (--set ad=değer).
    /// Duyarlılık analizi bunun üstünde yürür: her parametreyi ±%20 oynat,
    /// hedef metrikteki değişimi ölç.
    /// </summary>
    public readonly Dictionary<string, float> overrides = new Dictionary<string, float>();

    /// <summary>Komut satırında --sim var mı? Yoksa oyun normal açılır.</summary>
    public static bool RequestedIn(string[] args)
    {
        foreach (var a in args) if (a == "--sim") return true;
        return false;
    }

    public static SimConfig Parse(string[] args)
    {
        var c = new SimConfig();

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : null;

            switch (a)
            {
                case "--seed":     c.seed          = ParseInt(Next(), c.seed);            break;
                case "--profil":
                case "--profile":  c.profile       = Next() ?? c.profile;                 break;
                case "--cikti":
                case "--out":      c.outPath       = Next();                              break;
                case "--adim":
                case "--step":     c.frameStep     = ParseFloat(Next(), c.frameStep);     break;
                case "--nisan":
                case "--aim":      c.aimError      = ParseFloat(Next(), c.aimError);      break;
                case "--nisan-hiz":
                case "--aim-speed": c.aimErrorPerSpeed = ParseFloat(Next(), c.aimErrorPerSpeed); break;
                case "--sure":
                case "--max-seconds": c.maxSimSeconds = ParseFloat(Next(), c.maxSimSeconds); break;
                case "--duvar":
                case "--max-wall": c.maxWallSeconds = ParseFloat(Next(), c.maxWallSeconds); break;
                case "--zorluk":
                case "--difficulty": c.difficulty = ParseDifficulty(Next(), c.difficulty); break;

                case "--level":
                case "--levels":
                {
                    var v = Next();
                    if (v != null)
                    {
                        int dash = v.IndexOf('-');
                        if (dash > 0)
                        {
                            c.startLevel = ParseInt(v.Substring(0, dash), c.startLevel);
                            c.endLevel   = ParseInt(v.Substring(dash + 1), c.endLevel);
                        }
                        else
                        {
                            c.startLevel = c.endLevel = ParseInt(v, c.startLevel);
                        }
                    }
                    break;
                }

                case "--set":
                {
                    var v = Next();
                    int eq = v != null ? v.IndexOf('=') : -1;
                    if (eq > 0)
                        c.overrides[v.Substring(0, eq)] = ParseFloat(v.Substring(eq + 1), 0f);
                    break;
                }
            }
        }

        c.startLevel = Mathf.Clamp(c.startLevel, 1, GameProgress.TotalLevels);
        c.endLevel   = Mathf.Clamp(c.endLevel, c.startLevel, GameProgress.TotalLevels);
        return c;
    }

    // Sayılar NOKTALI okunur. Türkçe locale'de "1.25" virgül bekleyen bir
    // parse'ta 125 olurdu; BalanceLog'un aynı gerekçeyle InvariantCulture
    // kullanması gibi.
    static int ParseInt(string s, int fallback)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    static float ParseFloat(string s, float fallback)
        => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    static Difficulty ParseDifficulty(string s, Difficulty fallback)
    {
        if (string.IsNullOrEmpty(s)) return fallback;
        switch (s.ToLowerInvariant())
        {
            case "kolay": case "easy":   return Difficulty.Easy;
            case "normal":               return Difficulty.Normal;
            case "zor": case "hard":     return Difficulty.Hard;
            default:                     return fallback;
        }
    }

    /// <summary>Koşunun kimliği — log satırına ve dosya adına yazılır.</summary>
    public string Describe()
        => $"seed={seed} profil={profile} level={startLevel}-{endLevel} " +
           $"nisan={aimError.ToString(CultureInfo.InvariantCulture)}+" +
           $"{aimErrorPerSpeed.ToString(CultureInfo.InvariantCulture)}/hiz " +
           $"zorluk={difficulty} ezme={overrides.Count}";
}
