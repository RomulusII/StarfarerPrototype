using System.Collections;
using UnityEngine;

/// <summary>
/// Gemi slot'una kurulunca otomatik ateş açan turret.
/// Hedef seçimi türe göre değişir; barrel transform'u hedef yönüne döner.
/// Gatling: şarjör + reload. Roket: güdümlü mermi. Point Defence: kısa menzil.
/// </summary>
public class TurretController : ShipComponentBase
{
    public TurretType turretType     = TurretType.Gatling;
    public float      fireRate       = 1f;
    public float      damage         = 5f;
    public float      bulletSpeed    = 4f;
    public float      bulletLifeTime = 3f;
    public float      energyPerShot  = 1f;
    public int        magazineSize   = 10;
    public float      reloadTime     = 3f;

    // Runtime state
    float   _fireTimer;
    int     _currentMag;
    bool    _reloading;
    Transform _barrel;

    // Point Defence menzili (dünya birimi)
    const float PDRange = 3.5f;

    // -------------------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();
        componentName = TurretLabel();
        BuildVisual();
        _currentMag = magazineSize;
        _fireTimer  = Random.Range(0f, fireRate); // ilk atışı staggerla
    }

    void Update()
    {
        if (!IsOperational)  return;
        if (UpgradeUI.IsPaused) return;

        var target = FindTarget();

        if (target != null)
            AimAt(target.transform.position);

        _fireTimer -= Time.deltaTime;
        float effectiveFireRate = fireRate / GetMultiplier("fireRate");
        if (_fireTimer <= 0f && !_reloading && target != null)
        {
            bool hasEnergy = EnergyBus.Instance == null ||
                             EnergyBus.Instance.RequestEnergy(energyPerShot);
            if (hasEnergy)
                Fire(target, effectiveFireRate);
        }
    }

    // -------------------------------------------------------------------------
    // Hedefleme
    // -------------------------------------------------------------------------

    EnemyBot FindTarget()
    {
        var all = FindObjectsByType<EnemyBot>(FindObjectsSortMode.None);
        EnemyBot best  = null;
        float    bestD = float.MaxValue;

        foreach (var e in all)
        {
            float d = Vector2.Distance(transform.position, e.transform.position);

            if (turretType == TurretType.PointDefence && d > PDRange)
                continue;

            // Point Defence: Bomber öncelik
            if (turretType == TurretType.PointDefence &&
                e.enemyType == EnemyType.Bomber && best?.enemyType != EnemyType.Bomber)
            {
                best  = e;
                bestD = d;
                continue;
            }

            if (d < bestD)
            {
                bestD = d;
                best  = e;
            }
        }

        return best;
    }

    void AimAt(Vector3 worldPos)
    {
        var dir   = worldPos - transform.position;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, ang);
    }

    // -------------------------------------------------------------------------
    // Ateş etme
    // -------------------------------------------------------------------------

    void Fire(EnemyBot target, float effectiveFireRate)
    {
        _fireTimer = effectiveFireRate;

        if (turretType == TurretType.Gatling)
        {
            if (_currentMag <= 0) { StartCoroutine(Reload()); return; }
            _currentMag--;
            if (_currentMag <= 0) StartCoroutine(Reload());
        }

        SpawnBullet(target);
    }

    void SpawnBullet(EnemyBot target)
    {
        Vector3 spawnPos = transform.position + transform.right * 0.25f;

        var go = new GameObject("TurretBullet");
        go.transform.position = spawnPos;

        var tb = go.AddComponent<TurretBullet>();
        tb.damage      = damage * GetMultiplier("damage");
        tb.speed       = bulletSpeed;
        tb.weaponType  = BulletWeaponType();
        tb.isGuided    = turretType == TurretType.Rocket;
        tb.guidedTarget = turretType == TurretType.Rocket ? target : null;

        tb.SetDirection(transform.right);

        BuildBulletVisual(go, turretType);
        Destroy(go, bulletLifeTime);
    }

    IEnumerator Reload()
    {
        _reloading = true;
        yield return new WaitForSeconds(reloadTime);
        _currentMag = magazineSize;
        _reloading  = false;
    }

    WeaponType BulletWeaponType()
    {
        switch (turretType)
        {
            case TurretType.Plasma:       return WeaponType.Plasma;
            case TurretType.Laser:        return WeaponType.Laser;
            default:                      return WeaponType.Kinetic;
        }
    }

    // -------------------------------------------------------------------------
    // Görseller
    // -------------------------------------------------------------------------

    void BuildVisual()
    {
        Color baseColor = TurretColor();

        // Taban dairesi
        var baseTex = MakeTex(30, 30, baseColor * 0.7f);
        var baseGo  = new GameObject("Base");
        baseGo.transform.SetParent(transform, false);
        baseGo.transform.localPosition = Vector3.zero;
        baseGo.transform.localScale    = Vector3.one;
        var baseSR = baseGo.AddComponent<SpriteRenderer>();
        baseSR.sprite       = Sprite.Create(baseTex, new Rect(0,0,30,30), new Vector2(0.5f,0.5f), 100f);
        baseSR.sortingOrder = 3;

        // Namlu (sağa doğru, turret dönerken döner)
        var barrelTex = MakeTex(20, 8, baseColor);
        _barrel = new GameObject("Barrel").transform;
        _barrel.SetParent(transform, false);
        _barrel.localPosition = new Vector3(0.10f, 0f, 0f);
        _barrel.localScale    = Vector3.one;
        var barrelSR = _barrel.gameObject.AddComponent<SpriteRenderer>();
        barrelSR.sprite       = Sprite.Create(barrelTex, new Rect(0,0,20,8), new Vector2(0f,0.5f), 100f);
        barrelSR.sortingOrder = 4;
    }

    static void BuildBulletVisual(GameObject go, TurretType type)
    {
        Color c = type == TurretType.Plasma      ? new Color(0.4f, 1f, 0.3f) :
                  type == TurretType.Laser        ? Color.cyan :
                  type == TurretType.Rocket       ? new Color(1f, 0.5f, 0.1f) :
                  type == TurretType.PointDefence ? Color.yellow :
                  Color.white;

        int w = type == TurretType.Rocket ? 14 : 8;
        int h = type == TurretType.Rocket ? 6  : 4;

        var tex = MakeTex(w, h, c);
        var sr  = go.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(tex, new Rect(0,0,w,h), new Vector2(0f,0.5f), 100f);
        sr.sortingOrder = 3;
    }

    Color TurretColor()
    {
        switch (turretType)
        {
            case TurretType.Gatling:      return new Color(0.7f, 0.7f, 0.75f);
            case TurretType.Plasma:       return new Color(0.3f, 0.9f, 0.3f);
            case TurretType.Laser:        return new Color(0.2f, 0.8f, 1f);
            case TurretType.Rocket:       return new Color(1f,   0.5f, 0.1f);
            case TurretType.PointDefence: return new Color(1f,   0.9f, 0.2f);
            default:                      return Color.gray;
        }
    }

    string TurretLabel()
    {
        switch (turretType)
        {
            case TurretType.Gatling:      return "Gatling Turret";
            case TurretType.Plasma:       return "Plasma Turret";
            case TurretType.Laser:        return "Laser Turret";
            case TurretType.Rocket:       return "Rocket Turret";
            case TurretType.PointDefence: return "Point Defence";
            default:                      return "Turret";
        }
    }

    static Texture2D MakeTex(int w, int h, Color c)
    {
        var tex = new Texture2D(w, h);
        var px  = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // -------------------------------------------------------------------------
    // Configure (ShipLoadout tarafından çağrılır)
    // -------------------------------------------------------------------------

    public void Configure(ComponentDefinition def)
    {
        turretType     = def.turretType;
        fireRate       = def.turretFireRate       > 0 ? def.turretFireRate       : fireRate;
        damage         = def.turretDamage         > 0 ? def.turretDamage         : damage;
        bulletSpeed    = def.turretBulletSpeed    > 0 ? def.turretBulletSpeed    : bulletSpeed;
        bulletLifeTime = def.turretBulletLifeTime > 0 ? def.turretBulletLifeTime : bulletLifeTime;
        energyPerShot  = def.turretEnergyPerShot  > 0 ? def.turretEnergyPerShot  : energyPerShot;
        magazineSize   = def.turretMagazineSize   > 0 ? def.turretMagazineSize   : magazineSize;
        reloadTime     = def.turretReloadTime     > 0 ? def.turretReloadTime     : reloadTime;

        componentName  = TurretLabel();
        _currentMag    = magazineSize;
    }
}
