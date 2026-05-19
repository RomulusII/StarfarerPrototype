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

    public float productionAmount;
    public float maxShield;
    public float rechargeRate;
    public float repairRate;
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
