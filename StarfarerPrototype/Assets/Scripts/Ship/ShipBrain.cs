using UnityEngine;

public enum CombatPattern { Orbit, Strafe, HoverFire }

/// <summary>
/// Taktik savaş yapay zekası. ShipMovement üzerinde çalışır.
///
/// Durum makinesi:
///   Approaching  — hedefe açısal offset ile yaklaşır (direkt çarpışmaz)
///   Engaging     — seçilen pattern'ı uygular; ateş fırsatı açık
///   Disengaging  — hedefe ters yönde kaçar
///   Repositioning — kısa duraklama, yeni açı seçer
///
/// Pattern seçimi:
///   Orbit      — kıvrak gemiler; hedef etrafında yörünge çizer, sürekli ateş eder
///   Strafe     — hızlı gemiler; dalar, geçer, döner, tekrar dalar
///   HoverFire  — ağır gemiler; tercih mesafesinde durur, ateş eder
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
        _movement.MoveToward((Vector2)_target.position + offset);

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

        switch (pattern)
        {
            case CombatPattern.Orbit:    DoOrbit(dist);    break;
            case CombatPattern.Strafe:   DoStrafe(dist);   break;
            case CombatPattern.HoverFire: DoHoverFire(dist); break;
        }

        if (_stateTimer <= 0f || dist > engageRange + 4f)
        {
            _state = TacticalState.Disengaging;
        }
    }

    void UpdateDisengaging(float dist)
    {
        Vector2 away = ((Vector2)transform.position - (Vector2)_target.position).normalized;
        _movement.MoveInDirection(away);

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

    // Hedef etrafında yörünge — kıvrak gemiler için
    void DoOrbit(float dist)
    {
        Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
        Vector2 tangent  = new Vector2(-toTarget.y, toTarget.x).normalized * _orbitDir;

        // Uzaksa yaklaş, yakınsa uzaklaş; tangent her zaman baskın
        float radial   = Mathf.Clamp((dist - orbitRadius) / Mathf.Max(orbitRadius, 0.1f), -1f, 1f);
        float tangentW = 1f - Mathf.Abs(radial) * 0.4f;
        Vector2 dir    = (toTarget.normalized * radial + tangent * tangentW).normalized;
        _movement.MoveInDirection(dir);
    }

    // Dalar, geçer, döner, tekrar — hızlı gemiler için
    void DoStrafe(float dist)
    {
        if (_strafeInbound)
        {
            _movement.MoveInDirection(((Vector2)_target.position - (Vector2)transform.position).normalized);
            if (dist <= fireRange * 0.5f)
                _strafeInbound = false;
        }
        else
        {
            // Mevcut hız yönünde devam et
            if (_movement.Velocity.sqrMagnitude > 0.01f)
                _movement.MoveInDirection(_movement.Velocity.normalized);
            if (dist > engageRange * 0.8f)
                _strafeInbound = true;
        }
    }

    // Tercih mesafesinde dur, ateş et — ağır gemiler için
    void DoHoverFire(float dist)
    {
        Vector2 toTarget = (Vector2)_target.position - (Vector2)transform.position;
        const float tol  = 0.6f;
        if (dist > orbitRadius + tol)
            _movement.MoveInDirection(toTarget.normalized);
        else if (dist < orbitRadius - tol)
            _movement.MoveInDirection(-toTarget.normalized);
        else
            _movement.Brake();
    }
}
