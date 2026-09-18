public enum ComponentType { Generator, Shield, RepairUnit, Weapon, Turret, Hangar, Storage }
public enum ResourceType { RawMaterial, EnergyCrystal }
public enum WeaponType { Kinetic, Laser, Plasma }

public enum TurretBaseType { Kinetic, Energy, Missile }

/// <remarks>
/// SIRA KAYDA YAZILIR. Uzmanlaşma kayda <c>(int)</c> olarak giriyor
/// (bkz. SaveSystem.ApplyShip) ve mermiler de görsellerini <c>(int)specType</c>
/// ile saklıyor. Yani araya değer eklemek, o değerden SONRAKİ her uzmanlaşmayı
/// eski kayıtlarda başka bir şeye çevirir — kurulu bir Point Defence sessizce
/// Flak olarak geri gelirdi.
///
/// Yeni değer eklerken kural: ya listenin SONUNA, ya da yalnızca hiç
/// ulaşılamayan ([ileride]) değerlerin önüne. NuclearRocket ikinci yolla
/// eklendi — ardındaki ClusterMissile ve DecoyLauncher hiçbir kayıtta olamaz,
/// çünkü ne GetSpecsForBase onları listeliyor ne de TurretSpec tanımlıyor.
/// </remarks>
public enum TurretSpecType
{
    None,           // Uzmanlaşmamış — temel tip davranışı

    // Kinetic
    Gatling,        // Hızlı ateş, şarjör sistemi
    PointDefence,   // Kısa menzil, bomba/roket düşürür
    Flak,           // Yakınlık tapalı serpinti — kalabalığa alan hasarı
    Railgun,        // Yavaş, çok yüksek tek atış hasarı  [ileride]

    // Energy
    Laser,          // Sürekli enerji ışını
    Plasma,         // Yavaş, yüksek hasarlı top
    EMP,            // Hasar vermez, yavaşlatır  [ileride]

    // Missile
    HomingRocket,   // Güdümlü roket — çarpınca patlar, alan hasarı
    NuclearRocket,  // Nükleer başlık — çok yavaş, geniş ve ağır patlama
    ClusterMissile, // Parçalanmalı roket  [ileride]
    DecoyLauncher,  // Sahte hedef fırlatır  [ileride]
}

public static class TurretSpecHelper
{
    public static TurretBaseType GetBaseType(TurretSpecType spec) => spec switch
    {
        TurretSpecType.Gatling        => TurretBaseType.Kinetic,
        TurretSpecType.PointDefence   => TurretBaseType.Kinetic,
        TurretSpecType.Flak           => TurretBaseType.Kinetic,
        TurretSpecType.Railgun        => TurretBaseType.Kinetic,
        TurretSpecType.Laser          => TurretBaseType.Energy,
        TurretSpecType.Plasma         => TurretBaseType.Energy,
        TurretSpecType.EMP            => TurretBaseType.Energy,
        TurretSpecType.HomingRocket   => TurretBaseType.Missile,
        TurretSpecType.NuclearRocket  => TurretBaseType.Missile,
        TurretSpecType.ClusterMissile => TurretBaseType.Missile,
        TurretSpecType.DecoyLauncher  => TurretBaseType.Missile,
        _                             => TurretBaseType.Kinetic,
    };

    /// <summary>
    /// Anahtarlar ComponentCatalog'daki turret tanımlarıyla AYNIDIR; iki yer
    /// aynı metni göstermeli.
    /// </summary>
    public static string GetBaseTypeName(TurretBaseType bt) => bt switch
    {
        TurretBaseType.Kinetic => Loc.T("component.turret.kinetic"),
        TurretBaseType.Energy  => Loc.T("component.turret.energy"),
        TurretBaseType.Missile => Loc.T("component.turret.missile"),
        _                      => Loc.T("component.turret.fallback"),
    };

    public static string GetSpecName(TurretSpecType spec) => spec switch
    {
        TurretSpecType.None           => "—",
        TurretSpecType.Gatling        => "Gatling",
        TurretSpecType.PointDefence   => "Point Defence",
        TurretSpecType.Flak           => "Flak Cannon",
        TurretSpecType.Railgun        => "Railgun",
        TurretSpecType.Laser          => "Laser",
        TurretSpecType.Plasma         => "Plasma",
        TurretSpecType.EMP            => "EMP",
        TurretSpecType.HomingRocket   => "Homing Rocket",
        TurretSpecType.NuclearRocket  => "Nuclear Warhead",
        TurretSpecType.ClusterMissile => "Cluster Missile",
        TurretSpecType.DecoyLauncher  => "Decoy Launcher",
        _                             => "—",
    };

    public static TurretSpecType[] GetSpecsForBase(TurretBaseType bt) => bt switch
    {
        TurretBaseType.Kinetic => new[] { TurretSpecType.Gatling, TurretSpecType.PointDefence,
                                          TurretSpecType.Flak },
        TurretBaseType.Energy  => new[] { TurretSpecType.Laser, TurretSpecType.Plasma },
        TurretBaseType.Missile => new[] { TurretSpecType.HomingRocket,
                                          TurretSpecType.NuclearRocket },
        _                      => new TurretSpecType[0],
    };
}
