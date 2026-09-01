using UnityEngine;

/// <summary>
/// Point Defence'in bir hedefe nasıl baktığı.
///
/// Tek bir bool yetmiyordu: PD'nin "önce mühimmat, o yoksa küçük gemi, büyük
/// gövdeye ASLA" kuralı üç durumlu ve bool yalnızca ikisini anlatabiliyordu.
/// </summary>
public enum PointDefenceClass
{
    /// <summary>PD ateş etmez. Büyük ve zırhlı gövdeler — DPS'i orada boşa gider.</summary>
    None,

    /// <summary>Küçük, hafif gemiler ve asteroit parçaları. Mühimmat yoksa hedeftir.</summary>
    Small,

    /// <summary>Bomba ve füze. HER ZAMAN önceliklidir; kilit histerezisi bile geciktirmez.</summary>
    Munition,
}

/// <summary>
/// Turretlerin nişan alabileceği her şey. EnemyBot, BossShip, Asteroid ve Bomb
/// bu arayüzü uygular; TurretTargeting yalnızca bunun üzerinden çalışır.
///
/// Yeni bir hedef tipi eklemek için TurretController'a dokunmak gerekmez —
/// arayüzü uygulamak yeterlidir.
/// </summary>
public interface ITurretTarget
{
    Transform TargetTransform { get; }

    /// <summary>Öngörü (lead) hesabı için anlık hız.</summary>
    Vector2 TargetVelocity { get; }

    /// <summary>Hedef hâlâ vurulabilir mi (ölmüş/yok edilmiş değil mi)?</summary>
    bool IsValidTarget { get; }

    /// <summary>
    /// Bu silah tipiyle hedefi tamamen yok etmek için gereken HAM hasar.
    /// Dirençler ve kalan kalkan burada hesaba katılır — turret bu sayede
    /// "hangi hedefi daha çabuk öldürürüm" sorusunu doğru cevaplayabilir.
    /// </summary>
    float RawDamageToKill(WeaponType weaponType);

    /// <summary>
    /// Hedefin öldürülmemesi hâlinde yaratacağı zararın göreli ölçüsü.
    /// Düşmanlarda tehdit puanı, asteroitte çarpma hasarı, bombada yüksek sabit.
    /// </summary>
    float ThreatValue { get; }

    /// <summary>
    /// Point Defence bu hedefe nasıl bakar? Bkz. <see cref="PointDefenceClass"/>.
    /// </summary>
    PointDefenceClass PdClass { get; }

    /// <summary>
    /// Atış başına sabit hasar düşüşü. Zırhın etkisi turretin ATIŞ hasarına
    /// bağlıdır: aynı zırh, güçlü tek atışı biraz, zayıf çok atışı tamamen
    /// yer. Bu yüzden zırh RawDamageToKill'e gömülemez — turret kendi atış
    /// hasarını bilerek cezayı kendisi hesaplar (bkz. TurretTargeting.Score).
    /// </summary>
    float ArmorValue { get; }
}
