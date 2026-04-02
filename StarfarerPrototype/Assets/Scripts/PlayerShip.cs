using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Ekranın alt bölgesinde sabit duran ana gemi.
/// MountSlot'ları yönetir ve hasar alır.
/// Görsel: "Body" child objesinde 400x100px texture (4:1 yatay gemi gövdesi).
/// </summary>
public class PlayerShip : MonoBehaviour
{
    public float maxHullHP     = 200f;
    public float currentHullHP;
    public bool  IsAlive       => currentHullHP > 0f;

    public List<MountSlot> mountSlots { get; private set; }

    Vector3  _fixedPosition;
    HealthBar _healthBar;

    void Awake()
    {
        currentHullHP  = maxHullHP;
        _fixedPosition = transform.position;

        if (FindFirstObjectByType<EnergyBus>() == null)
        {
            var busGO = new GameObject("EnergyBus");
            busGO.AddComponent<EnergyBus>();
        }

        // 400x100 px → ppu 100 → dünya boyutu 4 x 1 birim
        Texture2D tex    = new Texture2D(400, 100);
        Color[]   pixels = new Color[400 * 100];
        Color shipColor  = new Color(0.3f, 0.3f, 0.4f);
        for (int i = 0; i < pixels.Length; i++) pixels[i] = shipColor;
        tex.SetPixels(pixels);
        tex.Apply();

        GameObject body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = Vector3.one;

        SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, 400, 100), new Vector2(0.5f, 0.5f), 100f);
        sr.sortingOrder = 0;

        // Trigger collider — EnemyBot çarpışma tespiti için (sprite 4x1 birim)
        BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
        col.size      = new Vector2(4f, 1f);
        col.isTrigger = true;

        if (FindFirstObjectByType<GeneratorComponent>() == null)
        {
            var genGO = new GameObject("Generator_Default");
            genGO.transform.SetParent(transform);
            genGO.transform.localPosition = Vector3.zero;
            var gen = genGO.AddComponent<GeneratorComponent>();
            gen.productionAmount = 15f;
        }

        if (FindFirstObjectByType<ShieldGeneratorComponent>() == null)
        {
            GameObject shieldGO = new GameObject("ShieldGenerator_Default");
            shieldGO.transform.SetParent(transform);
            shieldGO.transform.localPosition = Vector3.zero;
            var shield = shieldGO.AddComponent<ShieldGeneratorComponent>();
            shield.maxShield          = 100f;
            shield.rechargeRate       = 1f;
            shield.rechargeEnergyCost = 3f;
        }

        if (!TryGetComponent<ShipLoadout>(out _))
            gameObject.AddComponent<ShipLoadout>();

        var coreGenGO = new GameObject("CoreGenerator");
        coreGenGO.transform.SetParent(transform);
        coreGenGO.transform.localPosition = Vector3.zero;
        coreGenGO.transform.localScale    = Vector3.one;
        var coreGen = coreGenGO.AddComponent<GeneratorComponent>();
        coreGen.productionAmount = 8f;
    }

    void Start()
    {
        mountSlots = GetComponentsInChildren<MountSlot>().ToList();
        _healthBar = GetComponent<HealthBar>();
    }

    public void TakeDamage(float amount)
    {
        float remaining = ShieldGeneratorComponent.AbsorbDamageAll(amount);

        foreach (var sg in FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None))
            sg.NotifyDamageTaken();

        currentHullHP = Mathf.Max(0f, currentHullHP - remaining);

        if (_healthBar != null)
            _healthBar.currentHealth = currentHullHP;
    }

    public MountSlot GetRandomOperationalSlot()
    {
        var operational = mountSlots
            .Where(s => s.IsOccupied && s.installedComponent.IsOperational)
            .ToList();

        if (operational.Count == 0) return null;
        return operational[Random.Range(0, operational.Count)];
    }

    void LateUpdate()
    {
        // Hiçbir şeyin gemiyi hareket ettirememesi için pozisyonu her frame kilitle
        transform.position = _fixedPosition;
        transform.rotation = Quaternion.identity;
    }
}
