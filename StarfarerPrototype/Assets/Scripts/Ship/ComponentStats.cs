using System;
using UnityEngine;

/// <summary>
/// Bir komponentin OYUN İÇİ sayaçları — upgrade ekranındaki İSTATİSTİK paneli
/// buradan okur. Stat YÜKSELTMELERİYLE (<see cref="ShipComponentBase.StatLevels"/>)
/// karıştırılmamalı: onlar komponentin gücü, bunlar ne yaptığının dökümü.
///
/// Tek sınıf, her komponent kendi alanlarını doldurur — ComponentRuntimeState
/// ile aynı desen (JsonUtility polimorfizm bilmez).
///
/// Ömür: komponentle birlikte doğar, satılınca ölür. Ana silahta her silah
/// TİPİ ayrı sayar (<see cref="ShipLoadout.WeaponStats"/>): lazerin "isabet
/// oranı" ile raylı topunki tek bir sayıya ortalanırsa ikisi de anlamsızlaşır.
/// Turret uzmanlaşma değiştirince aynı gerekçeyle sıfırlanır. Kayıt katmanlarının
/// ikisine de girer (SaveSystem.SlotSave / WeaponSave).
/// </summary>
[Serializable]
public class ComponentStats
{
    // ── Silah: turret, ana silah, hangar (savaşçıların toplamı) ──────────────

    /// <summary>
    /// Hedefin GERÇEKTEN kaybettiği HP + kalkan: zırh ve dirençten sonra,
    /// ölümden sonraki taşma hariç. Ham hasar sayılsaydı zırhlı hedefe yazılan
    /// rakam yalan söylerdi. Asteroitlere verilen hasar dahil.
    /// </summary>
    public float damage;

    /// <summary>
    /// Mermili atışlar. Işın bu sayıma GİRMEZ — ıskalamaz; paydası sıfır kalır
    /// ve isabet oranı "—" görünür. Patlayan mermi (roket, flak) bir hedef
    /// yakaladıysa isabettir.
    /// </summary>
    public int   shotsFired, shotsHit;

    /// <summary>Son darbe — yalnızca gemiler (düşman, boss). Asteroit sayılmaz.</summary>
    public int   kills;

    /// <summary>Vurulup düşürülen mühimmat (bomba).</summary>
    public int   munitions;

    /// <summary>
    /// Silahın DÖVÜŞTE geçirdiği süre — efektif DPS'in paydası. Turret: bir
    /// hedefe kilitliyken (menzilde düşman varken). Savaşçılar: herhangi biri
    /// hedef kovalarken. Ana silah: menzilinde (bütün kadraj) düşman varken.
    /// Menzili kısa turret, uzakta ölen düşmanlar yüzünden cezalandırılmaz;
    /// enerjisi bittiği için ateş edemediği süre ise sayılır — o build'in kusuru.
    /// </summary>
    public float engagedTime;

    // ── Hangar ───────────────────────────────────────────────────────────────

    public float damageTaken;    // savaşçıların aldığı hasar
    public int   fightersLost;

    // ── Kalkan jeneratörü ────────────────────────────────────────────────────

    public float absorbed;       // bu jeneratörün emdiği hasar
    public float refilled;       // şarjla doldurduğu kalkan

    // ── Onarım birimi ────────────────────────────────────────────────────────

    public float repairedHull;   // gövdeye geri kazandırdığı HP
    public float repairedParts;  // komponentlere geri kazandırdığı HP

    // ── Enerji jeneratörü ────────────────────────────────────────────────────

    public float produced;       // üretilen enerji (karıştırma düşülmüş)

    public float Accuracy => shotsFired > 0 ? (float)shotsHit / shotsFired : -1f;
    public float Dps      => engagedTime > 0.5f ? damage / engagedTime : 0f;

    public ComponentStats Clone() => (ComponentStats)MemberwiseClone();
}

/// <summary>
/// Hasarın KİMDEN geldiği. Hasar yolları (TryDamage, AreaDamage, Shrapnel)
/// kaynağı parametre olarak taşımıyor; taşısaydı onlarca imza değişirdi.
/// Bunun yerine hasarı veren taraf bir kapsam açar, hasarı alan taraf
/// (EnemyBot, BossShip, hardpoint, asteroit, bomba) kaybettiğini kapsamdaki
/// sayaca yazar:
///
///     using (DamageSource.From(stats)) DamageUtil.TryDamage(...);
///
/// Kapsam dışındaki hasar (düşmanın düşmana verdiği, boss ölürken hardpoint'lerin
/// silinmesi) hiçbir sayaca yazılmaz. Tek iş parçacığı, iç içe kapsam güvenli.
/// </summary>
public static class DamageSource
{
    public static ComponentStats Current { get; private set; }

    public readonly struct Scope : IDisposable
    {
        readonly ComponentStats _previous;
        public Scope(ComponentStats previous) => _previous = previous;
        public void Dispose() => Current = _previous;
    }

    public static Scope From(ComponentStats stats)
    {
        var previous = Current;
        Current = stats;
        return new Scope(previous);
    }

    /// <summary>Hedefin kaybettiği miktar; <paramref name="killedShip"/> son darbeyse.</summary>
    public static void Dealt(float lost, bool killedShip)
    {
        if (Current == null) return;
        if (lost > 0f) Current.damage += lost;
        if (killedShip) Current.kills++;
    }

    public static void MunitionDestroyed()
    {
        if (Current != null) Current.munitions++;
    }
}
