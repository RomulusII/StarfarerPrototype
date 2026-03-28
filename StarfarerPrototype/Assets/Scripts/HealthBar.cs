using UnityEngine;

/// <summary>
/// World-space health and shield bars using SpriteRenderers.
/// Always rendered upright (world rotation zero) above the owning ship.
/// Shield bar (top, blue) — Health bar (bottom, green).
/// Fill bars are left-anchored: shrink from right as value decreases.
/// </summary>
public class HealthBar : MonoBehaviour
{
    public float maxShield    = 100f;
    public float currentShield = 100f;
    public float maxHealth    = 100f;
    public float currentHealth = 100f;
    public float barWidth     = 2f;
    public float barOffsetY   = 0.7f; // ship center'ından yukarıya mesafe

    const float BarHeight = 0.12f;
    const float BarGap    = 0.04f;

    Transform _shieldBg;
    Transform _shieldFill;
    Transform _healthBg;
    Transform _healthFill;

    void Awake()
    {
        _healthBg   = MakeBar("HealthBg",   new Color(0.4f,  0.05f, 0.05f), 10);
        _healthFill = MakeBar("HealthFill", new Color(0.1f,  0.9f,  0.2f),  11);
        _shieldBg   = MakeBar("ShieldBg",   new Color(0.05f, 0.1f,  0.3f),  10);
        _shieldFill = MakeBar("ShieldFill", new Color(0.2f,  0.4f,  1.0f),  11);

        // Can barı başlangıçta gizli
        SetHealthBarVisible(false);
        // Kalkan barı: maxShield == 0 ise gizle
        SetShieldBarVisible(maxShield > 0f);
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
    }

    Transform MakeBar(string objName, Color color, int sortOrder)
    {
        Texture2D tex = new Texture2D(1, 1);
        tex.filterMode = FilterMode.Point;
        tex.SetPixel(0, 0, color);
        tex.Apply();

        var go = new GameObject(objName);
        go.transform.SetParent(transform); // PlayerShip'in child'ı olarak ata
        var sr = go.AddComponent<SpriteRenderer>();
        // Pivot sol-orta (0, 0.5) → scale.x değişince sol kenar sabit kalır
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        sr.sortingOrder = sortOrder;
        return go.transform;
    }

    void LateUpdate()
    {
        // HP full ise gizle, hasar almışsa sürekli göster
        SetHealthBarVisible(currentHealth < maxHealth);

        // Kalkan görünürlüğü: maxShield > 0 ise her zaman göster
        SetShieldBarVisible(maxShield > 0f);

        float shieldRatio = maxShield > 0f ? Mathf.Clamp01(currentShield / maxShield) : 0f;
        float healthRatio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        // Lokal koordinatlar: geminin merkezinden offset
        float leftX   = -barWidth * 0.5f;
        float healthY = barOffsetY;
        float shieldY = barOffsetY + BarHeight + BarGap;
        float z       = -0.1f; // parent'ın önüne al

        PlaceBar(_healthBg,   leftX, healthY, z, barWidth,               BarHeight);
        PlaceBar(_healthFill, leftX, healthY, z, barWidth * healthRatio, BarHeight);
        PlaceBar(_shieldBg,   leftX, shieldY, z, barWidth,               BarHeight);
        PlaceBar(_shieldFill, leftX, shieldY, z, barWidth * shieldRatio, BarHeight);
    }

    void PlaceBar(Transform t, float x, float y, float z, float w, float h)
    {
        t.localPosition = new Vector3(x, y, z);
        t.rotation      = Quaternion.identity; // gemi dönse bile barlar dünya uzayında dik kalır
        t.localScale    = new Vector3(w, h, 1f);
    }

    void SetHealthBarVisible(bool visible)
    {
        if (_healthBg)   _healthBg.gameObject.SetActive(visible);
        if (_healthFill) _healthFill.gameObject.SetActive(visible);
    }

    void SetShieldBarVisible(bool visible)
    {
        if (_shieldBg)   _shieldBg.gameObject.SetActive(visible);
        if (_shieldFill) _shieldFill.gameObject.SetActive(visible);
    }

    void OnDestroy()
    {
        if (_shieldBg)   Destroy(_shieldBg.gameObject);
        if (_shieldFill) Destroy(_shieldFill.gameObject);
        if (_healthBg)   Destroy(_healthBg.gameObject);
        if (_healthFill) Destroy(_healthFill.gameObject);
    }
}
