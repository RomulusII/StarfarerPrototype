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
/// Oluşturmak için: Project penceresinde sağ tık →
/// Create → Starfarer → Upload Config, dosyayı `Assets/Resources/` altına koy.
/// </summary>
[CreateAssetMenu(fileName = "UploadConfig", menuName = "Starfarer/Upload Config")]
public class UploadConfig : ScriptableObject
{
    [Tooltip("Örn. https://akinayan.de/starfarer/log.php — boşsa gönderim kapalı.")]
    public string endpoint = "";

    [Tooltip("Sunucudaki TOKEN ile aynı olmalı.")]
    public string token = "";

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

    public static bool Active
    {
        get
        {
            var c = Instance;
            return c != null && c.enabled && !string.IsNullOrEmpty(c.endpoint);
        }
    }
}
