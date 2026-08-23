public enum ComponentType { Generator, Shield, RepairUnit, Weapon, Turret, Hangar, Storage }
public enum ResourceType { RawMaterial, EnergyCrystal }
public enum WeaponType { Kinetic, Laser, Plasma }

public enum TurretBaseType { Kinetic, Energy, Missile }

public enum TurretSpecType
{
    None,           // Uzmanlaşmamış — temel tip davranışı

    // Kinetic
    Gatling,        // Hızlı ateş, şarjör sistemi
    PointDefence,   // Kısa menzil, bomba/roket düşürür
    Flak,           // Alan hasarı  [ileride]
    Railgun,        // Yavaş, çok yüksek tek atış hasarı  [ileride]

    // Energy
    Laser,          // Sürekli enerji ışını
    Plasma,         // Yavaş, yüksek hasarlı top
    EMP,            // Hasar vermez, yavaşlatır  [ileride]

    // Missile
    HomingRocket,   // Güdümlü roket
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
        TurretSpecType.ClusterMissile => TurretBaseType.Missile,
        TurretSpecType.DecoyLauncher  => TurretBaseType.Missile,
        _                             => TurretBaseType.Kinetic,
    };

    public static string GetBaseTypeName(TurretBaseType bt) => bt switch
    {
        TurretBaseType.Kinetic => "Raylı Turret",
        TurretBaseType.Energy  => "Enerji Turret",
        TurretBaseType.Missile => "Füze Turret",
        _                      => "Turret",
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
        TurretSpecType.ClusterMissile => "Cluster Missile",
        TurretSpecType.DecoyLauncher  => "Decoy Launcher",
        _                             => "—",
    };

    public static TurretSpecType[] GetSpecsForBase(TurretBaseType bt) => bt switch
    {
        TurretBaseType.Kinetic => new[] { TurretSpecType.Gatling, TurretSpecType.PointDefence },
        TurretBaseType.Energy  => new[] { TurretSpecType.Laser, TurretSpecType.Plasma },
        TurretBaseType.Missile => new[] { TurretSpecType.HomingRocket },
        _                      => new TurretSpecType[0],
    };
}
