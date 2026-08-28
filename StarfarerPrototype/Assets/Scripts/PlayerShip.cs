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
        GameObject body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = Vector3.one;

        SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.PlayerBody, 400, 100,
                              new Color(0.3f, 0.3f, 0.4f));
        sr.sortingOrder = -10;

        // Trigger collider — gövde hasarı için (sprite 4x1 birim).
        // Oyuncu tarafında hitbox siluetten KÜÇÜK olmalı: ana gemi kaçamadığı
        // için kıl payı ıskalar oyuncunun lehine yorumlanır. Oran SkinEntry'de.
        BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
        col.size      = new Vector2(4f, 1f);
        col.isTrigger = true;
        SkinLibrary.TryApplyCollider(gameObject, SkinId.PlayerBody, isTrigger: true);

        // Kalkan küresi — gemiyi tamamen saran daire, EnemyBullet tarafından yakalanır
        var shieldGO = new GameObject("ShieldSphere");
        shieldGO.transform.SetParent(transform, false);
        shieldGO.transform.localPosition = Vector3.zero;
        var shieldCircle    = shieldGO.AddComponent<CircleCollider2D>();
        shieldCircle.radius    = ShieldEffect.ShieldRadius;
        shieldCircle.isTrigger = true;
        shieldGO.AddComponent<ShieldSphereCollider>();

        if (!TryGetComponent<ShipLoadout>(out _))
            gameObject.AddComponent<ShipLoadout>();

        // World-space slot objeleri — gemi 4x1 birim koordinat sistemine göre
        Vector2[] slotPositions = new Vector2[]
        {
            new Vector2(-1.5f,  0.8f),  // 0 — Üst Sol
            new Vector2( 0f,    0.8f),  // 1 — Üst Orta
            new Vector2( 1.5f,  0.8f),  // 2 — Üst Sağ
            new Vector2(-1.5f,  0f),    // 3 — Orta Sol
            new Vector2(-0.5f,  0f),    // 4 — Orta OrtaSol
            new Vector2( 0.5f,  0f),    // 5 — Orta OrtaSağ (Weapon)
            new Vector2( 1.5f,  0f),    // 6 — Orta Sağ
            new Vector2(-1.5f, -0.8f),  // 7 — Alt Sol
            new Vector2( 0f,   -0.8f),  // 8 — Alt Orta
            new Vector2( 1.5f, -0.8f),  // 9 — Alt Sağ
        };

        for (int i = 0; i < slotPositions.Length; i++)
        {
            var slotGO = new GameObject($"Slot_{i}");
            slotGO.transform.SetParent(transform, false);
            slotGO.transform.localPosition = slotPositions[i];
            slotGO.transform.localScale    = Vector3.one;

            var visual          = slotGO.AddComponent<SlotVisual>();
            visual.slotIndex    = i;
            visual.isWeaponSlot = (i == 1);
        }

    }

    void Start()
    {
        mountSlots = GetComponentsInChildren<MountSlot>().ToList();
        _healthBar = GetComponent<HealthBar>();
    }

    public void TakeDamage(float amount, bool bypassShields = false)
    {
        float remaining;
        if (bypassShields)
        {
            remaining = amount;
        }
        else
        {
            remaining = ShieldGeneratorComponent.AbsorbDamageAll(amount);
            foreach (var sg in FindObjectsByType<ShieldGeneratorComponent>(FindObjectsSortMode.None))
                sg.NotifyDamageTaken();
        }

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
