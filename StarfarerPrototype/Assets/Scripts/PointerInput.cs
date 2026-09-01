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
    /// <summary>İşaretçinin ekran konumu. Hiçbir girdi yoksa false döner.</summary>
    public static bool TryPosition(out Vector2 screen)
    {
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
