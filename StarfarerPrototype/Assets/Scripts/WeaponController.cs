using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Kinetik  — basılı tutunca otomatik ateş, enerji yok.
/// Lazer    — basılı tut → anlık ışın, burnDuration boyunca sürekli DPS + enerji tüketimi.
///            Her beam bitince fireRate kadar beklenir, sonra yeni beam atılabilir.
/// Plazma   — basılı tut → namlu ucunda küre büyür, bırak → büyüklüğe göre geniş bolt fırlar.
///            Minimum şarj süresi (chargeTime * 0.25) dolmadan bırakılırsa iptal.
/// </summary>
public class WeaponController : MonoBehaviour
{
    public WeaponType weaponType   = WeaponType.Kinetic;
    public float damage            = 10f;
    public float fireRate          = 1.00f;   // Kinetik: atışlar arası, Plazma: atış sonrası bekleme
    public float energyCostPerShot = 3f;      // Lazer: enerji/saniye (beam aktifken)
    public float chargeTime        = 2.0f;    // Plazma: tam şarja ulaşma süresi

    private float      _nextFireTime;
    private float      _chargeStart = -1f;
    private GameObject _chargeSphere;
    private LaserBeam  _activeLaserBeam;  // aktif beam varsa yeni ateş edilmez

    private Sprite _kineticSprite;
    private Sprite _plasmaCircle;  // şarj küresi ve bolt için

    void Start()
    {
        _kineticSprite = MakeSprite(10, 30, Color.white);
        _plasmaCircle  = MakeCircleSprite(32, Color.white); // renk runtime'da set edilir
    }

    void OnDestroy()
    {
        CancelCharge();
        CancelLaserBeam();
    }

    void Update()
    {
        if (UpgradeUI.IsPaused)
        {
            CancelCharge();
            CancelLaserBeam();
            return;
        }

        switch (weaponType)
        {
            case WeaponType.Kinetic: UpdateKinetic(); break;
            case WeaponType.Laser:   UpdateLaser();   break;
            case WeaponType.Plasma:  UpdatePlasma();  break;
        }
    }

    // -------------------------------------------------------------------------
    // Mod: Kinetik
    // -------------------------------------------------------------------------

    void UpdateKinetic()
    {
        if (Mouse.current.leftButton.isPressed && Time.time >= _nextFireTime)
        {
            _nextFireTime = Time.time + fireRate;
            SpawnBullet(_kineticSprite, WeaponType.Kinetic, speed: 6f);
        }
    }

    // -------------------------------------------------------------------------
    // Mod: Lazer — sürekli ışın, basılı tut = açık, bırak = kapalı
    // -------------------------------------------------------------------------

    void UpdateLaser()
    {
        bool held = Mouse.current.leftButton.isPressed;

        if (held && _activeLaserBeam == null)
        {
            // Child olarak spawn — parent (WeaponMount) döndükçe beam yönü otomatik güncellenir
            var go = new GameObject("LaserBeam");
            go.transform.SetParent(transform, false);
            // Namlu ucundan başlasın — kökten çıkarsa ışın namlunun içinde görünür
            go.transform.localPosition = new Vector3(0f, WeaponMount.BarrelLength, 0f);
            go.transform.localRotation = Quaternion.identity;

            float energyMulti    = BoostController.Mode == BoostMode.Shield ? 1f / 3f :
                                   BoostController.Mode == BoostMode.Weapon  ? 3f      : 1f;

            var beam             = go.AddComponent<LaserBeam>();
            beam.damage          = damage;
            beam.weaponType      = WeaponType.Laser;
            beam.continuous      = true;
            beam.energyPerSecond = energyCostPerShot * energyMulti;
            beam.maxRange        = 22f;
            beam.Init();
            _activeLaserBeam = beam;
        }
        else if (!held)
        {
            CancelLaserBeam();
        }
    }

    void CancelLaserBeam()
    {
        if (_activeLaserBeam == null) return;
        Destroy(_activeLaserBeam.gameObject);
        _activeLaserBeam = null;
    }

    // -------------------------------------------------------------------------
    // Mod: Plazma — Another World tarzı şarj + bırak
    // -------------------------------------------------------------------------

    void UpdatePlasma()
    {
        bool held     = Mouse.current.leftButton.isPressed;
        bool released = Mouse.current.leftButton.wasReleasedThisFrame;

        if (held)
        {
            if (_chargeStart < 0f)
            {
                _chargeStart  = Time.time;
                _chargeSphere = CreateChargeSphere();
            }
            float ratio = ChargeRatio();
            UpdateChargeSphere(ratio);
        }

        if (released)
        {
            float heldSeconds = _chargeStart >= 0f ? Time.time - _chargeStart : 0f;
            float ratio       = Mathf.Clamp01(heldSeconds / chargeTime);
            CancelCharge();

            float minCharge = chargeTime * 0.25f;
            if (heldSeconds >= minCharge && Time.time >= _nextFireTime)
            {
                _nextFireTime = Time.time + fireRate;
                FirePlasmaBolt(ratio);
            }
        }
    }

    float ChargeRatio() =>
        _chargeStart >= 0f ? Mathf.Clamp01((Time.time - _chargeStart) / chargeTime) : 0f;

    void CancelCharge()
    {
        _chargeStart = -1f;
        if (_chargeSphere != null) { Destroy(_chargeSphere); _chargeSphere = null; }
    }

    GameObject CreateChargeSphere()
    {
        var go = new GameObject("PlasmaCharge");
        go.transform.SetParent(transform, false);
        // Namlu ucu — uzunluk WeaponMount.BarrelLength'ten gelir.
        // transform.up = ateş yönü → local Y pozitifi = namlu ucu
        go.transform.localPosition = new Vector3(0f, WeaponMount.BarrelLength, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = _plasmaCircle;
        sr.sortingOrder = 15;
        go.transform.localScale = Vector3.one * 0.04f;
        return go;
    }

    void UpdateChargeSphere(float ratio)
    {
        if (_chargeSphere == null) return;
        float scale = Mathf.Lerp(0.04f, 0.60f, ratio);
        _chargeSphere.transform.localScale = Vector3.one * scale;
        var sr = _chargeSphere.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = Color.Lerp(
                new Color(0.3f, 1f, 0.2f, 0.75f),
                new Color(0.9f, 1f, 0.4f, 1.00f),
                ratio);
    }

    void FirePlasmaBolt(float chargeRatio)
    {
        float dmgMulti = BoostController.Mode == BoostMode.Weapon ? 2f   :
                         BoostController.Mode == BoostMode.Shield  ? 1f/3f : 1f;
        float sclMulti = BoostController.Mode == BoostMode.Weapon ? 1.5f :
                         BoostController.Mode == BoostMode.Shield  ? 0.6f  : 1f;

        // Beam genişliği = şarj anındaki küre çapı
        float beamWidth = Mathf.Lerp(0.10f, 0.65f, chargeRatio) * sclMulti;
        float maxLen    = 60f;                                       // sabit, ekranı her zaman aşar
        float spd       = Mathf.Lerp(14f, 28f, chargeRatio);       // ×2 hız (önceki değişiklikten)
        float totalDmg  = damage * Mathf.Lerp(0.5f, 2.5f, chargeRatio) * dmgMulti;

        var go = new GameObject("PlasmaBeam");
        // Namlu ucundan spawn — uzunluk WeaponMount'tan gelir
        go.transform.SetPositionAndRotation(
            transform.position + transform.up * WeaponMount.BarrelLength,
            transform.rotation);

        var beam = go.AddComponent<PlasmaBeam>();
        beam.beamWidth    = beamWidth;
        beam.maxLength    = maxLen;
        beam.headSpeed    = spd;
        beam.totalEnergy  = totalDmg;
        // Bir düşmanı 0.33 saniyede tüketir; kalabalık grupta daha hızlı
        beam.dps          = totalDmg * 3f;
        beam.weaponType   = WeaponType.Plasma;
        beam.emitDuration = 0.2f;   // namludan çıkış süresi — sonrası bolt olarak uçar
    }

    // -------------------------------------------------------------------------
    // Ortak yardımcılar
    // -------------------------------------------------------------------------

    void SpawnBullet(Sprite sprite, WeaponType type, float speed)
    {
        float damageMulti = BoostController.Mode == BoostMode.Weapon ? 2f   :
                            BoostController.Mode == BoostMode.Shield  ? 1f/3f : 1f;
        float scaleMulti  = BoostController.Mode == BoostMode.Weapon ? 1.5f :
                            BoostController.Mode == BoostMode.Shield  ? 0.6f  : 1f;

        var go = new GameObject("Bullet");
        // Namlu ucundan çıksın — mount noktası namlunun kökü
        go.transform.SetPositionAndRotation(
            transform.position + transform.up * WeaponMount.BarrelLength,
            transform.rotation);
        go.transform.localScale = Vector3.one * scaleMulti;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = 20;

        var b = go.AddComponent<Bullet>();
        b.speed      = speed;
        b.damage     = damage * damageMulti;
        b.weaponType = type;
    }

    static Sprite MakeSprite(int w, int h, Color color)
    {
        var tex    = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    // Dairesel (alpha kenar kesen) sprite — plazma küre ve bolt için
    static Sprite MakeCircleSprite(int res, Color color)
    {
        var tex     = new Texture2D(res, res);
        tex.filterMode = FilterMode.Bilinear;
        var pixels  = new Color[res * res];
        float center = (res - 1) / 2f;
        float radius = center;
        for (int i = 0; i < pixels.Length; i++)
        {
            float dx   = (i % res) - center;
            float dy   = (i / res) - center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            // Yumuşak kenar (anti-alias benzeri)
            float alpha = Mathf.Clamp01(1f - (dist - radius + 1.5f));
            pixels[i] = new Color(color.r, color.g, color.b, alpha * color.a);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }

    /// <summary>ShipLoadout tarafından çağrılır.</summary>
    public void Configure(ComponentDefinition def)
    {
        CancelCharge(); // silah değişince şarjı iptal et
        weaponType        = def.weaponType;
        damage            = def.weaponDamage            > 0 ? def.weaponDamage            : 10f;
        fireRate          = def.weaponFireRate           > 0 ? def.weaponFireRate           : 0.15f;
        energyCostPerShot = def.weaponEnergyCostPerShot  > 0 ? def.weaponEnergyCostPerShot  : 3f;
        chargeTime        = def.weaponChargeTime         > 0 ? def.weaponChargeTime         : 2.0f;
    }
}
