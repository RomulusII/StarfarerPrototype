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
        if (other.GetComponent<BarrierShield>() != null) return ImpactSurface.Shield;
        return other.GetComponent<Asteroid>() != null ? ImpactSurface.Rock
                                                      : ImpactSurface.Hull;
    }

    /// <summary>Hedefin zırhı. Zırh kavramı olmayan hedeflerde 0.</summary>
    public static float ArmorOf(Collider2D other)
    {
        if (other == null) return 0f;

        var barrier = other.GetComponent<BarrierShield>();
        if (barrier != null && barrier.owner != null) return barrier.owner.ArmorValue;

        var enemy = other.GetComponent<EnemyBot>();
        if (enemy != null) return enemy.ArmorValue;

        var boss = other.GetComponent<BossShip>();
        if (boss != null) return boss.ArmorValue;

        return 0f;   // hardpoint ve asteroit zırhsız
    }

    /// <summary>
    /// Çarpışılan collider'a hasar uygular.
    /// Sıra: BarrierShield → BossHardpoint → BossShip gövdesi → EnemyBot → Asteroid
    /// Hasar uygulandıysa true döner.
    /// </summary>
    /// <param name="armorPreApplied">
    /// Işınlar için true. Zırh eşiği atış başına işler; sürekli bir kaynak onu
    /// kendisi ORAN olarak hesaplar (BalanceConfig.BeamArmorEfficiency) ve
    /// hedefin bir kez daha kesmesini istemez.
    /// </param>
    public static bool TryDamage(Collider2D other, float damage, WeaponType weaponType,
                                 bool armorPreApplied = false)
    {
        // Yay kalkanı — gövdenin ÖNÜNDE ayrı bir collider. Önden gelen mermi
        // buraya çarpar; yandan gelen onu ıskalayıp aşağıdaki gövde dalına düşer.
        var barrier = other.GetComponent<BarrierShield>();
        if (barrier != null && barrier.owner != null)
        {
            barrier.owner.TakeShieldDamage(damage, weaponType, armorPreApplied);
            return true;
        }

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
            boss.TakeDamage(damage, weaponType, armorPreApplied);
            return true;
        }

        // Normal düşman
        var enemy = other.GetComponent<EnemyBot>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, weaponType, armorPreApplied);
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
