using UnityEngine;

/// <summary>
/// Dogfight'ın geçmesi gereken dünya alanı. Bizim ürettiğimiz savaşçılar
/// düşmanı kovalarken ekrandan tamamen çıkıyordu; bu sınır onları oyuncunun
/// görebileceği bölgede tutar.
///
/// Sınırlar kameranın görebildiği bölgeden türetildi (gemi (0,-2), kadraj
/// tabanı yaklaşık (4.9, -1.8), zoom 5→7):
///   - Dikey: max zoom'da görünür bant ≈ -8.7 .. +5.3. Sınır bunun biraz içinde;
///     gemi sınıra dayandığında dönüş kavisi hâlâ görüntüde kalır.
///   - Yatay: ileri (sağ) taraf çok daha geniş — kamera oraya kayıyor ve düşmanlar
///     oradan geliyor. Sol taraf dar, çünkü kadraj zaten sola pek açılmıyor.
/// </summary>
public static class CombatArea
{
    public const float MinX =  -7f;
    public const float MaxX =  17f;
    public const float MinY =  -7.5f;
    public const float MaxY =   4.5f;

    /// <summary>Sınıra bu kadar kala geri dönmeye başlanır — kenara yapışma olmasın.</summary>
    public const float Margin = 0.5f;

    public static bool Contains(Vector2 p)
        => p.x >= MinX && p.x <= MaxX && p.y >= MinY && p.y <= MaxY;

    /// <summary>
    /// Alanın dışındaysa, içeride kalan en yakın nokta (kenardan Margin kadar içeride).
    /// Gemi buraya yöneldiğinde geniş bir kavisle geri döner.
    /// </summary>
    public static Vector2 ClosestPointInside(Vector2 p) => new Vector2(
        Mathf.Clamp(p.x, MinX + Margin, MaxX - Margin),
        Mathf.Clamp(p.y, MinY + Margin, MaxY - Margin));
}
