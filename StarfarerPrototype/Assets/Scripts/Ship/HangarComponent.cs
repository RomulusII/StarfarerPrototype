using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gemi hangarı. Toplayıcı ve savaş gemilerini otomatik üretir.
/// Hayatta olan gemi sayısı maksimum sayının altına düşünce üretim sayacı başlar.
/// </summary>
public class HangarComponent : ShipComponentBase
{
    public int   baseMaxCollectors   = 1;
    public int   baseMaxFighters     = 0;
    public float baseProductionTime  = 15f;
    public float baseShipSpeed       = 3f;
    public float baseCollectorHP     = 50f;
    public float baseFighterHP       = 40f;
    public float baseSalvageRate     = 5f;
    public float baseFighterDamage   = 8f;
    public float baseFighterFireRate = 2f;

    // Efektif değerler — stat upgrade çarpanları uygulanmış
    public int   MaxCollectors      => baseMaxCollectors + GetStatLevel("maxCollectors");
    public int   MaxFighters        => baseMaxFighters   + GetStatLevel("maxFighters");
    public float EffProductionTime  => baseProductionTime / GetMultiplier("productionSpeed");
    public float EffShipSpeed       => baseShipSpeed      * GetMultiplier("speed");
    public float EffCollectorHP     => baseCollectorHP    * GetMultiplier("maxHP");
    public float EffFighterHP       => baseFighterHP      * GetMultiplier("maxHP");
    public float EffSalvageRate     => baseSalvageRate    * GetMultiplier("salvageRate");
    public float EffFighterFireRate => baseFighterFireRate / GetMultiplier("fireRate");
    public float EffFighterDamage   => baseFighterDamage  * GetMultiplier("fireRate");

    readonly List<CollectorShip> _collectors = new();
    readonly List<FighterShip>   _fighters   = new();
    float _collectorTimer = -1f;
    float _fighterTimer   = -1f;

    protected override void Awake()
    {
        _skipVisual = true; // Hangar meta-komponent, halka/HP barı gösterilmez
        base.Awake();
        componentName     = "Hangar";
        BuildVisual();
    }

    void Update()
    {
        if (!IsOperational) return;
        if (UpgradeUI.IsPaused) return;

        _collectors.RemoveAll(c => c == null);
        _fighters.RemoveAll(f => f == null);

        // Toplayıcı üretimi
        if (_collectors.Count < MaxCollectors)
        {
            if (_collectorTimer < 0f) _collectorTimer = EffProductionTime;
            _collectorTimer -= Time.deltaTime;
            if (_collectorTimer <= 0f) { SpawnCollector(); _collectorTimer = -1f; }
        }
        else _collectorTimer = -1f;

        // Savaş gemisi üretimi
        if (_fighters.Count < MaxFighters)
        {
            if (_fighterTimer < 0f) _fighterTimer = EffProductionTime;
            _fighterTimer -= Time.deltaTime;
            if (_fighterTimer <= 0f) { SpawnFighter(); _fighterTimer = -1f; }
        }
        else _fighterTimer = -1f;
    }

    void SpawnCollector()
    {
        var go  = new GameObject("Collector");
        go.transform.position = transform.position;
        var col = go.AddComponent<CollectorShip>();
        col.Init(transform, EffShipSpeed, EffCollectorHP, EffSalvageRate);
        _collectors.Add(col);
    }

    void SpawnFighter()
    {
        var go  = new GameObject("Fighter");
        go.transform.position = transform.position;
        var fig = go.AddComponent<FighterShip>();
        fig.Init(transform, EffShipSpeed, EffFighterHP, EffFighterFireRate, EffFighterDamage);
        _fighters.Add(fig);
    }

    public override void OnStatUpgraded(string key)
    {
        if (key != "speed") return;
        float s = EffShipSpeed;
        foreach (var c in _collectors)
            if (c != null) c.SetSpeed(s);
        foreach (var f in _fighters)
            if (f != null) f.SetSpeed(s);
    }

    void BuildVisual()
    {
        var tex = new Texture2D(36, 24);
        var px  = new Color[36 * 24];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0.30f, 0.45f, 0.60f);
        tex.SetPixels(px); tex.Apply();
        var body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = Vector3.one;
        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0,0,36,24), new Vector2(0.5f,0.5f), 100f);
        sr.sortingOrder = 3;
    }
}
