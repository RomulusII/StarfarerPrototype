using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Kaydet → yükle → kaydet testi. Tam kaydın TEK doğrulaması.
///
/// Koşu belirtilen anda dünyayı yakalar ve yazar, sahneyi yeniden yükler
/// (menüden dönüşle aynı yol), "Devam Et" yoluyla geri yükler ve geri yükleme
/// biter bitmez — hiçbir nesne kendi Update'ini çalıştırmadan — dünyayı YENİDEN
/// yakalar. İki yakalama aynı olmalı.
///
/// Neden bu kadar sıkı: kayıt sınıflara dağılmış ~150 alan. Bir sınıfa sonradan
/// eklenen ve kayda eklenmeyen bir alan çökme üretmez; devam edilen oyunda
/// sessizce yanlış davranır (gemi yanlış fazda, sayaç sıfırlanmış). Farkı
/// oyunda görmek neredeyse imkânsız, iki JSON'u yan yana koymak kesin.
///
/// Sayılar TOLERANSLA karşılaştırılır: bazı alanlar Time.time'a göre çevrilip
/// geri çevriliyor (siperin salınım fazı) ve kayan nokta bu gidiş-dönüşte
/// son basamakta oynar. Tolerans, bir karelik zaman kaymasını (1/60 sn)
/// YAKALAYACAK kadar dar tutuldu.
///
/// Çıkış kodları: 0 aynı · 5 fark var · 6 yakalanamadı · 7 koşu test anından
/// önce bitti.
///
/// Kullanım:
///   Starfarer-sim.exe -batchmode -nographics --sim --level 10-10 ^
///       --kayit-testi kampanya --kayit-ani 45
/// </summary>
public static class SaveRoundTrip
{
    public static bool AwaitingRestore { get; private set; }
    public static bool Started         { get; private set; }

    static string         s_before;
    static string         s_mode;
    static WorldSave.Slot s_slot;

    const float Tolerance = 1e-3f;

    /// <returns>Test devredeyse true — koşunun normal bitiş kontrolleri atlanır.</returns>
    public static bool Tick(SimConfig cfg)
    {
        if (string.IsNullOrEmpty(cfg.saveTest)) return false;
        if (Started) return true;
        if (Time.timeSinceLevelLoad < cfg.saveTestAt) return false;

        Run(cfg.saveTest == "serbest");
        return true;
    }

    static void Run(bool free)
    {
        Started = true;
        s_slot  = free ? WorldSave.Slot.Free : WorldSave.Slot.Campaign;
        s_mode  = free ? "serbest" : "kampanya";

        var w = WorldSave.Capture(s_mode);
        if (w == null)
        {
            Debug.LogError("[KayitTesti] dünya yakalanamadı");
            Application.Quit(6);
            return;
        }

        s_before = JsonUtility.ToJson(w, true);
        WorldSave.Write(s_slot, w);
        Debug.Log($"[KayitTesti] kaydedildi — {Summary(w)}");

        AwaitingRestore = true;
        WorldSave.RestoreFinished += Compare;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    static void Compare()
    {
        WorldSave.RestoreFinished -= Compare;
        AwaitingRestore = false;

        var    w     = WorldSave.Capture(s_mode);
        string after = w != null ? JsonUtility.ToJson(w, true) : "";
        var    diffs = Diff(s_before, after, 30);

        if (diffs.Count == 0)
        {
            Debug.Log($"[KayitTesti] BASARILI — {Summary(w)}");
            Application.Quit(0);
            return;
        }

        Debug.LogError($"[KayitTesti] BASARISIZ — {diffs.Count}+ fark · sonra: {(w != null ? Summary(w) : "yok")}");
        foreach (var d in diffs) Debug.LogError("[KayitTesti] " + d);
        Application.Quit(5);
    }

    static string Summary(WorldState w) =>
        $"level {w.level} · düşman {w.enemies.Count} · boss {w.bosses.Count} · " +
        $"formasyon {w.formations.Count} · asteroit {w.asteroids.Count} · enkaz {w.debris.Count} · " +
        $"mermi {w.bullets.Count}/{w.turretBullets.Count}/{w.enemyBullets.Count} · " +
        $"bomba {w.bombs.Count} · lazer {w.lasers.Count} · plazma {w.plasma.Count} · " +
        $"toplayıcı {w.collectors.Count} · savaşçı {w.fighters.Count} · komponent {w.components.Count}";

    /// <summary>
    /// Satır satır karşılaştırma (JSON girintili yazılır: alan başına bir satır).
    /// Farkın YERİNİ söyleyebilmek için her farkın önüne o satırın içinde
    /// bulunduğu listenin adı ve en yakın "id" satırı eklenir.
    /// </summary>
    static List<string> Diff(string a, string b, int max)
    {
        var la  = a.Split('\n');
        var lb  = b.Split('\n');
        var out_ = new List<string>();

        if (la.Length != lb.Length)
            out_.Add($"satır sayısı farklı: {la.Length} → {lb.Length} (bir listenin uzunluğu değişti)");

        int n = Mathf.Min(la.Length, lb.Length);
        string list = "", id = "";

        for (int i = 0; i < n && out_.Count < max; i++)
        {
            string x = la[i].TrimEnd('\r');
            string y = lb[i].TrimEnd('\r');

            string t = x.Trim();
            if (t.EndsWith("[")) list = t.Split(':')[0].Trim('"', ' ');
            if (t.StartsWith("\"id\"")) id = t.TrimEnd(',');

            if (x == y || SameNumber(x, y)) continue;
            out_.Add($"satır {i + 1} [{list} {id}]: {x.Trim()}  →  {y.Trim()}");
        }
        return out_;
    }

    static bool SameNumber(string x, string y)
    {
        int cx = x.IndexOf(':'), cy = y.IndexOf(':');
        if (cx < 0 || cy < 0) return false;
        if (x.Substring(0, cx) != y.Substring(0, cy)) return false;

        string vx = x.Substring(cx + 1).Trim().TrimEnd(',');
        string vy = y.Substring(cy + 1).Trim().TrimEnd(',');

        if (!float.TryParse(vx, NumberStyles.Float, CultureInfo.InvariantCulture, out float fx)) return false;
        if (!float.TryParse(vy, NumberStyles.Float, CultureInfo.InvariantCulture, out float fy)) return false;

        return Mathf.Abs(fx - fy) <= Tolerance * Mathf.Max(1f, Mathf.Abs(fx));
    }
}
