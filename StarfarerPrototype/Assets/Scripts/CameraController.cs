using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse / dokunmatik pozisyonuna göre kamerayı kaydırır ve zoom yapar.
/// Merkeze yakın yavaş, kenara yakın hızlı (power curve).
/// Z pozisyonu -10 sabit, ortographic size 5-7 arası kayar.
///
/// KADRAJ: Ana gemi sabit duruyor; kamerayı ona göre konumlandırırız. Hedef,
/// geminin EKRANDA belirli bir oranda durması: solda ve HUD'dan kalan dikey
/// bandın ortasında. İleri (sağ) tarafta böylece daha çok alan kalır.
///
/// Kayma dünya birimiyle değil ekran oranıyla verilir; Free Aspect'te pencere
/// en-boy oranı ve zoom değiştiğinde kadraj bozulmasın diye. Gerekli dünya
/// kayması her karede kameranın o anki yarı genişlik/yüksekliğinden türetilir.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    [Header("Kadraj")]
    [Tooltip("Gemi ekranın solundan bu oranda dursun. 0.5 = tam orta, 0.29 = solda.")]
    [Range(0.05f, 0.95f)] public float shipScreenX = 0.29f;

    [Tooltip("Gemi ekranın üstünden bu oranda dursun. Üstteki HUD şeridi hesaba " +
             "katılarak kalan bandın ortası: 0.52.")]
    [Range(0.05f, 0.95f)] public float shipScreenY = 0.52f;

    [Tooltip("Mouse ile yatay kaydırma menzili (birim), kadraj tabanının etrafında.")]
    public float panRange = 8f;

    [Header("Upgrade Kadrajı")]
    [Tooltip("Upgrade ekranı açıkken gemi ekranın SOLUNDAN bu oranda dursun.\n\n" +
             "Ekranın kenarları panellerle kaplı: GENEL şeridi solda 0.11'e kadar, " +
             "OPSİYON DETAYI ve bileşen listesi sağda 0.785'ten sonra " +
             "(bkz. UpgradeUI.BuildListPanel / BuildHoverDetailPanel). Geriye kalan " +
             "boş bandın ortası 0.45 — ekranın ortası değil. Gemiyi 0.5'e koymak " +
             "onu sağa, listenin dibine itiyordu.")]
    [Range(0.05f, 0.95f)] public float upgradeShipScreenX = 0.45f;

    [Tooltip("Upgrade ekranı açıkken gemi ekranın ÜSTÜNDEN bu oranda dursun.\n\n" +
             "SLOT BİLGİSİ paneli üstteki %33'ü kaplıyor (UpgradeUI'da y 0.67–0.95). " +
             "Gemi 5 birimlik görüş alanının 2.4 birimini, yani yüksekliğin " +
             "%48'ini kaplıyor: üst kenarı panelin altında kalsın diye merkez " +
             "0.57'nin altına inemez. 0.60 biraz pay bırakır — geminin sırt " +
             "kulesi ve slot halkaları da panelin arkasına girmesin.")]
    [Range(0.05f, 0.95f)] public float upgradeShipScreenY = 0.60f;

    [Tooltip("Upgrade ekranındaki ortographic size.")]
    public float upgradeZoomSize = 2.5f;

    [Header("Zoom")]
    [Tooltip("Dinlenme hâlindeki ortographic size.")]
    public float minZoomSize = 5f;

    [Tooltip("Tam zoom-out'taki ortographic size. ViewBounds bunu okuyup dünyanın " +
             "ne kadar geniş olması gerektiğini hesaplar — doğum noktaları ve toz " +
             "alanı buradan türer, o yüzden sabit değil ALAN olmalı.")]
    public float maxZoomSize = 7f;

    PlayerShip _ship;

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

        Vector2 basePos = FramingBase();

        float moveT = Mathf.Clamp01((t - 0.8f) / 0.2f);
        float curvedMoveT = Mathf.Pow(moveT, 2f);
        Vector3 targetPos = new Vector3(
            basePos.x + direction.x * curvedMoveT * panRange,
            basePos.y,
            -10f);
        transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 3f);

        float zoomT = Mathf.Clamp01((t - 0.9f) / 0.1f);
        float targetSize = Mathf.Lerp(minZoomSize, maxZoomSize, zoomT);
        _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, targetSize, Time.deltaTime * 3f);
    }

    /// <summary>
    /// Geminin istenen ekran oranında görünmesi için kameranın olması gereken yer.
    /// Kayma, o anki ortographic size ve en-boy oranından hesaplanır.
    /// </summary>
    Vector2 FramingBase()
    {
        if (_ship == null) _ship = FindFirstObjectByType<PlayerShip>();
        Vector2 shipPos = _ship != null ? (Vector2)_ship.transform.position : Vector2.zero;

        float halfH = _cam.orthographicSize;
        float halfW = halfH * _cam.aspect;

        // Ekran x'i sola kayarsa kamera sağa gider; ekran y'si aşağı inerse kamera yukarı
        return new Vector2(
            shipPos.x + (0.5f - shipScreenX) * 2f * halfW,
            shipPos.y + (shipScreenY - 0.5f) * 2f * halfH);
    }

    public void ZoomToShip(Vector3 shipPosition, System.Action onComplete)
    {
        _savedPosition  = transform.position;
        _savedSize      = _cam.orthographicSize;

        // Kamerayı gemiye ORTALAMAK gemiyi ekranın ortasına koyar — ama ekranın
        // ortası boş değil. Oyun içi kadrajla aynı formül kullanılır, yalnızca
        // oranlar upgrade panellerine göredir.
        float halfH = upgradeZoomSize;
        float halfW = halfH * _cam.aspect;

        _targetPosition = new Vector3(
            shipPosition.x + (0.5f - upgradeShipScreenX) * 2f * halfW,
            shipPosition.y + (upgradeShipScreenY - 0.5f) * 2f * halfH,
            -10f);
        _targetSize     = upgradeZoomSize;
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
