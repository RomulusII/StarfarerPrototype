using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Oyun dünyasının TAM kaydı: gemiler, mermiler, enkaz, zamanlayıcılar — bir
/// nesnenin pozisyonu, yönü, hız vektörü ve iç durumu dahil.
///
/// NEDEN TAM: eksik bir kayıt bir KAÇIŞ YOLUDUR. Uçuştaki mermiler kaydedilmezse
/// kalkanına bomba yaklaşan oyuncu oyunu kapatıp açarak o bombadan kurtulur;
/// düşmanlar kaydedilmezse zor bir dalga kapatıp açarak silinir. Kaydedilip
/// geri yüklenen dünya, bırakılan dünyanın AYNISI olmalı.
///
/// NEDEN DOSYA, PlayerPrefs DEĞİL: tam kayıt yoğun bir anda ~100 KB'a çıkar.
/// WebGL'de PlayerPrefs toplamda ~1 MB ile sınırlı ve tek bir blok olarak
/// tutuluyor; persistentDataPath tarayıcıda IndexedDB'ye gider ve şablonda
/// otomatik senkron açık (bkz. index.html autoSyncPersistentDataPath).
///
/// KAPSAM — kaydedilmeyenler BİLEREK dışarıda:
///   · Görsel efektler (kıvılcım, patlama, kalkan parlaması, level bandı) —
///     oyun durumu taşımazlar, yarım saniyede biterler.
///   · Asteroidin kaya şekli — her asteroit rastgele çizilir; geri yüklenen
///     kaya aynı boyutta, aynı HP'de, aynı yerde ama farklı dokuda olur.
///   · Oyuncunun basılı tuttuğu girdi (sürekli lazer, plazma şarjı) — menüye
///     dönülürken girdi zaten kilitli ve bunlar iptal ediliyor.
///   · Bölüm geçiş anlatımının İÇİNDEKİ yer — geçiş baştan oynar.
///
/// İKİ AŞAMALI GERİ YÜKLEME: nesneler ilk aşamada kurulup kimlik tablosuna
/// yazılır; referanslar (hedef, formasyon, enkaz) ikinci aşamada çözülür.
/// Kurulumunu Start'ta yapan sınıflar (EnemyBot, BossShip, Asteroid...)
/// durumlarını Start'ın SONUNDA uygular — aksi hâlde Start kendi varsayılanlarını
/// kaydın üstüne yazardı. Kimlik tablosu bu Start'lar bitene kadar yaşar
/// (<see cref="WorldRestoreFinisher"/>).
/// </summary>
public static class WorldSave
{
    public enum Slot { Campaign, Free }

    // ── Dosya ─────────────────────────────────────────────────────────────────

    // Simülasyon koşusu AYRI klasöre yazar: sim player'ı oyunla aynı şirket ve
    // ürün adını taşıyor, yani aynı persistentDataPath'i paylaşıyor. Kaydet/
    // yükle testi aynı klasörü kullansaydı editörde oynanan oyunun kaydını
    // silerdi.
    //
    // Her koşu KENDİ alt klasörüne yazar (çıktı dosyasının adıyla): testler
    // paralel koşuyor ve ortak klasörde birbirinin kaydını geri yüklüyorlardı.
    static string Dir => SimRuntime.Active
        ? Path.Combine(Application.persistentDataPath, "save-sim", SimRunTag)
        : Path.Combine(Application.persistentDataPath, "save");

    static string SimRunTag =>
        !string.IsNullOrEmpty(SimRuntime.Config.outPath)
            ? Path.GetFileNameWithoutExtension(SimRuntime.Config.outPath)
            : "s" + SimRuntime.Config.seed;

    public static string PathOf(Slot slot) =>
        Path.Combine(Dir, slot == Slot.Campaign ? "kampanya-dunya.json" : "serbest-dunya.json");

    public static WorldState Load(Slot slot)
    {
        string path = PathOf(slot);
        if (!File.Exists(path)) return null;
        try
        {
            var w = JsonUtility.FromJson<WorldState>(File.ReadAllText(path));
            return w != null && w.version == WorldState.CurrentVersion && w.ship != null ? w : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WorldSave] Kayıt okunamadı, yok sayılıyor: {e.Message}");
            return null;
        }
    }

    public static bool Exists(Slot slot) => Load(slot) != null;

    public static void Delete(Slot slot)
    {
        try
        {
            string path = PathOf(slot);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WorldSave] Kayıt silinemedi: {e.Message}");
        }
    }

    /// <summary>Sahneyi yakalar ve yazar. Başarısızsa (gemi yok) false.</summary>
    public static bool Save(Slot slot)
    {
        var w = Capture(slot == Slot.Campaign ? "kampanya" : "serbest");
        if (w == null) return false;
        Write(slot, w);
        return true;
    }

    public static void Write(Slot slot, WorldState w)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(PathOf(slot), JsonUtility.ToJson(w));
        }
        catch (Exception e)
        {
            Debug.LogError($"[WorldSave] Kayıt yazılamadı: {e.Message}");
        }
    }

    // ── Kimlikler ─────────────────────────────────────────────────────────────

    static Dictionary<UnityEngine.Object, int> s_ids;
    static Dictionary<int, Component>          s_registry;
    static ShipLoadout                         s_loadout;
    static Transform                           s_player;

    /// <summary>Bir nesnenin kayıt kimliği. Kayıt dışında çağrılırsa 0.</summary>
    public static int IdOf(UnityEngine.Object o)
    {
        if (o == null || s_ids == null) return 0;
        if (!s_ids.TryGetValue(o, out int id))
        {
            id = s_ids.Count + 1;
            s_ids[o] = id;
        }
        return id;
    }

    /// <summary>Bir Transform'un kayıt referansı: -1 ana gemi, >0 kimlik, 0 yok.</summary>
    public static int RefOf(Transform t)
    {
        if (t == null) return 0;
        if (t == s_player || t.GetComponent<PlayerShip>() != null) return -1;

        var go = t.gameObject;
        if (go.TryGetComponent<EnemyBot>(out var e))      return IdOf(e);
        if (go.TryGetComponent<BossShip>(out var b))      return IdOf(b);
        if (go.TryGetComponent<Asteroid>(out var a))      return IdOf(a);
        if (go.TryGetComponent<Bomb>(out var bomb))       return IdOf(bomb);
        if (go.TryGetComponent<FighterShip>(out var f))   return IdOf(f);
        if (go.TryGetComponent<CollectorShip>(out var c)) return IdOf(c);
        if (go.TryGetComponent<Debris>(out var d))        return IdOf(d);
        return 0;
    }

    public static int RefOf(ITurretTarget t) =>
        t is Component c && c != null ? IdOf(c) : 0;

    /// <summary>Komponent referansı: slot + 1, yok = 0.</summary>
    public static int SlotOf(ShipComponentBase comp)
    {
        if (comp == null || s_loadout == null) return 0;
        foreach (var (slot, _, c) in s_loadout.EnumerateSlots())
            if (c == comp) return slot + 1;
        return 0;
    }

    public static Component Resolve(int id)
    {
        if (id <= 0 || s_registry == null) return null;
        return s_registry.TryGetValue(id, out var c) && c != null ? c : null;
    }

    public static T Resolve<T>(int id) where T : Component => Resolve(id) as T;

    public static Transform ResolveTransform(int id)
    {
        if (id == -1) return s_player;
        var c = Resolve(id);
        return c != null ? c.transform : null;
    }

    public static ITurretTarget ResolveTarget(int id) => Resolve(id) as ITurretTarget;

    public static ShipComponentBase ResolveSlot(int slotRef)
    {
        if (slotRef <= 0 || s_loadout == null) return null;
        return s_loadout.GetSlotComponent(slotRef - 1);
    }

    /// <summary>
    /// Geri yükleme sürüyor mu (kurulumdan bitiriciye kadar)? Hareket modeli bu
    /// sürede entegrasyon yapmaz: geri yükleme karenin ortasında çalışıyor ve
    /// o karenin deltaTime'ı zaman durdurulmadan ÖNCE hesaplanmıştı — anında
    /// kurulan gemiler (toplayıcı, savaşçı) bir kare ilerliyordu.
    /// </summary>
    public static bool IsRestoring => s_registry != null;

    static void Register(int id, Component c)
    {
        if (id > 0 && c != null) s_registry[id] = c;
    }

    // ── Yakalama ──────────────────────────────────────────────────────────────

    public static WorldState Capture(string mode)
    {
        var loadout = UnityEngine.Object.FindFirstObjectByType<ShipLoadout>();
        var ship    = UnityEngine.Object.FindFirstObjectByType<PlayerShip>();
        if (loadout == null || ship == null) return null;

        s_loadout = loadout;
        s_player  = ship.transform;
        s_ids     = new Dictionary<UnityEngine.Object, int>();

        try
        {
            var w = new WorldState
            {
                mode  = mode,
                level = GameProgress.CurrentLevel,
                ship  = SaveSystem.CaptureShip(),
                hull  = ship.currentHullHP,
                energy = EnergyBus.Instance != null ? EnergyBus.Instance.currentEnergy : 0f,
                boost = (int)BoostController.Mode,
                rng   = JsonUtility.ToJson(UnityEngine.Random.state),
            };
            if (w.ship == null) return null;

            ShieldGeneratorComponent.CaptureOrphan(out w.orphanShield, out w.orphanMaxShield);

            var wc = ship.GetComponentInChildren<WeaponController>();
            if (wc != null) w.weaponCooldown = wc.CooldownRemaining;

            // Nesne listeleri SIRALANIR. FindObjectsByType'ın sırası kararsızdır;
            // sıralanmasa aynı dünya iki kez kaydedildiğinde kimlikler ve liste
            // sırası farklı çıkar — kaydet/yükle/kaydet testi eşitliği hiç
            // doğrulayamazdı. Kimlikler de bu sırayla VERİLİR, referans
            // yazılmadan önce: hedefi henüz yakalanmamış bir nesneye işaret eden
            // bir referans, yakalama sırasına bağlı bir numara almasın.
            var groups     = Sorted(UnityEngine.Object.FindObjectsByType<FormationGroup>(FindObjectsSortMode.None), g => g.Anchor, g => g.Timer);
            var enemies    = Sorted(UnityEngine.Object.FindObjectsByType<EnemyBot>(FindObjectsSortMode.None), e => e.transform.position, e => e.CurrentHP);
            var bosses     = Sorted(UnityEngine.Object.FindObjectsByType<BossShip>(FindObjectsSortMode.None), b => b.transform.position, b => b.CurrentHP);
            var asteroids  = Sorted(UnityEngine.Object.FindObjectsByType<Asteroid>(FindObjectsSortMode.None), a => a.transform.position, a => a.hp);
            var bombs      = Sorted(UnityEngine.Object.FindObjectsByType<Bomb>(FindObjectsSortMode.None), b => b.transform.position, b => b.hp);
            var debris     = Sorted(UnityEngine.Object.FindObjectsByType<Debris>(FindObjectsSortMode.None), d => d.transform.position, d => d.resourceAmount);
            var collectors = Sorted(UnityEngine.Object.FindObjectsByType<CollectorShip>(FindObjectsSortMode.None), c => c.transform.position, c => c.currentHP);
            var fighters   = Sorted(UnityEngine.Object.FindObjectsByType<FighterShip>(FindObjectsSortMode.None), f => f.transform.position, f => f.currentHP);

            foreach (var g in groups) if (g.Active) IdOf(g);
            foreach (var e in enemies)    IdOf(e);
            foreach (var b in bosses)     IdOf(b);
            foreach (var a in asteroids)  IdOf(a);
            foreach (var b in bombs)      IdOf(b);
            foreach (var d in debris)     IdOf(d);
            foreach (var c in collectors) IdOf(c);
            foreach (var f in fighters)   IdOf(f);

            foreach (var (slot, _, comp) in loadout.EnumerateSlots())
            {
                if (comp == null) continue;
                var s = new ComponentRuntimeState { slot = slot };
                comp.CaptureRuntime(s);
                w.components.Add(s);
            }

            foreach (var g in groups)     if (g.Active) w.formations.Add(g.CaptureState());
            foreach (var e in enemies)    w.enemies.Add(e.CaptureState());
            foreach (var b in bosses)     w.bosses.Add(b.CaptureState());
            foreach (var a in asteroids)  w.asteroids.Add(a.CaptureState());
            foreach (var b in bombs)      w.bombs.Add(b.CaptureState());
            foreach (var d in debris)     w.debris.Add(d.CaptureState());
            foreach (var c in collectors) w.collectors.Add(c.CaptureState());
            foreach (var f in fighters)   w.fighters.Add(f.CaptureState());

            foreach (var b in Sorted(UnityEngine.Object.FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None), b => b.transform.position, b => b.damage))
                w.enemyBullets.Add(b.CaptureState());
            foreach (var b in Sorted(UnityEngine.Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None), b => b.transform.position, b => b.damage))
                w.bullets.Add(b.CaptureState());
            foreach (var b in Sorted(UnityEngine.Object.FindObjectsByType<TurretBullet>(FindObjectsSortMode.None), b => b.transform.position, b => b.damage))
                w.turretBullets.Add(b.CaptureState());
            foreach (var b in Sorted(UnityEngine.Object.FindObjectsByType<LaserBeam>(FindObjectsSortMode.None), b => b.transform.position, b => b.Remaining))
            {
                var s = CaptureLaser(b);
                if (s != null) w.lasers.Add(s);
            }
            foreach (var p in Sorted(UnityEngine.Object.FindObjectsByType<PlasmaBeam>(FindObjectsSortMode.None), p => p.transform.position, p => p.TotalEnergy))
                w.plasma.Add(p.CaptureState());

            var spawner = UnityEngine.Object.FindFirstObjectByType<EnemySpawner>();
            if (spawner != null && spawner.debugFreeSpawn) w.free = spawner.CaptureFreeRun();

            var chapter = UnityEngine.Object.FindFirstObjectByType<ChapterManager>();
            if (chapter != null) w.chapter = chapter.CaptureState();

            var field = UnityEngine.Object.FindFirstObjectByType<AsteroidSpawner>();
            w.asteroidField = field != null ? field.CaptureState() : new AsteroidFieldState();

            return w;
        }
        finally
        {
            s_ids = null;
        }
    }

    /// <summary>
    /// Işınlar sahibinin ÇOCUĞU olarak yaşar (turret ya da düşman namlusu) ve
    /// sahibiyle birlikte döner; kayıt da sahibine göre yazılır. Oyuncunun
    /// sürekli ışını kaydedilmez — basılı tutulan girdiye bağlı.
    /// </summary>
    static LaserBeamState CaptureLaser(LaserBeam beam)
    {
        if (beam.continuous) return null;

        var s = beam.CaptureState();

        var turret = beam.GetComponentInParent<TurretController>();
        if (turret != null)
        {
            s.ownerSlot = SlotOf(turret);
            return s.ownerSlot > 0 ? s : null;
        }

        var bot = beam.GetComponentInParent<EnemyBot>();
        if (bot == null) return null;

        s.ownerEnemy = IdOf(bot);
        s.onBarrel   = beam.transform.parent != null && beam.transform.parent == bot.BarrelTransform;
        return s;
    }

    static List<T> Sorted<T>(T[] items, Func<T, Vector3> pos, Func<T, float> tie) where T : Component
    {
        var list = new List<T>(items.Length);
        foreach (var i in items) if (i != null) list.Add(i);
        list.Sort((a, b) =>
        {
            Vector3 pa = pos(a), pb = pos(b);
            int c = pa.x.CompareTo(pb.x);
            if (c != 0) return c;
            c = pa.y.CompareTo(pb.y);
            if (c != 0) return c;
            return tie(a).CompareTo(tie(b));
        });
        return list;
    }

    // ── Geri yükleme ──────────────────────────────────────────────────────────

    /// <summary>Kayıt bittiğinde bir kez tetiklenir — kaydet/yükle testi dinler.</summary>
    public static event Action RestoreFinished;

    /// <summary>
    /// Geminin kurulumu (<see cref="SaveSystem.ApplyShip"/>) uygulandıktan SONRA
    /// çağrılır. Mod durumu (bölüm / serbest spawner) çağıranın işidir.
    /// </summary>
    public static void RestoreWorld(WorldState w)
    {
        if (w == null) return;

        s_loadout  = UnityEngine.Object.FindFirstObjectByType<ShipLoadout>();
        var ship   = UnityEngine.Object.FindFirstObjectByType<PlayerShip>();
        s_player   = ship != null ? ship.transform : null;
        s_registry = new Dictionary<int, Component>();

        // ZAMAN DURUR. Geri yükleme karenin Update aşamasından SONRA çalışıyor
        // (menü akışının coroutine'i); aynı karenin LateUpdate'inde hareket
        // modeli geri yüklenen hızla bir kare ilerletiyor, bitirici ise ancak
        // sonraki karede çalışıyordu. Kaydet/yükle testi bunu tam bir karelik
        // fark olarak yakaladı (yaş +1/60 sn, toplayıcı birkaç yüzde birim
        // kaymış). timeScale 0 iken deltaTime 0 ve Time.time ilerlemez —
        // bitirici eski değeri geri koyana kadar dünya kayıttaki anda durur.
        s_timeScale     = Time.timeScale;
        Time.timeScale  = 0f;

        // ── 1. aşama: nesneleri kur, kimliklerini kaydet ─────────────────────

        var groups = new List<(FormationGroup g, FormationState s)>();
        foreach (var s in w.formations)
        {
            var g = FormationGroup.Rebuild(s);
            Register(s.id, g);
            groups.Add((g, s));
        }

        foreach (var s in w.enemies)
        {
            var bot = EnemySpawner.Rebuild(RebuildEnemyData(s.dataJson, s.typeName), s.pos);
            if (bot == null) continue;
            bot.PendingRestore = s;   // Start'ın sonunda uygulanır
            Register(s.id, bot);
        }

        foreach (var s in w.bosses)
        {
            var boss = BossShip.Rebuild(s);
            if (boss != null) Register(s.id, boss);
        }

        foreach (var s in w.asteroids)
        {
            var a = Asteroid.Spawn(s.pos, (Asteroid.Size)s.size, s.drift, s.separation);
            a.PendingRestore = s;
            Register(s.id, a);
        }

        foreach (var s in w.debris)     Register(s.id, Debris.Rebuild(s));
        foreach (var s in w.bombs)      Register(s.id, Bomb.Rebuild(s));

        var collectors = new List<(CollectorShip c, CollectorState s)>();
        foreach (var s in w.collectors)
        {
            var c = CollectorShip.Rebuild(s);
            Register(s.id, c);
            collectors.Add((c, s));
        }

        var fighters = new List<(FighterShip f, FighterState s)>();
        foreach (var s in w.fighters)
        {
            var f = FighterShip.Rebuild(s);
            Register(s.id, f);
            fighters.Add((f, s));
        }

        // ── 2. aşama: referans taşıyan ve Start beklemeyen durumlar ──────────

        foreach (var (g, s) in groups) g.RestoreMembers(s);

        foreach (var s in w.components)
        {
            var comp = s_loadout != null ? s_loadout.GetSlotComponent(s.slot) : null;
            if (comp != null) comp.RestoreRuntime(s);
        }

        foreach (var (c, s) in collectors) c.RestoreState(s);
        foreach (var (f, s) in fighters)   f.RestoreState(s);

        foreach (var s in w.enemyBullets)  EnemyBullet.Rebuild(s);
        foreach (var s in w.bullets)       Bullet.Rebuild(s);
        foreach (var s in w.turretBullets) TurretBullet.Rebuild(s);
        foreach (var s in w.plasma)        PlasmaBeam.Rebuild(s);

        // Küresel durum. Gövde EN SONA kalır: onarım birimlerinin zırh bonusu
        // kurulumda tavanı büyütürken mevcut HP'ye de ekleniyor.
        ShieldGeneratorComponent.RestoreOrphan(w.orphanShield, w.orphanMaxShield);
        EnergyBus.Instance?.RestoreEnergy(w.energy);
        BoostController.Restore((BoostMode)w.boost);

        if (ship != null)
            ship.currentHullHP = Mathf.Min(w.hull, ship.maxHullHP);

        var go = new GameObject("WorldRestoreFinisher");
        go.AddComponent<WorldRestoreFinisher>().Init(w);
    }

    /// <summary>
    /// Ölçeklenmiş tip verisini geri kurar. Taban tip fabrikasından değil
    /// KAYITTAKİ sayılardan: düşman doğduğu levelin (ve zorluğun) çarpanlarını
    /// taşır, yükleme anındakileri değil.
    ///
    /// JsonUtility nesne referanslarını örnek kimliğiyle yazar ve o kimlik
    /// uygulama yeniden açıldığında geçersizdir — hatta başka bir nesneye denk
    /// gelebilir. Tek referans alanı <c>splitInto</c> bu yüzden fabrikadan
    /// yeniden bağlanır.
    /// </summary>
    static EnemyTypeData RebuildEnemyData(string json, string typeName)
    {
        var d = ScriptableObject.CreateInstance<EnemyTypeData>();
        JsonUtility.FromJsonOverwrite(json, d);
        d.name = typeName;   // skin anahtarı ve tip kimliği addan türer

        var baseType = EnemyTypeData.ByName(d.name);
        d.splitInto  = baseType != null ? baseType.splitInto : null;
        return d;
    }

    static float s_timeScale = 1f;

    internal static void FinishRestore(WorldState w)
    {
        try { FinishRestoreSteps(w); }
        finally { Time.timeScale = s_timeScale; }
    }

    static void FinishRestoreSteps(WorldState w)
    {
        // Namlular ancak düşmanların Start'ında kuruluyor; ışınlar onlara bağlı.
        foreach (var s in w.lasers) RestoreLaser(s);

        // Silahın beklemesi mutlak zamana karşı tutuluyor (Time.time + kalan).
        // Kurulum karesinde yazılsaydı bir kare eksik sayılırdı.
        if (s_player != null)
        {
            var wc = s_player.GetComponentInChildren<WeaponController>();
            if (wc != null) wc.RestoreCooldown(w.weaponCooldown);
        }

        // Rastgelelik EN SON: kurulum sırasında Awake/Start'ların çektiği sayılar
        // akışı ilerletti. Burada geri konan durum, kaydın alındığı andaki akıştır.
        if (!string.IsNullOrEmpty(w.rng) && w.rng.Contains("s0"))
            UnityEngine.Random.state = JsonUtility.FromJson<UnityEngine.Random.State>(w.rng);

        try { RestoreFinished?.Invoke(); }
        finally { s_registry = null; }
    }

    static void RestoreLaser(LaserBeamState s)
    {
        Transform parent = null;
        if (s.ownerSlot > 0)
        {
            var turret = ResolveSlot(s.ownerSlot);
            if (turret != null) parent = turret.transform;
        }
        else
        {
            var bot = Resolve<EnemyBot>(s.ownerEnemy);
            if (bot != null) parent = s.onBarrel && bot.BarrelTransform != null ? bot.BarrelTransform : bot.transform;
        }
        if (parent == null) return;

        LaserBeam.Rebuild(s, parent);
    }
}

/// <summary>
/// Geri yüklemenin son adımı. Yürütme sırası en öndedir: yeni kurulan
/// nesnelerin Start'ları bu Update'ten ÖNCE bitmiş olur, ama hiçbir oyun
/// nesnesi kendi Update'ini henüz çalıştırmamıştır — dünya tam olarak kayıttaki
/// anda durur.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class WorldRestoreFinisher : MonoBehaviour
{
    WorldState _state;

    public void Init(WorldState w) => _state = w;

    void Update()
    {
        var w = _state;
        _state = null;
        Destroy(gameObject);
        if (w != null) WorldSave.FinishRestore(w);
    }
}
