using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turret hedef seçimi ve kilitlenme mantığı.
///
/// PUANLAMA — "dikkatimin saniyesi başına ne kadar tehdit ortadan kalkar":
///
///     puan = tehdit / (öldürme süresi + mermi uçuş süresi)
///
///   tehdit          = temel tehdit × yakınlık aciliyeti
///                     Gemiye yaklaşan hedef daha acildir.
///   öldürme süresi  = (bu silahla öldürmek için gereken ham hasar) / (saniyedeki hasar)
///                     Dirençler burada devrededir: raylı top asteroidi yarı sürede,
///                     lazer kalkanlı gemiyi çok daha çabuk bitirir.
///   uçuş süresi     = mesafe / mermi hızı
///                     Uzak hedef hem geç vurulur hem ıskalanma ihtimali yüksektir.
///
/// Böylece "en yakın", "en çok zarar vereceğim" ve "en çabuk öldüreceğim" tek bir
/// orana iner; ayrı ayrı ağırlıklandırmaya gerek kalmaz.
///
/// KİLİTLENME:
///   - Hedef her karede değil, ReevaluateInterval'de bir yeniden değerlendirilir.
///   - Kilitli hedef geçerli ve menzildeyken kilit korunur.
///   - Rakip bir hedef ancak puanı kilitli hedefin SwitchAdvantage katı kadar
///     yüksekse kiliti kırar. Ölmek üzere olan, yakın ve bu silaha zayıf bir hedef
///     bu barajı kolayca aşar; benzer değerdeki hedefler aşamaz.
/// </summary>
public static class TurretTargeting
{
    /// <summary>Yeniden değerlendirme aralığı (saniye). Her kare taramanın anlamı yok.</summary>
    public const float ReevaluateInterval = 0.35f;

    /// <summary>Rakip hedefin kiliti kırmak için gereken puan üstünlüğü.</summary>
    public const float SwitchAdvantage = 1.6f;

    // Yakınlık aciliyeti: ana gemiye bu mesafeden yakın hedefler öne çıkar
    const float UrgencyRange = 7f;
    const float UrgencyBoost = 2.5f;   // temas mesafesinde tehdit bu katsayıyla çarpılır

    const float MinCost = 0.05f;       // sıfıra bölmeyi engeller

    /// <summary>
    /// Menzildeki hedefler arasından en yüksek puanlıyı döndürür.
    /// current verilirse kilit histerezisi uygulanır.
    /// </summary>
    public static ITurretTarget Select(
        Vector3 turretPos, Vector3 shipPos,
        float range, float dps, float bulletSpeed, WeaponType weaponType,
        bool pointDefenceOnly, ITurretTarget current, float shotDamage = 0f)
    {
        ITurretTarget best      = null;
        float         bestScore = 0f;
        float         currentScore = 0f;
        bool          currentStillValid = false;

        foreach (var t in EnumerateTargets())
        {
            if (!t.IsValidTarget) continue;
            if (pointDefenceOnly && !t.IsPointDefencePriority) continue;

            float dist = Vector2.Distance(turretPos, t.TargetTransform.position);
            if (dist > range) continue;

            float score = Score(t, dist, shipPos, dps, bulletSpeed, weaponType, shotDamage);

            if (ReferenceEquals(t, current))
            {
                currentStillValid = true;
                currentScore      = score;
            }

            if (score > bestScore) { bestScore = score; best = t; }
        }

        // Kilit korunuyor mu? Rakip yeterince üstün değilse mevcut hedefte kal.
        if (currentStillValid && bestScore < currentScore * SwitchAdvantage)
            return current;

        return best;
    }

    /// <summary>Tek bir hedefin puanı — formül sınıf dokümanında açıklanmıştır.</summary>
    public static float Score(ITurretTarget t, float dist, Vector3 shipPos,
                              float dps, float bulletSpeed, WeaponType weaponType,
                              float shotDamage = 0f)
    {
        float rawToKill = t.RawDamageToKill(weaponType);
        if (rawToKill <= 0f) return 0f;

        // Zırh, turretin ETKİN DPS'ini düşürür. Zırh 18'e karşı 20 hasarlı bir
        // atış yalnızca 2 geçirir — ham DPS aynı görünse de öldürme süresi
        // 10 katına çıkar. Bu düzeltme olmadan turret asla vuramayacağı bir
        // hedefe kilitlenip mermilerini boşa harcar.
        float effDps = dps * ArmorEfficiency(t, shotDamage);

        float killTime   = effDps > 0.001f ? rawToKill / effDps : float.MaxValue;
        float flightTime = bulletSpeed > 0.001f ? dist / bulletSpeed : 0f;
        float cost       = Mathf.Max(killTime + flightTime, MinCost);

        // Gemiye yaklaşan hedef daha acil
        float distToShip = Vector2.Distance(shipPos, t.TargetTransform.position);
        float urgency    = 1f + (UrgencyBoost - 1f)
                         * (1f - Mathf.Clamp01(distToShip / UrgencyRange));

        return t.ThreatValue * urgency / cost;
    }

    /// <summary>
    /// Zırhın bu turretin hasarına etkisi (0–1). shotDamage bilinmiyorsa 1 döner —
    /// zırh yok sayılır, eski davranış korunur.
    /// </summary>
    static float ArmorEfficiency(ITurretTarget t, float shotDamage)
    {
        if (shotDamage <= 0.001f) return 1f;

        float armor = t.ArmorValue;
        if (armor <= 0f) return 1f;

        float effective = BalanceConfig.Instance.ApplyArmor(shotDamage, armor);
        return effective / shotDamage;
    }

    /// <summary>
    /// Sahnedeki tüm hedefler. FindObjectsByType her tip için ayrı tarama yapar;
    /// çağrı sıklığı ReevaluateInterval ile sınırlı olduğu için maliyeti kabul edilebilir.
    /// </summary>
    static IEnumerable<ITurretTarget> EnumerateTargets()
    {
        foreach (var e in Object.FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
            yield return e;

        foreach (var b in Object.FindObjectsByType<BossShip>(FindObjectsSortMode.None))
            yield return b;

        foreach (var a in Object.FindObjectsByType<Asteroid>(FindObjectsSortMode.None))
            yield return a;

        foreach (var bomb in Object.FindObjectsByType<Bomb>(FindObjectsSortMode.None))
            yield return bomb;
    }
}
