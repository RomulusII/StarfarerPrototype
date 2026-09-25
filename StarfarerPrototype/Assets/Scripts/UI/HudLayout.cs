using UnityEngine;

/// <summary>
/// Üst şeridin ortak ölçüleri — üç ayrı canvas (EnergyBar, BoostHUD, UpgradeUI)
/// aynı sayıları kullanır. Üçü de 1920×1080 referanslı ve match 0.5; yani
/// piksel cinsinden verilen ölçü üçünde de aynı yere düşer.
///
/// YÜKSELT (oyunda) ile KAPAT (upgrade ekranında) AYNI dikdörtgendir: oyuncu
/// ekranı açtığı yerden kapatır. Ayrı ayrı yazılsaydı biri diğerinden sapardı.
///
/// Düğmeler barlardan KALIN ve kenardan İÇERİDE: telefonlarda ekranın en
/// kenarındaki dokunuşlar sık sık kaçıyor.
/// </summary>
public static class HudLayout
{
    /// <summary>Enerji/metal/kristal barlarının yüksekliği. 44 → 53 (+%20): küçük ekranda okunmuyordu.</summary>
    public const float BarHeight = 53f;

    public const float TopButtonWidth  = 180f;
    public const float TopButtonHeight = 68f;

    /// <summary>Düğmenin sağ ve üst kenardan uzaklığı.</summary>
    public const float EdgeInsetX = 18f;
    public const float EdgeInsetY = 8f;

    /// <summary>Barların sağda düğmeye bıraktığı boşluk.</summary>
    public const float BarsRightMargin = TopButtonWidth + EdgeInsetX * 2f;

    /// <summary>Düğmenin alt kenarı, üstten piksel — altındaki paneller buradan başlar.</summary>
    public const float TopButtonBottom = EdgeInsetY + TopButtonHeight;

    /// <summary>YÜKSELT / KAPAT dikdörtgeni.</summary>
    public static void PlaceTopRightButton(RectTransform r)
    {
        r.anchorMin        = Vector2.one;
        r.anchorMax        = Vector2.one;
        r.pivot            = Vector2.one;
        r.sizeDelta        = new Vector2(TopButtonWidth, TopButtonHeight);
        r.anchoredPosition = new Vector2(-EdgeInsetX, -EdgeInsetY);
    }
}
