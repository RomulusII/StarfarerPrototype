using UnityEngine;

/// <summary>
/// Fizik tabanlı gemi hareketi. mass ve enginePower'dan maxSpeed, acceleration ve
/// turnRate türetilir. Küçük/hafif gemiler daha kıvrak; büyük/ağır gemiler daha hantal.
///
/// API:
///   MoveToward(target)    — hedefe hızlanır, yaklaşınca frenler
///   MoveInDirection(dir)  — yönde tam hızla ilerler (fren yok)
///   Brake()               — durağa yavaşlar
/// </summary>
public class ShipMovement : MonoBehaviour
{
    [Header("Stats")]
    public float mass        = 1f;
    public float enginePower = 3f;

    const float AccelFactor = 2f;
    const float TurnFactor  = 30f;   // derece/saniye, enginePower/mass birimi başına

    public float MaxSpeed     => enginePower / mass;
    public float Acceleration => enginePower / mass * AccelFactor;
    public float TurnRate     => enginePower / mass * TurnFactor;

    Vector2 _velocity;
    float   _facingAngle;

    public Vector2 Velocity => _velocity;

    public void Initialize(float initialFacingDeg = 180f)
    {
        _facingAngle       = initialFacingDeg;
        transform.rotation = Quaternion.Euler(0f, 0f, _facingAngle);
    }

    // Hedefe yaklaştıkça frenler, tam durur
    public void MoveToward(Vector2 target)
    {
        Vector2 toTarget = target - (Vector2)transform.position;
        float dist       = toTarget.magnitude;
        float brakeDist  = _velocity.sqrMagnitude / (2f * Mathf.Max(Acceleration, 0.01f));
        float speedMult  = dist > brakeDist ? 1f : dist / Mathf.Max(brakeDist, 0.001f);
        Vector2 desired  = dist > 0.05f ? toTarget.normalized * MaxSpeed * speedMult : Vector2.zero;
        Integrate(desired);
    }

    // Yönde tam hızda ilerler, yön değişimi yumuşaktır
    public void MoveInDirection(Vector2 dir)
    {
        Vector2 desired = dir.sqrMagnitude > 0.001f ? dir.normalized * MaxSpeed : Vector2.zero;
        Integrate(desired);
    }

    // Durağa yavaşlar
    public void Brake()
    {
        Integrate(Vector2.zero);
    }

    public bool IsNear(Vector2 target, float threshold = 0.15f)
        => (target - (Vector2)transform.position).sqrMagnitude < threshold * threshold;

    public bool IsAlmostStopped(float threshold = 0.05f)
        => _velocity.sqrMagnitude < threshold * threshold;

    void Integrate(Vector2 desired)
    {
        _velocity          = Vector2.MoveTowards(_velocity, desired, Acceleration * Time.deltaTime);
        transform.position += (Vector3)(_velocity * Time.deltaTime);

        if (_velocity.sqrMagnitude > 0.01f)
        {
            float target = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            _facingAngle = Mathf.MoveTowardsAngle(_facingAngle, target, TurnRate * Time.deltaTime);
        }

        transform.rotation = Quaternion.Euler(0f, 0f, _facingAngle);
    }
}
