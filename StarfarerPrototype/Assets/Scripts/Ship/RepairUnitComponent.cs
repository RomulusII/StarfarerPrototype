using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pasif tamir ünitesi. Sahnedeki hasarlı komponentleri enerji harcayarak
/// yavaşça tamir eder. Bir anda tek komponenti hedefler: HP oranı en düşük olan.
///
/// Ayrıca gövde ZIRHINI taşır: "armor" statı ana geminin max HP'sini yükseltir.
/// Onarım birimine bağlanmasının sebebi tematik değil, yapısal — gövde bakımı
/// zaten bu modülün işi ve zırh, tamir hızıyla aynı slotta rekabet ediyor:
/// "daha çok HP" ile "HP'yi daha hızlı geri kazan" arasında bir seçim doğuyor.
/// </summary>
public class RepairUnitComponent : ShipComponentBase
{
    public float repairRate      = 8f;
    public float energyPerRepair = 1f;

    /// <summary>Zırh statının anahtarı — UpgradeUI, BalanceConfig ve kayıt aynı adı kullanır.</summary>
    public const string ArmorKey = "armor";

    // Kurulu onarım birimlerinin kaydı. FindObjectsByType her yükseltmede
    // taranabilirdi ama OnDisable sırasında yok edilmekte olan komponent hâlâ
    // listeye giriyor ve zırh bonusu satıştan sonra da yaşıyordu.
    static readonly List<RepairUnitComponent> s_units = new();

    protected override void Awake()
    {
        base.Awake();
        componentName = "Repair Unit";
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!s_units.Contains(this)) s_units.Add(this);
        RefreshHullArmor();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        s_units.Remove(this);
        RefreshHullArmor();
    }

    public override void OnStatUpgraded(string key)
    {
        if (key == ArmorKey) RefreshHullArmor();
    }

    // ── Zırh ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bu birimin gövdeye kattığı EK HP. Çarpan değil toplam kullanılır: iki
    /// zırhlı onarım birimi çarpılsaydı ikinci birim birincinin katı kadar
    /// değer üretirdi ve tek doğru oyun "hepsini onarım birimiyle doldur"
    /// olurdu. Toplamsal olunca ikinci birim aynı miktarı ekler, fazlası değil.
    /// </summary>
    public float HullBonus(float baseHull)
        => baseHull * (BalanceConfig.Instance.StatMultiplier(GetStatLevel(ArmorKey)) - 1f);

    /// <summary>Sahnedeki tüm onarım birimlerinin zırhını toplayıp gemiye uygular.</summary>
    public static void RefreshHullArmor()
    {
        var ship = FindFirstObjectByType<PlayerShip>();
        if (ship == null) return;

        float bonus = 0f;
        foreach (var ru in s_units)
        {
            if (ru == null) continue;
            bonus += ru.HullBonus(ship.baseMaxHullHP);
        }
        ship.SetMaxHull(ship.baseMaxHullHP + bonus);
    }

    // ── Tamir ─────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsOperational) return;

        float effectiveRate   = repairRate     * GetMultiplier("repairRate");
        float effectiveEnergy = energyPerRepair / GetMultiplier("energyEfficiency");

        if (EnergyBus.Instance == null ||
            !EnergyBus.Instance.RequestEnergy(effectiveEnergy * Time.deltaTime))
            return;

        ShipComponentBase compTarget = FindMostDamagedComponent();
        float compRatio = compTarget != null && compTarget.maxHP > 0f
            ? compTarget.currentHP / compTarget.maxHP : 1f;

        var ps = FindFirstObjectByType<PlayerShip>();
        float hullRatio = ps != null && ps.maxHullHP > 0f
            ? ps.currentHullHP / ps.maxHullHP : 1f;

        // En çok hasarlı hedefi onar (hull veya komponent)
        if (ps != null && hullRatio < 1f && hullRatio <= compRatio)
            ps.currentHullHP = Mathf.Min(ps.maxHullHP, ps.currentHullHP + effectiveRate * Time.deltaTime);
        else if (compTarget != null)
            compTarget.Repair(effectiveRate * Time.deltaTime);
    }

    ShipComponentBase FindMostDamagedComponent()
    {
        var all = FindObjectsByType<ShipComponentBase>(FindObjectsSortMode.None);

        ShipComponentBase target   = null;
        float             lowestRatio = 1f;

        foreach (var comp in all)
        {
            if (comp == this)        continue;
            if (comp.maxHP <= 0f)    continue;
            if (comp.currentHP >= comp.maxHP) continue; // hasar yok

            float ratio = comp.currentHP / comp.maxHP;
            if (ratio < lowestRatio)
            {
                lowestRatio = ratio;
                target      = comp;
            }
        }

        return target;
    }
}
