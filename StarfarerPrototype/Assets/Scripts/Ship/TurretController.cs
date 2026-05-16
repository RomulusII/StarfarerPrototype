using System.Collections;
using UnityEngine;

/// <summary>
/// Gemi slot'una kurulunca otomatik ateş açan turret.
/// baseType: Kinetic / Energy / Missile — satın alırken belirlenir, değişmez.
/// specType: uzmanlaşma — Upgrade ekranından değiştirilebilir.
/// </summary>
public class TurretController : ShipComponentBase
{
    public TurretBaseType baseType     = TurretBaseType.Kinetic;
    public TurretSpecType specType     = TurretSpecType.None;
    public float          fireRate     = 1f;
    public float          damage       = 5f;
    public float          bulletSpeed  = 4f;
    public float          bulletLifeTime = 3f;
    public float          energyPerShot  = 1f;
    public int            magazineSize   = 10;
    public float          reloadTime     = 3f;
    [Tooltip("Saniyede derece — turretin maksimum dönüş hızı.")]
    public float          turnRate       = 180f;

    float   _fireTimer;
    int     _currentMag;
    bool    _reloading;
    Transform _barrel;

    const float PDRange = 5.5f;

    // -------------------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();
        componentName = BuildLabel();
        BuildVisual();
        _currentMag = magazineSize;
        _fireTimer  = Random.Range(0f, fireRate);
        ApplySpecTurnRate();
    }

    void Update()
    {
        if (!IsOperational)     return;
        if (UpgradeUI.IsPaused) return;

        var target = FindTarget();

        if (target != null)
            AimAt(target.position);

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

    Transform FindTarget()
    {
        if (specType == TurretSpecType.PointDefence)
        {
            var bombs = FindObjectsByType<Bomb>(FindObjectsSortMode.None);
            Transform nearestBomb  = null;
            float     nearestBombD = float.MaxValue;
            foreach (var b in bombs)
            {
                float d = Vector2.Distance(transform.position, b.transform.position);
                if (d <= PDRange && d < nearestBombD) { nearestBombD = d; nearestBomb = b.transform; }
            }
            if (nearestBomb != null) return nearestBomb;
        }

        // Boss hedefleme
        var boss = FindFirstObjectByType<BossShip>();
        Transform bestTransform = null;
        float     bestD         = float.MaxValue;

        if (boss != null)
        {
            float d = Vector2.Distance(transform.position, boss.transform.position);
            if (specType != TurretSpecType.PointDefence || d <= PDRange)
            {
                bestD         = d;
                bestTransform = boss.transform;
            }
        }

        // Normal düşmanlar
        var all = FindObjectsByType<EnemyBot>(FindObjectsSortMode.None);
        EnemyBot bestBot = null;

        foreach (var e in all)
        {
            float d = Vector2.Distance(transform.position, e.transform.position);

            if (specType == TurretSpecType.PointDefence && d > PDRange)
                continue;

            bool ePriority    = e.data?.movementKind == EnemyMovementKind.Approach
                             || e.data?.movementKind == EnemyMovementKind.BombRun;
            bool bestPriority = bestBot?.data?.movementKind == EnemyMovementKind.Approach
                             || bestBot?.data?.movementKind == EnemyMovementKind.BombRun;

            if (specType == TurretSpecType.PointDefence && ePriority && !bestPriority)
            {
                bestBot = e; bestD = d;
                continue;
            }

            if (d < bestD && (!bestPriority || ePriority))
            {
                bestD = d; bestBot = e;
            }
        }

        if (bestBot != null) return bestBot.transform;
        return bestTransform;
    }

    void AimAt(Vector3 worldPos)
    {
        var   dir     = worldPos - transform.position;
        float target  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float current = transform.eulerAngles.z;
        float next    = Mathf.MoveTowardsAngle(current, target, turnRate * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, next);
    }

    // -------------------------------------------------------------------------
    // Ateş etme
    // -------------------------------------------------------------------------

    void Fire(Transform target, float effectiveFireRate)
    {
        _fireTimer = effectiveFireRate;

        if (specType == TurretSpecType.Gatling)
        {
            if (_currentMag <= 0) { StartCoroutine(Reload()); return; }
            _currentMag--;
            if (_currentMag <= 0) StartCoroutine(Reload());
        }

        SpawnBullet(target);
    }

    void SpawnBullet(Transform target)
    {
        Vector3 spawnPos = transform.position + transform.right * 0.25f;

        var go = new GameObject("TurretBullet");
        go.transform.position = spawnPos;

        var tb = go.AddComponent<TurretBullet>();
        tb.damage      = damage * GetMultiplier("damage");
        tb.speed       = bulletSpeed;
        tb.weaponType  = BulletWeaponType();

        bool isRocket = specType == TurretSpecType.HomingRocket ||
                        (baseType == TurretBaseType.Missile && specType == TurretSpecType.None);
        tb.isGuided     = isRocket;
        tb.guidedTarget = isRocket ? target : null;
        if (isRocket) { tb.turnRate = 150f; tb.hp = 3f; }

        tb.SetDirection(transform.right);

        BuildBulletVisual(go, specType);
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
        return specType switch
        {
            TurretSpecType.Plasma => WeaponType.Plasma,
            TurretSpecType.Laser  => WeaponType.Laser,
            TurretSpecType.EMP    => WeaponType.Laser,
            _                     => WeaponType.Kinetic,
        };
    }

    // -------------------------------------------------------------------------
    // Uzmanlaşma (runtime spec değişimi)
    // -------------------------------------------------------------------------

    public void Specialize(TurretSpecType newSpec, ComponentDefinition newDef)
    {
        specType = newSpec;

        fireRate       = newDef.turretFireRate       > 0 ? newDef.turretFireRate       : fireRate;
        damage         = newDef.turretDamage         > 0 ? newDef.turretDamage         : damage;
        bulletSpeed    = newDef.turretBulletSpeed    > 0 ? newDef.turretBulletSpeed    : bulletSpeed;
        bulletLifeTime = newDef.turretBulletLifeTime > 0 ? newDef.turretBulletLifeTime : bulletLifeTime;
        energyPerShot  = newDef.turretEnergyPerShot  > 0 ? newDef.turretEnergyPerShot  : energyPerShot;
        magazineSize   = newDef.turretMagazineSize   > 0 ? newDef.turretMagazineSize   : magazineSize;
        reloadTime     = newDef.turretReloadTime     > 0 ? newDef.turretReloadTime     : reloadTime;

        _currentMag = magazineSize;
        ApplySpecTurnRate();
        componentName = BuildLabel();
        RebuildVisual();
    }

    // -------------------------------------------------------------------------
    // Görseller
    // -------------------------------------------------------------------------

    void BuildVisual()
    {
        Color baseColor = TurretColor();

        var baseGo = new GameObject("Base");
        baseGo.transform.SetParent(transform, false);
        var baseSR = baseGo.AddComponent<SpriteRenderer>();
        baseSR.sprite       = Sprite.Create(MakeTex(30, 30, baseColor * 0.7f),
                                  new Rect(0,0,30,30), new Vector2(0.5f,0.5f), 100f);
        baseSR.sortingOrder = 3;

        _barrel = new GameObject("Barrel").transform;
        _barrel.SetParent(transform, false);
        _barrel.localPosition = new Vector3(0.10f, 0f, 0f);
        var barrelSR = _barrel.gameObject.AddComponent<SpriteRenderer>();
        barrelSR.sprite       = Sprite.Create(MakeTex(20, 8, TurretColor()),
                                    new Rect(0,0,20,8), new Vector2(0f,0.5f), 100f);
        barrelSR.sortingOrder = 4;
    }

    void RebuildVisual()
    {
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        _barrel = null;
        BuildVisual();
    }

    static void BuildBulletVisual(GameObject go, TurretSpecType spec)
    {
        Color c = spec switch
        {
            TurretSpecType.Plasma       => new Color(0.4f, 1f, 0.3f),
            TurretSpecType.Laser        => Color.cyan,
            TurretSpecType.HomingRocket => new Color(1f, 0.5f, 0.1f),
            TurretSpecType.PointDefence => Color.yellow,
            _                           => Color.white,
        };

        int w = spec == TurretSpecType.HomingRocket ? 14 : 8;
        int h = spec == TurretSpecType.HomingRocket ? 6  : 4;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = Sprite.Create(MakeTex(w, h, c), new Rect(0,0,w,h), new Vector2(0f,0.5f), 100f);
        sr.sortingOrder = 3;
    }

    Color TurretColor() => specType switch
    {
        TurretSpecType.Gatling      => new Color(0.7f, 0.7f, 0.75f),
        TurretSpecType.PointDefence => new Color(1f,   0.9f, 0.2f),
        TurretSpecType.Laser        => new Color(0.2f, 0.8f, 1f),
        TurretSpecType.Plasma       => new Color(0.3f, 0.9f, 0.3f),
        TurretSpecType.HomingRocket => new Color(1f,   0.5f, 0.1f),
        _ => baseType switch
        {
            TurretBaseType.Energy  => new Color(0.4f, 0.8f, 0.9f),
            TurretBaseType.Missile => new Color(0.9f, 0.6f, 0.2f),
            _                      => new Color(0.65f, 0.65f, 0.70f),
        }
    };

    void ApplySpecTurnRate()
    {
        turnRate = specType switch
        {
            TurretSpecType.HomingRocket => 90f,
            TurretSpecType.Laser        => 126f,
            _                           => 180f,
        };
    }

    string BuildLabel()
    {
        string baseName = TurretSpecHelper.GetBaseTypeName(baseType);
        if (specType == TurretSpecType.None)
            return baseName;
        return $"{baseName} — {TurretSpecHelper.GetSpecName(specType)}";
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
        baseType       = def.turretBaseType;
        specType       = def.turretSpecType;
        fireRate       = def.turretFireRate       > 0 ? def.turretFireRate       : fireRate;
        damage         = def.turretDamage         > 0 ? def.turretDamage         : damage;
        bulletSpeed    = def.turretBulletSpeed    > 0 ? def.turretBulletSpeed    : bulletSpeed;
        bulletLifeTime = def.turretBulletLifeTime > 0 ? def.turretBulletLifeTime : bulletLifeTime;
        energyPerShot  = def.turretEnergyPerShot  > 0 ? def.turretEnergyPerShot  : energyPerShot;
        magazineSize   = def.turretMagazineSize   > 0 ? def.turretMagazineSize   : magazineSize;
        reloadTime     = def.turretReloadTime     > 0 ? def.turretReloadTime     : reloadTime;

        componentName = BuildLabel();
        _currentMag   = magazineSize;
        ApplySpecTurnRate();
    }
}
