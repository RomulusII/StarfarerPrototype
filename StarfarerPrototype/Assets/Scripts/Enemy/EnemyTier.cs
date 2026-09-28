using UnityEngine;

/// <summary>
/// Düşman TIER'ları — aynı tipin daha güçlü sürümleri (T2 Zırhlı, T3 Ağır,
/// T4 Elit). Davranış, hız ve çeviklik AYNI; değişen dayanıklılık, hasar ve
/// zırh. Görünüşte aynı siluet, zırh plakaları tier renginde (Tools/SkinGen/
/// tiers.js) ve gövdenin altında tier sayısı kadar chevron (<see cref="TierMarker"/>).
///
/// "Düz düşman statı" kararıyla çelişmez: o karar "aynı Swarm gizemli biçimde
/// sertleşiyor, tehdit puanı yalan söylüyor" diye alınmıştı. Tier hem GÖRÜNÜR
/// hem de DÜRÜST FİYATLI — kendi tehdit puanını taşır. Bunun iki sonucu
/// kendiliğinden gelir:
///   - dalga bütçesinden gücü kadar yer yer, dalgada daha az gemi olur;
///   - enkaz tehditten türediği için gücüyle orantılı düşürür.
///
/// Tier tipin bir KOPYASIDIR (bölüm/zorluk çarpanlarıyla aynı desen): adı
/// korunur — skin anahtarı, kayıt ve "sahada kaç tane var" sayımı addan türer.
/// </summary>
public static class EnemyTier
{
    public const int Max = 4;

    //                                   —    T1    T2    T3    T4
    static readonly float[] HpMult     = { 1f,  1f, 1.8f, 3.0f, 5.0f };
    static readonly float[] DamageMult = { 1f,  1f, 1.3f, 1.7f, 2.2f };
    static readonly float[] ArmorAdd   = { 0f,  0f, 1f,   3f,   6f   };

    /// <summary>
    /// Tehdit çarpanı. KAĞIT TAHMİNİ: tehdit formülü (dayanıklılık × (DPS+1)
    /// + yetenek) Swarm, Avcı ve Armored için elle uygulandı, tier başına
    /// ortalama alındı. Yetenek payı yüksek tiplerde (Bomber) biraz fazla
    /// fiyatlar. Telemetri tier'ı taşır; ölçünce tip başına düzeltilir.
    /// </summary>
    static readonly float[] ThreatMult = { 1f,  1f, 2.3f, 4.7f, 10f  };

    /// <summary>
    /// Zırh değerleri kasten KÜÇÜK: Swarm T4'e +9 zırh 10 hasarlı başlangıç
    /// topunu atış başına 1'e düşürüp o gemiyi ölümsüz yapardı. +6'da top 4
    /// geçirir — ve T4 zaten yalnızca yükseltilmiş silahların levellerinde çıkar.
    /// </summary>
    public static float ArmorBonus(int tier) => ArmorAdd[Mathf.Clamp(tier, 0, Max)];

    /// <summary>
    /// Tier renkleri — Tools/SkinGen/tiers.js'teki zırh tonlarıyla AYNI olmalı
    /// (sprite plakaları oradan, chevron buradan). Ayrı yaşayan iki sayı.
    /// </summary>
    public static Color ColorOf(int tier) => tier switch
    {
        2 => new Color(196 / 255f, 128 / 255f,  62 / 255f),   // bronz
        3 => new Color(110 / 255f, 150 / 255f, 205 / 255f),   // çelik mavisi
        4 => new Color(240 / 255f, 200 / 255f,  80 / 255f),   // altın
        _ => Color.white,
    };

    /// <summary>
    /// Ağır tipler (taban tehdidi 20 ve üstü: Kaleci, Tabya) en fazla T2.
    /// Kaleci T4 1.000 HP ve 18 zırh ederdi — bir dalga değil bir duvar.
    /// </summary>
    public const int HeavyThreat = 20;

    public static int MaxTierFor(EnemyTypeData t)
        => t != null && t.threatScore >= HeavyThreat ? 2 : Max;

    /// <summary>
    /// Tipin verilen tier'daki kopyası. T1 (ya da zaten tier'lı bir tip) için
    /// kaynağın kendisi döner.
    ///
    /// Destek gemileri ve Bariyer için ayrı kural gerekmez: silahsız oldukları
    /// için hasar çarpanı boşa düşer, auraların gücü (zırh aurası, kalkan
    /// dalgası, onarım) çarpılmaz — yalnızca HP ve zırh büyür.
    /// </summary>
    public static EnemyTypeData Apply(EnemyTypeData src, int tier)
    {
        if (src == null) return null;
        tier = Mathf.Clamp(tier, 1, MaxTierFor(src));
        if (tier <= 1 || src.tier > 1) return src;

        var d = Object.Instantiate(src);
        d.name = src.name;   // Instantiate "(Clone)" ekler; ad tipin kimliği
        d.tier = tier;

        d.maxHP              = src.maxHP              * HpMult[tier];
        d.maxShield          = src.maxShield          * HpMult[tier];
        // Şarj hızı kalkanla aynı çarpanı alır — pencere uzunluğu korunur
        // (EnemySpawner.ApplyScaling'deki gerekçe).
        d.shieldRechargeRate = src.shieldRechargeRate * HpMult[tier];
        d.fireDamage         = src.fireDamage         * DamageMult[tier];
        d.armor              = src.armor + ArmorAdd[tier];
        d.threatScore        = Mathf.Max(src.threatScore + 1,
                                         Mathf.RoundToInt(src.threatScore * ThreatMult[tier]));

        // Skin yokken çizilen prosedürel gövde de tier'ı okutsun
        var c = ColorOf(tier);
        d.bodyColor   = Color.Lerp(src.bodyColor, c, 0.45f);
        d.barrelColor = c * 0.8f;
        return d;
    }

    // ── Hangi levelde ne çıkar ────────────────────────────────────────────────

    //                                    T1     T2     T3     T4
    static readonly float[] Ch1to3  = { 1.00f, 0.00f, 0.00f, 0.00f };
    static readonly float[] Ch4to5  = { 0.80f, 0.20f, 0.00f, 0.00f };
    static readonly float[] Ch6to7  = { 0.60f, 0.30f, 0.10f, 0.00f };
    static readonly float[] Ch8to9  = { 0.40f, 0.35f, 0.20f, 0.05f };
    static readonly float[] Ch10    = { 0.25f, 0.35f, 0.25f, 0.15f };

    static float[] DistributionFor(int level)
    {
        int chapter = GameProgress.ChapterOf(Mathf.Max(1, level));
        if (chapter <= 3) return Ch1to3;
        if (chapter <= 5) return Ch4to5;
        if (chapter <= 7) return Ch6to7;
        if (chapter <= 9) return Ch8to9;
        return Ch10;
    }

    public static int RollTier(int level)
    {
        var dist = DistributionFor(level);
        float r = Random.value;
        for (int i = 0; i < dist.Length; i++)
        {
            r -= dist[i];
            if (r < 0f) return i + 1;
        }
        return 1;
    }

    /// <summary>
    /// Seçilmiş tipe tier zarı atar. Tier'ın tehdidi kalan bütçeye sığmazsa
    /// bir alt tier'a iner — bütçe tier yüzünden hiçbir zaman aşılmaz.
    /// <paramref name="level"/> 0 ise tier atılmaz (tanıtım leveli, modeller).
    /// </summary>
    public static EnemyTypeData Roll(EnemyTypeData t, int level, float budgetLeft)
    {
        if (t == null || level <= 0) return t;

        for (int tier = Mathf.Min(RollTier(level), MaxTierFor(t)); tier > 1; tier--)
        {
            var d = Apply(t, tier);
            if (d.threatScore <= budgetLeft) return d;
        }
        return t;
    }

    // ── Kayıt ve görünüş ──────────────────────────────────────────────────────

    /// <summary>
    /// Henüz doğmamış kollar kayda TİP ADIYLA yazılıyor (ChapterManager);
    /// tier o ada eklenir ("Swarm#3"). Yazılmasaydı kayıttan dönen kol T1
    /// doğardı — kaydet/aç ile düşmanı zayıflatmak bir kaçış yolu olurdu.
    /// </summary>
    public static string Encode(EnemyTypeData t)
        => t == null ? null : t.tier > 1 ? $"{t.name}#{t.tier}" : t.name;

    public static EnemyTypeData Decode(string s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        int hash = s.IndexOf('#');
        if (hash < 0) return EnemyTypeData.ByName(s);
        int.TryParse(s.Substring(hash + 1), out int tier);
        return Apply(EnemyTypeData.ByName(s.Substring(0, hash)), tier);
    }

    /// <summary>Tier sprite'ının skin anahtarı ("enemy.swarm.t3"); T1'de tabanınki.</summary>
    public static string SkinKey(EnemyTypeData d)
        => d.tier > 1 ? $"{d.SkinId}.t{d.tier}" : d.SkinId;

    /// <summary>Bilgi kutusunda tipin adının yanına: " — Zırhlı".</summary>
    public static string Suffix(EnemyTypeData d)
        => d != null && d.tier > 1 ? " — " + Loc.T("enemy.tier." + d.tier) : "";
}
