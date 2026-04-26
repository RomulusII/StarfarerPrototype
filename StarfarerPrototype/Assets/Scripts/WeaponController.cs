using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Kinetik  — sol tık basılı tutunca otomatik ateş, enerji yok.
/// Lazer    — sol tık basılı tutunca hızlı ateş, her atışta enerji tüketir.
///            Enerji bitince durur.
/// Plazma   — sol tık basılı tutunca şarj olur, dolunca otomatik burst atar.
///            Burst arasında fireRate kadar beklenir.
/// </summary>
public class WeaponController : MonoBehaviour
{
    public WeaponType weaponType   = WeaponType.Kinetic;
    public float damage            = 10f;
    public float fireRate          = 0.15f;
    public float energyCostPerShot = 3f;   // Lazer
    public float chargeTime        = 0.8f; // Plazma
    public int   burstCount        = 3;    // Plazma

    private float   _nextFireTime;
    private float   _chargeStart  = -1f;
    private bool    _burstRunning;

    private Sprite _kineticSprite;
    private Sprite _laserSprite;
    private Sprite _plasmaSprite;

    void Start()
    {
        _kineticSprite = MakeSprite(10, 30, Color.white);
        _laserSprite   = MakeSprite(4,  44, Color.cyan);
        _plasmaSprite  = MakeSprite(18, 18, new Color(0.4f, 1f, 0.3f));
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

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
            SpawnBullet(_kineticSprite, WeaponType.Kinetic, speed: 12f);
        }
    }

    // -------------------------------------------------------------------------
    // Mod: Lazer
    // -------------------------------------------------------------------------

    void UpdateLaser()
    {
        if (!Mouse.current.leftButton.isPressed) return;
        if (Time.time < _nextFireTime) return;

        float energyMulti = BoostController.Mode == BoostMode.Shield ? 1f / 3f :
                            BoostController.Mode == BoostMode.Weapon  ? 3f : 1f;

        if (EnergyBus.Instance == null ||
            !EnergyBus.Instance.RequestEnergy(energyCostPerShot * energyMulti))
            return;

        _nextFireTime = Time.time + fireRate;
        SpawnBullet(_laserSprite, WeaponType.Laser, speed: 16f);
    }

    // -------------------------------------------------------------------------
    // Mod: Plazma
    // -------------------------------------------------------------------------

    void UpdatePlasma()
    {
        if (Mouse.current.leftButton.isPressed)
        {
            if (_chargeStart < 0f)
                _chargeStart = Time.time;

            if (!_burstRunning && Time.time - _chargeStart >= chargeTime && Time.time >= _nextFireTime)
            {
                _nextFireTime = Time.time + fireRate * burstCount + 0.3f;
                _chargeStart  = -1f;
                StartCoroutine(FireBurst());
            }
        }
        else
        {
            _chargeStart = -1f;
        }
    }

    IEnumerator FireBurst()
    {
        _burstRunning = true;
        for (int i = 0; i < burstCount; i++)
        {
            SpawnBullet(_plasmaSprite, WeaponType.Plasma, speed: 10f);
            yield return new WaitForSeconds(0.08f);
        }
        _burstRunning = false;
    }

    // -------------------------------------------------------------------------
    // Ortak yardımcılar
    // -------------------------------------------------------------------------

    void SpawnBullet(Sprite sprite, WeaponType type, float speed)
    {
        float damageMulti = BoostController.Mode == BoostMode.Weapon ? 2f :
                            BoostController.Mode == BoostMode.Shield  ? 1f / 3f : 1f;
        float scaleMulti  = BoostController.Mode == BoostMode.Weapon ? 1.5f :
                            BoostController.Mode == BoostMode.Shield  ? 0.6f : 1f;

        var go = new GameObject("Bullet");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = Vector3.one * scaleMulti;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = 2;

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

    /// <summary>ShipLoadout tarafından çağrılır.</summary>
    public void Configure(ComponentDefinition def)
    {
        weaponType        = def.weaponType;
        damage            = def.weaponDamage     > 0 ? def.weaponDamage     : 10f;
        fireRate          = def.weaponFireRate    > 0 ? def.weaponFireRate    : 0.15f;
        energyCostPerShot = def.weaponEnergyCostPerShot > 0 ? def.weaponEnergyCostPerShot : 3f;
        chargeTime        = def.weaponChargeTime  > 0 ? def.weaponChargeTime  : 0.8f;
        burstCount        = def.weaponBurstCount  > 0 ? def.weaponBurstCount  : 3;
    }
}
