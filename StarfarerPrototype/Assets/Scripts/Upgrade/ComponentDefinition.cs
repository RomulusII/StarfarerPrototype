using UnityEngine;

[CreateAssetMenu(fileName = "ComponentDef", menuName = "Starfarer/ComponentDefinition")]
public class ComponentDefinition : ScriptableObject
{
    public string componentName;
    public ComponentType componentType;
    public int tier = 1;
    public ResourceType costResource;
    public int cost;
    public int sellValue;
    public string description;

    [Tooltip("Stat upgrade fiyatlarının dayandığı taban. Zincirin TÜM halkaları " +
             "aynı değeri taşır (son tier'ın fiyatı). Eskiden stat maliyeti " +
             "o anki tier'ın fiyatından hesaplanıyordu; Mk1'de stat basıp sonra " +
             "tier atlamak 4× ucuza geliyordu. 0 ise cost kullanılır.")]
    public int statCostBase;

    [Tooltip("Sv0 enerji tüketimi. Stat seviyesiyle birlikte büyür — jeneratör " +
             "yetişemezse yeni yükseltme yapılamaz.")]
    public float baseEnergyCost;

    public float productionAmount;
    public float maxShield;
    public float rechargeRate;
    public float repairRate;

    // Depo alanları — kaynak tavanına eklenen kapasite
    public float storageMetal;
    public float storageCrystal;
    public WeaponType weaponType;
    public float weaponDamage;
    public float weaponFireRate;
    public float weaponEnergyCostPerShot;
    public float weaponChargeTime;
    public int   weaponBurstCount;

    // Turret alanları
    public TurretBaseType turretBaseType;
    public TurretSpecType turretSpecType;
    public int            specCost;         // Uzmanlaşma maliyeti (base maliyet hariç)
    public ResourceType   specCostResource;
    public float turretFireRate;
    public float turretDamage;
    public float turretBulletSpeed;
    public float turretBulletLifeTime;
    public float turretEnergyPerShot;
    public int   turretMagazineSize;
    public float turretReloadTime;
    public float turretBurnDuration;   // Lazer spec: beam yanma süresi (saniye)

    public ComponentDefinition upgradeTo;
}
