using UnityEngine;

/// <summary>
/// Enkaz (Debris) toplamak için hangardan üretilen küçük gemi.
/// Durum makinesi: Idle → GoToDebris → Collecting → Returning → Idle.
///
/// Kurallar:
///   - Enkaz hangardan MaxDebrisRange'den uzaksa hedef almaz / bırakır.
///   - Tip ayrımı yapmaz: ne bulursa toplar. Kargo tipe göre ayrı sayılır ama
///     kapasite toplam üzerinden işler; hangara varınca hepsi birden boşaltılır.
///   - Kargo dolunca Returning'e geçer.
///   - Kapasite dolmamışsa aynı seferde birden fazla enkaz toplayabilir.
///   - Toplanacak enkaz kalmadıysa ve kargoda bir şey varsa boşta beklemez —
///     ana gemiye dönüp kargoyu boşaltır.
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

    // Diğer toplayıcıların çakışma kontrolü için
    public Debris ClaimedDebris =>
        (_phase == Phase.GoToDebris || _phase == Phase.Collecting) ? _target : null;
    Transform    _hangar;
    ShipMovement _movement;
    float        _hoverPhase;

    // Kargo tip başına ayrı tutulur; kapasite toplam üzerinden kontrol edilir.
    //
    // Miktarlar KESİRLİDİR. Eskiden kargo tam birime yuvarlanıyor, artan kesir
    // ayrı bir birikeçte bekliyor ve boşaltmada SIFIRLANIYORDU. Level 1'de bir
    // asteroit parçası 0.5 kaynak düşürür — yani kristalin tamamı, metalin de
    // her seferin artığı yuvarlamada yanıyordu.
    static readonly int TypeCount = System.Enum.GetValues(typeof(ResourceType)).Length;
    float[] _cargo;         // tip başına kargo
    float   _cargoTotal;    // tüm tiplerin toplamı — kapasite bunun üzerinden

    const float Mass          = 1.5f;
    const float MaxDebrisRange = 12f;   // hangardan max enkaz takip mesafesi

    void Awake()
    {
        _hoverPhase  = Random.Range(0f, Mathf.PI * 2f);
        _cargo       = new float[TypeCount];
        BuildVisual();

        var col       = gameObject.AddComponent<CircleCollider2D>();
        col.radius    = 0.2f;
        col.isTrigger = true;

        var rb          = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType     = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        _movement         = gameObject.AddComponent<ShipMovement>();
        _movement.mass    = Mass;
        _movement.agility = 1.6f;   // iş gemisi: enkaza hassas yanaşabilmeli
        _movement.grip    = 0.97f;
        _movement.wanderAngle = 0f;  // kaçamak manevra yok — düz ve verimli gider
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
                if (_cargoTotal >= maxCargo) { _phase = Phase.Returning; break; }
                var d = FindClosestDebris();
                if (d != null) { _target = d; _phase = Phase.GoToDebris; }
                else if (_cargoTotal > 0f) _phase = Phase.Returning; // toplanacak yok → boşalt
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
                // Enkaz sola kayarken toplayıcıyı menzil dışına sürüklemesin
                if (DebrisTooFar(_target))              { _target = null; _phase = Phase.Idle; break; }
                if (_cargoTotal >= maxCargo)            { _phase = Phase.Returning; break; }
                // Enkaz ile birlikte sürüklen — motor kapalı, kalan hız sıfırlanır
                _movement.Halt();
                transform.position += (Vector3)(_target.Velocity * Time.deltaTime);

                int   ti   = (int)_target.resourceType;
                float take = Mathf.Min(salvageRate * Time.deltaTime, maxCargo - _cargoTotal);
                float got  = _target.Collect(take);
                _cargo[ti]  += got;
                _cargoTotal += got;

                if (_cargoTotal >= maxCargo) _phase = Phase.Returning;
                break;

            case Phase.Returning:
                if (_hangar == null) { _phase = Phase.Idle; break; }
                _movement.MoveToward(_hangar.position);
                if (Vector2.Distance(transform.position, _hangar.position) < 0.6f)
                {
                    UnloadCargo();
                    _phase = Phase.Idle;
                }
                break;
        }
    }

    /// <summary>Her kaynak tipini kendi envanterine boşaltır.</summary>
    void UnloadCargo()
    {
        for (int i = 0; i < _cargo.Length; i++)
        {
            if (_cargo[i] > 0f)
                ResourceInventory.Instance?.Add((ResourceType)i, _cargo[i]);
            _cargo[i] = 0f;
        }
        _cargoTotal = 0f;
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
        // Başka toplayıcıların zaten hedeflediği enkazları dışla
        var claimed = new System.Collections.Generic.HashSet<Debris>();
        foreach (var c in FindObjectsByType<CollectorShip>(FindObjectsSortMode.None))
            if (c != this && c.ClaimedDebris != null)
                claimed.Add(c.ClaimedDebris);

        var    all   = FindObjectsByType<Debris>(FindObjectsSortMode.None);
        Debris best  = null;
        float  bestD = float.MaxValue;
        foreach (var d in all)
        {
            if (claimed.Contains(d)) continue;
            if (DebrisTooFar(d)) continue;
            float dist = Vector2.Distance(transform.position, d.transform.position);
            if (dist < bestD) { bestD = dist; best = d; }
        }
        return best;
    }

    public void SetSpeed(float speed)
    {
        _movement.enginePower = speed * Mass;
    }

    public void TakeDamage(float amount)
    {
        currentHP -= amount;
        if (currentHP <= 0f) Destroy(gameObject);
    }

    void BuildVisual()
    {
        var sr        = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.Collector, 24, 12,
                              new Color(0.30f, 0.75f, 0.40f));
        sr.sortingOrder = 5;
    }
}
