using UnityEngine;

/// <summary>
/// Enkaz (Debris) toplamak için hangardan üretilen küçük gemi.
/// Durum makinesi: Idle → GoToDebris → Collecting → Returning → Idle.
///
/// Kurallar:
///   - Enkaz hangardan MaxDebrisRange'den uzaksa hedef almaz / bırakır.
///   - Kargo dolunca Returning'e geçer, hangara varınca boşaltır.
///   - Kapasite dolmamışsa aynı seferde birden fazla enkaz toplayabilir.
/// </summary>
public class CollectorShip : MonoBehaviour
{
    enum Phase { Idle, GoToDebris, Collecting, Returning }

    public float maxHP       = 50f;
    public float currentHP   = 50f;
    public float salvageRate = 5f;
    public float maxCargo    = 30f;

    Phase        _phase = Phase.Idle;
    Debris       _target;
    Transform    _hangar;
    ShipMovement _movement;
    int          _cargo;        // tam birim kargo
    float        _accumulator;  // kesirli birikim
    float        _hoverPhase;

    const float Mass          = 1.5f;
    const float MaxDebrisRange = 12f;   // hangardan max enkaz takip mesafesi

    void Awake()
    {
        _hoverPhase = Random.Range(0f, Mathf.PI * 2f);
        BuildVisual();

        var col       = gameObject.AddComponent<CircleCollider2D>();
        col.radius    = 0.2f;
        col.isTrigger = true;

        var rb          = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        _movement      = gameObject.AddComponent<ShipMovement>();
        _movement.mass = Mass;
    }

    public void Init(Transform hangar, float speed, float maxHP, float salvageRate)
    {
        _hangar               = hangar;
        this.maxHP            = maxHP;
        this.currentHP        = maxHP;
        this.salvageRate      = salvageRate;
        _movement.enginePower = speed * Mass;
        _movement.Initialize(0f);
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        switch (_phase)
        {
            case Phase.Idle:
                if (_cargo >= (int)maxCargo) { _phase = Phase.Returning; break; }
                var d = FindClosestDebris();
                if (d != null) { _target = d; _phase = Phase.GoToDebris; }
                else HoverNearHangar();
                break;

            case Phase.GoToDebris:
                if (_target == null || _target.IsEmpty) { _target = null; _phase = Phase.Idle; break; }
                if (DebrisTooFar(_target))               { _target = null; _phase = Phase.Idle; break; }
                _movement.MoveToward(_target.transform.position);
                if (Vector2.Distance(transform.position, _target.transform.position) < 0.3f)
                    _phase = Phase.Collecting;
                break;

            case Phase.Collecting:
                if (_target == null || _target.IsEmpty) { _target = null; _phase = Phase.Idle; break; }
                if (_cargo >= (int)maxCargo)             { _phase = Phase.Returning; break; }
                _movement.Brake();
                _accumulator += _target.Collect(salvageRate * Time.deltaTime);
                while (_accumulator >= 1f)
                {
                    _cargo++;
                    _accumulator -= 1f;
                }
                if (_cargo >= (int)maxCargo) _phase = Phase.Returning;
                break;

            case Phase.Returning:
                if (_hangar == null) { _phase = Phase.Idle; break; }
                _movement.MoveToward(_hangar.position);
                if (Vector2.Distance(transform.position, _hangar.position) < 0.6f)
                {
                    ResourceInventory.Instance?.Add(ResourceType.RawMaterial, _cargo);
                    _cargo       = 0;
                    _accumulator = 0f;
                    _phase       = Phase.Idle;
                }
                break;
        }
    }

    bool DebrisTooFar(Debris d)
    {
        if (_hangar == null) return false;
        return Vector2.Distance(_hangar.position, d.transform.position) > MaxDebrisRange;
    }

    void HoverNearHangar()
    {
        if (_hangar == null) return;
        float t      = Time.time * 0.7f + _hoverPhase;
        var   offset = new Vector3(Mathf.Cos(t) * 0.9f, Mathf.Sin(t) * 0.5f, 0f);
        _movement.MoveToward(_hangar.position + offset);
    }

    Debris FindClosestDebris()
    {
        var    all   = FindObjectsByType<Debris>(FindObjectsSortMode.None);
        Debris best  = null;
        float  bestD = float.MaxValue;
        foreach (var d in all)
        {
            if (DebrisTooFar(d)) continue;
            float dist = Vector2.Distance(transform.position, d.transform.position);
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;
        if (currentHP <= 0f) Destroy(gameObject);
    }

    void BuildVisual()
    {
        var tex = new Texture2D(24, 12);
        var px  = new Color[24 * 12];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0.30f, 0.75f, 0.40f);
        tex.SetPixels(px); tex.Apply();
        var sr        = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, 24, 12), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 5;
    }
}
