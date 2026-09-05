using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// <see cref="BalanceLog"/>'un yazdığı dosyayı sunucuya gönderir. Arkadaşlara
/// dağıtılan Android build'inden log toplamak için — tek kişilik test verisiyle
/// denge kalibre edilemez, isabet oranı gibi sayılar oyuncudan oyuncuya değişir.
///
/// **Dosyanın TAMAMI gönderilir, artımlı değil.** Dosya adı oturum kimliğidir,
/// sunucu üzerine yazar: yani kopan bir bağlantı çift kayıt değil, yalnızca
/// gecikmiş bir kayıt üretir. Artımlı gönderim (bayt ofseti) daha az veri
/// taşırdı ama sunucuda birleştirme hatası riskini getirirdi; 4 dakikalık bir
/// oturum ~100 KB, sıkıştırmaya bile değmez.
///
/// **Ağ yoksa hiçbir şey kaybolmaz.** Kayıt zaten diskte; gönderim başarısızsa
/// dosya durur ve bir sonraki oturumda yeniden denenir.
///
/// **Cihaz kimliği rastgeledir.** <c>SystemInfo.deviceUniqueIdentifier</c>
/// donanım parmak izidir ve arkadaşların telefonlarını kalıcı olarak
/// etiketlerdi; oysa bize yalnızca "aynı kurulumun oturumlarını grupla" lazım.
/// PlayerPrefs'te tutulan bir GUID bunu karşılar, kimseyi tanımlamaz.
/// </summary>
public class BalanceUploader : MonoBehaviour
{
    const string DeviceKey = "starfarer.deviceId";

    /// <summary>
    /// Gönderim gerekiyorsa yükleyiciyi kurar. <c>Resources/UploadConfig.asset</c>
    /// yoksa HİÇBİR ŞEY yapmaz — yerel oynayışta ne nesne doğar ne ağ isteği
    /// açılır. GameManager çağırır (runtime kurulum deseni, ayrı sahne gerekmez).
    /// </summary>
    public static void EnsureExists()
    {
        if (_instance != null || !UploadConfig.Active) return;
        new GameObject("BalanceUploader").AddComponent<BalanceUploader>();
    }

    /// <summary>Kurulum başına rastgele kimlik — donanım parmak izi DEĞİL.</summary>
    public static string DeviceId
    {
        get
        {
            var id = PlayerPrefs.GetString(DeviceKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString("N").Substring(0, 12);
                PlayerPrefs.SetString(DeviceKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }
    }

    static BalanceUploader _instance;

    /// <summary>
    /// Sunucu isteği KURAL GEREĞİ reddetti (4xx) — yanlış token, kapatılmış uç.
    /// Bu oturumda bir daha denenmez.
    ///
    /// Ağ kopması ile yanlış yapılandırmayı ayırmak şart: ilki tekrar denemeyi
    /// hak eder, ikincisi asla düzelmez. İkisi aynı sayılınca yanlış token'la
    /// dağıtılan bir build her açılışta bütün birikmiş dosyaları tek tek
    /// gönderip 403 yiyor, hiçbir şey silinmiyor ve log'da yalnızca "sonraki
    /// oturumda tekrar denenecek" yazıyordu.
    /// </summary>
    static bool _rejected;

    void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start() => StartCoroutine(UploadPending());

    /// <summary>
    /// ÖNCEKİ oturumlardan kalan kayıtları gönderir ve gidenleri siler.
    ///
    /// Bu olmadan sınıfın "başarısızsa sonraki oturumda tekrar denenecek" sözü
    /// tutulmuyordu: <see cref="Flush"/> yalnızca AÇIK oturumun dosyasına bakar,
    /// yeni oturum ise yeni bir dosya açar — kopan gönderim kalıcı kayıptı.
    ///
    /// PC'de asıl kazanç bu. Masaüstünde oyun kapanırken coroutine'in bitmesine
    /// izin verilmez, yani <c>OnApplicationQuit</c> içindeki gönderim pratikte
    /// hiç tamamlanmaz. Kapanışta yarışmak yerine bir sonraki AÇILIŞTA toplamak
    /// o yarışı tamamen ortadan kaldırır.
    ///
    /// Tekrar göndermek zararsız: sunucu dosyayı ADIYLA yazar
    /// (log.php → file_put_contents), yani aynı oturum iki kez giderse üzerine
    /// yazılır, çift kayıt oluşmaz.
    /// </summary>
    IEnumerator UploadPending()
    {
        string current = Normalize(BalanceLog.CurrentPath);
        foreach (var f in ListPending())
        {
            if (string.Equals(Normalize(f), current, System.StringComparison.OrdinalIgnoreCase))
                continue;
            yield return UploadFile(f, deleteOnSuccess: true);
        }
    }

    /// <summary>
    /// Klasördeki kayıtları eskiden yeniye listeler. Coroutine'den AYRI metot:
    /// iterator gövdesinde catch'li try bloğu olamaz — <see cref="ReadAll"/>
    /// da aynı kısıt yüzünden ayrı duruyor.
    /// </summary>
    static string[] ListPending()
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "balance");
            if (!Directory.Exists(dir)) return System.Array.Empty<string>();

            var files = Directory.GetFiles(dir, "*.jsonl");
            System.Array.Sort(files);   // ad zaman damgasıyla başlar: sıra kronolojik
            return files;
        }
        catch (IOException)
        {
            return System.Array.Empty<string>();
        }
    }

    /// <summary>Açık oturumun dosyasını ayırt edebilmek için yol normalleştirme.</summary>
    static string Normalize(string path)
        => string.IsNullOrEmpty(path) ? "" : Path.GetFullPath(path);

    static void DeleteQuietly(string path)
    {
        try { File.Delete(path); }
        catch (IOException e) { Debug.LogWarning($"[BalanceUploader] silinemedi: {e.Message}"); }
    }

    /// <summary>
    /// Kaydı gönder. Level sonunda ve oyun kapanışında çağrılır — her olayda
    /// göndermek 4 dakikada yüzlerce istek demek olurdu.
    /// </summary>
    public static void Flush()
    {
        if (_instance == null) return;
        _instance.StartCoroutine(_instance.UploadFile(BalanceLog.CurrentPath, deleteOnSuccess: false));
    }

    /// <summary>
    /// Tek bir kaydı gönderir. <paramref name="deleteOnSuccess"/> yalnızca
    /// KAPANMIŞ oturumlar için doğrudur; açık oturumun dosyasına hâlâ yazılıyor.
    /// </summary>
    IEnumerator UploadFile(string path, bool deleteOnSuccess)
    {
        var cfg = UploadConfig.Instance;
        if (!UploadConfig.Active || _rejected) yield break;

        if (string.IsNullOrEmpty(path) || !File.Exists(path)) yield break;

        // Okuma AYRI bir metotta: coroutine'in içinde catch'li bir try bloğu
        // olamaz (C# iterator kısıtı), yoksa `yield break` derlenmez.
        byte[] body = ReadAll(path);
        if (body == null) yield break;

        string url = $"{cfg.endpoint}?t={UnityWebRequest.EscapeURL(cfg.token)}" +
                     $"&d={UnityWebRequest.EscapeURL(DeviceId)}" +
                     $"&f={UnityWebRequest.EscapeURL(Path.GetFileName(path))}";

        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/x-ndjson");
        req.timeout = 20;

        yield return req.SendWebRequest();

        if (req.responseCode >= 400 && req.responseCode < 500)
        {
            // Yapılandırma hatası. 403 = token yanlış (sunucudaki TOKEN ile
            // UploadConfig.token aynı olmalı), 404 = uç yolu yanlış.
            _rejected = true;
            Debug.LogError($"[BalanceUploader] sunucu REDDETTİ (HTTP {req.responseCode}) — " +
                           "gönderim bu oturumda kapatıldı. Token/endpoint yanlış: " +
                           $"{cfg.endpoint} · teşhis için ?ping=1 aç.");
        }
        else if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[BalanceUploader] gönderilemedi ({req.error}) — " +
                             "kayıt diskte kaldı, sonraki oturumda tekrar denenecek");
        }
        else
        {
            Debug.Log($"[BalanceUploader] {Path.GetFileName(path)} — " +
                      $"{body.Length / 1024} KB gönderildi");
            if (deleteOnSuccess) DeleteQuietly(path);
        }
    }

    /// <summary>
    /// Kaydı tamamen okur. Dosya O SIRADA YAZILIYOR olabilir, bu yüzden
    /// <c>FileShare.ReadWrite</c> şart: paylaşımsız açılırsa kilit çakışır ve
    /// gönderim sessizce hiç çalışmaz.
    /// </summary>
    static byte[] ReadAll(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buf  = new byte[fs.Length];
            int read = 0;
            while (read < buf.Length)
            {
                int n = fs.Read(buf, read, buf.Length - read);
                if (n <= 0) break;
                read += n;
            }
            return buf;
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[BalanceUploader] okunamadı: {e.Message}");
            return null;
        }
    }

    void OnApplicationPause(bool paused)
    {
        // Android'de "kapanış" diye bir şey yok: kullanıcı uygulamayı arka plana
        // atar ve sistem onu sessizce öldürür. OnApplicationQuit çoğu zaman hiç
        // çalışmaz — duraklama tek güvenilir kancadır.
        //
        // Kaydı KAPATMIYORUZ, yalnızca gönderiyoruz: oyuncu geri dönerse aynı
        // oturum devam etmeli. Dosya zaten satır satır diske yazıldığı için
        // (AutoFlush) o an diskte olan hâli eksiksizdir.
        if (paused) Flush();
    }

    void OnApplicationQuit()
    {
        BalanceLog.Close();
        Flush();
    }
}
