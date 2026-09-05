using UnityEngine;

/// <summary>
/// Tek bir komponentin tanımı.
///
/// TIER YOK. Bir zamanlar her komponentin Mk1→Mk2→Mk3 zinciri ve ayrıca stat
/// seviyeleri vardı; iki eksen aynı şeyi (güç) iki farklı fiyat eğrisiyle
/// satıyordu ve "önce hangisini alayım" sorusunun tek doğru cevabı vardı.
/// Zincirler kaldırıldı: her komponentin TEK sürümü var, tüm ilerleme stat
/// seviyelerinden (0–10) gelir. Silah TİPLERİ (Kinetik / Lazer / Plazma) ve
/// turret uzmanlaşmaları tier değildir — onlar duruyor, çünkü güç değil
/// KARAKTER seçtiriyorlar.
/// </summary>
[CreateAssetMenu(fileName = "ComponentDef", menuName = "Starfarer/ComponentDefinition")]
public class ComponentDefinition : ScriptableObject
{
    [Tooltip("Metin tablosu anahtarı (component.*), ekrana yazılacak ad değil. " +
             "Görünen ad ComponentCatalog.DisplayName'den gelir; telemetri ve " +
             "nesne adları anahtarı kullanarak dilden bağımsız kalır.")]
    public string componentName;
    public ComponentType componentType;
    public ResourceType costResource;
    public int cost;
    public int sellValue;
    public string description;

    [Tooltip("Stat upgrade fiyatlarının dayandığı taban. Tier'lar kalkınca stat " +
             "eğrisi tek ilerleme ekseni oldu; taban buna göre komponentin kendi " +
             "fiyatından türetilir. 0 ise cost kullanılır.")]
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
}
