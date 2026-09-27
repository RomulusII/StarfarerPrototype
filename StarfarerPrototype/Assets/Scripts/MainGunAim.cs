using UnityEngine;

/// <summary>
/// Ana silahın otomatik nişanı: SEÇİLİ HEDEF ve OTOMATİK modlarda namluyu
/// döndürür ve tetiği çeker. Elle modda hiçbir şey yapmaz — namlu imleci
/// izler (<see cref="WeaponMount"/>), tetik oyuncunundur (<see cref="WeaponController"/>).
///
/// Otomatik modlar bilgisayar ister (<see cref="FireControl.EffectiveMainGunMode"/>)
/// ve nişanın KALİTESİ de bilgisayardan gelir: öngörü ve sapma turretlerle aynı
/// sayılardır. Yükseltmesi düşük bir bilgisayarla iyi bir oyuncunun elle nişanı
/// daha isabetlidir — otomatik mod bir kolaylıktır, bedava bir güç değil.
///
/// Silahla aynı nesnede durur ve ondan ÖNCE çalışır: tetik kararı aynı karede
/// okunmalı, bir kare gecikmeli değil.
/// </summary>
[DefaultExecutionOrder(-20)]
public class MainGunAim : MonoBehaviour
{
    /// <summary>Namlunun dönüş hızı (derece/sn). Elle modda namlu imlece anında döner.</summary>
    const float TurnRate = 360f;

    /// <summary>Tetiğin çekilmesi için namlunun nişan noktasına yakınlığı (derece).</summary>
    const float AimTolerance = 2f;

    /// <summary>Lazer ana silahın hızlı hedef tercihi — lazer turretiyle aynı gerekçe.</summary>
    const float LaserSpeedBias = 1.5f;

    /// <summary>Şu an geçerli mod.</summary>
    public AimMode Mode { get; private set; } = AimMode.Manual;

    /// <summary>Namlu ve tetik bu bileşende mi.</summary>
    public bool Automated => Mode != AimMode.Manual;

    /// <summary>Otomatik modda tetik basılı mı.</summary>
    public bool TriggerHeld { get; private set; }

    ShipLoadout       _loadout;
    WeaponController  _weapon;
    ITurretTarget     _locked;
    float             _retargetTimer;
    float             _jitter;          // bu atışın sapması (derece), her atışta yeniden çekilir

    void Awake() => _weapon = GetComponent<WeaponController>();

    void Update()
    {
        if (_loadout == null) _loadout = GetComponentInParent<ShipLoadout>();
        Mode        = _loadout != null ? FireControl.EffectiveMainGunMode(_loadout.MainGunAimMode)
                                       : AimMode.Manual;
        TriggerHeld = false;

        if (!Automated || PointerInput.Locked || _weapon == null) return;

        var target = Mode == AimMode.Auto ? AcquireAuto() : MarkedInRange();
        if (target == null) return;

        Vector3 pos    = target.TargetTransform.position;
        bool    beam   = _weapon.weaponType == WeaponType.Laser;
        Vector3 aim    = beam ? pos
                              : FireControl.AimPoint(transform.position, pos, target.TargetVelocity,
                                                     _weapon.ProjectileSpeed);

        // transform.up namlu yönü (bkz. WeaponMount): açıdan 90° düşülür.
        Vector2 dir    = FireControl.Rotate((Vector2)(aim - transform.position), beam ? 0f : _jitter);
        float   want   = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        float   next   = Mathf.MoveTowardsAngle(transform.eulerAngles.z, want, TurnRate * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, next);

        TriggerHeld = Mathf.Abs(Mathf.DeltaAngle(next, want)) < AimTolerance;
    }

    /// <summary>Silah her atışta çağırır — bir sonraki atışın sapması çekilir.</summary>
    public void OnShot() => _jitter = FireControl.RollSpread();

    ITurretTarget AcquireAuto()
    {
        bool lockValid = _locked != null && (_locked as Object) != null && _locked.IsValidTarget
                      && InRange(_locked);
        if (!lockValid) _locked = null;

        _retargetTimer -= Time.deltaTime;
        if (_retargetTimer <= 0f || _locked == null)
        {
            _retargetTimer = TurretTargeting.ReevaluateInterval;
            Vector3 shipPos = _loadout != null ? _loadout.transform.position : transform.position;
            float   dps     = _weapon.weaponType == WeaponType.Laser
                            ? _weapon.damage
                            : _weapon.damage / Mathf.Max(0.05f, _weapon.fireRate);
            _locked = TurretTargeting.Select(
                transform.position, shipPos,
                ViewBounds.MaxShotRange, dps, _weapon.ProjectileSpeed, _weapon.weaponType,
                false, _locked, _weapon.damage,
                _weapon.weaponType == WeaponType.Laser ? LaserSpeedBias : 0f);
        }
        return _locked;
    }

    ITurretTarget MarkedInRange()
    {
        var t = TargetMarker.Current;
        return t != null && t.IsValidTarget && InRange(t) ? t : null;
    }

    bool InRange(ITurretTarget t)
        => Vector2.Distance(transform.position, t.TargetTransform.position) <= ViewBounds.MaxShotRange;

    // ── Kayıt ─────────────────────────────────────────────────────────────────

    public ITurretTarget LockedTarget => _locked;

    public void RestoreLock(ITurretTarget t) => _locked = t;
}
