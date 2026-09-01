using UnityEngine;

public enum BoostMode { None, Shield, Weapon }

/// <summary>
/// Aktif boost modunu tutan statik sınıf.
/// ShieldGeneratorComponent ve WeaponController buradan okur.
/// </summary>
public static class BoostController
{
    public static BoostMode Mode { get; private set; } = BoostMode.None;

    /// <summary>
    /// Aynı mod tekrar seçilirse None'a döner (toggle). Farklı mod seçilirse diğeri iptal olur.
    /// </summary>
    public static void Toggle(BoostMode mode)
    {
        var prev = Mode;
        Mode = (Mode == mode) ? BoostMode.None : mode;
        if (prev == Mode) return;

        // Denge ölçümü: boost, hasarı ×2 ile ×1/3 arasında oynatıyor — yani
        // isabet/TTK ölçümlerini sessizce ikiye böler ya da katlar. Mod
        // geçişleri kaydedilmezse "aynı silahla aynı düşman" diye topladığımız
        // örnekler aslında üç ayrı silahın karışımı olur.
        //
        // Süre de gerekli: modun ne kadar AÇIK KALDIĞI, oyuncunun boost'u bir
        // araç olarak mı kullandığını yoksa açıp unuttuğunu mu gösterir.
        float now = Time.time;
        BalanceLog.Event("boost")
                  .Str("mod",   Mode.ToString())
                  .Str("onceki", prev.ToString())
                  .Num("sure",  _changedAt >= 0f ? now - _changedAt : 0f)
                  .End();
        _changedAt = now;
    }

    static float _changedAt = -1f;
}
