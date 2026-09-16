using System.Runtime.InteropServices;

/// <summary>
/// Tarayıcı sayfasının kendi düğmeleri (APK indir, tam ekran) YALNIZCA açılış
/// menüsünde ve upgrade ekranında görünür.
///
/// Neden: oyun sırasında sağ alt köşe dövüş alanıdır. Düğmeler oradaki
/// düşmanların üstünü örtüyordu; telefonda nişan alan parmak yanlışlıkla tam
/// ekrana ya da APK indirmeye basıyordu. İkisi de oyun durmuşken yapılacak
/// işler.
///
/// Düğmeler HTML katmanında durur (bkz. index.html — orada durmalarının
/// gerekçesi yazılı), yani Unity onları çizmez; yalnızca hangi ekranın açık
/// olduğunu sayfaya bildirir. O bilgiyi yalnızca oyun bilir.
///
/// Durum TÜRETİLİR, çağıranlar "göster/gizle" demez: menü ve upgrade ekranı
/// kendi bayraklarını (<see cref="StartMenuUI.IsOpen"/>,
/// <see cref="UpgradeUI.IsPaused"/>) değiştirdikten sonra yalnızca
/// <see cref="Refresh"/> çağırır. İki ekranın ayrı ayrı "gizle" demesi, biri
/// kapanırken diğeri açıkken düğmeleri yanlışlıkla gizlerdi.
///
/// Editörde ve WebGL dışındaki platformlarda hiçbir şey yapmaz.
/// </summary>
public static class WebChrome
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void Starfarer_SetWebChrome(int visible);
#endif

    // Son bildirilen durum. Statik kalması bilinçli: sahne yeniden yüklenince
    // (menüye dönüş, restart) sayfa da aynı durumda kalıyor.
    static int _last = -1;

    public static void Refresh()
    {
        int visible = StartMenuUI.IsOpen || UpgradeUI.IsPaused ? 1 : 0;
        if (visible == _last) return;
        _last = visible;

#if UNITY_WEBGL && !UNITY_EDITOR
        Starfarer_SetWebChrome(visible);
#endif
    }
}
