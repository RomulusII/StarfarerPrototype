using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tüm gemi komponentlerinin base class'ı.
///
/// HP sıfırlandığında zorluk ayarına göre davranır:
///   Easy   — deaktif kalır, RepairUnit maxHP'ye tamir edince yeniden açılır.
///   Normal/Hard — slot temizlenir, GO yok edilir.
///
/// Görsel:
///   Her komponent için ince bir HP barı oluşturulur (HealthBar.cs ile aynı yaklaşım).
///   Barı yalnızca hasar alındığında görünür. Dünya uzayında konumlandırılır (LateUpdate),
///   parent döndüğünde bozulmaz.
///   _skipVisual = true (Hangar gibi meta-komponentler) ise hiçbir görsel oluşturulmaz.
/// </summary>
public abstract class ShipComponentBase : MonoBehaviour
{
    public string componentName;
    public float  maxHP            = 10f;
    public float  currentHP;
    public float  energyConsumption = 0f;

    bool _deactivated = false;

    public bool IsDeactivated => _deactivated;
    public bool IsOperational  => currentHP > 0f && !_deactivated;

    /// <summary>Subclass Awake'de base.Awake() öncesi true yaparsa görsel atlanır.</summary>
    protected bool _skipVisual = false;

    // ── Stat upgrade sistemi ──────────────────────────────────────────────────

    public readonly Dictionary<string, int> StatLevels = new();

    /// <summary>
    /// Stat tavanı. Tier zincirleri kaldırılınca 8'den 10'a çıkarıldı — tier'ların
    /// taşıdığı güç stat eğrisine devredildi. Tavanı 10 tutmak, komponent
    /// tavanlarını eski Mk3 + Sv8 seviyesinin yakınında bırakır.
    /// </summary>
    public const int MaxStatLevel = 10;

    /// <summary>
    /// Seviye başına güç çarpanı. Sabit 1.5 idi; BalanceConfig'e taşındı çünkü
    /// hasar ve ateş hızı ikisi de DPS'e çarpımsal giriyor ve 1.5 ile oyuncu
    /// üstünlüğü kampanya boyunca 6× kayıyordu.
    /// </summary>
    public float GetMultiplier(string key) =>
        BalanceConfig.Instance.StatMultiplier(GetStatLevel(key));

    public int GetStatLevel(string key) =>
        StatLevels.TryGetValue(key, out var lvl) ? lvl : 0;

    /// <summary>
    /// En yüksek stat seviyesi. Enerji tüketimi buna bağlanır — statların
    /// TOPLAMINA bağlansaydı yedi izli Hangar, iki izli turretten katbekat
    /// fazla enerji yerdi; oysa ikisi de "aynı derecede yükseltilmiş".
    /// </summary>
    public int HighestStatLevel
    {
        get
        {
            int max = 0;
            foreach (var lvl in StatLevels.Values) if (lvl > max) max = lvl;
            return max;
        }
    }

    public void ApplyStatUpgrade(string key)
    {
        int cur = GetStatLevel(key);
        if (cur >= MaxStatLevel) return;
        StatLevels[key] = cur + 1;
        RefreshEnergyDraw();
        OnStatUpgraded(key);
    }

    public virtual void OnStatUpgraded(string key) { }

    /// <summary>Tüm stat izlerini olduğu gibi kopyalar.</summary>
    public void CopyStatLevelsFrom(ShipComponentBase other)
    {
        if (other == null) return;
        StatLevels.Clear();
        foreach (var kv in other.StatLevels) StatLevels[kv.Key] = kv.Value;
        RefreshEnergyDraw();
        foreach (var key in StatLevels.Keys) OnStatUpgraded(key);
    }

    // ── Enerji tüketimi ───────────────────────────────────────────────────────
    //
    // EnergyBus'ın üretim/tüketim muhasebesi uzun süre yazılıydı ama kapalıydı:
    // her komponent Awake'de energyConsumption = 0f yapıyordu, dolayısıyla
    // TotalConsumption her zaman sıfırdı. Artık besleniyor.

    /// <summary>Sv0 tüketimi — ShipLoadout kurulumda ComponentDefinition'dan verir.</summary>
    public float baseEnergyCost = 0f;

    public float EnergyDrawAt(int statLevel)
        => baseEnergyCost * BalanceConfig.Instance.EnergyMultiplier(statLevel);

    /// <summary>Bir sonraki stat seviyesinin getireceği EK enerji yükü.</summary>
    public float NextUpgradeEnergyDelta(string key)
    {
        // Yalnızca EN YÜKSEK izi zorlamak tüketimi artırır; geride kalan bir izi
        // yükseltmek bedavadır. Tüketim zaten en yüksek seviyeye bağlı.
        int next = Mathf.Max(HighestStatLevel, GetStatLevel(key) + 1);
        return EnergyDrawAt(next) - EnergyDrawAt(HighestStatLevel);
    }

    /// <summary>Taban tüketimi ayarlar ve EnergyBus kaydını tazeler.</summary>
    public void SetEnergyBase(float value)
    {
        baseEnergyCost = value;
        RefreshEnergyDraw();
    }

    protected void RefreshEnergyDraw()
    {
        float next = EnergyDrawAt(HighestStatLevel);
        if (Mathf.Approximately(next, energyConsumption)) return;

        if (EnergyBus.Instance != null && IsOperational)
        {
            EnergyBus.Instance.UnregisterConsumer(energyConsumption);
            EnergyBus.Instance.RegisterConsumer(next);
        }
        energyConsumption = next;
    }

    // ── Görsel sabitler ───────────────────────────────────────────────────────

    const float k_ringSize  = 0.35f;  // daire çapı (world unit) — komponent pozisyon göstergesi
    const float k_barWidth  = k_ringSize;
    const float k_barHeight = 0.05f;  // ince bar (~5 px)
    // Bar, halkanın üst kenarına yerleşir
    const float k_barOffY   = k_ringSize * 0.5f + k_barHeight * 0.5f + 0.01f;

    static readonly Color k_ringNormal      = new Color(0.45f, 0.85f, 1f,  0.25f);
    static readonly Color k_ringDeactivated = new Color(0.80f, 0.12f, 0.1f, 0.40f);

    /// <summary>
    /// Komponentin skin anahtari - sinif adindan turer
    /// (GeneratorComponent -> "component.generator").
    /// </summary>
    protected virtual string SkinKey => SkinId.ForComponent(GetType().Name);

    /// <summary>
    /// Halka rengi. Prosedurel halka BEYAZ dokudur, rengini buradan alir ve
    /// kasten soluktur (yalnizca konum gostergesi). Skin varsa sprite kendi
    /// rengini tasir, o yuzden normalde tam beyaz gecilir - yoksa ikon soluklasir.
    /// Deaktif durum her iki yolda da kirmiziya doner.
    /// </summary>
    Color RingColor(bool deactivated)
    {
        bool skinned = SkinLibrary.Has(SkinKey);
        if (deactivated) return skinned ? new Color(1f, 0.35f, 0.30f, 1f) : k_ringDeactivated;
        return skinned ? Color.white : k_ringNormal;
    }

    SpriteRenderer _ringRenderer;
    Transform      _hpBg;
    Transform      _hpFill;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        currentHP = maxHP;
        if (!_skipVisual) SpawnVisual();
    }

    void LateUpdate()
    {
        if (_hpBg == null) return;

        float ratio   = maxHP > 0f ? Mathf.Clamp01(currentHP / maxHP) : 0f;
        bool  damaged = ratio < 1f;

        // Göster / gizle
        if (_hpBg.gameObject.activeSelf != damaged)
        {
            _hpBg.gameObject.SetActive(damaged);
            _hpFill.gameObject.SetActive(damaged);
        }

        if (!damaged) return;

        // World-space konumlandırma — HealthBar.cs ile aynı yaklaşım, parent rotasyonundan etkilenmez
        Vector3 pos   = transform.position;
        float   leftX = pos.x - k_barWidth * 0.5f;
        float   barY  = pos.y + k_barOffY;
        float   z     = pos.z - 0.1f;

        // Zemin: tam genişlik, kırmızı
        _hpBg.position   = new Vector3(leftX, barY, z);
        _hpBg.rotation   = Quaternion.identity;
        _hpBg.localScale = new Vector3(k_barWidth, k_barHeight, 1f);

        // Dolgu: sol kenardan sağa büyür, yeşil
        _hpFill.position   = new Vector3(leftX, barY, z - 0.01f);
        _hpFill.rotation   = Quaternion.identity;
        _hpFill.localScale = new Vector3(k_barWidth * Mathf.Max(ratio, 0.01f), k_barHeight, 1f);
    }

    // ── Hasar ve Tamir ────────────────────────────────────────────────────────

    public virtual void TakeDamage(float amount)
    {
        if (!IsOperational) return;
        currentHP = Mathf.Max(0f, currentHP - amount);
        if (currentHP == 0f)
            OnComponentDestroyed();
    }

    public virtual void Repair(float amount)
    {
        currentHP = Mathf.Min(currentHP + amount, maxHP);

        if (_deactivated && currentHP >= maxHP)
        {
            _deactivated = false;
            if (_ringRenderer != null) _ringRenderer.color = RingColor(deactivated: false);
            if (EnergyBus.Instance != null)
                EnergyBus.Instance.RegisterConsumer(energyConsumption);
        }
    }

    protected virtual void OnComponentDestroyed()
    {
        if (DifficultyManager.Current == Difficulty.Easy)
        {
            _deactivated = true;
            if (_ringRenderer != null) _ringRenderer.color = RingColor(deactivated: true);
            if (EnergyBus.Instance != null)
                EnergyBus.Instance.UnregisterConsumer(energyConsumption);
        }
        else
        {
            var loadout = FindFirstObjectByType<ShipLoadout>();
            loadout?.ClearSlotForComponent(this);
            Destroy(gameObject);
        }
    }

    protected virtual void OnEnable()
    {
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.RegisterConsumer(energyConsumption);
    }

    protected virtual void OnDisable()
    {
        if (EnergyBus.Instance != null)
            EnergyBus.Instance.UnregisterConsumer(energyConsumption);
    }

    // ── Görsel: spawn ─────────────────────────────────────────────────────────

    void SpawnVisual()
    {
        // Halka — komponentin konumunu gösterir, sabit renk (HealthBar gibi hasar bilgisi vermez)
        var ringGo = new GameObject("CompRing");
        ringGo.transform.SetParent(transform, false);
        ringGo.transform.localPosition = Vector3.zero;
        ringGo.transform.localScale    = Vector3.one * k_ringSize;
        _ringRenderer              = ringGo.AddComponent<SpriteRenderer>();
        _ringRenderer.sprite       = SkinLibrary.GetOrNull(SkinKey) ?? GetRingSprite();
        _ringRenderer.color        = RingColor(deactivated: false);
        _ringRenderer.sortingOrder = -5;

        // HP barı — zemin (kırmızı), başlangıçta gizli
        _hpBg   = MakeBarGO("CompHPBg",   new Color(0.70f, 0.05f, 0.05f, 0.92f), -4);
        _hpFill = MakeBarGO("CompHPFill", new Color(0.15f, 0.82f, 0.20f, 1.00f), -3);

        _hpBg.gameObject.SetActive(false);
        _hpFill.gameObject.SetActive(false);
    }

    /// <summary>HealthBar.cs MakeBar() ile aynı pattern — sol-orta pivot sprite.</summary>
    Transform MakeBarGO(string goName, Color color, int sortOrder)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform);

        var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
        sr.color        = color;
        sr.sortingOrder = sortOrder;

        return go.transform;
    }

    // ── Halka sprite builder ──────────────────────────────────────────────────

    static Sprite _ringSprite;

    static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        const int res    = 32;
        var       tex    = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var       px     = new Color32[res * res];
        float     centre = res * 0.5f;
        float     outer  = centre - 0.5f;

        for (int i = 0; i < px.Length; i++)
        {
            float x = (i % res) + 0.5f - centre;
            float y = (i / res) + 0.5f - centre;
            float d = Mathf.Sqrt(x * x + y * y);
            // Anti-aliased dolu daire — kenar 1.5px yumuşatma
            float a = Mathf.Clamp01(outer - d + 1.5f);
            px[i] = new Color32(255, 255, 255, (byte)(a * 255));
        }

        tex.SetPixels32(px);
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        return _ringSprite;
    }
}
