using UnityEngine;

/// <summary>
/// Geminin ön tarafındaki silah noktası.
/// Sprite sarı olarak built-in kaynaktan atanır;
/// her karede mouse dünya pozisyonuna doğru döner.
/// </summary>
public class WeaponMount : MonoBehaviour
{
    void Awake()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        sr.sprite = Resources.GetBuiltinResource<Sprite>("Sprites/Default");
        sr.color = new Color(1f, 0.92f, 0f); // sarı
    }

    void Update()
    {
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = (Vector2)(mouseWorld - transform.position);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
