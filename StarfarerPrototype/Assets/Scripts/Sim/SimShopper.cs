using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sahte oyuncunun ALIŞVERİŞ tarafı. Nişan ve ateş ayrı sınıfta (SimPilot).
///
/// NEDEN EN AZ İKİ PROFİL: tek bir politika kendi tercihini denge sanır.
/// Bugüne kadarki bütün stat eğrisi hesabı "en ucuz statı al" varsayımıyla
/// yapıldı (bkz. CLAUDE.md, statStep tablosu) ve o varsayım hiç sınanmadı.
/// Gerçek oyuncu ikisinin arasındadır; ikisini birden koşturmak, eğrinin
/// hangi aralıkta sağlam olduğunu gösterir.
///
///   ucuz — her an alınabilecek EN UCUZ yükseltmeyi al. Genişler, derinleşmez.
///   odak — tek bir izi sonuna kadar götür, o iz için PARA BİRİKTİR.
///
/// KİLİT AÇICILAR her iki profilde de ortaktır ve politikanın parçası değil,
/// oyunun kurallarının sonucudur:
///   • Depo — istenen yükseltme kaynak TAVANINDAN pahalıysa hiç birikemez.
///     Depo kurulmadan Sv6 üstü hiçbir şey alınamaz; oyuncu bunu görmeden
///     ilerleyemez, simülasyon da göremezse sonsuza kadar bekler.
///   • Jeneratör — enerji kapısına takılan bir yükseltme parayla açılmaz.
/// İkisi de "politika" değil, çıkmaz kaçınmasıdır.
///
/// ALIŞVERİŞ EKRANI AÇILMAZ. Gerçek oyuncu Tab'a basıp oyunu durdurur; sahte
/// oyuncu satın almayı akış içinde yapar. Fark ölçülen şeye dokunmaz: duran
/// oyunda geçen süre zaten sıfırdır. Duraklatmayı taklit etseydik ölçüm
/// UI akışını da ölçmeye başlardı.
///
/// EKSİK (bilinçli): turret uzmanlaşması (Point Defence, Gatling…) satın
/// alınmıyor. Uzmanlaşma güç değil KARAKTER seçimidir ve doğru profilini
/// yazmak, bomba yoğunluğu gibi ölçülmemiş sayılara dayanır. Önce temel
/// eğri ölçülecek.
/// </summary>
public class SimShopper : MonoBehaviour
{
    /// <summary>Karar sıklığı (oyun saniyesi).</summary>
    const float ShopInterval = 1.0f;

    /// <summary>Tek karar turunda yapılabilecek en fazla alım — sonsuz döngü emniyeti.</summary>
    const int MaxBuysPerTick = 8;

    enum Kind { Install, ComponentStat, WeaponStat }

    struct Option
    {
        public Kind                Kind;
        public ComponentDefinition Def;
        public int                 Slot;
        public string              Key;
        public WeaponType          Weapon;
        public int                 Cost;
        public ResourceType        Resource;
    }

    ShipLoadout _loadout;
    float       _nextShop;

    void Start() => _loadout = GetComponent<ShipLoadout>();

    void Update()
    {
        if (_loadout == null) { _loadout = GetComponent<ShipLoadout>(); return; }
        if (ResourceInventory.Instance == null) return;
        if (Time.time < _nextShop) return;

        _nextShop = Time.time + ShopInterval;

        bool focused = SimRuntime.Config.profile == "odak";
        for (int i = 0; i < MaxBuysPerTick; i++)
            if (!(focused ? BuyFocused() : BuyCheapest())) break;
    }

    // ── Profil: en ucuz ──────────────────────────────────────────────────────

    bool BuyCheapest()
    {
        var options = Enumerate();
        Option best = default;
        bool   found = false;

        foreach (var o in options)
        {
            if (!Affordable(o)) continue;
            if (!found || o.Cost < best.Cost) { best = o; found = true; }
        }

        if (found) return Execute(best);

        // Hiçbir şey alınamıyor: sebebi para değil de tavan veya enerji ise
        // kilidi aç. Değilse beklemek doğru davranış.
        return Unblock(options);
    }

    // ── Profil: odaklı ───────────────────────────────────────────────────────

    /// <summary>
    /// Öncelik listesi. Sıra bir denge iddiası değil, bir OYUNCU tipidir:
    /// önce ana silahın hasarı (tek atış hasarı zırh eşiğini yener), sonra
    /// otomatik ateş gücü, sonra hayatta kalma.
    /// </summary>
    bool BuyFocused()
    {
        var options = Enumerate();

        // 1) Ana silah hasarı
        if (TryTake(options, o => o.Kind == Kind.WeaponStat && o.Key == "damage")) return true;
        // 2) İlk iki turret
        if (CountInstalled(ComponentType.Turret) < 2 &&
            TryTake(options, o => o.Kind == Kind.Install &&
                                  o.Def.componentType == ComponentType.Turret)) return true;
        // 3) Turret hasarı
        if (TryTake(options, o => o.Kind == Kind.ComponentStat &&
                                  o.Def.componentType == ComponentType.Turret &&
                                  o.Key == "damage")) return true;
        // 4) Kalkan
        if (TryTake(options, o => o.Kind == Kind.ComponentStat &&
                                  o.Def.componentType == ComponentType.Shield &&
                                  o.Key == "maxShield")) return true;

        // Sıradaki hedef alınamıyorsa PARA BİRİKTİRİLİR — odaklı oyuncunun
        // tanımı bu. Yalnızca çıkmaz varsa müdahale edilir.
        return Unblock(options);
    }

    bool TryTake(List<Option> options, System.Func<Option, bool> match)
    {
        foreach (var o in options)
            if (match(o) && Affordable(o))
                return Execute(o);
        return false;
    }

    // ── Çıkmaz kaçınma ───────────────────────────────────────────────────────

    /// <summary>
    /// Hiçbir alım yapılamadığında: hedeflenen yükseltme kaynak TAVANININ
    /// üstündeyse depo, enerjiye takılıyorsa jeneratör alınır. İkisi de
    /// alınamıyorsa gerçekten beklemek gerekiyordur.
    /// </summary>
    bool Unblock(List<Option> options)
    {
        var inv = ResourceInventory.Instance;

        bool capBlocked    = false;
        bool energyBlocked = false;

        foreach (var o in options)
        {
            if (o.Cost > inv.CapacityOf(o.Resource)) capBlocked = true;
            if (o.Kind == Kind.ComponentStat &&
                !ShipLoadout.HasEnergyHeadroom(_loadout.StatUpgradeEnergyDelta(o.Slot, o.Key)))
                energyBlocked = true;
        }

        if (capBlocked    && BuyType(options, ComponentType.Storage))   return true;
        if (energyBlocked && BuyType(options, ComponentType.Generator)) return true;
        return false;
    }

    /// <summary>Bu tipten önce yükseltme, o olmazsa kurulum dener.</summary>
    bool BuyType(List<Option> options, ComponentType type)
    {
        foreach (var o in options)
            if (o.Kind == Kind.ComponentStat && o.Def.componentType == type && Affordable(o))
                return Execute(o);

        foreach (var o in options)
            if (o.Kind == Kind.Install && o.Def.componentType == type && Affordable(o))
                return Execute(o);

        return false;
    }

    // ── Seçenekler ───────────────────────────────────────────────────────────

    /// <summary>
    /// Şu anda YASAL olan bütün alımlar (karşılanabilir olsun ya da olmasın).
    /// Karşılanabilirlik ayrı sorulur: kilit açıcıların "neden alınamıyor"
    /// sorusuna cevap verebilmesi için yasal ama pahalı olanlar da listede.
    /// </summary>
    List<Option> Enumerate()
    {
        var list = new List<Option>();
        int max  = ShipComponentBase.MaxStatLevel;

        // Kurulu komponentlerin stat izleri
        foreach (var (slot, def, comp) in _loadout.EnumerateSlots())
        {
            if (def == null || comp == null) continue;
            var tracks = ComponentCatalog.StatTracks(def.componentType);
            if (tracks == null) continue;

            foreach (var (key, _) in tracks)
            {
                int lvl = comp.GetStatLevel(key);
                if (lvl >= max) continue;
                if (!ShipLoadout.HasEnergyHeadroom(_loadout.StatUpgradeEnergyDelta(slot, key))) continue;

                list.Add(new Option
                {
                    Kind     = Kind.ComponentStat,
                    Def      = def,
                    Slot     = slot,
                    Key      = key,
                    Cost     = ComponentCatalog.StatUpgradeCost(def, lvl, key),
                    Resource = def.costResource,
                });
            }
        }

        // Ana silahın stat izleri
        var wType = _loadout.GetActiveWeaponType();
        var wDef  = _loadout.GetWeaponDef(wType);
        if (wDef != null)
        {
            foreach (var (key, _) in ComponentCatalog.WeaponStatTracks(wType))
            {
                int lvl = _loadout.GetWeaponStatLevel(wType, key);
                if (lvl >= max) continue;

                list.Add(new Option
                {
                    Kind     = Kind.WeaponStat,
                    Def      = wDef,
                    Weapon   = wType,
                    Key      = key,
                    Cost     = ComponentCatalog.StatUpgradeCost(wDef, lvl, key),
                    Resource = wDef.costResource,
                });
            }
        }

        // Boş slota kurulum — ilk boş slot yeter; hangi slota kurulduğu
        // dengeyi değiştirmiyor (slot konumu yalnızca görseldir).
        int empty = FirstEmptySlot();
        if (empty >= 0)
        {
            foreach (var def in ComponentCatalog.Purchasable)
                list.Add(new Option
                {
                    Kind     = Kind.Install,
                    Def      = def,
                    Slot     = empty,
                    Cost     = def.cost,
                    Resource = def.costResource,
                });
        }

        return list;
    }

    int FirstEmptySlot()
    {
        for (int i = 0; i < _loadout.slotCount; i++)
        {
            if (i == ShipLoadout.WeaponSlotIndex) continue;
            if (_loadout.IsSlotEmpty(i)) return i;
        }
        return -1;
    }

    int CountInstalled(ComponentType type)
    {
        int n = 0;
        foreach (var (_, def, comp) in _loadout.EnumerateSlots())
            if (def != null && comp != null && def.componentType == type) n++;
        return n;
    }

    bool Affordable(Option o)
        => ResourceInventory.Instance.Get(o.Resource) >= o.Cost;

    // ── Uygulama ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Alımı UpgradeUI ile AYNI sırayla yapar: enerji kapısı → kaynak → etki.
    /// Sıra önemli; ters olsaydı kaynağı harcayıp enerjiye takılmak mümkündü.
    /// </summary>
    bool Execute(Option o)
    {
        var inv = ResourceInventory.Instance;

        switch (o.Kind)
        {
            case Kind.Install:
                // InstallComponent enerji ve kaynağı kendi kontrol eder.
                return _loadout.InstallComponent(o.Def, o.Slot);

            case Kind.ComponentStat:
            {
                var comp = _loadout.GetSlotComponent(o.Slot);
                if (comp == null) return false;
                if (!ShipLoadout.HasEnergyHeadroom(_loadout.StatUpgradeEnergyDelta(o.Slot, o.Key)))
                    return false;
                if (!inv.TrySpend(o.Resource, o.Cost)) return false;

                comp.ApplyStatUpgrade(o.Key);
                LogUpgrade(comp.componentName, o.Key, comp.GetStatLevel(o.Key), o.Cost, o.Resource);
                return true;
            }

            case Kind.WeaponStat:
            {
                if (!inv.TrySpend(o.Resource, o.Cost)) return false;
                _loadout.ApplyWeaponStatUpgrade(o.Weapon, o.Key);
                LogUpgrade(o.Def.componentName, o.Key,
                           _loadout.GetWeaponStatLevel(o.Weapon, o.Key), o.Cost, o.Resource);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// UpgradeUI'ın yazdığı satırın AYNISI. Sahte oyuncunun yükseltme temposu,
    /// insan oturumlarıyla aynı analizden geçebilmeli — ayrı bir olay tipi
    /// kullansaydık analiz iki koda ayrılırdı.
    /// </summary>
    static void LogUpgrade(string component, string key, int level, int cost, ResourceType res)
    {
        BalanceLog.Event("upgrade")
                  .Str("komponent", component)
                  .Str("iz",        key)
                  .Num("seviye",    level)
                  .Num("maliyet",   cost)
                  .Str("kaynak",    res.ToString())
                  .End();
    }
}
