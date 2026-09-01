using UnityEngine;

/// <summary>
/// Mermi çarpışmalarında hedef tespiti için ortak yardımcı.
/// EnemyBot, BossHardpoint, BossShip gövdesi ve Asteroid'i tek noktadan yönetir.
/// </summary>
public static class DamageUtil
{
    /// <summary>
    /// Bu collider bir KALKAN yüzeyi mi, öyleyse sahibi kim? İki kalkan tipi
    /// var — <see cref="BarrierShield"/> (yay) ve <see cref="BubbleShield"/>
    /// (küre) — ve ikisi de gövdeden AYRI collider taşır. Dört ayrı yerde
    /// "önce yayı sor, sonra küreyi sor" yazmak yerine soru burada bir kez
    /// cevaplanır; yeni bir kalkan şekli eklendiğinde de tek yer değişir.
    /// </summary>
    static EnemyBot ShieldOwnerOf(Collider2D other)
    {
        if (other == null) return null;

        var barrier = other.GetComponent<BarrierShield>();
        if (barrier != null) return barrier.owner;

        var bubble = other.GetComponent<BubbleShield>();
        return bubble != null ? bubble.owner : null;
    }

    /// <summary>
    /// Bu collider hangi yüzey? Kıvılcım rengi buradan gelir.
    /// Hedef tespitiyle aynı dosyada durur: iki soru da "bu collider ne"
    /// sorusunun parçası ve ayrı yerlerde yaşasalardı biri diğerinden sapardı.
    /// </summary>
    /// <summary>
    /// Denge kaydı için hedefin TİP ADI. "Bu silah neyi vuruyor" sorusu isabet
    /// oranını hedef tipine göre ayırmak için gerekli — kıvrak bir Avcı ile
    /// duran bir asteroit aynı sayıya karışmamalı.
    ///
    /// Yüzey tespitiyle (<see cref="SurfaceOf"/>) aynı dosyada durur: ikisi de
    /// "bu collider ne" sorusunun parçası.
    /// </summary>
    public static string TypeNameOf(Collider2D other)
    {
        if (other == null) return "?";

        var shieldOwner = ShieldOwnerOf(other);
        if (shieldOwner != null && shieldOwner.data != null) return shieldOwner.data.name;

        var enemy = other.GetComponent<EnemyBot>();
        if (enemy != null && enemy.data != null) return enemy.data.name;

        if (other.GetComponent<BossHardpoint>() != null) return "Hardpoint";
        if (other.GetComponent<BossShip>()      != null) return "Boss";
        if (other.GetComponent<Asteroid>()      != null) return "Asteroit";
        if (other.GetComponent<Bomb>()          != null) return "Bomba";

        return other.name;
    }

    public static ImpactSurface SurfaceOf(Collider2D other)
    {
        if (other == null) return ImpactSurface.Hull;
        if (ShieldOwnerOf(other) != null) return ImpactSurface.Shield;

        var bot = other.GetComponent<EnemyBot>();
        if (bot != null && bot.HasActiveShield) return ImpactSurface.Shield;

        return other.GetComponent<Asteroid>() != null ? ImpactSurface.Rock
                                                      : ImpactSurface.Hull;
    }

    /// <summary>
    /// Kalkana isabet hilalini tetikler. Çarpma NOKTASI yalnızca merminin
    /// kendisinde biliniyor; TryDamage'a bir parametre daha eklemek yerine
    /// çağıran taraf zaten elindeki konumu buraya veriyor.
    /// </summary>
    public static void ShieldFlash(Collider2D other, Vector2 hitPos)
    {
        if (other == null) return;

        var shieldOwner = ShieldOwnerOf(other);
        if (shieldOwner != null) { shieldOwner.ShieldFlash(hitPos); return; }

        other.GetComponent<EnemyBot>()?.ShieldFlash(hitPos);
    }

    /// <summary>Hedefin zırhı. Zırh kavramı olmayan hedeflerde 0.</summary>
    public static float ArmorOf(Collider2D other)
    {
        if (other == null) return 0f;

        var shieldOwner = ShieldOwnerOf(other);
        if (shieldOwner != null) return shieldOwner.ArmorValue;

        var enemy = other.GetComponent<EnemyBot>();
        if (enemy != null) return enemy.ArmorValue;

        var boss = other.GetComponent<BossShip>();
        if (boss != null) return boss.ArmorValue;

        return 0f;   // hardpoint ve asteroit zırhsız
    }

    /// <summary>
    /// Çarpışılan collider'a hasar uygular.
    /// Sıra: kalkan yüzeyi → BossHardpoint → BossShip gövdesi → EnemyBot → Asteroid
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
        // Kalkan yüzeyi — gövdeden AYRI bir collider, gövdeden önce sorulur.
        // Yay kalkanında yalnızca önden gelen mermi buraya çarpar, yandan gelen
        // onu ıskalayıp aşağıdaki gövde dalına düşer. Küresel kalkanda ise her
        // yönden gelen çarpar — kabuğu kesip gövdeyi ıskalayan mermi artık
        // öbür taraftan çıkmıyor.
        var shieldOwner = ShieldOwnerOf(other);
        if (shieldOwner != null)
        {
            shieldOwner.TakeShieldDamage(damage, weaponType, armorPreApplied);
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
