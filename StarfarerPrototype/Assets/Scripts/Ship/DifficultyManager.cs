using UnityEngine;

public enum Difficulty { Easy, Normal, Hard }

/// <summary>
/// Oyun zorluğunu tutan statik sınıf. Zorluk İKİ şeyi değiştirir:
///
///   1. Komponent HP'si sıfırlanınca ne olur (bkz. ShipComponentBase):
///      Easy        — deaktif kalır, RepairUnit onarınca yeniden açılır.
///      Normal/Hard — komponent tamamen yok edilir, yeniden kurulum gerekir.
///
///   2. Düşman HP'si (kalkan dahil) ve hasarı (bkz. <see cref="EnemyMultiplier"/>):
///      Easy ×0.8 · Normal ×1 · Hard ×1.2 — sayıların sahibi BalanceConfig.
///
/// İkinci madde sonradan eklendi. Uzun süre zorluğun TEK etkisi birinci
/// maddeydi, yani Normal ile Zor birebir aynı oyundu ve menüdeki seçim
/// serbest modda hiçbir şey değiştirmiyordu (orada komponent kaybı nadir).
///
/// Seçim PlayerPrefs'te hatırlanır ve kayıttan GERİ YÜKLENMEZ: menüde
/// işaretli olan zorluk oynanan zorluktur. Eskiden "Devam Et" kayıttaki
/// zorluğu sessizce geri yazıyordu — menüde Zor işaretliyken Kolay oynamak
/// mümkündü ve bunu ekrandan anlamanın yolu yoktu.
/// </summary>
public static class DifficultyManager
{
    public static Difficulty Current { get; set; } = Difficulty.Normal;

    /// <summary>
    /// Düşman HP'si (kalkan ve kalkan şarjı dahil) ile hasarının çarpanı.
    /// HP ve hasar AYNI çarpanı alır: yalnızca HP'yi büyütmek Zor modu tehlikeli
    /// değil yalnızca UZUN yapardı.
    /// </summary>
    public static float EnemyMultiplier => BalanceConfig.Instance.EnemyDifficultyMultiplier(Current);

    const string PrefKey = "starfarer.difficulty";

    /// <summary>Menü açılırken çağrılır — son seçilen zorluk işaretli gelsin.</summary>
    public static void LoadPreference()
    {
        if (PlayerPrefs.HasKey(PrefKey))
            Current = (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey), 0, 2);
    }

    public static void SavePreference()
    {
        PlayerPrefs.SetInt(PrefKey, (int)Current);
        PlayerPrefs.Save();
    }
}
