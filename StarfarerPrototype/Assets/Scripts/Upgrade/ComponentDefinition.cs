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
    public float weaponDamage;
    public float weaponFireRate;

    public ComponentDefinition upgradeTo;
}
