using UnityEngine;

/// <summary>
/// Işın tabanlı lazer atışı.
/// Spawn edildiği anda transform.up yönünde Physics2D raycast yapar;
/// ilk düşmana çarpar veya maxRange'e kadar uzanır.
/// burnDuration boyunca her frame hasar × deltaTime ve enerji tüketir.
/// Enerji biterse veya süre dolarsa beam kesilir.
/// </summary>
public class LaserBeam : MonoBehaviour
{
    public float      damage          = 80f;   // hasar/saniye (burn boyunca)
    public float      burnDuration    = 0.1f;  // yanma süresi (saniye)
    public float      energyPerSecond = 20f;   // enerji tüketimi/saniye
    public WeaponType weaponType      = WeaponType.Laser;
    public float      maxRange        = 22f;

    float        _remaining;
    Collider2D   _target;
    Vector3      _endPoint;
    LineRenderer _line;

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

        _remaining -= Time.deltaTime;

        float energyMulti = BoostController.Mode == BoostMode.Shield ? 1f / 3f :
                            BoostController.Mode == BoostMode.Weapon  ? 3f      : 1f;
        float dmgMulti    = BoostController.Mode == BoostMode.Weapon  ? 2f      :
                            BoostController.Mode == BoostMode.Shield   ? 1f / 3f : 1f;

        // Enerji kontrolü — yetersizse beam anında kesilir
        bool hasEnergy = EnergyBus.Instance == null ||
            EnergyBus.Instance.RequestEnergy(energyPerSecond * energyMulti * Time.deltaTime);

        if (!hasEnergy || _remaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Raycast her frame güncellenir — hareket eden düşmanları yakalar
        UpdateRaycast();
        UpdateVisual();

        if (_target != null)
            DamageUtil.TryDamage(_target, damage * dmgMulti * Time.deltaTime, weaponType);
    }

    // ── Raycast ───────────────────────────────────────────────────────────────

    void UpdateRaycast()
    {
        Vector2 origin    = transform.position;
        Vector2 direction = transform.up; // WeaponMount'un aim yönü

        var hits = Physics2D.RaycastAll(origin, direction, maxRange);
        // Mesafeye göre sırala — en yakın önce
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        _target   = null;
        _endPoint = transform.position + (Vector3)(direction * maxRange);

        foreach (var hit in hits)
        {
            var c = hit.collider;
            if (c.GetComponent<EnemyBot>()      != null ||
                c.GetComponent<BossHardpoint>() != null ||
                c.GetComponent<BossShip>()      != null)
            {
                _target   = c;
                _endPoint = hit.point;
                break;
            }
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

        // Kalan süreye göre parlaklık azalır — fade out efekti
        float t = Mathf.Clamp01(_remaining / burnDuration);
        _line.startColor = new Color(0.3f, 0.9f, 1f, t);
        _line.endColor   = new Color(0.6f, 1f,   1f, t * 0.3f);
    }

    void OnDestroy()
    {
        if (_line != null && _line.material != null)
            Destroy(_line.material);
    }
}
