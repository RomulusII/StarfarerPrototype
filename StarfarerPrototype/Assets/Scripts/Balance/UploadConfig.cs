using UnityEngine;

/// <summary>
/// Log gönderiminin ayarları. <c>Resources/UploadConfig.asset</c> YOKSA gönderim
/// tamamen kapalıdır — yerel oynayışta sunucuya trafik gitmez ve kimsenin
/// bilgisayarında sessizce ağ isteği açılmaz.
///
/// Aynı desen `SkinSet` ve `BalanceConfig` ile aynı: asset varsa devrede, yoksa
/// sistem kendini kapatıyor. Ayrıca token böylece KAYNAK KODA girmiyor —
/// asset repoda tutulmayabilir.
///
/// Token'ın APK'nın içinde olacağını unutma: istemci tarafı bir anahtar gizli
/// tutulamaz. Zaten amacı gizlilik değil, açık bir POST ucunun bot trafiğiyle
/// dolmasını engellemek.
///
/// BURADAKİLER YALNIZCA YAZMA TOKEN'LARI. Sunucudaki okuma token'ı (liste ve
/// indirme uçları) buraya GİRMEZ; o yalnızca Tools/Balance/pull.config.json
/// içinde yaşar. Sebep: tarayıcı build'i herkese açık bir linkten iniyor ve
/// içindeki token çıkarılabilir — tek token düzeninde bu, paketi açan herkesin
/// bütün kayıtları indirebilmesi demekti (bkz. log.php başlığı).
///
/// Oluşturmak için: Project penceresinde sağ tık →
/// Create → Starfarer → Upload Config, dosyayı `Assets/Resources/` altına koy.
/// </summary>
[CreateAssetMenu(fileName = "UploadConfig", menuName = "Starfarer/Upload Config")]
public class UploadConfig : ScriptableObject
{
    [Tooltip("Örn. https://akinayan.de/starfarer/log/log.php — boşsa gönderim kapalı.")]
    public string endpoint = "";

    [Tooltip("PC ve Android build'leri için yazma token'ı — sunucudaki " +
             "WRITE_TOKEN_NATIVE ile aynı olmalı.")]
    public string token = "";

    [Tooltip("Tarayıcı build'i için yazma token'ı — sunucudaki WRITE_TOKEN_WEB " +
             "ile aynı olmalı. BOŞSA tarayıcıda gönderim yapılmaz.")]
    public string tokenWeb = "";

    [Tooltip("Kapalıysa asset dursa bile gönderim yapılmaz — editörde test " +
             "ederken kendi verini karıştırmamak için.")]
    public bool enabled = true;

    static UploadConfig _instance;
    static bool         _searched;

    /// <summary>Asset yoksa null — çağıran taraf gönderimi atlar.</summary>
    public static UploadConfig Instance
    {
        get
        {
            if (_searched) return _instance;
            _searched = true;
            _instance = Resources.Load<UploadConfig>("UploadConfig");
            return _instance;
        }
    }

    /// <summary>
    /// Bu platformun yazma token'ı. Tarayıcıda <see cref="tokenWeb"/>, diğer
    /// her yerde <see cref="token"/>.
    ///
    /// TARAYICIDA NATIVE TOKEN'A DÜŞMEZ. Düşseydi, tokenWeb'i doldurmayı
    /// unutmak native token'ı herkese açık bir sayfaya gömerdi ve iki ayrı
    /// token tutmanın tek sebebi olan "birini iptal et, diğeri çalışmaya
    /// devam etsin" güvencesi sessizce kaybolurdu. Boşsa gönderim kapalı:
    /// eksik ayar veri kaybettirir, sızıntı ise geri alınamaz.
    /// </summary>
    public static string Token
    {
        get
        {
            var c = Instance;
            if (c == null) return "";
#if UNITY_WEBGL && !UNITY_EDITOR
            return c.tokenWeb;
#else
            return c.token;
#endif
        }
    }

    /// <summary>
    /// Gönderim gerçekten yapılabilir mi.
    ///
    /// TOKEN de aranır. Eskiden yalnızca endpoint'e bakılıyordu; token boş
    /// kalınca sunucu her isteğe 403 döner, istemci ise bunu geçici bir hata
    /// sayıp "sonraki oturumda tekrar denenecek" derdi. Yani yanlış kurulmuş
    /// bir build, hiç veri göndermeden sessizce dosya biriktiriyordu.
    /// Anahtarı olmayan istemci hiç çalmamalı.
    /// </summary>
    public static bool Active
    {
        get
        {
            var c = Instance;
            return c != null && c.enabled
                && !string.IsNullOrEmpty(c.endpoint)
                && !string.IsNullOrEmpty(Token);
        }
    }
}
