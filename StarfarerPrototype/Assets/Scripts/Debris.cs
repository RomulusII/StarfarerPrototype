using UnityEngine;

/// <summary>
/// Düşman gemisi patladığında geride kalan enkaz.
/// Kendi yönünde yavaş sürüklenur; CollectorShip tarafından toplanır.
/// </summary>
public class Debris : MonoBehaviour
{
    public ResourceType resourceType   = ResourceType.RawMaterial;
    public float        resourceAmount = 10f;

    Vector2 _velocity;

    public bool    IsEmpty   => resourceAmount <= 0f;
    public Vector2 Velocity  => _velocity;

    public void Init(Vector2 velocity, float amount, ResourceType type = ResourceType.RawMaterial)
    {
        _velocity      = velocity;
        resourceAmount = amount;
        resourceType   = type;
    }

    void Awake()
    {
        BuildVisual();
        // Toplanmayan enkaz 3 dakika sonra yok olur — oyuncunun geri kalmasını önler
        Destroy(gameObject, 180f);
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;
        transform.position += (Vector3)(_velocity * Time.deltaTime);
    }

    /// <summary>İstenen miktarı tüketir; gerçekte tüketilen miktarı döner.</summary>
    public float Collect(float amount)
    {
        float actual = Mathf.Min(amount, resourceAmount);
        resourceAmount -= actual;
        if (resourceAmount <= 0f)
            Destroy(gameObject);
        return actual;
    }

    void BuildVisual()
    {
        var tex = new Texture2D(12, 10);
        var px  = new Color[12 * 10];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(0.55f, 0.45f, 0.30f);
        tex.SetPixels(px); tex.Apply();
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0,0,12,10), new Vector2(0.5f,0.5f), 100f);
        sr.sortingOrder = -1;
    }
}
