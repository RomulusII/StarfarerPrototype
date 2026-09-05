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

        var touch = Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.isPressed)
        {
            screen = touch.primaryTouch.position.ReadValue();
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

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
                return !OverUI(touch.primaryTouch.touchId.ReadValue());

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
            if (touch != null && touch.primaryTouch.press.wasReleasedThisFrame)
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasReleasedThisFrame;
        }
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
