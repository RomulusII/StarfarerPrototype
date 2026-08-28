/// <summary>
/// Skin anahtarları. SkinSet asset'indeki <c>id</c> alanları bu değerlerle eşleşir.
///
/// Düşman tipleri buraya YAZILMAZ — <c>EnemyTypeData.SkinId</c> anahtarı kendi
/// adından türetir (<c>"enemy." + name.ToLower()</c>), böylece yeni düşman tipi
/// eklemek bu dosyaya dokunmayı gerektirmez.
/// </summary>
public static class SkinId
{
    // Oyuncu tarafı
    public const string PlayerBody          = "player.body";
    public const string PlayerBarrel        = "player.barrel";
    public const string Collector           = "player.collector";
    public const string Hangar              = "player.hangar";
    public const string PlayerBulletKinetic = "player.bullet.kinetic";
    public const string PlayerBulletPlasma  = "player.bullet.plasma";
    public const string Fighter             = "player.fighter";
    public const string FighterBullet       = "player.fighter.bullet";

    // Turretler — taban ve namlu ayrı sprite
    public const string TurretBase          = "turret.base";
    public const string TurretBarrel        = "turret.barrel";
    public const string TurretBullet        = "turret.bullet";

    // Düşman tarafı
    public const string EnemyBullet         = "enemy.bullet.hull";
    public const string EnemyBulletComponent= "enemy.bullet.component";
    public const string EnemyBarrel         = "enemy.barrel";
    public const string Bomb                = "enemy.bomb";
    public const string BossBody            = "boss.body";
    public const string BossHardpoint       = "boss.hardpoint";

    // Ortam
    public const string Asteroid            = "world.asteroid";
    public const string DebrisMetal         = "world.debris.metal";
    public const string DebrisCrystal       = "world.debris.crystal";

    // Efektler — beyaz doku, rengi SpriteRenderer verir
    public const string ShieldBubble        = "fx.shield";

    /// <summary>Düşman tipi adından skin anahtarı üretir (Swarm -> "enemy.swarm").</summary>
    public static string ForEnemy(string typeName) =>
        string.IsNullOrEmpty(typeName) ? null : "enemy." + Normalize(typeName);

    /// <summary>
    /// Object.Instantiate kopyanın adına "(Clone)" ekler. Runtime kopyası
    /// üzerinden gelen ad bu yüzden temizlenir — aksi halde kopya kendi skin'ini
    /// bulamaz ve hata vermeden dikdörtgene döner.
    /// </summary>
    static string Normalize(string typeName)
    {
        string s = typeName.ToLowerInvariant();
        while (s.EndsWith("(clone)")) s = s.Substring(0, s.Length - 7).TrimEnd();
        return s;
    }

    /// <summary>Boss adından skin anahtarı üretir (Sentinel -> "boss.sentinel").</summary>
    public static string ForBoss(string bossName) =>
        string.IsNullOrEmpty(bossName) ? null : "boss." + Normalize(bossName);

    /// <summary>Hardpoint tipinden skin anahtarı üretir.</summary>
    public static string ForHardpoint(HardpointType type) =>
        "boss.hardpoint." + type.ToString().ToLowerInvariant();

    /// <summary>
    /// Komponent sinif adindan skin anahtari uretir
    /// (GeneratorComponent -> "component.generator").
    /// Sinif adindan turedigi icin yeni komponent tipi eklemek bu dosyaya
    /// dokunmayi gerektirmez; SkinSet'e girdi eklemek yeterlidir.
    /// </summary>
    public static string ForComponent(string className)
    {
        if (string.IsNullOrEmpty(className)) return null;
        string s = className.ToLowerInvariant();
        if (s.EndsWith("component")) s = s.Substring(0, s.Length - 9);
        return "component." + s;
    }
}
