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
    public float weaponEnergyCostPerShot; // Laser: her atışta harcanan enerji
    public float weaponChargeTime;        // Plasma: dolum süresi (saniye)
    public int   weaponBurstCount;        // Plasma: burst başına mermi sayısı

    public ComponentDefinition upgradeTo;
}
