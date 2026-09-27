using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Nişan ve ateş girdisinin TEK kaynağı: masaüstünde fare, mobilde dokunma.
///
/// Neden gerekti: <c>Mouse.current</c> telefonda NULL'dır. `WeaponController` ve
/// `WeaponMount` onu kontrolsüz okuyordu, yani Android build'i ilk karede
/// NullReferenceException ile ateş ve nişanı birden kaybediyordu.
/// `CameraController` doğru deseni zaten kullanıyordu; burada ortaklaştırıldı —
/// üç yerde ayrı ayrı yazılsaydı biri yine unutulurdu.
///
/// **Dokunmada nişan ve ateş AYNI girdidir.** Farede konum sürekli, tetik ayrı;
/// dokunmada parmağın olduğu yer hem nişan hem tetiktir. Bu bir kısıtlama değil,
/// mobil nişan almanın doğal hâli: parmağını sürükleyerek nişanlar, kaldırınca
/// ateşi kesersin.
///
/// **UI üstündeki dokunma ateş SAYILMAZ.** BOOST düğmesine basmak aynı zamanda
/// silahı ateşlerdi; masaüstünde fark edilmez çünkü tıklama UI tarafından
/// yutulur, dokunmada ise yutulmaz.
/// </summary>
public static class PointerInput
{
    /// <summary>
    /// Oyun girdisi kilitli mi. Tam ekran bir menü açıkken silah NE DÖNER NE
    /// ATEŞ EDER.
    ///
    /// Tek bir yerde toplandı çünkü kilit iki ayrı davranışı kapsıyor ve ikisi
    /// ayrı dosyalarda: <see cref="WeaponMount"/> nişan alır,
    /// <see cref="WeaponController"/> ateş eder. İkisi de yalnızca
    /// <c>UpgradeUI.IsPaused</c>'a bakıyordu; açılış menüsü eklendiğinde
    /// namlu menünün ARKASINDA farenin peşinde dönmeye devam etti — oyuncu
    /// daha oyuna başlamamışken gemi nişan alıyordu.
    ///
    /// Game Over da dahil: gemi yok edilmişken namlunun dönmesinin anlamı yok.
    /// </summary>
    public static bool Locked
        => UpgradeUI.IsPaused || StartMenuUI.IsOpen || GameManager.IsGameOver;

    /// <summary>
    /// Girdiyi donanım yerine üreten kaynak. Simülasyonun sahte pilotu bunu
    /// doldurur; başka hiçbir yerde set edilmez.
    ///
    /// Neden pilot doğrudan <c>WeaponController</c>'a bağlanmıyor: sahte
    /// oyuncunun ölçtüğü şey oyunun GERÇEK ateş yolu olmalı — nişan açısı,
    /// namlu ucu, şarj süresi, UI yutması. Ayrı bir yol açsaydık simülasyon
    /// kendi yazdığımız kestirmeyi ölçer, oyunu değil.
    ///
    /// <see cref="Locked"/> kaynağın ÜSTÜNDEDİR: kilit çağıranlarda sorulur
    /// (WeaponMount / WeaponController), yani sahte pilot da menü açıkken
    /// ateş edemez. Simülasyonda menü zaten açılmıyor.
    /// </summary>
    public static IPointerSource Source;

    /// <summary>İşaretçinin ekran konumu. Hiçbir girdi yoksa false döner.</summary>
    public static bool TryPosition(out Vector2 screen)
    {
        if (Source != null) return Source.TryPosition(out screen);

        if (TryAimTouch(out var aim))
        {
            screen = aim.position.ReadValue();
            return true;
        }

        var mouse = Mouse.current;
        if (mouse != null)
        {
            screen = mouse.position.ReadValue();
            return true;
        }

        screen = default;
        return false;
    }

    /// <summary>Ateş tetiği basılı mı? (fare sol tuş / ekrana dokunma)</summary>
    public static bool FireHeld
    {
        get
        {
            if (Source != null) return Source.FireHeld;

            if (TryAimTouch(out _)) return true;
            if (AnyTouchPressed()) return false;   // yalnızca UI'daki parmaklar var

            // Fareyle kamera pedini tutmak da sol tuştur; ateş sayılmamalı.
            if (CameraPadHUD.AnyHeld) return false;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }
    }

    /// <summary>Tetik bu karede bırakıldı mı? Plazma şarjını bırakmak için.</summary>
    public static bool FireReleased
    {
        get
        {
            if (Source != null) return Source.FireReleased;

            var touch = Touchscreen.current;
            if (touch != null)
                foreach (var t in touch.touches)
                    if (t.press.wasReleasedThisFrame && t.touchId.ReadValue() == s_aimTouchId)
                        return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasReleasedThisFrame;
        }
    }

    /// <summary>
    /// Nişan parmağı: UI'nın ÜSTÜNDE BAŞLAMAYAN ilk basılı dokunuş.
    ///
    /// Eskiden birincil dokunuş (ilk parmak) okunuyordu. Sol başparmak kamera
    /// pedini tutarken sağ başparmak nişan alınca birincil dokunuş PED oluyordu:
    /// namlu pede dönüyor, ateş ise UI üstünde olduğu için kesiliyordu. İki
    /// parmakla oynamak mümkün değildi.
    ///
    /// Parmak bir kez nişan parmağı seçilince kalkana kadar öyle kalır — sürüklerken
    /// bir düğmenin üstünden geçmesi nişanı kesmemeli.
    /// </summary>
    static int s_aimTouchId = -1;

    static bool TryAimTouch(out UnityEngine.InputSystem.Controls.TouchControl aim)
    {
        aim = null;
        var ts = Touchscreen.current;
        if (ts == null) return false;

        // Önce mevcut nişan parmağı hâlâ basılı mı
        foreach (var t in ts.touches)
            if (t.press.isPressed && t.touchId.ReadValue() == s_aimTouchId) { aim = t; return true; }

        s_aimTouchId = -1;
        foreach (var t in ts.touches)
        {
            if (!t.press.isPressed) continue;
            int id = t.touchId.ReadValue();
            if (OverUI(id)) continue;
            s_aimTouchId = id;
            aim = t;
            return true;
        }
        return false;
    }

    static bool AnyTouchPressed()
    {
        var ts = Touchscreen.current;
        if (ts == null) return false;
        foreach (var t in ts.touches) if (t.press.isPressed) return true;
        return false;
    }

    static bool OverUI(int pointerId)
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
}

/// <summary>
/// Nişan ve ateşin donanım dışı kaynağı. Tek uygulayıcısı simülasyonun
/// pilotudur (<c>SimInput</c>); arayüz olarak durması, sim kodunun oyunun
/// çalışan derlemesine sızmaması içindir — <c>PointerInput</c> kimin
/// bağlandığını bilmez.
/// </summary>
public interface IPointerSource
{
    bool TryPosition(out Vector2 screen);
    bool FireHeld     { get; }
    bool FireReleased { get; }
}
