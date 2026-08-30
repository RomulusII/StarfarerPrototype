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
    [Tooltip("Zırh yükseltmesi olmadan gövde HP'si. maxHullHP bundan TÜRER — " +
             "doğrudan yazılmaz, yoksa zırh bonusu yeniden hesaplanınca silinir.")]
    public float baseMaxHullHP = 200f;

    public float maxHullHP     = 200f;
    public float currentHullHP;
    public bool  IsAlive       => currentHullHP > 0f;

    public List<MountSlot> mountSlots { get; private set; }

    Vector3        _fixedPosition;
    HealthBar      _healthBar;
    SpriteRenderer _bodyRenderer;

    void Awake()
    {
        maxHullHP      = baseMaxHullHP;
        currentHullHP  = maxHullHP;
        _fixedPosition = transform.position;

        if (FindFirstObjectByType<EnergyBus>() == null)
        {
            var busGO = new GameObject("EnergyBus");
            busGO.AddComponent<EnergyBus>();
        }

        // 400x240 px → ppu 100 → dünya boyutu 4 x 2.4 birim
        GameObject body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale    = Vector3.one;

        SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(SkinId.PlayerBody, 400, 240,
                              new Color(0.3f, 0.3f, 0.4f));
        sr.sortingOrder = -10;
        _bodyRenderer   = sr;

        // Trigger collider — gövde hasarı için (sprite 4x2.4 birim).
        // Oyuncu tarafında hitbox siluetten KÜÇÜK olmalı: ana gemi kaçamadığı
        // için kıl payı ıskalar oyuncunun lehine yorumlanır. Oran SkinEntry'de.
        BoxCollider2D col = gameObject.AddComponent<BoxCollider2D>();
        col.size      = new Vector2(4f, 2.4f);
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

        // World-space slot objeleri.
        //
        // Konumlar geminin YAPILARINI takip eder, düzgün bir ızgara değildir.
        // Eskiden 3x3+1 ızgaraydı (y = ±0.8) ve gemi 4x1 birim olduğu için
        // komponentler gövdenin tamamen DIŞINDA, boşlukta duruyordu. Izgarayı
        // koruyup gemiyi büyütmek denendi: slotları kapsamak gövdenin dört
        // köşede de tam yükseklikte olmasını gerektiriyor, yani siluet zorunlu
        // olarak tuğlaya dönüyordu. Bunun yerine slotlar gövdeye taşındı.
        //
        // Her konum Tools/SkinGen/player.js'teki bir yapıya oturur; biri
        // değişirse diğeri de değişmeli. Tuval ↔ dünya: canvas = (800+400x, 480+400y).
        //
        // Başlangıç donanımının slot numaraları ComponentCatalog.StartingLoadout'ta:
        // jeneratör 0, kalkan 3, hangar 6 — yani makine bloğu ve hangar modülü.
        Vector2[] slotPositions = new Vector2[]
        {
            new Vector2(-1.29f,  0.75f), // 0 — Kıç makine bloğu, üst   (jeneratör)
            new Vector2( 0.20f,  0.87f), // 1 — Sırt kulesi, ön         (ANA SİLAH)
            new Vector2( 1.10f,  0.38f), // 2 — Baş, üst
            new Vector2(-1.29f,  0.00f), // 3 — Kıç makine bloğu, orta  (kalkan)
            new Vector2(-0.40f,  0.87f), // 4 — Sırt kulesi, arka
            new Vector2(-0.45f, -0.15f), // 5 — Bel gövdesi, sol
            new Vector2(-0.40f, -0.87f), // 6 — Karın hangar modülü     (hangar)
            new Vector2(-1.29f, -0.75f), // 7 — Kıç makine bloğu, alt
            new Vector2( 0.25f, -0.15f), // 8 — Bel gövdesi, sağ
            new Vector2( 1.10f, -0.45f), // 9 — Baş, alt
        };

        for (int i = 0; i < slotPositions.Length; i++)
        {
            var slotGO = new GameObject($"Slot_{i}");
            slotGO.transform.SetParent(transform, false);
            slotGO.transform.localPosition = slotPositions[i];
            slotGO.transform.localScale    = Vector3.one;

            var visual          = slotGO.AddComponent<SlotVisual>();
            visual.slotIndex    = i;
            visual.isWeaponSlot = (i == ShipLoadout.WeaponSlotIndex);
        }

    }

    void Start()
    {
        mountSlots = GetComponentsInChildren<MountSlot>().ToList();
        _healthBar = GetComponent<HealthBar>();

        // Bar geometrisi GÖVDEDEN türer, sahnedeki sabitten değil. Sahnede
        // barOffsetY = 0.7 yazıyor ve bu 4x1 birimlik eski gövdeye göreydi;
        // gövde 2.4 birime çıkınca bar hull'un İÇİNDE kalırdı. Türetilmiş
        // olması, görsel bir daha değiştiğinde elle güncelleme gerektirmez.
        if (_healthBar != null && _bodyRenderer != null && _bodyRenderer.sprite != null)
        {
            Vector2 size = _bodyRenderer.sprite.bounds.size;
            _healthBar.barWidth   = size.x * 0.55f;
            _healthBar.barOffsetY = size.y * 0.5f + 0.15f;
        }
    }

    /// <summary>
    /// Gövde tavanını değiştirir (onarım biriminin "Zırh" statı çağırır).
    /// Artan tavan mevcut HP'ye de eklenir: oyuncu zırhı satın aldığı anda
    /// faydasını görmeli, onarım biriminin yetişmesini beklememeli. Azalan
    /// tavanda HP kırpılır — zırhlı onarım birimini satmak gerçek bir kayıptır.
    /// </summary>
    public void SetMaxHull(float newMax)
    {
        newMax = Mathf.Max(1f, newMax);
        float delta = newMax - maxHullHP;
        maxHullHP = newMax;
        currentHullHP = delta > 0f
            ? currentHullHP + delta
            : Mathf.Min(currentHullHP, maxHullHP);

        if (_healthBar != null)
        {
            _healthBar.maxHealth     = maxHullHP;
            _healthBar.currentHealth = currentHullHP;
        }
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
