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

    void Awake()
    {
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Kaydı gönder. Level sonunda ve oyun kapanışında çağrılır — her olayda
    /// göndermek 4 dakikada yüzlerce istek demek olurdu.
    /// </summary>
    public static void Flush()
    {
        if (_instance == null) return;
        _instance.StartCoroutine(_instance.Upload());
    }

    IEnumerator Upload()
    {
        var cfg = UploadConfig.Instance;
        if (!UploadConfig.Active) yield break;

        string path = BalanceLog.CurrentPath;
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

        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning($"[BalanceUploader] gönderilemedi ({req.error}) — " +
                             "kayıt diskte kaldı, sonraki oturumda tekrar denenecek");
        else
            Debug.Log($"[BalanceUploader] {body.Length / 1024} KB gönderildi");
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
