using UnityEngine;

/// <summary>
/// Mermi çarpışmalarında hedef tespiti için ortak yardımcı.
/// EnemyBot, BossHardpoint, BossShip gövdesi ve Asteroid'i tek noktadan yönetir.
/// </summary>
public static class DamageUtil
{
    /// <summary>
    /// Bu collider hangi yüzey? Kıvılcım rengi buradan gelir.
    /// Hedef tespitiyle aynı dosyada durur: iki soru da "bu collider ne"
    /// sorusunun parçası ve ayrı yerlerde yaşasalardı biri diğerinden sapardı.
    /// </summary>
    public static ImpactSurface SurfaceOf(Collider2D other)
    {
        if (other == null) return ImpactSurface.Hull;
        return other.GetComponent<Asteroid>() != null ? ImpactSurface.Rock
                                                      : ImpactSurface.Hull;
    }

    /// <summary>
    /// Çarpışılan collider'a hasar uygular.
    /// Sıra: BossHardpoint → BossShip gövdesi → EnemyBot → Asteroid
    /// Hasar uygulandıysa true döner.
    /// </summary>
    public static bool TryDamage(Collider2D other, float damage, WeaponType weaponType)
    {
        // Boss hardpoint (collider doğrudan hardpoint GO'sunda)
        var hardpoint = other.GetComponent<BossHardpoint>();
        if (hardpoint != null && hardpoint.IsAlive)
        {
            hardpoint.TakeDamage(damage);
            return true;
        }

        // Boss gövdesi (collider boss'un ana GO'sunda, hardpoint değil)
        var boss = other.GetComponent<BossShip>();
        if (boss != null)
        {
            boss.TakeDamage(damage, weaponType);
            return true;
        }

        // Normal düşman
        var enemy = other.GetComponent<EnemyBot>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, weaponType);
            return true;
        }

        // Asteroit — düşman değil ama vurulabilir ve parçalanır
        var asteroid = other.GetComponent<Asteroid>();
        if (asteroid != null)
        {
            asteroid.TakeDamage(damage, weaponType);
            return true;
        }

        return false;
    }
}
