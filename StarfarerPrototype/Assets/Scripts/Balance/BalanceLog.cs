using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Denge ölçümü için HAM OLAY kaydı. Satır başına bir JSON nesnesi (JSONL),
/// oturum başına bir dosya.
///
/// Neden özet değil ham olay: hangi özeti isteyeceğimizi henüz bilmiyoruz.
/// "Ortalama TTK" kaydetseydik, sonradan "peki Armored'a karşı KİNETİK ile TTK
/// neydi" diye soramazdık. Ham olaydan her özet türetilebilir, tersi olmaz.
///
/// Ölçmek istediğimiz asıl şey, tehdit puanının DOĞRULANMASI:
///
///     gözlenen_tehdit ≈ α · (o gemiye harcanan oyuncu-saniyesi)
///                     + β · (o geminin oyuncuya verdiği hasar)
///
/// İkisi de burada kaydedilen olaylardan türer (enemy_spawn/enemy_death ve
/// player_damage). Formülün çıktısı bununla karşılaştırılınca artıklar hangi
/// yetenek puanının yanlış olduğunu doğrudan söyler — tahminle değil.
///
/// Bugüne kadarki bütün denge sayıları %100 İSABET varsayımıyla kalibre edildi.
/// Gerçek isabet oranı ölçülmemiş tek kritik bilinmeyendir ve tüm TTK'ları
/// doğrudan çarpar; shot_fired/shot_hit çifti bunun için var.
///
/// ── Kullanım ──────────────────────────────────────────────────────────────
///
///     BalanceLog.Event("enemy_death")
///               .Str("tip", data.name)
///               .Num("tehdit", data.threatScore)
///               .End();
///
/// <see cref="Row"/> bir STRUCT'tır ve kapalıyken her çağrı tek bir dallanmaya
/// iner — çöp üretmez. Kare başına çalışan yollarda (ışın hasarı) yine de
/// çağrıyı <see cref="Enabled"/> ile sarmalayın: asıl maliyet stringlerin
/// hazırlanmasıdır, bu sınıf onu göremez.
///
/// **Satırlar İÇ İÇE GEÇEMEZ.** Tek bir paylaşılan StringBuilder kullanılır;
/// bir zincir <c>End()</c> ile kapanmadan ikinci bir <c>Event()</c> açılırsa
/// ilk satır bozulur. Pratikte kural şu: alan değerlerinde başka bir şey
/// LOGLAYAN metot çağırmayın (hazır değeri geçin). Havuzlanmış tampon
/// alternatifi çöp üretirdi ve bu bir geliştirme aracı.
/// </summary>
public static class BalanceLog
{
    /// <summary>
    /// Her yerde AÇIK: editör, PC ve Android build'i. Eskiden build'de kapalıydı
    /// ("ölçüm bir geliştirme aracı") ama bu, ölçümü tek kişiye hapsediyordu.
    /// İsabet oranı gibi sayılar oyuncudan oyuncuya değişir; denge ancak
    /// dağıtılan build'lerden veri gelirse kalibre edilebilir.
    ///
    /// Kayıt HER ZAMAN yalnızca diske yazılır. Sunucuya gitmesi AYRI bir
    /// karardır ve <see cref="UploadConfig"/> asset'inin varlığına bağlıdır:
    /// asset yoksa dosya cihazda durur, hiçbir ağ isteği açılmaz.
    /// </summary>
    public static bool Enabled = true;

    static StreamWriter _writer;
    static string       _path;

    /// <summary>Açık kaydın dosya yolu — yükleyici buradan okur.</summary>
    public static string CurrentPath => _path;

    /// <summary>
    /// Doluysa <see cref="Begin"/> tarih damgalı ad üretmek yerine BU dosyayı
    /// açar. Yalnızca simülasyon doldurur (bkz. SimRuntime): paralel koşan
    /// süreçler aynı saniyede başlar ve zaman damgalı ad çakışırdı.
    /// </summary>
    public static string PathOverride;
    static string       _mode = "-";
    static readonly StringBuilder _sb = new StringBuilder(256);

    // Sayılar NOKTA ile yazılır. Türkçe locale'de varsayılan ayraç virgüldür ve
    // JSON'u sessizce bozar — analiz tarafı "1,5" görüp iki alan sanardı.
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Kaydın hangi bağlamda alındığı — kampanya mı serbest mod mu.</summary>
    public static void Begin(string mode)
    {
        if (!Enabled) return;

        // Simülasyon koşusu kaydı kendi dosyasına ister; çağıranların (şu an
        // ChapterManager ve GameManager) bunu bilmesi gerekmiyor.
        if (!string.IsNullOrEmpty(PathOverride)) { BeginAt(PathOverride, mode); return; }

        var dir = Path.Combine(Application.persistentDataPath, "balance");
        Directory.CreateDirectory(dir);
        Prune(dir);

        Open(Path.Combine(dir, $"{System.DateTime.Now:yyyyMMdd-HHmmss}-{mode}.jsonl"), mode);
    }

    /// <summary>
    /// Kaydı BELİRTİLEN dosyaya açar. Simülasyon koşusu başına bir dosya gerekir
    /// ve dosyayı koşucu adlandırır: paralel koşan onlarca süreç aynı saniyede
    /// başlar, zaman damgalı ad ikisini birbirinin üstüne yazardı. Prune de
    /// çalıştırılmaz — koşu çıktısı 30 dosyada kırpılamaz.
    /// </summary>
    public static void BeginAt(string path, string mode)
    {
        if (!Enabled) return;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        Open(path, mode);
    }

    static void Open(string path, string mode)
    {
        _mode = mode;
        Close();

        _path   = path;
        _writer = new StreamWriter(_path, append: false);

        // Satır satır diske yaz. Editörde Play durdurulduğunda OnDisable her
        // zaman yetişmiyor: ilk kayıtta son satır YARIM kaldı, çünkü tampon
        // kısmen boşalmıştı. Saniyede birkaç satırlık bir akışta AutoFlush'ın
        // maliyeti yok, veri kaybının maliyeti ise bütün oturum.
        _writer.AutoFlush = true;

        Event("session")
            .Str("unity", Application.unityVersion)
            .Num("startLevel", GameProgress.CurrentLevel)
            .End();

        Debug.Log($"[BalanceLog] kayıt açıldı: {_path}");
    }

    public static void Close()
    {
        if (_writer == null) return;
        _writer.Flush();
        _writer.Dispose();
        _writer = null;
    }

    /// <summary>Diskte tutulan en fazla oturum sayısı.</summary>
    const int KeepFiles = 30;

    /// <summary>
    /// Klasörün sınırsız büyümesini engeller. Gönderim AÇIKSA yükleyici
    /// başarıyla giden dosyayı zaten siler ve burası hiç iş yapmaz. Gönderim
    /// KAPALIYSA (UploadConfig asset'i yok) kimse temizlemez; o zaman oyuncunun
    /// cihazında oturum başına bir dosya sonsuza kadar birikirdi.
    ///
    /// Dosya adı zaman damgasıyla başlar, yani sözlük sırası kronolojik sıradır.
    /// Yeni dosya HENÜZ açılmadan çağrılır: kendini silme riski yok.
    /// </summary>
    static void Prune(string dir)
    {
        try
        {
            var files = Directory.GetFiles(dir, "*.jsonl");
            if (files.Length <= KeepFiles) return;
            System.Array.Sort(files);
            for (int i = 0; i < files.Length - KeepFiles; i++) File.Delete(files[i]);
        }
        catch (IOException)
        {
            // Temizlik başarısız olsa bile kaydın açılmasını engellememeli.
        }
    }

    // ── Satır kurma ───────────────────────────────────────────────────────────

    /// <summary>
    /// Yeni bir olay satırı başlatır. Zincirin sonunda <see cref="Row.End"/>
    /// çağrılmazsa satır YAZILMAZ (yarım satır bozuk JSONL üretirdi).
    /// </summary>
    public static Row Event(string type)
    {
        if (!Enabled || _writer == null) return default;

        _sb.Clear();
        _sb.Append("{\"t\":").Append(Time.time.ToString("0.###", Inv))
           .Append(",\"ev\":\"").Append(type)
           .Append("\",\"mode\":\"").Append(_mode)
           .Append("\",\"lvl\":").Append(GameProgress.CurrentLevel);
        return new Row(true);
    }

    /// <summary>
    /// Tek bir olay satırı. Struct'tır: kapalıyken <c>_on</c> false olur ve
    /// bütün zincir dallanmaya iner, hiçbir şey ayrılmaz.
    /// </summary>
    public readonly struct Row
    {
        readonly bool _on;
        internal Row(bool on) { _on = on; }

        public Row Num(string key, float value)
        {
            if (_on) _sb.Append(",\"").Append(key).Append("\":")
                        .Append(value.ToString("0.###", Inv));
            return this;
        }

        public Row Num(string key, int value)
        {
            if (_on) _sb.Append(",\"").Append(key).Append("\":").Append(value);
            return this;
        }

        public Row Str(string key, string value)
        {
            if (_on) _sb.Append(",\"").Append(key).Append("\":\"")
                        .Append(Escape(value)).Append('"');
            return this;
        }

        public Row Bool(string key, bool value)
        {
            if (_on) _sb.Append(",\"").Append(key).Append("\":")
                        .Append(value ? "true" : "false");
            return this;
        }

        public void End()
        {
            if (!_on) return;
            _sb.Append('}');
            _writer.WriteLine(_sb.ToString());
        }
    }

    static string Escape(string s)
        => string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
