using UnityEngine;

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
    /// Point Defence'in öncelikli hedefi mi? (bomba, kalkan içine girmiş küçük gemiler)
    /// </summary>
    bool IsPointDefencePriority { get; }
}
