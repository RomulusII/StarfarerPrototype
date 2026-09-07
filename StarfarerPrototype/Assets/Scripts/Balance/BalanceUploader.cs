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
    /// Sunucu isteği KURAL GEREĞİ reddetti — yanlış token, kapatılmış uç (4xx)
    /// ya da dolu kota (507). Bu oturumda bir daha denenmez.
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

    void Start()
    {
        StartCoroutine(UploadPending());
        StartCoroutine(PeriyodikGonderim());
    }

    /// <summary>Kaç saniyede bir açık oturumun yeni satırları gönderilir.</summary>
    const float GonderimAraligi = 30f;

    /// <summary>
    /// Her dosya için sunucudaki BAYT SAYISI. Bir sonraki gönderim buradan
    /// devam eder; sunucu her yazımdan sonra yeni boyutu döndürüyor, yani bu
    /// sözlük sunucunun söylediğinin önbelleği. Ofsetin tek doğruluk kaynağı
    /// sunucu: diskte tutmak, tam da kaybolduğundan şüphelendiğimiz depoya
    /// (tarayıcıda IndexedDB) yazmak olurdu.
    /// </summary>
    readonly System.Collections.Generic.Dictionary<string, long> _gonderilen =
        new System.Collections.Generic.Dictionary<string, long>();

    bool _mesgul;

    /// <summary>
    /// Açık oturumu düzenli aralıkla gönderir.
    ///
    /// NEDEN GEREKLİ: gönderim eskiden yalnızca level sonunda ve kapanışta
    /// yapılıyordu, yani oyunun verisi kaydın DOSYADA HAYATTA KALMASINA
    /// bağlıydı. Tarayıcıda bu varsayım tutmadı: üç oturum boyunca kayıtlar
    /// IndexedDB'ye yazıldı ama sayfa yenilenince hiçbiri sunucuya ulaşmadı
    /// (yalnızca önceki günlerden kalan dosyalar gitti). Otuz saniyede bir
    /// gönderince verinin sağ kalması artık depolamanın kaprisine bağlı değil.
    ///
    /// Yalnızca DOSYA BÜYÜDÜYSE istek atılır; duran bir oyunda ağ trafiği
    /// olmaz. Ekleme yolu sayesinde her turda yalnızca yeni baytlar gidiyor,
    /// dosyanın tamamı değil.
    ///
    /// Gerçek zaman kullanılır: menü ve upgrade ekranı timeScale'i sıfırlıyor,
    /// WaitForSeconds ise orada durur ve gönderim sessizce ölürdü.
    /// </summary>
    IEnumerator PeriyodikGonderim()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(GonderimAraligi);

            if (_rejected) yield break;   // yapılandırma bozuk, ısrar etmenin anlamı yok

            string path = BalanceLog.CurrentPath;
            if (string.IsNullOrEmpty(path)) continue;

            long diskte = BoyutOku(path);
            long gonderilmis = _gonderilen.TryGetValue(Normalize(path), out long g) ? g : -1;
            if (diskte <= 0 || diskte == gonderilmis) continue;

            yield return UploadFile(path, deleteOnSuccess: false);
        }
    }

    /// <summary>Dosya boyutu; okunamıyorsa -1. Coroutine'de try/catch olamaz.</summary>
    static long BoyutOku(string path)
    {
        try { return File.Exists(path) ? new FileInfo(path).Length : -1; }
        catch (IOException) { return -1; }
    }

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
    /// iterator gövdesinde catch'li try bloğu olamaz — <see cref="ReadFrom"/>
    /// ve <see cref="BoyutOku"/> da aynı kısıt yüzünden ayrı duruyor.
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

        // Aynı anda tek gönderim. Periyodik tur ile level sonu flush'ı üst üste
        // binerse ikisi de aynı ofsetten gönderir, ikincisi 409 yer ve boşuna
        // bir teşhis gürültüsü üretirdi.
        if (_mesgul) yield break;
        _mesgul = true;

        // Token platforma göre seçilir (bkz. UploadConfig.Token): tarayıcı
        // build'i kendi yazma token'ını taşır, native build başkasını.
        string taban = $"{cfg.endpoint}?t={UnityWebRequest.EscapeURL(UploadConfig.Token)}" +
                       $"&d={UnityWebRequest.EscapeURL(DeviceId)}" +
                       $"&f={UnityWebRequest.EscapeURL(Path.GetFileName(path))}";

        string anahtar = Normalize(path);

        // Ofseti bilmiyorsak SUNUCUYA sor. Yerel bir sayaç tutmak bir istek
        // kazandırırdı ama tam da kaybolduğundan şüphelendiğimiz depoya
        // yazmak olurdu; üstelik önceki oturumdan kalan bir dosyanın ne
        // kadarının gittiğini yalnızca sunucu bilir.
        if (!_gonderilen.ContainsKey(anahtar))
        {
            using var boyutReq = UnityWebRequest.Get(taban + "&size=1");
            boyutReq.timeout = 15;
            yield return boyutReq.SendWebRequest();

            if (boyutReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[BalanceUploader] boyut sorulamadı ({boyutReq.error}) — " +
                                 "sonraki turda tekrar denenecek");
                _mesgul = false;
                yield break;
            }
            if (!long.TryParse(boyutReq.downloadHandler.text.Trim(), out long uzak)) uzak = 0;
            _gonderilen[anahtar] = uzak;
        }

        long ofset = _gonderilen[anahtar];

        // Okuma AYRI bir metotta: coroutine'in içinde catch'li bir try bloğu
        // olamaz (C# iterator kısıtı), yoksa `yield break` derlenmez.
        byte[] body = ReadFrom(path, ofset);
        if (body == null) { _mesgul = false; yield break; }
        if (body.Length == 0)
        {
            // Sunucu dosyanın tamamına sahip. Kapanmış bir oturumsa yereldeki
            // kopyanın işi bitti.
            if (deleteOnSuccess) DeleteQuietly(path);
            _mesgul = false;
            yield break;
        }

        string url = taban + $"&o={ofset}";

        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/x-ndjson");
        req.timeout = 20;

        yield return req.SendWebRequest();

        _mesgul = false;

        if (req.responseCode == 409)
        {
            // Ofset tutmadı: sunucudaki dosya beklediğimizden farklı boyutta.
            // Önbelleklenen ofseti atıyoruz, sonraki tur sunucuya yeniden
            // sorup doğru yerden devam edecek. Yapılandırma hatası DEĞİL —
            // 4xx sayıp gönderimi kapatmak, tek bir senkron kaymasında bütün
            // oturumun verisini çöpe atardı.
            _gonderilen.Remove(Normalize(path));
            Debug.LogWarning($"[BalanceUploader] {Path.GetFileName(path)} ofset uyuşmadı (409) — " +
                             $"sunucu: {req.downloadHandler.text.Trim()}; sonraki turda düzeltilecek");
        }
        else if (req.responseCode == 507)
        {
            // Sunucu kotası dolu. Geçici sayılır ve dosya diskte kalır, ama bu
            // oturumda ısrar etmenin anlamı yok: kota kendi kendine boşalmaz,
            // sunucudaki kayıtların indirilip temizlenmesi gerekir.
            _rejected = true;
            Debug.LogError("[BalanceUploader] sunucu kotası DOLU (507) — gönderim bu oturumda " +
                           "kapatıldı. Kayıtlar diskte kaldı; sunucuda yer açılınca gidecekler " +
                           "(node Tools/Balance/pull.js ile indirip sunucudan sil).");
        }
        else if (req.responseCode >= 400 && req.responseCode < 500)
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
            // Sunucu yeni boyutu döndürüyor ("ok 12345"): ofsetin tek doğruluk
            // kaynağı o, biz yalnızca önbellekliyoruz. Kendi hesabımızı
            // tutsaydık bir kayma sessizce birikirdi.
            var yanit = req.downloadHandler.text.Trim().Split(' ');
            if (yanit.Length == 2 && long.TryParse(yanit[1], out long yeniBoyut))
                _gonderilen[Normalize(path)] = yeniBoyut;
            else
                _gonderilen.Remove(Normalize(path));   // beklenmedik yanıt: yeniden sor

            Debug.Log($"[BalanceUploader] {Path.GetFileName(path)} — " +
                      $"+{body.Length / 1024} KB (ofset {ofset})");

            if (deleteOnSuccess) DeleteQuietly(path);
        }
    }

    /// <summary>
    /// Kaydın <paramref name="ofset"/> baytından SONRASINI okur. Dosya o sırada
    /// yazılıyor olabilir, bu yüzden <c>FileShare.ReadWrite</c> şart:
    /// paylaşımsız açılırsa kilit çakışır ve gönderim sessizce hiç çalışmaz.
    ///
    /// Ofset dosyadan büyükse boş dizi döner — sunucuda bizde olandan fazlası
    /// var demektir (başka bir istemci ya da eski bir gönderim); o durumda
    /// yazacak bir şeyimiz yok.
    /// </summary>
    static byte[] ReadFrom(string path, long ofset)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (ofset >= fs.Length) return System.Array.Empty<byte>();

            fs.Seek(ofset, SeekOrigin.Begin);
            var buf  = new byte[fs.Length - ofset];
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
