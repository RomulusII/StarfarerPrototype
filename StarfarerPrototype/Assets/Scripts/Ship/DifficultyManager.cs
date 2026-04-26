public enum Difficulty { Easy, Normal, Hard }

/// <summary>
/// Oyun zorluğunu tutan statik sınıf.
/// Easy  — komponent HP sıfırlanınca deaktif kalır, RepairUnit onarınca yeniden açılır.
/// Normal/Hard — komponent tamamen yok edilir, yeniden kurulum gerekir.
/// </summary>
public static class DifficultyManager
{
    public static Difficulty Current { get; set; } = Difficulty.Normal;
}
