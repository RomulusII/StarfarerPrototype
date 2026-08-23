using UnityEngine;

public enum CombatPattern { Orbit, Strafe, HoverFire }

/// <summary>
/// Taktik savaş yapay zekası. ShipMovement üzerinde çalışır.
///
/// Durum makinesi:
///   Approaching  — hedefe açısal offset ile yaklaşır (direkt çarpışmaz)
///   Engaging     — seçilen pattern'ı uygular; ateş fırsatı açık
///   Disengaging  — hedeften çapraz açıyla kaçar (radyal değil)
///   Repositioning — kısa duraklama, yeni açı seçer
///
/// Pattern seçimi:
///   Orbit      — kıvrak gemiler; hedef etrafında yörünge çizer, sürekli ateş eder
///   Strafe     — hızlı gemiler; dalar, geçer, döner, tekrar dalar
///   HoverFire  — ağır gemiler; tercih mesafesinde durur, ateş eder
///
/// ShipMovement roket-itkili bir modeldir: burada verilen her yön komutu
/// "burnunu şuraya çevir ve it" anlamına gelir, anlık hız değişimi değil.
/// Gemiler bu yüzden kavis çizer; yörünge yarıçapı MinTurnRadius'un altına
/// inemez ve dar dönüş isteyen manevralar önce yavaşlamayı gerektirir.
///
/// Nişan / kaçamak ayrımı: ateş menzilindeyken burun hedefe net döner. Diğer tüm
/// anlarda evasive salınım açıktır — burun 1–2 saniyede bir yenilenen küçük
/// rastgele açılarla oynar, kaçış ise radyal yerine 25–55° çapraz yapılır.
/// </summary>
[RequireComponent(typeof(ShipMovement))]
public class ShipBrain : MonoBehaviour
{
    [Header("Config")]
    public CombatPattern pattern           = CombatPattern.Orbit;
    public float         engageRange       = 5f;
    public float         fireRange         = 3.5f;
    public float         orbitRadius       = 4f;
    public float         engageDuration    = 5f;
    public float         repositionDelay   = 1f;

    [Tooltip("Kaçarken uzaklaşma vektöründen sapma açısı (derece). 0 = radyal kaçış.")]
    public float         escapeAngle       = 40f;

    [Tooltip("Açıksa gemi CombatArea dışına çıkınca taktiği bırakıp geri döner. " +
             "Bizim savaşçılarımız için açık — düşmanı kovalarken ekrandan çıkmasınlar.")]
    public bool          leashToCombatArea = false;

    public bool      HasTarget     => _target != null;
    public bool      InFireRange   => HasTarget &&
                                      Vector2.Distance(transform.position, _target.position) <= fireRange;
    public Transform TargetTransform => _target;
    public Vector3   TargetPosition  => _target != null ? _target.position : Vector3.zero;

    enum TacticalState { Approaching, Engaging, Disengaging, Repositioning }

    ShipMovement  _movement;
    Transform     _target;
    TacticalState _state = TacticalState.Repositioning;
    float         _stateTimer;
    float         _approachAngle;
    int           _orbitDir = 1;
    bool          _strafeInbound = true;
    float         _escapeOffset;

    // Kaçış açısı escapeAngle etrafında ±%40 dağılır — her kaçış birbirine benzemesin
    const float EscapeJitter = 0.4f;

    void Awake()
    {
        _movement      = GetComponent<ShipMovement>();
        _approachAngle = Random.Range(0f, 360f);
        _stateTimer    = Random.Range(0f, repositionDelay); // spawn stagger
    }

    public void SetTarget(Transform t)
    {
        if (_target == t) return;
        _target        = t;
        _state         = TacticalState.Approaching;
        _approachAngle = Random.Range(0f, 360f);
        _strafeInbound = true;
    }

    public void ClearTarget()
    {
        _target = null;
        _state  = TacticalState.Repositioning;
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;
        if (_target == null) return;

        // Alan dışına çıktıysa taktik beklesin, önce görüş alanına dön
        if (leashToCombatArea && !CombatArea.Contains(transform.position))
        {
            _movement.MoveToward(CombatArea.ClosestPointInside(transform.position));
            return;
        }

        float dist = Vector2.Distance(transform.position, _target.position);

        switch (_state)
        {
            case TacticalState.Approaching:   UpdateApproaching(dist); break;
            case TacticalState.Engaging:      UpdateEngaging(dist);    break;
            case TacticalState.Disengaging:   UpdateDisengaging(dist); break;
            case TacticalState.Repositioning: UpdateRepositioning();   break;
        }
    }

    // -------------------------------------------------------------------------
    // Durumlar
    // -------------------------------------------------------------------------

    void UpdateApproaching(float dist)
    {
        float   rad    = _approachAngle * Mathf.Deg2Rad;
        Vector2 offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (engageRange * 0.4f);
        _movement.MoveToward((Vector2)_target.position + offset, evasive: true);

        if (dist <= engageRange)
        {
            _state         = TacticalState.Engaging;
            _stateTimer    = engageDuration * Random.Range(0.75f, 1.25f);
            _orbitDir      = Random.Range(0, 2) == 0 ? 1 : -1;
            _strafeInbound = true;
        }
    }

    void UpdateEngaging(float dist)
    {
        _stateTimer -= Time.deltaTime;

        // Nişan menzilindeyken burun net kalır; dışındayken kaçamak salınım yapar
        bool evade = !InFireRange;

        switch (pattern)
        {
            case CombatPattern.Orbit:     DoOrbit(dist, evade);     break;
            case CombatPattern.Strafe:    DoStrafe(dist, evade);    break;
            case CombatPattern.HoverFire: DoHoverFire(dist, evade); break;
        }

        if (_stateTimer <= 0f || dist > engageRange + 4f)
        {
            _state        = TacticalState.Disengaging;
            _escapeOffset = Random.Range(escapeAngle * (1f - EscapeJitter),
                                         escapeAngle * (1f + EscapeJitter))
                          * (Random.Range(0, 2) == 0 ? 1f : -1f);
        }
    }

    void UpdateDisengaging(float dist)
    {
        // Radyal (tam ters) kaçış tahmin edilebilir ve nişan almayı kolaylaştırır.
        // Bunun yerine çapraz açıyla kaçar, üstüne kaçamak salınım biner.
        Vector2 away   = ((Vector2)transform.position - (Vector2)_target.position).normalized;
        Vector2 escape = ShipMovement.Rotate(away, _escapeOffset);
        _movement.MoveInDirection(escape, evasive: true);

        if (dist >= engageRange * 1.8f)
        {
            _state         = TacticalState.Repositioning;
            _stateTimer    = repositionDelay * Random.Range(0.8f, 1.4f);
            _approachAngle += Random.Range(90f, 270f); // farklı açıdan yaklaş
        }
    }

    void UpdateRepositioning()
    {
        _movement.Brake();
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f)
            _state = TacticalState.Approaching;
    }

    // -------------------------------------------------------------------------
    // Pattern'lar
    // -------------------------------------------------------------------------

    // Hedef etrafında yörünge — kıvrak gemiler için.
    // Yörünge yarıçapı geminin çizebileceği en dar kavisten küçük olamaz;
    // aksi halde gemi yörüngeyi tutturamaz ve spiral çizer.
    void DoOrbit(float dist, bool evade)
    {
        float   radius   = EffectiveOrbitRadius;
        Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
        Vector2 tangent  = new Vector2(-toTarget.y, toTarget.x).normalized * _orbitDir;

        // Uzaksa yaklaş, yakınsa uzaklaş; tangent her zaman baskın
        float radial   = Mathf.Clamp((dist - radius) / Mathf.Max(radius, 0.1f), -1f, 1f);
        float tangentW = 1f - Mathf.Abs(radial) * 0.4f;
        Vector2 dir    = (toTarget.normalized * radial + tangent * tangentW).normalized;
        _movement.MoveInDirection(dir, evade);
    }

    // Dalar, geçer, döner, tekrar — hızlı gemiler için
    void DoStrafe(float dist, bool evade)
    {
        if (_strafeInbound)
        {
            _movement.MoveInDirection((Vector2)_target.position - (Vector2)transform.position, evade);
            if (dist <= fireRange * 0.5f)
                _strafeInbound = false;
        }
        else
        {
            // Geçişten sonra düz uç: burun nereye bakıyorsa oraya tam gaz.
            // Hız sıfıra yakınsa da burun yönü geçerli bir referanstır.
            Vector2 run = _movement.Velocity.sqrMagnitude > 0.01f
                ? _movement.Velocity
                : _movement.Facing;
            // Geçiş sonrası kaçış: nişan alınmadığı an, salınım her zaman açık
            _movement.MoveInDirection(run, evasive: true);
            if (dist > engageRange * 0.8f)
                _strafeInbound = true;
        }
    }

    // Tercih mesafesinde dur, ateş et — ağır gemiler için.
    // Geri çekilirken burun hedefte kalır, retro itkiyle uzaklaşır —
    // ağır bir gemi kaçmak için 180° dönmez.
    void DoHoverFire(float dist, bool evade)
    {
        Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
        const float tol  = 0.6f;
        if (dist > orbitRadius + tol)
            _movement.MoveInDirection(toTarget, evade);
        else if (dist < orbitRadius - tol)
            _movement.Reverse(toTarget);
        else
            _movement.FaceAndBrake(toTarget);
    }

    float EffectiveOrbitRadius =>
        Mathf.Max(orbitRadius, _movement.MinTurnRadius * 1.15f);
}
