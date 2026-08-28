using System;
using UnityEngine;

/// <summary>
/// Boss gemisindeki yıkılabilir bileşen.
/// HP sıfırlanınca BossShip'e bildirim gönderir ve görsel olarak devre dışı kalır.
/// </summary>
public class BossHardpoint : MonoBehaviour
{
    public BossHardpointDef def;
    public Action<BossHardpoint> OnDestroyed;

    float          _hp;
    float          _maxHp;
    SpriteRenderer _sr;
    HealthBar      _healthBar;
    bool           _dead;

    public bool IsAlive => !_dead;
    public HardpointType Type => def.type;

    public void Init(BossHardpointDef definition)
    {
        def   = definition;
        _hp   = definition.hp;
        _maxHp = definition.hp;

        // Collider
        var col    = gameObject.AddComponent<BoxCollider2D>();
        col.size   = new Vector2(definition.width / 100f, definition.height / 100f);
        col.isTrigger = false;
        // Skin varsa hitbox sprite siluetinden türer; yoksa yukarıdaki kutu kalır
        SkinLibrary.TryApplyCollider(gameObject, SkinId.ForHardpoint(definition.type));

        // Görsel
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite       = SkinLibrary.Get(SkinId.ForHardpoint(definition.type),
                               SkinId.BossHardpoint,
                               definition.width, definition.height, definition.color);
        _sr.sortingOrder = 2;

        // HP barı
        _healthBar              = gameObject.AddComponent<HealthBar>();
        _healthBar.maxHealth    = _maxHp;
        _healthBar.currentHealth = _hp;
        _healthBar.barWidth     = definition.width / 100f * 1.2f;
        _healthBar.barOffsetY   = definition.height / 100f * 0.7f;
    }

    public void TakeDamage(float amount)
    {
        if (_dead) return;

        _hp = Mathf.Max(0f, _hp - amount);
        if (_healthBar != null) _healthBar.TakeDamage(amount);

        if (_hp <= 0f)
            Die();
    }

    void Die()
    {
        _dead = true;

        // Yıkılan görünüm: rengi karart
        if (_sr != null)
            _sr.color = new Color(0.15f, 0.12f, 0.10f, 0.85f);

        // HP barı gizle
        if (_healthBar != null)
            _healthBar.gameObject.SetActive(false);

        // Collider devre dışı — artık vurulmasın
        var col = GetComponent<BoxCollider2D>();
        if (col != null) col.enabled = false;

        // Duman efekti (basit renk değişimi — sprite'lar eklenince güzelleşir)
        SpawnDebris();

        OnDestroyed?.Invoke(this);
    }

    void SpawnDebris()
    {
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("Debris");
            go.transform.position = transform.position;
            var d = go.AddComponent<Debris>();
            d.Init(UnityEngine.Random.insideUnitCircle.normalized
                   * UnityEngine.Random.Range(0.2f, 0.8f),
                   UnityEngine.Random.Range(3f, 8f));
        }
    }
}
