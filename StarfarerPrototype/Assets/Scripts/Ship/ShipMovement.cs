using UnityEngine;

/// <summary>
/// 2B roket-itkili gemi uçuş modeli. Tüm AI gemileri (EnemyBot, FighterShip,
/// CollectorShip, BossShip) bu modeli kullanır.
///
/// KURALLAR
///  1. İtki yalnızca burun doğrultusunda uygulanır — gemi yana doğru hızlanamaz.
///     Arkasında roket varmış gibi kuyruktan buruna doğru ivmelenir.
///  2. Dönüş hızı ankı hıza bağlıdır: dururken kendi ekseninde dönebilir,
///     tam hızda yavaş döner. Kavis yarıçapı = hız / dönüş hızı → hızlıyken
///     geniş çember, yavaşken dar çember.
///  3. Gemi dönerken hareket vektörü burnu takip eder (grip). Klasik fizikte
///     böyle olmaz ama uzay gemisi hissini doğal kılar. grip &lt; 1 ise kavislerde
///     dışa doğru hafif savrulma (drift) kalır.
///  4. Kalan yanal kayma SlipDamping ile sönümlenir — drift her zaman toparlanır.
///  5. Burun istenen yönden saptıkça gaz kesilir (90°+ sapmada tamamen). Gemi
///     önce yavaşlar, döner, sonra yeniden ivmelenir.
///  6. MoveToward fren mesafesini v²/(2a) ile hesaplar, retro itkiyle yavaşlar.
///  7. Komutlar Update'te verilir, entegrasyon LateUpdate'te tek sefer yapılır —
///     aynı karede çift ilerleme olmaz. Komut verilmezse gemi süzülür (coast).
///  8. Nişan almadığı anlarda gemi kaçamak manevra yapar: burun 1–2 saniyede bir
///     yenilenen küçük rastgele açılarla salınır (evasive: true). Nişan alırken
///     sapma uygulanmaz — gemi hedefine net döner.
///
/// API:
///   MoveToward(target, evasive)   — hedefe burnu çevirir, hızlanır, varışta frenler
///   MoveInDirection(dir, evasive) — yöne döner, tam gaz
///   FaceAndBrake(dir)      — burnu yönde tutar, retro ile durur
///   Reverse(dir)           — burnu yönde tutar, geri iter (uzaklaşır)
///   Face(dir)              — sadece döner, gaz yok
///   Brake() / Coast() / Halt()
/// </summary>
public class ShipMovement : MonoBehaviour
{
    [Header("Stats")]
    public float mass        = 1f;
    public float enginePower = 3f;

    [Header("Uçuş karakteri")]
    [Tooltip("Dönüş hızı çarpanı. Küçük kıvrak gemiler > 1, ağır gemiler < 1.")]
    public float agility = 1f;

    [Tooltip("Hareket vektörünün burnu ne kadar takip ettiği. 1 = savrulma yok, " +
             "0 = saf atalet (gemi yan yan kayar). 0.85–0.95 doğal görünür.")]
    [Range(0f, 1f)] public float grip = 0.9f;

    [Tooltip("Retro itkinin ana itkiye oranı. Fren ve geri gitme gücü.")]
    public float retroFactor = 0.5f;

    [Header("Kaçamak Manevra")]
    [Tooltip("Nişan almadığı anlarda burnun rastgele saptığı max açı (derece). " +
             "0 = kaçamak manevra yok. Ağır gemilerde düşük tutulmalı.")]
    public float wanderAngle = 15f;

    [Tooltip("Yeni rastgele sapma seçme aralığı (saniye).")]
    public float wanderIntervalMin = 1f;
    public float wanderIntervalMax = 2f;

    [Tooltip("Manevra iticili büyük gemiler (boss, taşıyıcı): itki her yöne " +
             "uygulanabilir ve burun yönü hareketten bağımsızdır. Küçük gemilerde " +
             "kapalı olmalı — kavis, grip ve kaçamak manevra kuralları atlanır.")]
    public bool omniThrust = false;

    const float AccelFactor   = 2f;    // enginePower/mass birimi başına ivme
    const float TurnFactor    = 30f;   // derece/sn, enginePower/mass birimi başına
    const float PivotTurnMul  = 2.2f;  // dururken dönüş hızı çarpanı
    const float CruiseTurnMul = 0.65f; // tam hızda dönüş hızı çarpanı
    const float SlipDamping   = 3f;    // yanal kaymanın sönümlenme hızı (birim/sn²)
    const float StopEpsilon   = 0.02f;
    const float MaxReverse    = 0.35f; // geri giderken maxSpeed'in oranı
    const float ReverseAssistMin = 70f;  // bu sapmanın altında retro yok — kavis hız kaybettirmez
    const float ReverseAssistMax = 160f; // bu sapmada tam retro — gemi durup döner
    const float ArrivalDeadZone  = 0.02f; // MoveToward'ın durma toleransı (birim)
    const float WanderSlewFactor = 1.5f;  // sapmanın yeni hedefe kayma hızı (wanderAngle × bu)

    public float MaxSpeed     => enginePower / mass;
    public float Acceleration => enginePower / mass * AccelFactor;

    /// <summary>Referans dönüş hızı (derece/sn). Gerçek dönüş hızı hıza göre ölçeklenir.</summary>
    public float TurnRate => enginePower / mass * TurnFactor * Mathf.Max(agility, 0.05f);

    /// <summary>Tam hızda çizebildiği en dar kavis yarıçapı.</summary>
    public float MinTurnRadius => MaxSpeed / Mathf.Max(TurnRate * CruiseTurnMul * Mathf.Deg2Rad, 0.001f);

    /// <summary>Anki hız ve dönüş kabiliyetiyle çizebildiği kavis yarıçapı.</summary>
    public float CurrentTurnRadius
    {
        get
        {
            float speed = _velocity.magnitude;
            if (speed < StopEpsilon) return 0f;
            float maxSpeed = MaxSpeed;
            float speedT   = maxSpeed > 0.001f ? Mathf.Clamp01(speed / maxSpeed) : 0f;
            float rate     = TurnRate * Mathf.Lerp(PivotTurnMul, CruiseTurnMul, speedT) * Mathf.Deg2Rad;
            return rate > 0.0001f ? speed / rate : float.MaxValue;
        }
    }

    /// <summary>
    /// Hedef, geminin şu anki kavis çemberinin içinde mi? İçindeyse gemi ne kadar
    /// dönerse dönsün hedefe ulaşamaz — çevresinde sonsuz çember çizer. Bu durumda
    /// yavaşlaması gerekir: hız düştükçe kavis daralır ve hedef erişilebilir olur.
    /// </summary>
    public bool IsInsideTurnCircle(Vector2 target)
    {
        float r = CurrentTurnRadius;
        if (r < 0.01f) return false;

        Vector2 pos = transform.position;
        Vector2 f   = Facing;
        Vector2 to  = target - pos;

        // Hedef hangi taraftaysa dönüş merkezi o tarafta
        float   cross  = f.x * to.y - f.y * to.x;
        Vector2 side   = new Vector2(-f.y, f.x) * (cross >= 0f ? 1f : -1f);
        Vector2 center = pos + side * r;

        return (target - center).sqrMagnitude < r * r;
    }

    enum CmdMode { Coast, Thrust, Brake }

    Vector2 _velocity;
    float   _facingAngle;

    CmdMode _mode;
    float   _cmdHeading;
    float   _cmdThrottle;
    bool    _hasCommand;
    bool    _evasive;

    // Kaçamak manevra: hedef sapma 1–2 sn'de bir yenilenir, burun ona doğru süzülür
    float _wanderCurrent;
    float _wanderTarget;
    float _wanderTimer;

    /// <summary>Kaçamak manevranın anki burun sapması (derece).</summary>
    public float WanderOffset => _wanderCurrent;

    public Vector2 Velocity     => _velocity;
    public float   Speed        => _velocity.magnitude;
    public float   FacingAngle  => _facingAngle;
    public Vector2 Facing       => AngleToDir(_facingAngle);

    // ── Kurulum ───────────────────────────────────────────────────────────────

    public void Initialize(float initialFacingDeg = 180f)
    {
        _facingAngle       = initialFacingDeg;
        _velocity          = Vector2.zero;
        _hasCommand        = false;
        _mode              = CmdMode.Coast;
        transform.rotation = Quaternion.Euler(0f, 0f, _facingAngle);
    }

    // ── Komutlar ──────────────────────────────────────────────────────────────

    /// <summary>Hedefe döner, hızlanır; fren mesafesine girince retro ile yavaşlar.</summary>
    public void MoveToward(Vector2 target, bool evasive = false)
    {
        Vector2 toTarget = target - (Vector2)transform.position;
        float   dist     = toTarget.magnitude;
        if (dist < 0.001f) { Brake(); return; }

        Vector2 dir     = toTarget / dist;
        float   heading = DirToAngle(dir);

        // Hedefe doğru olan hız bileşeni ile fren mesafesi: v² / (2a)
        float closing   = Vector2.Dot(_velocity, dir);
        float brakeAcc  = Mathf.Max(Acceleration * retroFactor, 0.01f);
        float brakeDist = closing > 0f ? closing * closing / (2f * brakeAcc) : 0f;

        // KURAL 6 — Fren mesafesine girildiyse, ya da hedef kavis çemberinin
        // içinde kaldığı için dönerek ulaşılamıyorsa: yavaşla.
        float excess = dist - brakeDist;
        if (excess > ArrivalDeadZone && !IsInsideTurnCircle(target))
            Command(heading, Mathf.Clamp01(excess / 0.6f), CmdMode.Thrust, evasive);
        else
            Command(heading, 0f, CmdMode.Brake, evasive);
    }

    /// <summary>
    /// Yöne döner ve tam gaz ilerler. evasive = true ise burun 1–2 saniyede bir
    /// yenilenen küçük rastgele sapmalarla salınır — nişan alınmasını zorlaştırır.
    /// </summary>
    public void MoveInDirection(Vector2 dir, bool evasive = false)
    {
        if (dir.sqrMagnitude < 0.000001f) { Brake(); return; }
        Command(DirToAngle(dir), 1f, CmdMode.Thrust, evasive);
    }

    /// <summary>Burnu verilen yönde tutar, retro itkiyle durur.</summary>
    public void FaceAndBrake(Vector2 dir)
    {
        float heading = dir.sqrMagnitude > 0.000001f ? DirToAngle(dir) : _facingAngle;
        Command(heading, 0f, CmdMode.Brake);
    }

    /// <summary>Burnu verilen yönde tutup geri iter — ağır gemilerin mesafe koruması.</summary>
    public void Reverse(Vector2 dir, float throttle = 1f)
    {
        float heading = dir.sqrMagnitude > 0.000001f ? DirToAngle(dir) : _facingAngle;
        Command(heading, -Mathf.Clamp01(throttle), CmdMode.Thrust);
    }

    /// <summary>Sadece döner — gaz yok, mevcut hızla süzülür.</summary>
    public void Face(Vector2 dir, bool evasive = false)
    {
        if (dir.sqrMagnitude < 0.000001f) { Coast(); return; }
        Command(DirToAngle(dir), 0f, CmdMode.Coast, evasive);
    }

    /// <summary>Mevcut burun yönünü koruyarak durağa yavaşlar.</summary>
    public void Brake() => Command(_facingAngle, 0f, CmdMode.Brake);

    /// <summary>Motor kapalı, mevcut hız ve yönle süzülür.</summary>
    public void Coast() => Command(_facingAngle, 0f, CmdMode.Coast);

    /// <summary>Hızı anında sıfırlar (dock, kenetlenme gibi durumlar için).</summary>
    public void Halt()
    {
        _velocity = Vector2.zero;
        Coast();
    }

    public bool IsNear(Vector2 target, float threshold = 0.15f)
        => (target - (Vector2)transform.position).sqrMagnitude < threshold * threshold;

    public bool IsAlmostStopped(float threshold = 0.05f)
        => _velocity.sqrMagnitude < threshold * threshold;

    /// <summary>Burnun verilen yönden sapması (derece, mutlak).</summary>
    public float HeadingErrorTo(Vector2 dir)
        => dir.sqrMagnitude < 0.000001f
            ? 0f
            : Mathf.Abs(Mathf.DeltaAngle(_facingAngle, DirToAngle(dir)));

    void Command(float heading, float throttle, CmdMode mode, bool evasive = false)
    {
        _cmdHeading  = heading;
        _cmdThrottle = throttle;
        _mode        = mode;
        _hasCommand  = true;
        _evasive     = evasive;
    }

    // ── Entegrasyon ───────────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (UpgradeUI.IsPaused) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float maxSpeed = MaxSpeed;

        if (omniThrust)
        {
            IntegrateOmni(dt, maxSpeed);
            return;
        }

        // KURAL 8 — Kaçamak manevra sapması sürekli işler; sadece nişan almayan
        // komutlarda burna uygulanır. Böylece nişandan çıkınca sapma tazedir.
        UpdateWander(dt);
        float heading = _cmdHeading + (_evasive ? _wanderCurrent : 0f);

        // KURAL 2 — Dönüş hızı hıza göre azalır: hızlıyken geniş kavis.
        float speedT   = maxSpeed > 0.001f ? Mathf.Clamp01(_velocity.magnitude / maxSpeed) : 0f;
        float turnRate = TurnRate * Mathf.Lerp(PivotTurnMul, CruiseTurnMul, speedT);

        float prevAngle = _facingAngle;
        if (_hasCommand)
            _facingAngle = Mathf.MoveTowardsAngle(_facingAngle, heading, turnRate * dt);
        float turned = Mathf.DeltaAngle(prevAngle, _facingAngle);

        // KURAL 3 — Hareket vektörü burnu takip eder. Hız büyüklüğü korunur;
        // dönmek gemiyi yavaşlatmaz, sadece yönünü değiştirir.
        if (_velocity.sqrMagnitude > StopEpsilon * StopEpsilon && Mathf.Abs(turned) > 0.0001f)
            _velocity = Rotate(_velocity, turned * grip);

        Vector2 facing = AngleToDir(_facingAngle);
        Vector2 side   = new Vector2(-facing.y, facing.x);

        // KURAL 4 — Kalan yanal kayma sönümlenir.
        float lateral = Vector2.Dot(_velocity, side);
        if (Mathf.Abs(lateral) > 0.0001f)
        {
            float damped = Mathf.MoveTowards(lateral, 0f, SlipDamping * dt);
            _velocity += side * (damped - lateral);
        }

        switch (_mode)
        {
            case CmdMode.Thrust:
            {
                // KURAL 5 — Burun sapmışsa gaz kesilir.
                float error = Mathf.DeltaAngle(_facingAngle, heading);
                float gate  = ThrottleGate(error);

                if (_cmdThrottle >= 0f)
                {
                    // KURAL 1 — İtki yalnız burun doğrultusunda.
                    _velocity += facing * (Acceleration * _cmdThrottle * gate * dt);

                    // Sadece gerçek geri dönüşlerde (70°+) retro devreye girer.
                    // Normal kavislerde hız korunur — gemi yavaşlamadan çember çizer.
                    float assist = Mathf.InverseLerp(ReverseAssistMin, ReverseAssistMax,
                                                     Mathf.Abs(error)) * _cmdThrottle;
                    if (assist > 0.001f)
                        _velocity = Vector2.MoveTowards(_velocity, Vector2.zero,
                                                        Acceleration * retroFactor * assist * dt);
                }
                else
                {
                    // Retro itki: burun hedefte kalır, gemi geri geri uzaklaşır.
                    float delta   = Acceleration * retroFactor * -_cmdThrottle * gate * dt;
                    float forward = Vector2.Dot(_velocity, facing);
                    float wanted  = Mathf.Max(forward - delta, -maxSpeed * MaxReverse);
                    _velocity += facing * (wanted - forward);
                }
                break;
            }

            case CmdMode.Brake:
                _velocity = Vector2.MoveTowards(_velocity, Vector2.zero,
                                                Acceleration * retroFactor * dt);
                break;
        }

        float speed = _velocity.magnitude;
        if (speed > maxSpeed && speed > 0.0001f)
            _velocity *= maxSpeed / speed;
        else if (speed < StopEpsilon && _mode == CmdMode.Brake)
            _velocity = Vector2.zero;

        transform.position += (Vector3)(_velocity * dt);
        transform.rotation  = Quaternion.Euler(0f, 0f, _facingAngle);

        // Komut verilmeyen kare = süzülme
        _hasCommand = false;
        _mode       = CmdMode.Coast;
        _evasive    = false;
    }

    /// <summary>
    /// 1–2 saniyede bir yeni bir rastgele sapma hedefi seçer ve mevcut sapmayı
    /// ona doğru süzer. Ani kırılma değil, yavaşça salınan bir burun hareketi.
    /// </summary>
    void UpdateWander(float dt)
    {
        if (wanderAngle <= 0.01f) { _wanderCurrent = 0f; return; }

        _wanderTimer -= dt;
        if (_wanderTimer <= 0f)
        {
            _wanderTimer  = Random.Range(wanderIntervalMin, wanderIntervalMax);
            _wanderTarget = Random.Range(-wanderAngle, wanderAngle);
        }

        _wanderCurrent = Mathf.MoveTowards(_wanderCurrent, _wanderTarget,
                                           wanderAngle * WanderSlewFactor * dt);
    }

    /// <summary>
    /// Manevra iticili büyük gemiler: itki komut yönünde uygulanır, burun yönü
    /// Initialize ile verilen açıda sabit kalır. 1–5 numaralı kurallar geçerli
    /// değildir — bu gemiler kavis çizmez, mevki tutar.
    /// </summary>
    void IntegrateOmni(float dt, float maxSpeed)
    {
        switch (_mode)
        {
            case CmdMode.Thrust:
            {
                Vector2 dir = AngleToDir(_cmdHeading) * Mathf.Sign(_cmdThrottle);
                float   acc = _cmdThrottle >= 0f
                    ? Acceleration * _cmdThrottle
                    : Acceleration * retroFactor * -_cmdThrottle;
                _velocity += dir * (acc * dt);
                break;
            }

            case CmdMode.Brake:
                _velocity = Vector2.MoveTowards(_velocity, Vector2.zero,
                                                Acceleration * retroFactor * dt);
                break;
        }

        float speed = _velocity.magnitude;
        if (speed > maxSpeed && speed > 0.0001f)
            _velocity *= maxSpeed / speed;
        else if (speed < StopEpsilon && _mode == CmdMode.Brake)
            _velocity = Vector2.zero;

        transform.position += (Vector3)(_velocity * dt);
        transform.rotation  = Quaternion.Euler(0f, 0f, _facingAngle);

        _hasCommand = false;
        _mode       = CmdMode.Coast;
        _evasive    = false;
    }

    /// <summary>0° sapmada tam gaz, 90° ve üstünde gaz kesik.</summary>
    static float ThrottleGate(float errorDeg)
    {
        float a = Mathf.Abs(errorDeg);
        return a >= 90f ? 0f : Mathf.Cos(a * Mathf.Deg2Rad);
    }

    public static Vector2 Rotate(Vector2 v, float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        float c   = Mathf.Cos(rad);
        float s   = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    static Vector2 AngleToDir(float deg)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    static float DirToAngle(Vector2 dir) => Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
}
