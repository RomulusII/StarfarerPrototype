using UnityEngine;

/// <summary>
/// Bir silahın hedefini kimin seçtiği ve kimin nişan aldığı.
///
/// SIRA KAYDA YAZILIR (<c>SlotSave.aimMode</c>, <c>SaveData.mainGunAim</c>).
/// Otomatik 0'dır: alan eklenmeden önceki kayıtlarda turretler zaten otomatikti,
/// eksik alan 0 okununca aynı davranış geri gelir.
/// </summary>
public enum AimMode
{
    /// <summary>Hedefi de nişanı da silah seçer — turretlerin tarihsel davranışı.</summary>
    Auto     = 0,
    /// <summary>Hedefi oyuncu işaretler (sol tık), nişanı silah alır.</summary>
    Assisted = 1,
    /// <summary>Silah imlece döner ve oyuncu tetiğe bastıkça ateş eder.</summary>
    Manual   = 2,
}

/// <summary>
/// Atış kontrolü: nişan modunun kuralları ve nişan İSABETİNİN tek sahibi.
///
/// İsabet bilgisayar komponentinden gelir (<see cref="ComputerComponent"/>).
/// Bilgisayarsız bir turret hedefin ŞU ANKİ konumuna ateş eder ve birkaç derece
/// sapar — yani yana doğru uçan bir gemiyi pek vuramaz, ama kendisine doğru
/// gelen tehdidi (öngörü gerektirmeyen radyal hareket) vurur. Bilgisayar
/// yükseltildikçe nişan merminin buluşma noktasına kayar ve sapma daralır.
///
/// Turretler, otomatik moddaki ana silah ve füzelerin güdümü aynı sayıları
/// buradan okur: ayrı ayrı yazılsaydı biri diğerinden sapardı.
/// </summary>
public static class FireControl
{
    // ── Nişan modu anahtarı ───────────────────────────────────────────────────

    const string CustomAimingPref = "starfarer.customAiming";

    static int s_customAiming = -1;   // -1: henüz okunmadı

    /// <summary>
    /// ÖZEL NİŞAN açık mı. Kapalıyken her silah varsayılanına döner (turretler
    /// otomatik, ana silah elle); açıkken upgrade ekranında seçilen modlar
    /// geçerlidir. Oyuncu tek düğmeyle iki düzen arasında gidip gelebilsin diye
    /// seçimler SİLİNMEZ, yalnızca devre dışı kalır.
    ///
    /// Bir kontrol tercihi olduğu için kayda değil PlayerPrefs'e yazılır
    /// (zorluk ve dil seçimi gibi).
    /// </summary>
    public static bool CustomAiming
    {
        get
        {
            if (s_customAiming < 0) s_customAiming = PlayerPrefs.GetInt(CustomAimingPref, 0);
            return s_customAiming == 1;
        }
        set
        {
            s_customAiming = value ? 1 : 0;
            PlayerPrefs.SetInt(CustomAimingPref, s_customAiming);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Bir turret uzmanlaşmasının seçebileceği modlar.</summary>
    public static bool Allows(TurretSpecType spec, TurretBaseType bt, AimMode mode)
    {
        // Point Defence her zaman otomatiktir: bombayı kalkana varmadan vurmak
        // insan tepkisini bekleyemez.
        if (spec == TurretSpecType.PointDefence) return mode == AimMode.Auto;

        // Füzeler elle yönetilemez — güdüm bir hedef ister, imleç hedef değildir.
        if (bt == TurretBaseType.Missile && mode == AimMode.Manual) return false;

        return true;
    }

    /// <summary>Uzmanlaşma izin vermiyorsa en yakın izinli mod.</summary>
    public static AimMode Sanitize(TurretSpecType spec, TurretBaseType bt, AimMode mode)
    {
        if (Allows(spec, bt, mode)) return mode;
        if (Allows(spec, bt, AimMode.Assisted)) return AimMode.Assisted;
        return AimMode.Auto;
    }

    /// <summary>Bir turretin şu an geçerli modu (anahtar kapalıysa Otomatik).</summary>
    public static AimMode EffectiveTurretMode(TurretSpecType spec, TurretBaseType bt, AimMode chosen)
        => CustomAiming ? Sanitize(spec, bt, chosen) : AimMode.Auto;

    /// <summary>
    /// Ana silahın şu an geçerli modu. Otomatik modlar bilgisayar İSTER — elle
    /// nişan oyunun temel eylemi ve bedava otomatiğe dönmemeli. Bilgisayar
    /// yıkılırsa ana silah elle moda düşer.
    /// </summary>
    public static AimMode EffectiveMainGunMode(AimMode chosen)
    {
        if (!CustomAiming || !ComputerComponent.IsOnline) return AimMode.Manual;
        return chosen;
    }

    /// <summary>Sahnede işaretli hedefi kullanan bir silah var mı.</summary>
    public static bool AnyAssisted
    {
        get
        {
            if (!CustomAiming) return false;
            var ship = TargetMarker.Loadout;
            if (ship == null) return false;
            if (EffectiveMainGunMode(ship.MainGunAimMode) == AimMode.Assisted) return true;
            foreach (var (_, _, comp) in ship.EnumerateSlots())
                if (comp is TurretController tc && tc.EffectiveMode == AimMode.Assisted) return true;
            return false;
        }
    }

    // ── İsabet ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Öngörü: nişan noktası hedefin konumundan buluşma noktasına doğru ne kadar
    /// kayar (0 = doğrudan hedefe, 1 = tam buluşma noktası).
    /// </summary>
    public static float Lead
    {
        get
        {
            var cfg = BalanceConfig.Instance;
            var pc  = ComputerComponent.Active;
            if (pc == null) return cfg.aimLeadNoComputer;
            int lvl = pc.GetStatLevel(ComputerComponent.LeadKey);
            return Mathf.Clamp01(cfg.aimLeadBase + cfg.aimLeadPerLevel * lvl);
        }
    }

    /// <summary>Sapma: atış başına rastgele nişan hatasının tavanı (derece).</summary>
    public static float SpreadDeg
    {
        get
        {
            var cfg = BalanceConfig.Instance;
            var pc  = ComputerComponent.Active;
            if (pc == null) return cfg.aimSpreadNoComputer;
            int lvl = pc.GetStatLevel(ComputerComponent.PrecisionKey);
            return Mathf.Max(cfg.aimSpreadMin, cfg.aimSpreadBase * Mathf.Pow(cfg.aimSpreadDecay, lvl));
        }
    }

    /// <summary>Güdüm: füzelerin dönüş hızı çarpanı.</summary>
    public static float GuidanceMultiplier
    {
        get
        {
            var cfg = BalanceConfig.Instance;
            var pc  = ComputerComponent.Active;
            if (pc == null) return cfg.guidanceNoComputer;
            int lvl = pc.GetStatLevel(ComputerComponent.GuidanceKey);
            return cfg.guidanceBase * Mathf.Pow(cfg.guidanceStep, lvl);
        }
    }

    /// <summary>
    /// Tek atışın sapması (derece). Üçgen dağılım — iki düzgün sayının ortalaması:
    /// çoğu atış merkeze yakın gider, tavana yalnızca arada bir çıkar. Düzgün
    /// dağılımda her atış tavana eşit olasılıkla giderdi ve sapma olduğundan
    /// büyük okunurdu.
    /// </summary>
    public static float RollSpread()
    {
        float s = SpreadDeg;
        if (s <= 0f) return 0f;
        return (Random.Range(-s, s) + Random.Range(-s, s)) * 0.5f;
    }

    /// <summary>
    /// Bu atış sisteminin nişan noktası: hedefin konumu ile buluşma noktası
    /// arasında, öngörü kadar.
    /// </summary>
    public static Vector3 AimPoint(Vector2 from, Vector3 targetPos, Vector2 targetVel, float projectileSpeed)
    {
        float lead = Lead;
        if (lead <= 0f) return targetPos;
        Vector3 intercept = Intercept(from, targetPos, targetVel, projectileSpeed);
        return Vector3.LerpUnclamped(targetPos, intercept, lead);
    }

    /// <summary>
    /// Merminin hedefle buluşacağı nokta (ikinci derece denklem). Çözüm yoksa
    /// hedefin kendisi — mermi hedefe yetişemiyorsa en iyi tahmin odur.
    /// </summary>
    public static Vector3 Intercept(Vector2 from, Vector3 targetPos, Vector2 targetVel, float speed)
    {
        Vector2 toTarget = (Vector2)targetPos - from;

        float a = Vector2.Dot(targetVel, targetVel) - speed * speed;
        float b = 2f * Vector2.Dot(targetVel, toTarget);
        float c = Vector2.Dot(toTarget, toTarget);

        float t = 0f;
        if (Mathf.Abs(a) < 0.001f)
        {
            if (Mathf.Abs(b) > 0.001f) t = -c / b;
        }
        else
        {
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return targetPos;
            float sq = Mathf.Sqrt(disc);
            float t1 = (-b + sq) / (2f * a);
            float t2 = (-b - sq) / (2f * a);
            if      (t1 > 0f && t2 > 0f) t = Mathf.Min(t1, t2);
            else if (t1 > 0f)            t = t1;
            else if (t2 > 0f)            t = t2;
            else return targetPos;
        }

        if (t < 0f) return targetPos;
        return targetPos + (Vector3)(targetVel * t);
    }

    /// <summary>Bir yönü verilen derece kadar döndürür.</summary>
    public static Vector2 Rotate(Vector2 dir, float degrees)
    {
        if (degrees == 0f) return dir;
        float r = degrees * Mathf.Deg2Rad;
        float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(dir.x * cs - dir.y * sn, dir.x * sn + dir.y * cs);
    }

    /// <summary>İşaretçinin dünya konumu; girdi yoksa false.</summary>
    public static bool TryPointerWorld(out Vector3 world)
    {
        world = default;
        if (Camera.main == null || !PointerInput.TryPosition(out Vector2 screen)) return false;
        world   = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        world.z = 0f;
        return true;
    }
}
