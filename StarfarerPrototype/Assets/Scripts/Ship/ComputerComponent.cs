using UnityEngine;

/// <summary>
/// Atış kontrol bilgisayarı. Kendisi ateş etmez; gemideki bütün turretlerin ve
/// otomatik moddaki ana silahın nişan İSABETİNİ belirler (bkz. FireControl).
///
/// Üç iz:
///   "lead"      Öngörü     — nişan hedeften buluşma noktasına kayar
///   "precision" Hassasiyet — atış başına rastgele sapma daralır
///   "guidance"  Güdüm      — füzelerin dönüş hızı
///
/// Gemide EN FAZLA BİR tane kurulabilir (ShipLoadout.InstallComponent):
/// isabet bir toplam değil tek bir sistemin kalitesidir, ikinci bilgisayar
/// hiçbir şey eklemezdi.
///
/// Yıkılırsa (ya da Kolay modda deaktif kalırsa) bütün turretler bilgisayarsız
/// isabete düşer ve ana silah elle moda döner — Bomber'ın doğal hedefi.
/// </summary>
public class ComputerComponent : ShipComponentBase
{
    public const string LeadKey      = "lead";
    public const string PrecisionKey = "precision";
    public const string GuidanceKey  = "guidance";

    static ComputerComponent s_instance;

    /// <summary>Çalışan bilgisayar; yoksa ya da çalışmıyorsa null.</summary>
    public static ComputerComponent Active
        => s_instance != null && s_instance.IsOperational ? s_instance : null;

    /// <summary>Gemide çalışan bir bilgisayar var mı.</summary>
    public static bool IsOnline => Active != null;

    /// <summary>Gemide (çalışsın çalışmasın) kurulu bir bilgisayar var mı.</summary>
    public static bool IsInstalled => s_instance != null;

    protected override void Awake()
    {
        base.Awake();
        componentName = "Computer";
        maxHP         = 10f;   // elektronik — jeneratör kadar kırılgan
        currentHP     = maxHP;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        s_instance = this;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (s_instance == this) s_instance = null;
    }
}
