using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plazma bolt: üç fazlı hareket.
///
/// 1) GROWING  (emitDuration sn): Baş ilerler, kuyruk sabit → namludan çıkar.
/// 2) TRAVELING: Baş + kuyruk aynı hızda → sabit uzunlukta bolt ekran boyunca uçar.
/// 3) FADING   (enerji bitince): Baş durur, kuyruk yetişir → bolt solar ve yok olur.
///
/// Enerji bitmezse maxLength'e ulaşınca bolt görünmeden yok edilir.
/// </summary>
public class PlasmaBeam : MonoBehaviour
{
    // WeaponController tarafından spawn öncesi atanır
    public float      beamWidth;
    public float      maxLength    = 60f;
    public float      headSpeed;
    public float      totalEnergy;
    public float      dps;
    public float      emitDuration = 0.2f;   // Growing fazı süresi (saniye)
    public WeaponType weaponType;

    enum Phase { Growing, Traveling, Fading }

    Vector3 _origin;
    Vector3 _firingDir;
    float   _rotAngle;
    float   _headDist;
    float   _tailDist;
    float   _emitTimer;
    float   _initialEnergy;
    Phase   _phase = Phase.Growing;

    SpriteRenderer            _sr;
    readonly List<Collider2D> _buffer = new();
    readonly HashSet<int>     _hitSet = new();

    // Plazma alan hasarını HER KARE uyguluyor; kıvılcımı da her kare çıkarmak
    // tek bir bolt için saniyede yüzlerce parçacık demek olurdu. Lazerle aynı
    // yaklaşım: emisyon sayaçla kısılır.
    float       _sparkTimer;
    const float SparkInterval = 0.05f;

    // Hasar HER KAREDE uygulanır; zırh LaserBeam ile aynı modelle, ORAN olarak
    // hesaplanır (BalanceConfig.BeamArmorEfficiency). Bkz. LaserBeam.ApplyDamage.

    static readonly Color PlasmaSparkColor = new Color(0.55f, 1f, 0.4f);

    // ── Başlatma ──────────────────────────────────────────────────────────────

    void Awake()
    {
        _origin    = transform.position;
        _firingDir = transform.up;
        _rotAngle  = transform.eulerAngles.z;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1),
                                   new Vector2(0.5f, 0.5f), 1f);

        _sr              = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite       = sprite;
        _sr.color        = new Color(0.5f, 1f, 0.3f, 0.88f);
        _sr.sortingOrder = 2;
    }

    void Start()
    {
        _initialEnergy = totalEnergy;   // Awake'de henüz atanmamış olabilir
    }

    // ── Güncelleme ────────────────────────────────────────────────────────────

    void Update()
    {
        switch (_phase)
        {
            case Phase.Growing:
                _headDist  += headSpeed * Time.deltaTime;
                _emitTimer += Time.deltaTime;

                if (_emitTimer >= emitDuration)
                    _phase = Phase.Traveling;

                if (totalEnergy <= 0f)
                    _phase = Phase.Fading;
                break;

            case Phase.Traveling:
                _headDist += headSpeed * Time.deltaTime;
                _tailDist += headSpeed * Time.deltaTime;

                if (totalEnergy <= 0f)
                {
                    _phase = Phase.Fading;
                    break;
                }

                // Ekranı tamamen geçtiyse enerji harcamadan yok et
                if (_headDist >= maxLength)
                {
                    Destroy(gameObject);
                    return;
                }
                break;

            case Phase.Fading:
                _tailDist += headSpeed * Time.deltaTime;
                if (_tailDist >= _headDist)
                {
                    Destroy(gameObject);
                    return;
                }
                break;
        }

        float length = _headDist - _tailDist;
        if (length < 0.001f) { Destroy(gameObject); return; }

        // ── Transform ────────────────────────────────────────────────────────
        float centerDist     = (_tailDist + _headDist) * 0.5f;
        transform.position   = _origin + _firingDir * centerDist;
        transform.localScale = new Vector3(beamWidth, length, 1f);

        // ── Görsel renk / alfa ────────────────────────────────────────────────
        float alpha = _phase == Phase.Fading
            ? Mathf.Clamp01((_headDist - _tailDist) / Mathf.Max(0.001f, _headDist - _tailDist))
            : (_initialEnergy > 0f ? Mathf.Lerp(0.35f, 0.88f,
                                       Mathf.Clamp01(totalEnergy / _initialEnergy)) : 0.35f);

        _sr.color = new Color(0.5f, 1f, 0.3f, alpha);

        // ── Alan hasarı (Fading'de de kuyruk geçerken hasar verebilir) ────────
        if (totalEnergy <= 0f) return;

        _sparkTimer -= Time.deltaTime;

        _hitSet.Clear();
        int count = Physics2D.OverlapBox(
            (Vector2)transform.position,
            new Vector2(beamWidth, length),
            _rotAngle,
            ContactFilter2D.noFilter,
            _buffer);

        for (int i = 0; i < count; i++)
        {
            var col = _buffer[i];

            bool isEnemy = col.GetComponent<EnemyBot>()       != null
                        || col.GetComponent<BarrierShield>()  != null
                        || col.GetComponent<BossHardpoint>()  != null
                        || col.GetComponent<BossShip>()       != null;
            if (!isEnemy) continue;

            int id = col.gameObject.GetInstanceID();
            if (!_hitSet.Add(id)) continue;

            float tickDmg = Mathf.Min(dps * Time.deltaTime, totalEnergy);
            if (tickDmg <= 0f) break;

            // Zırh oran olarak: enerji bütçesinden düşen HAM hasardır, hedefe
            // varan ise zırhla ölçeklenmiş olanı. Işın zırhlı hedefte daha çabuk
            // tükenir — kalkan gibi delinmesi gereken bir engel olarak durur.
            float efficiency = BalanceConfig.Instance.BeamArmorEfficiency(
                dps, DamageUtil.ArmorOf(col));

            DamageUtil.TryDamage(col, tickDmg * efficiency, weaponType,
                                 armorPreApplied: true);
            totalEnergy -= tickDmg;

            if (_sparkTimer <= 0f)
                HitEffect.SpawnLaserSparks(col.transform.position, _firingDir,
                                           -(Vector2)_firingDir, PlasmaSparkColor);

            if (totalEnergy <= 0f)
            {
                _phase = Phase.Fading;
                break;
            }
        }

        // Kıvılcım sayacı döngüden SONRA sıfırlanır: aynı karede birden fazla
        // hedefi yakan bir bolt hepsinde kıvılcım çıkarsın, yalnızca ilkinde değil.
        if (_sparkTimer <= 0f) _sparkTimer = SparkInterval;
    }
}
