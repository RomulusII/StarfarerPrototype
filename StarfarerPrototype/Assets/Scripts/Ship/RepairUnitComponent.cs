using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pasif tamir ünitesi. Sahnedeki hasarlı komponentleri enerji harcayarak
/// yavaşça tamir eder. Bir anda tek komponenti hedefler: HP oranı en düşük olan.
///
/// Ayrıca ana geminin gövdesini taşır — iki ayrı iz, iki ayrı soru:
///
///   Gövde ("armor")   — max gövde HP'si. "Daha çok HP."
///   Zırh  ("plating") — gövdeye gelen HER İSABETTEN sabit hasar düşer,
///                       düşmanlardaki zırh eşiğinin aynısı. "Küçük isabetler
///                       sayılmasın."
///
/// Onarım birimine bağlanmalarının sebebi tematik değil, yapısal — gövde bakımı
/// zaten bu modülün işi ve ikisi tamir hızıyla aynı slotta rekabet ediyor.
///
/// Gövde izinin anahtarı tarihsel olarak "armor"dır ve kayıtlarda o adla
/// duruyor; ekranda "Gövde" yazar. Zırh izi eski "Enerji Verimi"nin yerini aldı
/// (o iz, en yüksek iz olduğunda tasarruf ettiğinden fazla enerji yakıyordu).
/// </summary>
public class RepairUnitComponent : ShipComponentBase
{
    public float repairRate      = 8f;
    public float energyPerRepair = 1f;

    /// <summary>Gövde (max HP) izinin anahtarı — kayıtla uyum için adı "armor" kaldı.</summary>
    public const string ArmorKey = "armor";

    /// <summary>Zırh (isabet başına hasar düşümü) izinin anahtarı.</summary>
    public const string PlatingKey = "plating";

    /// <summary>
    /// Seviye başına zırh. 0.5: Sv2 = 1 zırh Swarm'ın 3 hasarlı mermisini 2'ye
    /// indirir, Sv6 = 3 zırh onu tabana (%10) düşürür. Ağır toplara (15–30)
    /// neredeyse dokunmaz — kalabalığın cevabıdır, ağır tipleri önemsizleştirmez.
    /// </summary>
    public const float PlatingPerLevel = 0.5f;

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

    // ── Gövde ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bu birimin gövdeye kattığı EK HP. Çarpan değil toplam kullanılır: iki
    /// zırhlı onarım birimi çarpılsaydı ikinci birim birincinin katı kadar
    /// değer üretirdi ve tek doğru oyun "hepsini onarım birimiyle doldur"
    /// olurdu. Toplamsal olunca ikinci birim aynı miktarı ekler, fazlası değil.
    /// </summary>
    public float HullBonus(float baseHull)
        => baseHull * (BalanceConfig.Instance.StatMultiplier(GetStatLevel(ArmorKey)) - 1f);

    /// <summary>Sahnedeki tüm onarım birimlerinin gövde bonusunu toplayıp gemiye uygular.</summary>
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

    // ── Zırh ──────────────────────────────────────────────────────────────────

    /// <summary>Bu birimin zırhı (seviye × 0.5).</summary>
    public float Plating => GetStatLevel(PlatingKey) * PlatingPerLevel;

    /// <summary>
    /// Ana geminin gövde zırhı — çalışan birimlerin EN YÜKSEĞİ, toplamı değil.
    /// Zırh bir eşiktir; toplansaydı iki birim Swarm'ı hiç hasar veremez hâle
    /// getirirdi. Hasarlı/deaktif birimin zırhı sayılmaz: gövde bakımını yapan
    /// modül çalışmıyorsa kaplama da tutmaz.
    /// </summary>
    public static float HullPlating
    {
        get
        {
            float best = 0f;
            foreach (var ru in s_units)
                if (ru != null && ru.IsOperational && ru.Plating > best) best = ru.Plating;
            return best;
        }
    }

    /// <summary>
    /// Bu birime zırh yükseltmesi satılabilir mi? Yalnızca BAŞKA bir birimde
    /// zırh yokken. En yüksek geçerli olduğu için ikinci birime basılan zırh
    /// hiçbir şey yapmazdı — oyuncuyu boş bir yükseltmeye ödeme yaptırmak yerine
    /// iz o birimde hiç listelenmez. Zırhlı birim satılırsa iz yeniden açılır.
    /// </summary>
    public bool CanTakePlating
    {
        get
        {
            foreach (var ru in s_units)
                if (ru != null && ru != this && ru.GetStatLevel(PlatingKey) > 0) return false;
            return true;
        }
    }

    // ── Tamir ─────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!IsOperational) return;

        ShipComponentBase compTarget = FindMostDamagedComponent();
        float compRatio = compTarget != null && compTarget.maxHP > 0f
            ? compTarget.currentHP / compTarget.maxHP : 1f;

        var ps = FindFirstObjectByType<PlayerShip>();
        float hullRatio = ps != null && ps.maxHullHP > 0f
            ? ps.currentHullHP / ps.maxHullHP : 1f;

        bool repairHull = ps != null && hullRatio < 1f && hullRatio <= compRatio;

        // Onarılacak bir şey yoksa enerji ÇEKİLMEZ. Eskiden istek aramadan önce
        // yapılıyordu: birim boştayken de saniyede 1 enerji yakıyordu.
        if (!repairHull && compTarget == null) return;

        float effectiveRate = repairRate * GetMultiplier("repairRate");

        if (EnergyBus.Instance == null ||
            !EnergyBus.Instance.RequestEnergy(energyPerRepair * Time.deltaTime))
            return;

        // En çok hasarlı hedefi onar (hull veya komponent)
        if (repairHull)
        {
            float before = ps.currentHullHP;
            ps.currentHullHP = Mathf.Min(ps.maxHullHP, ps.currentHullHP + effectiveRate * Time.deltaTime);
            Stats.repairedHull += ps.currentHullHP - before;
        }
        else
        {
            float before = compTarget.currentHP;
            compTarget.Repair(effectiveRate * Time.deltaTime);
            Stats.repairedParts += compTarget.currentHP - before;
        }
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
