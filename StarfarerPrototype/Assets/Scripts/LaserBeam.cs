using UnityEngine;

/// <summary>
/// Işın tabanlı lazer.  Üç kullanım senaryosu:
///
/// 1) Oyuncu ana silahı (continuous=true):
///    WeaponController'ın child'ı olarak spawn edilir; parent döndükçe aim yönü takip eder.
///    Sol tık basılı olduğu sürece aktif, bırakılınca WeaponController destroy eder.
///    Enerji biterse kendisi kapanır.
///
/// 2) Lazer turret (continuous=false, burnDuration>0):
///    TurretController'ın child'ı olarak spawn edilir; burnDuration saniye yanar, sonra yok olur.
///    Enerji ateş anında ödenir, beam.energyPerSecond = 0.
///
/// 3) Düşman lazeri (continuous=false, hitsPlayer=true):
///    Düşman barrelından top-level spawn; transform.up = ateş yönü şeklinde ayarlanır.
///    burnDuration saniye player'a DPS verir.
/// </summary>
public class LaserBeam : MonoBehaviour
{
    public float      damage          = 80f;   // hasar/saniye
    public float      burnDuration    = 0.1f;  // continuous=false ise geçerli
    public float      energyPerSecond = 20f;   // enerji tüketimi/saniye (0 = ücretsiz)
    public WeaponType weaponType      = WeaponType.Laser;
    public float      maxRange        = 22f;
    public bool       continuous      = false; // true → süre sınırı yok, dışarıdan kapatılır
    public bool       hitsPlayer      = false; // true → düşman lazeri, player'a hasar verir

    /// <summary>
    /// Sürekli ışının hasar uygulama aralığı (saniye) — yani ışının "atışı".
    ///
    /// Zırh eşiği atış başına sabit hasar düşürür (bkz. BalanceConfig.ApplyArmor).
    /// Hasar her KAREDE uygulandığında tek seferlik miktar dps/60 oluyordu:
    /// 46 DPS'lik bir ışın için 0.77 hasar, zırhı 6 olan bir hedefte %10'a
    /// kırpılıyordu — ışın gücünün %90'ını zırha kaptırıyordu. Daha kötüsü sonuç
    /// KARE HIZINA bağlıydı: 120 fps'te oyuncu yarı hasar veriyordu.
    ///
    /// 0.25 sn'lik tik ışını "saniyede 4 atış yapan bir silah" yapar; atış
    /// başına ~11 hasar, yani kinetik bir mermiyle aynı mertebede.
    /// </summary>
    const float ContinuousTick = 0.25f;

    /// <summary>
    /// Bu ışın için bir "atış" ne kadar sürer?
    ///
    /// Patlama modunda (turret lazeri, düşman lazeri) yanmanın TAMAMI tek bir
    /// atıştır — tetiğe bir kez basılmıştır. Turret lazeri böylece 0.5 sn'lik
    /// yanmasını 13 hasarlık tek vuruş olarak indirir; kinetik turretin 12
    /// hasarlık mermisiyle aynı ligde ve zırha karşı aynı muameleyi görür.
    /// Bölünseydi zırh aynı yanmadan üç kez pay alırdı.
    /// </summary>
    float TickInterval => continuous ? ContinuousTick : Mathf.Max(burnDuration, 0.05f);

    float        _tickTimer;
    float        _pendingDamage;
    float        _remaining;
    Collider2D   _target;
    PlayerShip   _targetPlayer;
    FighterShip  _targetFighter;
    CollectorShip _targetCollector;
    Vector3      _endPoint;
    LineRenderer _line;
    Vector2      _hitNormal;
    float        _sparkTimer;

    // ── Başlatma ──────────────────────────────────────────────────────────────

    public void Init()
    {
        _remaining = burnDuration;
        UpdateRaycast();
        BuildVisual();
    }

    // ── Güncelleme ────────────────────────────────────────────────────────────

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (!continuous)
        {
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) { Flush(); Destroy(gameObject); return; }
        }

        // Enerji tüketimi (0 ise EnergyBus'a dokunmaz)
        if (energyPerSecond > 0f && EnergyBus.Instance != null)
        {
            if (!EnergyBus.Instance.RequestEnergy(energyPerSecond * Time.deltaTime))
            {
                Flush();
                Destroy(gameObject);
                return;
            }
        }

        // Boost çarpanları — sadece oyuncu silahları için geçerli
        float dmgMulti = !hitsPlayer
            ? (BoostController.Mode == BoostMode.Weapon  ? 2f      :
               BoostController.Mode == BoostMode.Shield   ? 1f / 3f : 1f)
            : 1f;

        UpdateRaycast();
        UpdateVisual();
        AccumulateDamage(damage * dmgMulti * Time.deltaTime);
        UpdateSparks();
    }

    // ── Raycast ───────────────────────────────────────────────────────────────

    void UpdateRaycast()
    {
        Vector2 origin    = transform.position;
        Vector2 direction = transform.up;

        var hits = Physics2D.RaycastAll(origin, direction, maxRange);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // Işın her karede yeniden atılır; hedefler de her karede SIFIRLANIR.
        // Düşman lazerinde _targetPlayer korunuyordu ve Init() zaten sahnedeki
        // gemiyi yazıyordu: ışın nereye bakarsa baksın, hatta hiçbir şeye
        // değmese bile, oyuncu hasar alıyordu.
        _target          = null;
        _targetPlayer    = null;
        _targetFighter   = null;
        _targetCollector = null;
        _endPoint        = transform.position + (Vector3)(direction * maxRange);

        foreach (var hit in hits)
        {
            var c = hit.collider;

            if (hitsPlayer)
            {
                // Düşman ışını oyuncu tarafında ne bulursa onu yakar —
                // savaşçılar ve toplayıcılar dahil.
                var ship      = c.GetComponent<PlayerShip>();
                var fighter   = c.GetComponent<FighterShip>();
                var collector = c.GetComponent<CollectorShip>();
                if (ship != null || fighter != null || collector != null)
                {
                    _targetPlayer    = ship;
                    _targetFighter   = fighter;
                    _targetCollector = collector;
                    _endPoint        = hit.point;
                    _hitNormal       = hit.normal;
                    break;
                }
            }
            else
            {
                if (c.GetComponent<EnemyBot>()      != null ||
                    c.GetComponent<BossHardpoint>() != null ||
                    c.GetComponent<BossShip>()      != null)
                {
                    _target    = c;
                    _endPoint  = hit.point;
                    _hitNormal = hit.normal;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Hasarı biriktirir ve DamageTick aralıklarında topluca uygular.
    /// Işın bir şeye değmiyorsa birikim de durur — beklerken şarj olup ilk
    /// temasta boşalan bir "hasar deposu" olmamalı.
    /// </summary>
    void AccumulateDamage(float amount)
    {
        bool hitting = hitsPlayer
            ? (_targetPlayer != null || _targetFighter != null || _targetCollector != null)
            : (_target != null);

        if (!hitting) { _pendingDamage = 0f; _tickTimer = 0f; return; }

        _pendingDamage += amount;
        _tickTimer     += Time.deltaTime;
        if (_tickTimer < TickInterval) return;

        _tickTimer = 0f;
        Flush();
    }

    void Flush()
    {
        if (_pendingDamage <= 0f) return;
        float amount   = _pendingDamage;
        _pendingDamage = 0f;

        if (hitsPlayer)
        {
            _targetPlayer?.TakeDamage(amount);
            _targetFighter?.TakeDamage(amount);
            _targetCollector?.TakeDamage(amount);
        }
        else if (_target != null)
        {
            DamageUtil.TryDamage(_target, amount, weaponType);
        }
    }

    // ── Görsel ────────────────────────────────────────────────────────────────

    void BuildVisual()
    {
        _line = gameObject.AddComponent<LineRenderer>();
        _line.positionCount     = 2;
        _line.startWidth        = 0.05f;
        _line.endWidth          = 0.02f;
        _line.useWorldSpace     = true;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows    = false;
        _line.sortingOrder      = 5;
        _line.material          = new Material(Shader.Find("Sprites/Default"));
        UpdateVisual();
    }

    void UpdateVisual()
    {
        if (_line == null) return;
        _line.SetPosition(0, transform.position);
        _line.SetPosition(1, _endPoint);

        // Burst modda fade out; sürekli modda tam parlak
        float alpha = continuous ? 1f : Mathf.Clamp01(_remaining / Mathf.Max(burnDuration, 0.001f));
        Color beamColor = hitsPlayer
            ? new Color(1f, 0.35f, 0.1f)   // düşman lazeri: turuncu-kırmızı
            : new Color(0.3f, 0.9f, 1f);    // oyuncu/turret lazeri: cyan

        _line.startColor = new Color(beamColor.r, beamColor.g, beamColor.b, alpha);
        _line.endColor   = new Color(beamColor.r, beamColor.g, beamColor.b, alpha * 0.3f);
    }

    void UpdateSparks()
    {
        bool hitting = hitsPlayer
            ? (_targetPlayer != null || _targetFighter != null || _targetCollector != null)
            : (_target != null);
        if (!hitting) return;

        _sparkTimer -= Time.deltaTime;
        if (_sparkTimer > 0f) return;
        _sparkTimer = 0.06f; // saniyede ~17 emit → 17-34 parçacık

        Color sparkColor = hitsPlayer
            ? new Color(1f,  0.45f, 0.1f)  // düşman lazeri: turuncu
            : new Color(0.4f, 1f,  1f);     // oyuncu/turret lazeri: cyan

        HitEffect.SpawnLaserSparks(_endPoint, transform.up, _hitNormal, sparkColor);
    }

    void OnDestroy()
    {
        if (_line != null && _line.material != null)
            Destroy(_line.material);
    }

    /// <summary>WeaponController ışını dışarıdan kapatırken artığı işletir.</summary>
    void OnDisable() => Flush();
}
