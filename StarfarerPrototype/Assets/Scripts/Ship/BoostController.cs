public enum BoostMode { None, Shield, Weapon }

/// <summary>
/// Aktif boost modunu tutan statik sınıf.
/// ShieldGeneratorComponent ve WeaponController buradan okur.
/// </summary>
public static class BoostController
{
    public static BoostMode Mode { get; private set; } = BoostMode.None;

    /// <summary>
    /// Aynı mod tekrar seçilirse None'a döner (toggle). Farklı mod seçilirse diğeri iptal olur.
    /// </summary>
    public static void Toggle(BoostMode mode)
    {
        Mode = (Mode == mode) ? BoostMode.None : mode;
    }
}
