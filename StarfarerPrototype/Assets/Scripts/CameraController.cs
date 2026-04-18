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
    private Camera _cam;

    private bool   _isUpgradeMode;
    private bool   _isRestoring;
    private Vector3 _savedPosition;
    private float  _savedSize;
    private Vector3 _targetPosition;
    private float  _targetSize;
    private System.Action _onZoomComplete;

    private const float ZoomSpeed = 5f;

    void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (_isUpgradeMode)
        {
            HandleUpgradeZoom();
            return;
        }

        Vector2 inputPos = ReadInputPosition();
        if (inputPos == Vector2.zero) return;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector2 delta = (inputPos - screenCenter) / screenCenter;
        float t = Mathf.Clamp01(delta.magnitude);
        Vector2 direction = delta.normalized;

        float moveT = Mathf.Clamp01((t - 0.8f) / 0.2f);
        float curvedMoveT = Mathf.Pow(moveT, 2f);
        Vector3 targetPos = new Vector3(direction.x * curvedMoveT * 8f, 0f, -10f);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 3f);

        float zoomT = Mathf.Clamp01((t - 0.9f) / 0.1f);
        float targetSize = Mathf.Lerp(5f, 7f, zoomT);
        _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, targetSize, Time.deltaTime * 3f);
    }

    public void ZoomToShip(Vector3 shipPosition, System.Action onComplete)
    {
        _savedPosition  = transform.position;
        _savedSize      = _cam.orthographicSize;

        _targetPosition = new Vector3(shipPosition.x, shipPosition.y, -10f);
        _targetSize     = 2.5f;
        _onZoomComplete = onComplete;

        _isRestoring   = false;
        _isUpgradeMode = true;
    }

    public void RestoreFromUpgrade()
    {
        _targetPosition = _savedPosition;
        _targetSize     = _savedSize;
        _isRestoring    = true;
    }

    private void HandleUpgradeZoom()
    {
        float step = ZoomSpeed * Time.unscaledDeltaTime;

        transform.position    = Vector3.Lerp(transform.position, _targetPosition, step);
        _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetSize, step);

        bool arrived = Vector3.Distance(transform.position, _targetPosition) < 0.01f
                    && Mathf.Abs(_cam.orthographicSize - _targetSize) < 0.01f;

        if (!arrived) return;

        transform.position    = _targetPosition;
        _cam.orthographicSize = _targetSize;

        if (_isRestoring)
        {
            _isUpgradeMode = false;
            _isRestoring   = false;
        }
        else
        {
            _onZoomComplete?.Invoke();
            _onZoomComplete = null;
        }
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
