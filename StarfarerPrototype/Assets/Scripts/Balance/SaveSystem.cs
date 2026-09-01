using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kampanya kaydı. 100 level tek oturumda oynanamaz — kayıt olmadan eğrinin
/// ikinci yarısı test bile edilemez.
///
/// PlayerPrefs + JsonUtility: prototip için yeterli, dosya yönetimi gerektirmez.
/// Tek slot vardır; "kayıt slotu seçme" akışı oynanışa bir şey katmıyor.
///
/// Komponent tanımları runtime'da üretildiği için referansları kaydedilemez;
/// tip + (turret ise) uzmanlaşma + (silah ise) silah tipi yazılır ve
/// <see cref="ComponentCatalog.Resolve"/> ile geri bulunur.
///
/// v2: tier zincirleri kaldırıldı. v1 kayıtları GEÇERSİZDİR ve sessizce yok
/// sayılır — yarısı artık var olmayan bir tier'a işaret ediyordu, göç etmeye
/// çalışmak "Mk3 kalkanım Mk1 oldu" gibi sessiz kayıplar üretirdi.
/// </summary>
public static class SaveSystem
{
    const string Key            = "starfarer.save.v2";
    const string MaxLevelKey    = "starfarer.maxLevel";
    const int    CurrentVersion = 2;

    // ── Serileştirilen yapı ───────────────────────────────────────────────────

    [Serializable]
    public class SlotSave
    {
        public int    slot;
        public int    componentType;
        public int    turretBase;
        public int    turretSpec;
        public int    weaponType;
        public string statKeys;      // "a|b|c" — JsonUtility Dictionary desteklemez
        public string statLevels;    // "1|2|3"
    }

    [Serializable]
    public class WeaponSave
    {
        public int weaponType;
        public int damageLevel;
        public int fireRateLevel;
    }

    [Serializable]
    public class SaveData
    {
        public int   version = CurrentVersion;
        public int   level   = 1;
        public float metal;
        public float crystal;
        public int   difficulty;
        public int   activeWeapon;
        public List<SlotSave>   slots   = new();
        public List<WeaponSave> weapons = new();
    }

    // ── Sorgular ──────────────────────────────────────────────────────────────

    public static bool HasSave => PlayerPrefs.HasKey(Key);

    /// <summary>Kayıttaki level — menüde "Devam Et (Level 34)" göstermek için.</summary>
    public static int SavedLevel
    {
        get
        {
            var d = Load();
            return d?.level ?? 1;
        }
    }

    /// <summary>
    /// Ulaşılmış en yüksek level. Level seçimi bununla sınırlanır — istenen her
    /// levele atlamak denge testini kolaylaştırırdı ama ilerlemeyi anlamsız kılardı.
    /// </summary>
    public static int MaxReachedLevel
    {
        get => Mathf.Max(1, PlayerPrefs.GetInt(MaxLevelKey, 1));
        set
        {
            if (value <= MaxReachedLevel) return;
            PlayerPrefs.SetInt(MaxLevelKey, value);
            PlayerPrefs.Save();
        }
    }

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }

    // ── Kaydetme ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Sahnedeki durumu yazar. Level geçişlerinde çağrılır — savaş ortasında
    /// kaydetmek yarım kalmış bir dalgayı geri yüklemeye çalışmak demek olurdu.
    /// </summary>
    public static void Save()
    {
        var loadout = UnityEngine.Object.FindFirstObjectByType<ShipLoadout>();
        var inv     = ResourceInventory.Instance;
        if (loadout == null || inv == null) return;

        var d = new SaveData
        {
            version      = CurrentVersion,
            level        = GameProgress.CurrentLevel,
            metal        = inv.metal,
            crystal      = inv.crystal,
            difficulty   = (int)DifficultyManager.Current,
            activeWeapon = (int)loadout.GetActiveWeaponType(),
        };

        foreach (var (slot, def, comp) in loadout.EnumerateSlots())
        {
            // Silahlar slot komponenti değil; ayrı listede tutulur
            if (def.componentType == ComponentType.Weapon) continue;

            var keys   = new List<string>();
            var levels = new List<string>();
            if (comp != null)
                foreach (var kv in comp.StatLevels)
                {
                    keys.Add(kv.Key);
                    levels.Add(kv.Value.ToString());
                }

            d.slots.Add(new SlotSave
            {
                slot          = slot,
                componentType = (int)def.componentType,
                turretBase    = (int)def.turretBaseType,
                turretSpec    = (int)def.turretSpecType,
                weaponType    = (int)def.weaponType,
                statKeys      = string.Join("|", keys),
                statLevels    = string.Join("|", levels),
            });
        }

        foreach (WeaponType wt in Enum.GetValues(typeof(WeaponType)))
        {
            if (!loadout.IsWeaponTypeUnlocked(wt)) continue;
            d.weapons.Add(new WeaponSave
            {
                weaponType    = (int)wt,
                damageLevel   = loadout.GetWeaponStatLevel(wt, "damage"),
                fireRateLevel = loadout.GetWeaponStatLevel(wt, "fireRate"),
            });
        }

        PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
        MaxReachedLevel = d.level;
    }

    // ── Yükleme ───────────────────────────────────────────────────────────────

    public static SaveData Load()
    {
        if (!PlayerPrefs.HasKey(Key)) return null;
        try
        {
            var d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
            return d != null && d.version == CurrentVersion ? d : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Kayıt okunamadı, yok sayılıyor: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Kaydı sahneye uygular. ShipLoadout.Start() başlangıç donanımını kurduktan
    /// SONRA çağrılmalıdır — yoksa bedava komponentler kaydın üstüne eklenir.
    /// </summary>
    public static bool Apply(SaveData d)
    {
        if (d == null) return false;

        var loadout = UnityEngine.Object.FindFirstObjectByType<ShipLoadout>();
        var inv     = ResourceInventory.Instance;
        if (loadout == null || inv == null) return false;

        GameProgress.CurrentLevel = d.level;
        DifficultyManager.Current = (Difficulty)d.difficulty;

        loadout.ClearAllSlots();

        foreach (var s in d.slots)
        {
            var def = ComponentCatalog.Resolve(
                (ComponentType)s.componentType,
                (TurretBaseType)s.turretBase, (TurretSpecType)s.turretSpec,
                (WeaponType)s.weaponType);
            if (def == null) continue;

            loadout.RestoreSlot(s.slot, def, ParseStats(s.statKeys, s.statLevels));
        }

        foreach (var w in d.weapons)
            loadout.RestoreWeapon((WeaponType)w.weaponType, w.damageLevel, w.fireRateLevel);

        loadout.FinishRestore((WeaponType)d.activeWeapon);

        // Kaynaklar depo kapasitesine bağlı; slotlar kurulduktan SONRA yazılmalı
        inv.metal   = Mathf.Min(d.metal,   inv.maxMetal);
        inv.crystal = Mathf.Min(d.crystal, inv.maxCrystal);

        return true;
    }

    static Dictionary<string, int> ParseStats(string keys, string levels)
    {
        var result = new Dictionary<string, int>();
        if (string.IsNullOrEmpty(keys)) return result;

        var k = keys.Split('|');
        var v = levels.Split('|');
        for (int i = 0; i < k.Length && i < v.Length; i++)
            if (!string.IsNullOrEmpty(k[i]) && int.TryParse(v[i], out var lvl))
                result[k[i]] = lvl;
        return result;
    }
}
