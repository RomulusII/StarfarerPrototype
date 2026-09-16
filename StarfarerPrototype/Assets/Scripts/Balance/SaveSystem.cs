using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kayıt girişi. İKİ KATMAN var ve ayrım anlamlıdır:
///
///   Level başı kaydı (kampanya) — PlayerPrefs, <c>starfarer.save.v2</c>.
///       Geminin kurulumu ve kaynaklar; level sınırında yazılır. Ölüm buraya
///       döndürür: kampanyanın cezası "son tamamlanan levele dön".
///
///   Dünya kaydı (kampanya + serbest) — dosya, bkz. <see cref="WorldSave"/>.
///       Sahnenin TAMAMI: gemiler, mermiler, enkaz, zamanlayıcılar. Menüye
///       dönüşte ve uygulama arka plana atıldığında yazılır; "Devam Et" tam bu
///       ana döner. Ölüm onu siler.
///
/// Level ortası kaydı eskiden YOKTU, çünkü kısmi bir kayıt bir kaynak kasma
/// açığıydı: yarım level oyna, kaynağı topla, çık, level başından devam et,
/// tekrarla. Tam kayıtla bu açık kapanır — devam eden oyun kaldığı yerden
/// sürer, levelin başına dönmez.
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
        // Bilgi amaçlı yazılır, GERİ YÜKLENMEZ — zorluğun sahibi menüdeki
        // seçimdir (bkz. DifficultyManager).
        public int   difficulty;
        public int   activeWeapon;
        public List<SlotSave>   slots   = new();
        public List<WeaponSave> weapons = new();
    }

    // ── Sorgular ──────────────────────────────────────────────────────────────

    // Anahtarın varlığına değil OKUNABİLİRLİĞİNE bakılır: geçersiz (v1) bir
    // kayıt varken "Devam Et" etkin görünüp tıklanınca baştan başlatıyordu.
    public static bool HasSave     => WorldSave.Exists(WorldSave.Slot.Campaign) || Load() != null;
    public static bool HasFreeSave => WorldSave.Exists(WorldSave.Slot.Free);

    /// <summary>Serbest kayıttaki dalga — menüde "Devam Et (Dalga 12)" için.</summary>
    public static int SavedFreeWave => WorldSave.Load(WorldSave.Slot.Free)?.free.waveIndex ?? 0;

    /// <summary>
    /// Devam edilecek level — menüde "Devam Et (Level 34)" için. Dünya kaydı
    /// varsa o, yoksa level başı kaydı: dünya kaydı her zaman daha yenidir,
    /// level sınırında yazılan kayıt onu siler.
    /// </summary>
    public static int SavedLevel
    {
        get
        {
            var w = WorldSave.Load(WorldSave.Slot.Campaign);
            if (w != null) return w.level;
            return Load()?.level ?? 1;
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

    /// <summary>Kampanyanın İKİ katmanını da siler — yeni oyun.</summary>
    public static void Delete()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        WorldSave.Delete(WorldSave.Slot.Campaign);
    }

    public static void DeleteFree() => WorldSave.Delete(WorldSave.Slot.Free);

    // ── Kaydetme ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Level başı kaydı — level geçişlerinde çağrılır. Dünya kaydını SİLER:
    /// o artık bitmiş bir levelin ortasını anlatıyor; kalsaydı "Devam Et"
    /// oyuncuyu tamamladığı levelin içine geri götürürdü.
    /// </summary>
    public static void Save()
    {
        var d = CaptureShip();
        if (d == null) return;

        d.level = GameProgress.CurrentLevel;
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
        MaxReachedLevel = d.level;

        WorldSave.Delete(WorldSave.Slot.Campaign);
    }

    /// <summary>Geminin kurulumu — iki katmanın ortak bölümü.</summary>
    public static SaveData CaptureShip()
    {
        var loadout = UnityEngine.Object.FindFirstObjectByType<ShipLoadout>();
        var inv     = ResourceInventory.Instance;
        if (loadout == null || inv == null) return null;

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

        return d;
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
    /// Level başı kaydını sahneye uygular. ShipLoadout.Start() başlangıç
    /// donanımını kurduktan SONRA çağrılmalıdır — yoksa bedava komponentler
    /// kaydın üstüne eklenir.
    /// </summary>
    public static bool Apply(SaveData d)
    {
        if (d == null) return false;
        GameProgress.CurrentLevel = d.level;
        return ApplyShip(d);
    }

    /// <summary>
    /// Yalnızca geminin kurulumunu uygular (slotlar, silahlar, kaynaklar) — iki
    /// katmanın ortak yolu. Aynı şartla: ShipLoadout.Start()'tan SONRA.
    ///
    /// Zorluk burada YAZILMAZ; menüde seçili olan geçerlidir.
    /// </summary>
    public static bool ApplyShip(SaveData d)
    {
        if (d == null) return false;

        var loadout = UnityEngine.Object.FindFirstObjectByType<ShipLoadout>();
        var inv     = ResourceInventory.Instance;
        if (loadout == null || inv == null) return false;

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
