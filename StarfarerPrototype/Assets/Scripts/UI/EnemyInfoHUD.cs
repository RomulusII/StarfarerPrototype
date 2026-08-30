using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Fareyi bir düşmanın üzerine getirince sol üstte açılan bilgi kutusu.
///
/// Neden var: düşman tipleri artık birbirinden davranışla ayrılıyor (zırh,
/// direnç, faz, aura, karıştırma) ama oyuncu ekranda yalnızca bir siluet ve
/// iki bar görüyordu. "Bu neden ölmüyor" sorusunun cevabı — zırh mı, direnç mi,
/// aura mı — hiçbir yerde yazmıyordu.
///
/// Değerler ÖLÇEKLENMİŞ kopyadan okunur (EnemySpawner.ApplyScaling runtime
/// kopyası üretir), yani asset'teki taban değil bu levelde gerçekten geçerli
/// olan sayı gösterilir.
///
/// Enerji barı yok: düşmanların enerji havuzu yok. O satırın yerini ZIRH aldı —
/// bu levelde vurmayı belirleyen sayı odur.
/// </summary>
public class EnemyInfoHUD : MonoBehaviour
{
    // Fare imlecinin altındaki daire yarıçapı (dünya birimi). Küçük düşmanların
    // hitbox'ı kasten dardır; imleci piksele oturtmak zorunda kalmamak için
    // tarama biraz cömert.
    const float PickRadius = 0.45f;

    const float PanelW = 340f;
    const float BarH   = 18f;

    Canvas        _canvas;
    GameObject    _panel;
    Text          _title;
    Text          _subtitle;
    Text          _body;
    RectTransform _hpFill, _shieldFill, _armorFill;
    Text          _hpText, _shieldText, _armorText;
    GameObject    _shieldRow;

    void Awake()
    {
        _canvas              = gameObject.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 12;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        BuildPanel();
        _canvas.enabled = false;
    }

    void Update()
    {
        if (GameManager.IsGameOver || UpgradeUI.IsPaused || StartMenuUI.IsOpen)
        {
            _canvas.enabled = false;
            return;
        }

        var target = PickUnderCursor();
        if (target == null)
        {
            _canvas.enabled = false;
            return;
        }

        _canvas.enabled = true;
        Show(target);
    }

    // =========================================================================
    // Hedef seçimi
    // =========================================================================

    /// <summary>
    /// İmlecin altındaki düşman. Boss gövdesi ve hardpoint'leri de sayılır —
    /// hardpoint'e denk gelen imleç boss'un kartını açar, çünkü oyuncunun
    /// merak ettiği geminin bütünüdür.
    /// </summary>
    Component PickUnderCursor()
    {
        var cam = Camera.main;
        if (cam == null || Mouse.current == null) return null;

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world  = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));

        var hits = Physics2D.OverlapCircleAll(world, PickRadius);
        if (hits == null || hits.Length == 0) return null;

        EnemyBot bestBot   = null;
        BossShip bestBoss  = null;
        float    bestBotD  = float.MaxValue;

        foreach (var c in hits)
        {
            if (c == null) continue;

            var boss = c.GetComponent<BossShip>() ?? c.GetComponentInParent<BossShip>();
            if (boss != null) { bestBoss = boss; continue; }

            // IsValidTarget kullanılmaz: faz hâlindeki düşman turretler için
            // geçersiz hedeftir ama ekranda duruyor ve oyuncunun sorması gereken
            // asıl gemi tam da odur.
            var bot = c.GetComponent<EnemyBot>();
            if (bot == null || !bot.isActiveAndEnabled) continue;

            float d = Vector2.Distance(world, bot.transform.position);
            if (d < bestBotD) { bestBotD = d; bestBot = bot; }
        }

        // Düşman gemisi boss'a tercih edilir: boss dev bir gövdedir ve önünden
        // geçen küçük gemiyi sorgulamak isteyen oyuncu boss'un kartını alırdı.
        if (bestBot != null) return bestBot;
        return bestBoss;
    }

    // =========================================================================
    // Doldurma
    // =========================================================================

    void Show(Component target)
    {
        if (target is EnemyBot bot)  { ShowEnemy(bot);  return; }
        if (target is BossShip boss) { ShowBoss(boss);  return; }
    }

    void ShowEnemy(EnemyBot bot)
    {
        var d = bot.Data;
        if (d == null) return;

        _title.text    = string.IsNullOrEmpty(d.displayName) ? d.name : d.displayName;
        _subtitle.text = $"{RoleLabel(d.role)}  ·  Tehdit {d.threatScore}";

        SetBar(_hpFill, _hpText, bot.CurrentHP, bot.MaxHP,
               new Color(0.25f, 0.82f, 0.30f));

        bool hasShield = bot.MaxShield > 0f;
        _shieldRow.SetActive(hasShield);
        if (hasShield)
            SetBar(_shieldFill, _shieldText, bot.CurrentShield, bot.MaxShield,
                   new Color(0.30f, 0.55f, 1f));

        // Zırh mutlak bir sayıdır; bar dolgusu kampanyanın zırh tavanına göre
        // ölçeklenir, böylece "bu gemi ne kadar zırhlı" göz kararı okunabilir.
        float armorMax = Mathf.Max(1f, LevelCurve.Instance.maxArmor);
        SetBar(_armorFill, _armorText, bot.ArmorValue, armorMax,
               new Color(0.85f, 0.72f, 0.25f), showMax: false);

        _body.text = BuildEnemyBody(d, bot);
    }

    void ShowBoss(BossShip boss)
    {
        var d = boss.Data;
        _title.text    = d != null && !string.IsNullOrEmpty(d.displayName) ? d.displayName : "Boss";
        _subtitle.text = $"Bölüm Patronu  ·  Tehdit {(d != null ? d.threatScore : 0)}";

        SetBar(_hpFill, _hpText, boss.CurrentHP, boss.MaxHP,
               new Color(0.25f, 0.82f, 0.30f));

        bool hasShield = boss.MaxShield > 0f;
        _shieldRow.SetActive(hasShield);
        if (hasShield)
            SetBar(_shieldFill, _shieldText, boss.CurrentShield, boss.MaxShield,
                   new Color(0.30f, 0.55f, 1f));

        float armorMax = Mathf.Max(1f, LevelCurve.Instance.maxArmor);
        SetBar(_armorFill, _armorText, boss.ArmorValue, armorMax,
               new Color(0.85f, 0.72f, 0.25f), showMax: false);

        var sb = new StringBuilder();
        if (d != null)
        {
            sb.AppendLine($"Hardpoint: {(d.hardpoints != null ? d.hardpoints.Length : 0)}");
        }
        _body.text = sb.ToString().TrimEnd();
    }

    static string BuildEnemyBody(EnemyTypeData d, EnemyBot bot)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"Silah: {WeaponLabel(d.weaponKind)}   {d.fireDamage:0.#} hasar / {d.fireRate:0.#}s");
        sb.AppendLine($"Menzil: {d.fireRange:0.#}   ·   Kaçamak: {d.evasionAngle:0.#}°");
        sb.AppendLine($"Hareket: {MovementLabel(d.movementKind)}");

        // Oyuncunun silah seçimini doğrudan etkileyen tek bilgi budur
        AppendResistances(sb, "Gövde", d.hullResistances);
        if (d.maxShield > 0f) AppendResistances(sb, "Kalkan", d.shieldResistances);

        // Savaşçılara karşı tutum — oyuncunun hangar yatırımını okuyabilmesi için
        string vsFighters = d.PursuesFighters
            ? "Savaşçıları kovalar"
            : (d.CanEngageFighters ? "Savaşçıya ateş eder, kovalamaz"
                                   : "Savaşçıları tamamen yok sayar");
        sb.AppendLine(vsFighters);

        if (d.energyDrain   > 0f) sb.AppendLine($"⚠ Enerji karıştırıcı: üretimi %{d.energyDrain * 100f:0} kısar ({d.energyDrainRange:0.#} menzil)");
        if (d.phaseInterval > 0f) sb.AppendLine($"⚠ Faz: {d.phaseInterval:0.#}s'de bir {d.phaseDuration:0.#}s vurulamaz");
        if (d.repairAura    > 0f) sb.AppendLine($"⚠ Onarım aurası: {d.repairAura:0.#} HP/s ({d.repairAuraRange:0.#} menzil)");
        if (d.splitInto  != null) sb.AppendLine($"⚠ Ölünce ikiye bölünür (%{d.splitHpRatio * 100f:0} HP)");
        if (bot.IsPhased)         sb.AppendLine("⚠ ŞU AN VURULAMAZ");

        return sb.ToString().TrimEnd();
    }

    static void AppendResistances(StringBuilder sb, string layer, DamageModifier[] mods)
    {
        if (mods == null || mods.Length == 0) return;
        sb.Append(layer).Append(": ");
        for (int i = 0; i < mods.Length; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append($"{WeaponTypeLabel(mods[i].weaponType)} ×{mods[i].multiplier:0.##}");
        }
        sb.AppendLine();
    }

    // =========================================================================
    // Etiketler
    // =========================================================================

    static string RoleLabel(EnemyRole r) => r switch
    {
        EnemyRole.Vanguard  => "Öncü",
        EnemyRole.Flank     => "Kanat",
        EnemyRole.Center    => "Merkez",
        EnemyRole.Rear      => "Arka",
        EnemyRole.Support   => "Destek",
        EnemyRole.Barrier   => "Bariyer",
        EnemyRole.Artillery => "Topçu",
        _                   => r.ToString(),
    };

    static string WeaponLabel(EnemyWeaponKind w) => w switch
    {
        EnemyWeaponKind.None           => "Silahsız",
        EnemyWeaponKind.Laser          => "Lazer",
        EnemyWeaponKind.Kinetic        => "Kinetik",
        EnemyWeaponKind.Cannon         => "Ağır top",
        EnemyWeaponKind.Rocket         => "Roket",
        EnemyWeaponKind.ComponentBurst => "Komponent burst",
        _                              => w.ToString(),
    };

    static string MovementLabel(EnemyMovementKind m) => m switch
    {
        EnemyMovementKind.Charge     => "Yörünge",
        EnemyMovementKind.HoverFire  => "Uzaktan döver",
        EnemyMovementKind.Approach   => "Yaklaşır, burst atar",
        EnemyMovementKind.Strafe     => "Dalar geçer",
        EnemyMovementKind.Stationary => "Sabit",
        EnemyMovementKind.BombRun    => "Bomba koşusu",
        EnemyMovementKind.AttackRun  => "Saldırı sortisi",
        _                            => m.ToString(),
    };

    static string WeaponTypeLabel(WeaponType w) => w switch
    {
        WeaponType.Kinetic => "Kinetik",
        WeaponType.Laser   => "Lazer",
        WeaponType.Plasma  => "Plazma",
        _                  => w.ToString(),
    };

    // =========================================================================
    // Panel kurulumu
    // =========================================================================

    void BuildPanel()
    {
        _panel = new GameObject("EnemyInfoPanel", typeof(RectTransform));
        _panel.transform.SetParent(transform, false);
        _panel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.10f, 0.92f);

        var r = (RectTransform)_panel.transform;
        r.anchorMin        = new Vector2(0f, 1f);
        r.anchorMax        = new Vector2(0f, 1f);
        r.pivot            = new Vector2(0f, 1f);
        // EnergyBar HUD şeridi üstte 44 px kaplıyor; kutu onun altından başlar
        r.anchoredPosition = new Vector2(12f, -56f);
        r.sizeDelta        = new Vector2(PanelW, 0f);

        var vl = _panel.AddComponent<VerticalLayoutGroup>();
        vl.padding                = new RectOffset(12, 12, 10, 10);
        vl.spacing                = 4f;
        vl.childForceExpandWidth  = true;
        vl.childForceExpandHeight = false;
        vl.childControlHeight     = true;

        var fit = _panel.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _title              = MakeText(_panel.transform, 30, FontStyle.Bold);
        _title.color        = Color.white;
        _subtitle           = MakeText(_panel.transform, 20, FontStyle.Normal);
        _subtitle.color     = new Color(0.62f, 0.72f, 0.85f);

        (_hpFill,     _hpText,     _)          = MakeBar(_panel.transform, "HP");
        (_shieldFill, _shieldText, _shieldRow) = MakeBar(_panel.transform, "KALKAN");
        (_armorFill,  _armorText,  _)          = MakeBar(_panel.transform, "ZIRH");

        _body                    = MakeText(_panel.transform, 19, FontStyle.Normal);
        _body.color              = new Color(0.82f, 0.86f, 0.92f);
        _body.horizontalOverflow = HorizontalWrapMode.Wrap;
        _body.verticalOverflow   = VerticalWrapMode.Overflow;
    }

    static Text MakeText(Transform parent, int size, FontStyle style)
    {
        var go = new GameObject("Txt", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<Text>();
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = size;
        t.fontStyle = style;
        t.color     = Color.white;
        t.alignment = TextAnchor.UpperLeft;
        // Yükseklik dışarıdaki VerticalLayoutGroup tarafından, metnin preferred
        // height'inden hesaplanır (childControlHeight). Buraya ayrıca bir
        // ContentSizeFitter koymak layout grubuyla çakışır.
        return t;
    }

    /// <summary>Zeminli, soldan dolan bar + üstüne binen sayısal etiket.</summary>
    static (RectTransform fill, Text label, GameObject row) MakeBar(Transform parent, string caption)
    {
        var row = new GameObject(caption + "Bar", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        row.AddComponent<Image>().color = new Color(0.13f, 0.14f, 0.18f, 0.95f);

        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = BarH + 6f;
        le.flexibleWidth   = 1f;

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(row.transform, false);
        fillGo.AddComponent<Image>();
        var fill              = (RectTransform)fillGo.transform;
        fill.anchorMin        = new Vector2(0f, 0f);
        fill.anchorMax        = new Vector2(1f, 1f);
        fill.pivot            = new Vector2(0f, 0.5f);
        fill.anchoredPosition = Vector2.zero;
        fill.sizeDelta        = Vector2.zero;

        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(row.transform, false);
        var lbl       = lblGo.AddComponent<Text>();
        lbl.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.fontSize  = 17;
        lbl.fontStyle = FontStyle.Bold;
        lbl.color     = Color.white;
        lbl.alignment = TextAnchor.MiddleLeft;
        var lr              = (RectTransform)lblGo.transform;
        lr.anchorMin        = Vector2.zero;
        lr.anchorMax        = Vector2.one;
        lr.anchoredPosition = new Vector2(7f, 0f);
        lr.sizeDelta        = new Vector2(-7f, 0f);

        return (fill, lbl, row);
    }

    static void SetBar(RectTransform fill, Text label, float cur, float max,
                       Color color, bool showMax = true)
    {
        float ratio = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
        fill.anchorMax = new Vector2(ratio, 1f);
        fill.sizeDelta = Vector2.zero;
        fill.GetComponent<Image>().color = color;

        string caption = fill.parent.name.Replace("Bar", "");
        label.text = showMax
            ? $"{caption}  {cur:0} / {max:0}"
            : $"{caption}  {cur:0.#}";
    }
}
