using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turret mermisi.
/// Normal mod: sabit yön, ömür sonunda yok olur.
/// Güdümlü mod (Roket): sınırlı dönüş hızıyla hedefe yönelir — organik yay çizer.
/// guidedTarget Transform'dur; EnemyBot ve BossShip dahil her hedefi izler.
/// </summary>
public class TurretBullet : MonoBehaviour
{
    public float      damage;
    public float      speed;
    public WeaponType weaponType  = WeaponType.Kinetic;
    public bool       isGuided;
    public Transform  guidedTarget;
    [Tooltip("Saniyede derece — roketin maksimum dönüş hızı.")]
    public float      turnRate    = 120f;
    [Tooltip("0 = vurulabilir değil. Roketler için ayarlanır.")]
    public float      hp         = 0f;

    Vector2 _dir;

    /// <summary>Collider yarıçapı — süpürme mesafesi buna göre uzatılır.</summary>
    const float Radius = 0.07f;

    // Süpürme sonuçları paylaşılan bir tamponda toplanır; her mermi her karede
    // dizi ayırsaydı hızlı ateş eden turretlerde GC yükü olurdu.
    static readonly List<RaycastHit2D> _sweep = new();

    public void TakeDamage(float amount)
    {
        if (hp <= 0f) return;
        hp -= amount;
        if (hp <= 0f) Destroy(gameObject);
    }

    void Awake()
    {
        var col    = gameObject.AddComponent<CircleCollider2D>();
        col.radius    = Radius;
        col.isTrigger = true;

        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    public void SetDirection(Vector2 dir)
    {
        _dir = dir.normalized;
        ApplyRotation();
    }

    Transform FindClosestTarget()
    {
        Transform best  = null;
        float     bestD = float.MaxValue;

        foreach (var e in FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
        {
            float d = Vector2.Distance(e.transform.position, transform.position);
            if (d < bestD) { bestD = d; best = e.transform; }
        }

        var boss = FindFirstObjectByType<BossShip>();
        if (boss != null)
        {
            float d = Vector2.Distance(boss.transform.position, transform.position);
            if (d < bestD) best = boss.transform;
        }

        return best;
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (isGuided)
        {
            if (guidedTarget == null)
                guidedTarget = FindClosestTarget();

            if (guidedTarget != null)
            {
                Vector2 desired = ((Vector2)guidedTarget.position
                                  - (Vector2)transform.position).normalized;
                float maxTurn  = turnRate * Time.deltaTime;
                float angle    = Vector2.SignedAngle(_dir, desired);
                float clamped  = Mathf.Clamp(angle, -maxTurn, maxTurn);
                _dir = Rotate(_dir, clamped).normalized;
                ApplyRotation();
            }
        }

        Sweep(speed * Time.deltaTime);
    }

    /// <summary>
    /// Yolu SÜPÜREREK ilerler. Mermi Update'te hareket ediyor ama trigger
    /// tespiti fizik adımında (0.02 sn) yapılıyor; hızlı mermi iki adım arasında
    /// hedefin ÜSTÜNDEN atlıyordu.
    ///
    /// Point Defence mermisi (hız 20) fizik adımı başına 0.40 birim gidiyor,
    /// bombayla çakışma penceresi ise (0.07 + 0.10) × 2 = 0.34 birim. Yani
    /// vuruşların çoğu hiç kaydedilmiyor ve PD'nin VARLIK SEBEBİ — bombayı
    /// kalkana varmadan düşürmek — işlemiyordu.
    ///
    /// Çözüm collider'ı şişirmek değil (o, mermiyi her şeye karşı şişmanlatır)
    /// yolun taranmasıdır: mermi ne kadar hızlanırsa hızlansın aradaki her şeyi
    /// görür. OnTriggerEnter2D yerinde kalır — merminin ÜSTÜNE gelen hedefler
    /// için gerekli.
    /// </summary>
    void Sweep(float distance)
    {
        if (distance <= 0f) return;

        int count = Physics2D.Raycast(transform.position, _dir,
                                      ContactFilter2D.noFilter, _sweep, distance + Radius);
        if (count > 0)
        {
            _sweep.Sort((a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < _sweep.Count; i++)
                if (TryHit(_sweep[i].collider, _sweep[i].point)) return;
        }

        transform.Translate(_dir * distance, Space.World);
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    void ApplyRotation()
    {
        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void OnTriggerEnter2D(Collider2D other) => TryHit(other, transform.position);

    /// <summary>
    /// Bu collider'a vurulabiliyorsa hasarı uygular ve true döner. Hem süpürme
    /// hem trigger yolu buradan geçer — iki ayrı kopya zamanla birbirinden sapardı.
    /// </summary>
    bool TryHit(Collider2D other, Vector2 hitPos)
    {
        if (other == null) return false;

        var bomb = other.GetComponent<Bomb>();
        if (bomb != null)
        {
            bomb.TakeDamage(damage);
            // Bomba tek vuruşta gider: Point Defence'in işini yaptığı görünsün
            HitEffect.SpawnImpact(hitPos, _dir, other.transform.position,
                                  ImpactSurface.Hull, damage, lethal: true);
            Destroy(gameObject);
            return true;
        }

        var surface = DamageUtil.SurfaceOf(other);

        if (DamageUtil.TryDamage(other, damage, weaponType))
        {
            bool lethal = other.GetComponent<HealthBar>()?.currentHealth <= 0f;

            BalanceLog.Event("shot_hit")
                      .Str("kaynak", "turret")
                      .Str("silah",  weaponType.ToString())
                      .Str("yuzey",  surface.ToString())
                      .Str("hedef",  DamageUtil.TypeNameOf(other))
                      .Num("hasar",  damage)
                      .Bool("oldurdu", lethal)
                      .End();

            HitEffect.SpawnImpact(hitPos, _dir, other.transform.position,
                                  surface, damage, lethal);
            if (surface == ImpactSurface.Shield)
                DamageUtil.ShieldFlash(other, hitPos);
            Destroy(gameObject);
            return true;
        }

        return false;
    }
}
