using UnityEngine;

/// <summary>
/// Enkaz (Debris) toplamak için hangardan üretilen küçük gemi.
/// Durum makinesi: Idle → GoToDebris → Collecting → Returning → Idle.
/// </summary>
public class CollectorShip : MonoBehaviour
{
    enum Phase { Idle, GoToDebris, Collecting, Returning }

    public float speed       = 3f;
    public float maxHP       = 50f;
    public float currentHP   = 50f;
    public float salvageRate = 5f;

    Phase     _phase = Phase.Idle;
    Debris    _target;
    Transform _hangar;
    float     _resourceAccum;
    float     _hoverPhase;

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
    }

    public void Init(Transform hangar, float speed, float maxHP, float salvageRate)
    {
        _hangar          = hangar;
        this.speed       = speed;
        this.maxHP       = maxHP;
        this.currentHP   = maxHP;
        this.salvageRate = salvageRate;
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        switch (_phase)
        {
            case Phase.Idle:
                var d = FindClosestDebris();
                if (d != null) { _target = d; _phase = Phase.GoToDebris; }
                else HoverNearHangar();
                break;

            case Phase.GoToDebris:
                if (_target == null) { _phase = Phase.Idle; break; }
                MoveToward(_target.transform.position);
                if (Vector2.Distance(transform.position, _target.transform.position) < 0.3f)
                    _phase = Phase.Collecting;
                break;

            case Phase.Collecting:
                if (_target == null || _target.IsEmpty) { _target = null; _phase = Phase.Returning; break; }
                ResourceType resType = _target.resourceType;
                float got = _target.Collect(salvageRate * Time.deltaTime);
                _resourceAccum += got;
                while (_resourceAccum >= 1f)
                {
                    ResourceInventory.Instance?.Add(resType, 1);
                    _resourceAccum -= 1f;
                }
                break;

            case Phase.Returning:
                if (_hangar == null) { _phase = Phase.Idle; break; }
                MoveToward(_hangar.position);
                if (Vector2.Distance(transform.position, _hangar.position) < 0.6f)
                    _phase = Phase.Idle;
                break;
        }
    }

    void MoveToward(Vector3 target)
    {
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        var dir = target - transform.position;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void HoverNearHangar()
    {
        if (_hangar == null) return;
        float t      = Time.time * 0.7f + _hoverPhase;
        var   offset = new Vector3(Mathf.Cos(t) * 0.9f, Mathf.Sin(t) * 0.5f, 0f);
        MoveToward(_hangar.position + offset);
    }

    Debris FindClosestDebris()
    {
        var all = FindObjectsByType<Debris>(FindObjectsSortMode.None);
        Debris best  = null;
        float  bestD = float.MaxValue;
        foreach (var d in all)
        {
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
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0,0,24,12), new Vector2(0.5f,0.5f), 100f);
        sr.sortingOrder = 5;
    }
}
