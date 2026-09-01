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
            if (_remaining <= 0f) { Destroy(gameObject); return; }
        }

        // Enerji tüketimi (0 ise EnergyBus'a dokunmaz)
        if (energyPerSecond > 0f && EnergyBus.Instance != null)
        {
            if (!EnergyBus.Instance.RequestEnergy(energyPerSecond * Time.deltaTime))
            {
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
        ApplyDamage(damage * dmgMulti, Time.deltaTime);
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
                if (c.GetComponent<EnemyBot>()       != null ||
                    c.GetComponent<BarrierShield>()  != null ||
                    c.GetComponent<BossHardpoint>()  != null ||
                    c.GetComponent<BossShip>()       != null)
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
    /// Hasarı HER KAREDE uygular — oyuncu hedefin barının akıcı düştüğünü görür.
    ///
    /// Bunu yapabilmemizin sebebi zırhın burada ORAN olarak hesaplanması:
    /// <see cref="BalanceConfig.BeamArmorEfficiency"/> ışını "saniyede bir atış"
    /// sayıp 0..1 arası bir katsayı döner, hedefe de zırhın uygulandığı
    /// bildirilir. Böylece hasarın sıklığı ile zırhın ısırığı BİRBİRİNDEN
    /// BAĞIMSIZ olur.
    ///
    /// Eskiden ikisi bağlıydı ve iki kötü seçenek vardı: sık uygula (zırh 60 kez
    /// ısırır, ışın gücünün %90'ını kaybeder, üstelik hasar kare hızına bağlanır)
    /// ya da seyrek uygula (zırh doğru ısırır ama hedef yarım saniye hiç hasar
    /// almamış gibi durur — ışının vurduğu görünmez).
    /// </summary>
    void ApplyDamage(float dps, float dt)
    {
        if (dps <= 0f || dt <= 0f) return;

        if (hitsPlayer)
        {
            // Oyuncu tarafında zırh eşiği yok; kalkan zaten havuz olarak emiyor
            float amount = dps * dt;
            _targetPlayer?.TakeDamage(amount);
            _targetFighter?.TakeDamage(amount);
            _targetCollector?.TakeDamage(amount);
            return;
        }

        if (_target == null) return;

        float efficiency = BalanceConfig.Instance.BeamArmorEfficiency(
            dps, DamageUtil.ArmorOf(_target));

        DamageUtil.TryDamage(_target, dps * efficiency * dt, weaponType,
                             armorPreApplied: true);
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

}
