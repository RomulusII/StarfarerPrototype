using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse / dokunmatik pozisyonuna göre kamerayı kaydırır ve zoom yapar.
/// Merkeze yakın yavaş, kenara yakın hızlı (power curve).
/// Z pozisyonu -10 sabit, ortographic size 5-7 arası kayar.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    private Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        Vector2 inputPos = ReadInputPosition();
        if (inputPos == Vector2.zero) return;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        // Ekran merkezine göre -1..+1 arası delta
        Vector2 delta = (inputPos - screenCenter) / screenCenter;

        // 0..1 arası uzaklık (köşe = ~1.41, clamp ile sınırla)
        float t = Mathf.Clamp01(delta.magnitude);

        Vector2 direction = delta.normalized;

        // Kayma: t > 0.8 olduğunda başlar, power curve korunur
        float moveT = Mathf.Clamp01((t - 0.8f) / 0.2f); // 0.8-1.0 → 0-1
        float curvedMoveT = Mathf.Pow(moveT, 2f);
        // Y ekseni hareketi kapalı — gemi sabit alt bölgede görünür kalsın
        Vector3 targetPos = new Vector3(direction.x * curvedMoveT * 8f, 0f, -10f);

        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 3f);

        // Zoom: t > 0.9 olduğunda başlar, t <= 0.9 iken ortho size sabit 5
        float zoomT = Mathf.Clamp01((t - 0.9f) / 0.1f); // 0.9-1.0 → 0-1
        float targetSize = Mathf.Lerp(5f, 7f, zoomT);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * 3f);
    }

    /// <summary>Touch varsa birincil dokunuş, yoksa mouse. İkisi de yoksa Vector2.zero.</summary>
    static Vector2 ReadInputPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();

        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();

        return Vector2.zero;
    }
}
