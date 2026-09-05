using UnityEngine;

/// <summary>
/// Sahte oyuncunun NİŞAN ve ATEŞ tarafı. Alışveriş ayrı sınıfta (SimShopper).
///
/// Girdiyi <see cref="PointerInput.Source"/> üzerinden verir, yani oyunun
/// gerçek ateş yolundan geçer: namlu döner, mermi namlu ucundan çıkar, plazma
/// şarj olur, lazer enerji yakar. Doğrudan WeaponController'a bağlansaydık
/// ölçtüğümüz şey oyunun ateş etmesi değil, kendi kestirmemiz olurdu.
///
/// HEDEF SEÇİMİ turretlerin formülünü kullanır (<see cref="TurretTargeting"/>):
/// "dikkatimin saniyesi başına ne kadar tehdit ortadan kalkar". Bu bir tercih
/// değil, elimizdeki tek YAZILI hedef seçme politikası — insan oyuncunun neyi
/// seçtiği henüz ölçülmedi. Ayrı bir sezgisel yazmak, ölçülmemiş bir
/// politikanın üstüne ikinci bir ölçülmemiş politika koymak olurdu.
///
/// İSABET MODELİ — bu sınıfın asıl işi:
///   Nişan açısına Gauss gürültüsü bindirilir. Sapma iki bileşenlidir:
///
///       sigma = aimError + aimErrorPerSpeed x hedefHizi
///
///   Hız bileşeni şart: tek bir sabit oran (%52) hangi TİPİN ıskalandığını yok
///   ederdi. Avcı ıskalamakla Kaleci ıskalamak aynı şey değil ve tehdit
///   formülünün doğrulanması tam olarak bu farkı sorar.
///
///   IŞIN ISKALAMAZ. Lazer anlık bir ışındır; gürültü ona uygulanmaz ve isabet
///   oranı paydasına da girmez (bkz. CLAUDE.md "Denge Ölçümü"). Plazma bir ışın
///   değil, uçan bir bolttur — o ıskalar.
///
/// Gürültü aimJitterPeriod'da bir yeniden çekilir. Her KAREDE çekilseydi namlu
/// titrer ve sürekli ateş eden bir silahta hata ortalanıp yok olurdu; hatanın
/// atış başına sabit kalması gerekiyor.
/// </summary>
public class SimPilot : MonoBehaviour, IPointerSource
{
    /// <summary>Hedefin yeniden değerlendirilme aralığı (sn).</summary>
    const float RetargetInterval = 0.25f;

    WeaponController _weapon;
    Transform        _mount;
    Camera           _cam;

    ITurretTarget _target;
    float         _nextRetarget;

    // Nişan gürültüsü — koşunun kendi akışından çekilir, oyunun Random'ından
    // AYRI: pilot politikasını değiştirmek düşmanların doğduğu yeri kaydırmamalı.
    float _aimNoiseDeg;
    float _nextJitter;

    // Plazma şarj döngüsü
    bool  _fireHeld;
    float _plasmaHeldSince;
    bool  _releaseThisFrame;

    void Start()
    {
        _weapon = FindFirstObjectByType<WeaponController>();
        _mount  = _weapon != null ? _weapon.transform : transform;
        _cam    = Camera.main;
        PointerInput.Source = this;
    }

    void OnDestroy()
    {
        if (ReferenceEquals(PointerInput.Source, this)) PointerInput.Source = null;
    }

    void Update()
    {
        if (_weapon == null)
        {
            _weapon = FindFirstObjectByType<WeaponController>();
            if (_weapon == null) return;
            _mount = _weapon.transform;
        }
        if (_cam == null) _cam = Camera.main;

        _releaseThisFrame = false;

        if (Time.time >= _nextRetarget)
        {
            _nextRetarget = Time.time + RetargetInterval;
            PickTarget();
        }

        if (Time.time >= _nextJitter)
        {
            _nextJitter  = Time.time + Mathf.Max(0.02f, SimRuntime.Config.aimJitterPeriod);
            _aimNoiseDeg = SampleAimNoise();
        }

        UpdateTrigger();
    }

    // ── Hedef ────────────────────────────────────────────────────────────────

    void PickTarget()
    {
        if (_target != null && !_target.IsValidTarget) _target = null;

        _target = TurretTargeting.Select(
            turretPos:        _mount.position,
            shipPos:          transform.position,
            range:            ViewBounds.MaxShotRange,
            dps:              WeaponDps(),
            bulletSpeed:      ProjectileSpeed(),
            weaponType:       _weapon.weaponType,
            pointDefenceOnly: false,
            current:          _target,
            shotDamage:       _weapon.damage,
            speedBias:        0f);
    }

    /// <summary>Ana silahın kabaca saniyelik hasarı — hedef puanlaması için.</summary>
    float WeaponDps()
    {
        switch (_weapon.weaponType)
        {
            // Lazer sürekli ışın: LaserBeam.damage saniyelik hasardır.
            case WeaponType.Laser:
                return _weapon.damage;

            // Plazma tam şarjda 2.5x, üstüne fireRate kadar bekleme.
            case WeaponType.Plasma:
                return _weapon.damage * 2.5f /
                       Mathf.Max(0.05f, _weapon.chargeTime + _weapon.fireRate);

            default:
                return _weapon.damage / Mathf.Max(0.05f, _weapon.fireRate);
        }
    }

    /// <summary>Mermi hızı. Işında "sonsuz" yerine yüksek bir sayı yeter.</summary>
    float ProjectileSpeed()
    {
        switch (_weapon.weaponType)
        {
            case WeaponType.Laser:  return 500f;
            case WeaponType.Plasma: return 21f;   // 14–28 arası; tam şarja yakın nişan alıyoruz
            default:                return WeaponController.KineticBulletSpeed;
        }
    }

    // ── Nişan ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Merminin hedefe varacağı noktayı çözer (iki adımlı yaklaşım) ve gürültüyü
    /// bindirir. Öngörü (lead) olmadan hızlı hedefler YAPISAL olarak ıskalanır;
    /// o zaman isabet oranı nişan modeline değil mermi hızına bağlı kalırdı.
    /// </summary>
    bool TryAimPoint(out Vector3 world)
    {
        world = default;
        if (_target == null || !_target.IsValidTarget) return false;

        Vector3 origin = _mount.position;
        Vector3 tp     = _target.TargetTransform.position;
        Vector2 tv     = _target.TargetVelocity;
        float   spd    = ProjectileSpeed();

        for (int i = 0; i < 2; i++)
        {
            float t = Vector2.Distance(origin, tp) / Mathf.Max(1f, spd);
            tp = (Vector3)((Vector2)_target.TargetTransform.position + tv * t);
        }

        Vector2 dir = (Vector2)tp - (Vector2)origin;
        if (dir.sqrMagnitude < 0.0001f) return false;

        // Işın ıskalamaz; gürültü yalnızca uçan mermiye uygulanır.
        if (_weapon.weaponType != WeaponType.Laser)
            dir = Rotate(dir, _aimNoiseDeg);

        world = origin + (Vector3)dir;
        return true;
    }

    float SampleAimNoise()
    {
        var cfg = SimRuntime.Config;
        float speed = _target != null && _target.IsValidTarget
                    ? _target.TargetVelocity.magnitude : 0f;
        float sigma = cfg.aimError + cfg.aimErrorPerSpeed * speed;
        return sigma <= 0f ? 0f : (float)Gaussian() * sigma;
    }

    /// <summary>Box-Muller. Koşunun kendi akışından, tohuma bağlı.</summary>
    static double Gaussian()
    {
        var rng = SimRuntime.Rng;
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return System.Math.Sqrt(-2.0 * System.Math.Log(u1)) *
               System.Math.Sin(2.0 * System.Math.PI * u2);
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ── Tetik ────────────────────────────────────────────────────────────────

    void UpdateTrigger()
    {
        bool hasTarget = _target != null && _target.IsValidTarget;

        if (_weapon.weaponType != WeaponType.Plasma)
        {
            // Kinetik ve lazer: hedef varken basılı tut.
            _fireHeld = hasTarget;
            return;
        }

        // Plazma: şarj et, dolunca bırak. Yarım şarjda bırakmak hasarı
        // 2.5x'ten 0.5x'e düşürürdü; tam şarjı beklemek plazmanın kendi
        // tasarımının istediği oyun.
        if (!hasTarget) { _fireHeld = false; return; }

        if (!_fireHeld)
        {
            _fireHeld        = true;
            _plasmaHeldSince = Time.time;
            return;
        }

        if (Time.time - _plasmaHeldSince >= _weapon.chargeTime)
        {
            _fireHeld         = false;
            _releaseThisFrame = true;
        }
    }

    // ── IPointerSource ───────────────────────────────────────────────────────

    public bool TryPosition(out Vector2 screen)
    {
        screen = default;
        if (_cam == null) return false;
        if (!TryAimPoint(out var world)) return false;

        // Dünya → ekran → (WeaponMount'ta) dünya. Gidiş-dönüş ortografik
        // kamerada birebirdir; çözünürlükten bağımsız çalışır.
        var p  = _cam.WorldToScreenPoint(world);
        screen = new Vector2(p.x, p.y);
        return true;
    }

    public bool FireHeld     => _fireHeld;
    public bool FireReleased => _releaseThisFrame;
}
