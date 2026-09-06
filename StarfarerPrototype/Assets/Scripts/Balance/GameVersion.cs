/// <summary>
/// Oyunun sürüm kimliği. Kayıtların hangi build'den geldiğini söyler.
///
/// Neden iki ayrı sayı var: bunlar farklı sorulara cevap verir.
///
///   Surum — oyuncunun gördüğü sürüm. Mağazaya giden numara budur ve
///           build script'leri PlayerSettings.bundleVersion'a bunu yazar,
///           yani ProjectSettings ile kod arasında sapma OLAMAZ.
///
///   Denge — DENGE REVİZYONU. Bir formül, bir sabit, bir düşman değeri
///           değiştiğinde artar. Sürüm numarası bunun için yetmez: aynı
///           1.0.0 altında onlarca ayar denemesi yapılır ve sonrasında
///           gelen kayıtlar öncekilerle karıştırılamaz. Log'da ayrı alan
///           olarak yazılır, analiz bu alana göre gruplar.
///
/// KURAL: dengeyi etkileyen her değişiklikte <see cref="Denge"/> artırılır.
/// Artırılmazsa iki farklı ayarın verisi tek havuzda toplanır ve ölçüm
/// sessizce anlamsızlaşır — kaybı fark etmek de mümkün olmaz.
///
/// Bu alanların HİÇ OLMADIĞI kayıtlar 2026-09-05 öncesine aittir; o
/// dosyalarda serbest mod bütçesi saatte %10 bileşik büyüyordu.
/// </summary>
public static class GameVersion
{
    /// <summary>Oyuncunun gördüğü sürüm (mağaza numarası).</summary>
    public const string Surum = "1.0.0";

    /// <summary>
    /// Denge revizyonu.
    ///   1 — serbest mod dalga bütçesi saatten KAYNAĞA taşındı,
    ///       Swarm hızı 2.4 -> 2.76.
    /// </summary>
    public const int Denge = 1;
}
