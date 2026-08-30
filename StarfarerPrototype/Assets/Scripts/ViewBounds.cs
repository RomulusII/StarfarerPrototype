using UnityEngine;

/// <summary>
/// Kameranın EN GENİŞ hâlinde görebildiği dünya dikdörtgeni — ve ondan türeyen
/// doğum / silinme sınırları.
///
/// Neden var: "sahne kenarı" sayısı yedi ayrı dosyaya elle serpiştirilmişti
/// (spawn x = 12, 14, 15; despawn x = -15, -17; despawn yarıçapı = 30; yıldız
/// alanı 36×14). Hiçbiri kamerayla bağlantılı değildi, dolayısıyla hiçbiri
/// doğru değildi: zoom-out + pan ile kadraj x ekseninde +32'ye kadar açılıyor,
/// yani düşmanlar ekranın ORTASINDA yoktan var oluyor ve toz alanı kadrajın
/// yalnızca yarısını kaplıyordu.
///
/// Sayılar artık kameranın kendi alanlarından türer (<see cref="CameraController"/>
/// maxZoomSize / shipScreenX / shipScreenY / panRange). Kadraj ayarı
/// değiştiğinde doğum sınırları da kendiliğinden takip eder.
/// </summary>
public static class ViewBounds
{
    /// <summary>
    /// Hesabın varsaydığı EN GENİŞ en-boy oranı. Gerçek oran bundan darsa
    /// yine bu kullanılır: pencere sonradan genişleyebilir ve dünya o an
    /// yetersiz kalırsa düşmanlar görünür alanda doğmaya başlar. Geniş
    /// tarafta hata yapmak ucuz, dar tarafta pahalı.
    /// </summary>
    public const float DesignAspect = 2.4f;

    /// <summary>Doğum noktası görünür kenarın bu kadar dışındadır.</summary>
    public const float SpawnMargin = 3f;

    /// <summary>Silinme sınırı görünür kenarın bu kadar dışındadır.</summary>
    public const float DespawnMargin = 5f;

    // Kamera ve gemi bulunamazsa (test sahnesi, erken Awake) kullanılan kadraj.
    // Bugünkü ayarlarla hesaplanmış değerlerdir; kod her durumda çalışsın diye.
    static readonly Rect FallbackRect = new Rect(-18f, -9f, 50f, 15f);

    static Rect  _rect;
    static float _computedAspect = -1f;

    /// <summary>Kameranın erişebildiği tüm dünya alanı (zoom-out + pan dahil).</summary>
    public static Rect Visible
    {
        get { Refresh(); return _rect; }
    }

    /// <summary>Düşman ve asteroitlerin doğduğu x — görünür alanın sağ dışı.</summary>
    public static float SpawnX => Visible.xMax + SpawnMargin;

    /// <summary>Soldan doğuş x'i.</summary>
    public static float SpawnXLeft => Visible.xMin - SpawnMargin;

    /// <summary>Üstten / alttan doğuş y'si.</summary>
    public static float SpawnYTop    => Visible.yMax + SpawnMargin;
    public static float SpawnYBottom => Visible.yMin - SpawnMargin;

    /// <summary>Bunun solunda kalan hiçbir şey görünmez; temizlenebilir.</summary>
    public static float DespawnX => Visible.xMin - DespawnMargin;

    /// <summary>
    /// Dünya merkezinden bu uzaklığın ötesindeki nesne kesinlikle görünmez.
    /// Köşeden ölçülür — sabit 30 birim, +32'ye kadar açılan kadrajda doğan
    /// düşmanı DOĞDUĞU KARE yok ediyordu.
    /// </summary>
    public static float DespawnRadius
    {
        get
        {
            var r = Visible;
            float far = Mathf.Max(
                new Vector2(r.xMin, r.yMin).magnitude, new Vector2(r.xMax, r.yMin).magnitude,
                Mathf.Max(new Vector2(r.xMin, r.yMax).magnitude, new Vector2(r.xMax, r.yMax).magnitude));
            return far + DespawnMargin;
        }
    }

    // ── Hesap ─────────────────────────────────────────────────────────────────

    static void Refresh()
    {
        var cam = Camera.main;
        float aspect = cam != null ? Mathf.Max(cam.aspect, DesignAspect) : DesignAspect;

        // Free Aspect'te pencere yeniden boyutlanabilir; oran kayda değer
        // ölçüde değiştiyse yeniden hesapla.
        if (_computedAspect > 0f && Mathf.Abs(aspect - _computedAspect) < 0.01f) return;

        var ctl = cam != null ? cam.GetComponent<CameraController>() : null;
        if (ctl == null) { _rect = FallbackRect; _computedAspect = aspect; return; }

        var ship = Object.FindFirstObjectByType<PlayerShip>();
        Vector2 shipPos = ship != null ? (Vector2)ship.transform.position : new Vector2(0f, -2f);

        float halfH = ctl.maxZoomSize;
        float halfW = halfH * aspect;

        // Kadraj tabanı: CameraController.FramingBase ile AYNI formül.
        float baseX = shipPos.x + (0.5f - ctl.shipScreenX) * 2f * halfW;
        float baseY = shipPos.y + (ctl.shipScreenY - 0.5f) * 2f * halfH;

        // Pan yalnızca yatayda; dikeyde kadraj sabit.
        float minX = baseX - ctl.panRange - halfW;
        float maxX = baseX + ctl.panRange + halfW;

        _rect = Rect.MinMaxRect(minX, baseY - halfH, maxX, baseY + halfH);
        _computedAspect = aspect;
    }

    /// <summary>Sahne yeniden yüklenince önbelleği düşürür.</summary>
    public static void Invalidate() => _computedAspect = -1f;
}
