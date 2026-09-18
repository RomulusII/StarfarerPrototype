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
/// HIZ TERCİHİ (yalnızca ışın turretleri):
///   Işın anlıktır — ıskalamaz. Mermili turretler ise hızlı, kaçamak bir hedefi
///   sık sık ıskalar; puanlama bunu göremiyordu çünkü formülde isabet oranı yok.
///   Sonuç: lazer turreti, mermili turretlerin zaten rahatça vurduğu yavaş ve
///   iri hedeflere kilitleniyor, asıl işe yarayacağı Avcı/Swarm gibi hedefleri
///   onlara bırakıyordu.
///
///   Açık bir <c>speedBias</c> ile lazer, hızlı hedeflerin puanını yükseltir.
///   Mermili turretlere ceza YAZILMADI: iki taraflı bir model tüm dengeyi
///   kaydırırdı, oysa çözülmek istenen tek şey ışının rolünü bulması.
///
/// POINT DEFENCE — üç kademe, kademe MUTLAK önceliktir (puanla çözülmez):
///   1. Menzilde bomba/füze varsa YALNIZCA onlara ateş eder ve kilit
///      histerezisi uygulanmaz: bomba kalkana varmadan vurulmalı.
///   2. Mühimmat yoksa hafif gövdeli gemilere (bkz. EnemyTypeData.IsLightHull)
///      ve küçük asteroit parçalarına.
///   3. İkisi de yoksa menzildeki HER gemiye.
///
///   Üçüncü kademe eskiden yoktu: "büyük/zırhlı gövdeye hiç ateş etme, atış
///   başına 8 hasar zırh eşiğinde erir" deniyordu. O gerekçe düşman statı
///   düzleşince büyük ölçüde geçersiz kaldı — levelden gelen +20 zırh yok,
///   13 tipin 11'i zırhsız. Boştaki bir PD'nin menzildeki bir gemiye ateş
///   etmemesi ise saf israftı. Zırhlı hedefte eşik hâlâ işliyor ve puanlama
///   onu zaten biliyor (ArmorEfficiency), yani PD zırhlı bir gemiyi ancak
///   başka hiçbir şey yokken ve düşük puanla seçer.
///
///   Kademe atlaması kilidi kırar, kademe düşüşü kırmaz: iri bir gövdeye
///   kilitli PD'nin menziline küçük bir gemi girerse hemen döner; küçük bir
///   gemiye kilitliyken iri bir gövdenin yaklaşması kilidi bozmaz.
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

    /// <summary>Hız tercihinin doyduğu hedef hızı (birim/sn). Avcı ~5, Swarm ~3.</summary>
    const float FastTargetSpeed = 4f;

    const float MinCost = 0.05f;       // sıfıra bölmeyi engeller

    /// <summary>
    /// Menzildeki hedefler arasından en yüksek puanlıyı döndürür.
    /// current verilirse kilit histerezisi uygulanır.
    /// </summary>
    /// <param name="speedBias">
    /// 0 = hız önemsiz (mermili turretler). Işın turretleri pozitif geçer:
    /// hızlı hedefin puanı en fazla (1 + speedBias) katına çıkar.
    /// </param>
    public static ITurretTarget Select(
        Vector3 turretPos, Vector3 shipPos,
        float range, float dps, float bulletSpeed, WeaponType weaponType,
        bool pointDefenceOnly, ITurretTarget current, float shotDamage = 0f,
        float speedBias = 0f)
    {
        ITurretTarget best      = null;
        float         bestScore = 0f;
        float         currentScore = 0f;
        bool          currentStillValid = false;

        // Point Defence ÜÇ kademeli seçer (bkz. sınıf dokümanı). Üç kova tek
        // geçişte doldurulur; ayrı taramalar sahneyi üç kez gezmek olurdu.
        ITurretTarget bestMunition = null, bestSmall = null, bestOther = null;
        float munitionScore = 0f, smallScore = 0f, otherScore = 0f;
        int   currentTier = PdTier(current);

        foreach (var t in EnumerateTargets())
        {
            if (!t.IsValidTarget) continue;

            float dist = Vector2.Distance(turretPos, t.TargetTransform.position);
            if (dist > range) continue;

            float score = Score(t, dist, shipPos, dps, bulletSpeed, weaponType, shotDamage, speedBias);

            if (ReferenceEquals(t, current))
            {
                currentStillValid = true;
                currentScore      = score;
            }

            if (pointDefenceOnly)
            {
                switch (t.PdClass)
                {
                    case PointDefenceClass.Munition:
                        if (score > munitionScore) { munitionScore = score; bestMunition = t; }
                        break;
                    case PointDefenceClass.Small:
                        if (score > smallScore) { smallScore = score; bestSmall = t; }
                        break;
                    default:
                        if (score > otherScore) { otherScore = score; bestOther = t; }
                        break;
                }
                continue;
            }

            if (score > bestScore) { bestScore = score; best = t; }
        }

        // MÜHİMMAT KİLİDİ KIRAR. Bomba kalkana varmadan vurulmalı; histerezisin
        // onu 0.35 saniye geciktirmesi bile bir bombayı kaçırmaya yeter.
        if (bestMunition != null) return bestMunition;

        // PD'de kademe MUTLAK önceliktir: daha üst kademede bir hedef belirdiyse
        // puan karşılaştırmasına hiç girilmez. Puanla çözülseydi yakındaki iri
        // bir gövde, biraz ötedeki küçük gemiyi puanla geçip PD'yi asıl işinden
        // alıkoyabilirdi.
        if (pointDefenceOnly)
        {
            if (bestSmall != null) { best = bestSmall; bestScore = smallScore; }
            else                   { best = bestOther; bestScore = otherScore; }

            int bestTier = PdTier(best);
            if (currentStillValid && bestTier < currentTier) return best;   // yükseliş: kilidi kır
            if (currentStillValid && bestTier > currentTier) return current; // düşüş: kilidi koru
        }

        // Kilit korunuyor mu? Rakip yeterince üstün değilse mevcut hedefte kal.
        if (currentStillValid && bestScore < currentScore * SwitchAdvantage)
            return current;

        return best;
    }

    /// <summary>
    /// Point Defence kademesi: küçük sayı = yüksek öncelik.
    /// Hedefi olmayan (null) durum en dibe konur ki her gerçek hedef onu geçsin.
    /// </summary>
    static int PdTier(ITurretTarget t) => t == null ? 3 : t.PdClass switch
    {
        PointDefenceClass.Munition => 0,
        PointDefenceClass.Small    => 1,
        _                          => 2,
    };

    /// <summary>Tek bir hedefin puanı — formül sınıf dokümanında açıklanmıştır.</summary>
    public static float Score(ITurretTarget t, float dist, Vector3 shipPos,
                              float dps, float bulletSpeed, WeaponType weaponType,
                              float shotDamage = 0f, float speedBias = 0f)
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

        return t.ThreatValue * urgency * SpeedPreference(t, speedBias) / cost;
    }

    /// <summary>
    /// Işın turretinin hızlı hedefe verdiği ek değer (1 .. 1+speedBias).
    /// speedBias 0 iken hesap tamamen devre dışıdır.
    /// </summary>
    static float SpeedPreference(ITurretTarget t, float speedBias)
    {
        if (speedBias <= 0f) return 1f;
        float speed = t.TargetVelocity.magnitude;
        return 1f + speedBias * Mathf.Clamp01(speed / FastTargetSpeed);
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
