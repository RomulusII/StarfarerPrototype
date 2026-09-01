using UnityEngine;

/// <summary>
/// Küresel kalkanın ÇARPIŞMA yüzeyi. <see cref="BarrierShield"/> ile aynı
/// desen: gövdeden ayrı bir collider, sahibi <c>owner</c>.
///
/// Neden gerekti: kabuk uzun süre yalnızca bir SPRITE'tı. Hasar gemi gövde
/// collider'ından geçiyordu, yani kalkan yüzeyini kesip gövdeyi ıskalayan bir
/// mermi hiçbir şeye çarpmadan öbür taraftan çıkıyordu. Oyuncunun gördüğü şey
/// ile oyunun bildiği şey farklıydı: ekranda bir kabuk var ama mermiler
/// içinden geçiyor.
///
/// Yarıçap sprite'ın kendisinden ÖLÇÜLÜR, ayrı bir sayı olarak yazılmaz —
/// bağımsız iki değer zamanla birbirinden sapar ve tam bu hata geri gelirdi.
/// (Aynı gerekçeyle hitbox da sprite siluetinden türüyor; bkz. SkinLibrary.)
///
/// Kalkan boşalınca görsel nesne komple kapatılır (<c>SetActive(false)</c>),
/// dolayısıyla collider da kapanır ve mermiler gövdeye ulaşır. Ayrı bir
/// "collider'ı kapat" yolu YOK: tek anahtar, iki durumun ayrışması imkânsız.
/// </summary>
public class BubbleShield : MonoBehaviour
{
    public EnemyBot owner;

    /// <summary>
    /// Kabuk görseline çarpışma yüzeyi ekler. Trigger'dır ve kendi
    /// Rigidbody2D'si yoktur — düşmanın gövdesindeki kinematik body'ye
    /// bağlanır, yani fizik açısından geminin bir parçasıdır.
    /// </summary>
    public static BubbleShield Attach(EnemyBot bot, GameObject visual, Sprite sprite)
    {
        if (bot == null || visual == null) return null;

        var col = visual.AddComponent<CircleCollider2D>();
        // Ölçek zaten dünya yarıçapına eşit (EnemyBot.BuildShieldVisual), bu
        // yüzden yerel yarıçap sprite'ın yerel yarıçapıdır.
        col.radius    = sprite != null ? sprite.bounds.extents.x : 1f;
        col.isTrigger = true;

        var s = visual.AddComponent<BubbleShield>();
        s.owner = bot;
        return s;
    }

    // ── Kabuk görseli ─────────────────────────────────────────────────────────

    /// <summary>
    /// Küresel kalkanın kabuğu: merkeze doğru şeffaf, kenarda parlak. Arkasındaki
    /// gemi görünmeli — kalkan bir duvar değil bir YÜZEY.
    ///
    /// Neredeyse boş bir iç, kenarda dar ve keskin bir halka. Eskiden iç dolgu
    /// 0.18 ve halka rim² ile yayvandı: sonuç bir yüzey değil DOLU BİR DİSK
    /// oluyor, arkasındaki gemiyi ve yıldızları boyuyordu. Ekranda kapladığı yer
    /// değil, KENARI okunmalı.
    ///
    /// <paramref name="aspect"/> kabuğu ELİPSE çevirir (dikey yarıçap / yatay
    /// yarıçap). Uzun gövdeli gemilerde daire ya burnu ve kıçı açıkta bırakır ya
    /// da gövdenin kat kat üstüne taşar; boss gövdesi 2:1'dir (bkz. BossShip).
    /// Şekil sprite'ın KENDİSİNDEN gelir, transform'u yamultmaktan değil —
    /// projenin uniform-ölçek kuralı (bkz. CLAUDE.md) ve halkanın düzgün
    /// kalması bunu gerektirir.
    ///
    /// Sprite'ın dış kenarı yatayda 1 birimdir (ppu = yatay yarıçap), yani
    /// <c>localScale</c> doğrudan yatay dünya yarıçapıdır.
    ///
    /// Tek kaynak: hem <see cref="EnemyBot"/> hem <see cref="BossShip"/> bunu
    /// kullanır. Ayrı ayrı üretilseydi iki kalkan zamanla birbirinden sapardı.
    /// </summary>
    public static Sprite Shell(float aspect = 1f)
    {
        aspect = Mathf.Clamp(aspect, 0.15f, 1f);

        int key = Mathf.RoundToInt(aspect * 100f);
        if (_shells.TryGetValue(key, out var cached) && cached != null) return cached;

        const int   Sz   = 128;
        const float OutX = 62f;            // ppu olarak da kullanılır → 1 birim
        const float InR  = 0.70f;          // halka nerede başlar (normalize)
        const float C    = Sz * 0.5f;

        float OutY = OutX * aspect;

        var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false)
                  { filterMode = FilterMode.Bilinear };
        var px = new Color[Sz * Sz];

        for (int i = 0; i < px.Length; i++)
        {
            float dx = ((i % Sz) + 0.5f - C) / OutX;
            float dy = ((i / Sz) + 0.5f - C) / OutY;
            float d  = Mathf.Sqrt(dx * dx + dy * dy);   // 1.0 = kabuk yüzeyi

            if (d > 1f) { px[i] = Color.clear; continue; }

            float rim  = Mathf.Clamp01((d - InR) / (1f - InR));
            float edge = Mathf.Clamp01((1f - d) * OutY / 2.5f); // dış kenar yumuşatma
            rim = rim * rim; rim = rim * rim;                   // rim^4 — dar halka
            px[i] = new Color(1f, 1f, 1f, (0.05f + 0.95f * rim) * edge);
        }

        tex.SetPixels(px);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, Sz, Sz), Vector2.one * 0.5f, OutX);
        _shells[key] = sprite;
        return sprite;
    }

    static readonly System.Collections.Generic.Dictionary<int, Sprite> _shells = new();
}
