using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turret mermisi.
/// Normal mod: sabit yön, ömür sonunda yok olur.
/// Güdümlü mod (Roket): sınırlı dönüş hızıyla hedefe yönelir — organik yay çizer.
/// Şarapnel modu (Flak): sigorta dolunca ya da ilk hedefte patlar, kıymık saçar.
/// guidedTarget Transform'dur; EnemyBot ve BossShip dahil her hedefi izler.
/// </summary>
public class TurretBullet : MonoBehaviour
{
    public float      damage;
    public float      speed;
    public WeaponType weaponType  = WeaponType.Kinetic;
    public bool       isGuided;
    public Transform  guidedTarget;
    [Tooltip("Saniyede derece — roketin maksimum dönüş hızı.")]
    public float      turnRate    = 120f;
    [Tooltip("0 = vurulabilir değil. Roketler için ayarlanır.")]
    public float      hp         = 0f;

    /// <summary>
    /// Patlama yarıçapı (dünya birimi). 0 = patlamaz, hasar yalnızca çarptığı
    /// hedefe gider.
    ///
    /// Patlayan mermide DOĞRUDAN hasar diye ayrı bir şey YOKTUR: çarptığı hedef
    /// de patlamanın içindedir ve mesafesi ~0 olduğu için tam hasarı zaten alır.
    /// İkisini ayrı uygulamak, merkeze en yakın hedefe iki kez vurmak olurdu.
    /// </summary>
    public float      blastRadius = 0f;

    /// <summary>
    /// Şarapnel kıymığı sayısı. 0 = şarapnel yok. Doluysa mermi alan hasarı
    /// vermez; <see cref="blastRadius"/> kıymığın menzili, <see cref="damage"/>
    /// kıymık başına hasardır (bkz. DamageUtil.Shrapnel).
    /// </summary>
    public int        shrapnel;

    /// <summary>
    /// Sigorta: patlamaya kalan yol (dünya birimi). 0 = sigorta yok, mermi
    /// yalnızca çarpınca patlar. Turret bunu nişan aldığı buluşma noktasına
    /// olan mesafeyle kurar. Kalan SÜRE değil kalan YOL yazılır: mermi
    /// sabit hızla gidiyor ve yol, kayıttan dönüşte de aynı noktayı verir.
    /// </summary>
    public float      fuse;

    /// <summary>
    /// Ateşlendiği andaki kadraj genişliği. Turret kendi nişan alıyor, yani
    /// zoom onun isabetini ETKİLEMEMELİ — alan tam da bunu sınamak için var:
    /// ana silahın isabeti zoom'la düşerken turret'ınki düşmüyorsa, sebep
    /// kadraj değil nişan mekaniğidir. Kontrol grubu.
    /// </summary>
    public float      zoomAtFire;

    /// <summary>
    /// Ömür (sn). Eskiden ateşleyen taraf <c>Destroy(go, ömür)</c> çağırıyordu;
    /// gecikmeli Destroy'un kalan süresi okunamaz, yani uçuştaki mermi
    /// kaydedilemezdi. 0 = sınırsız.
    /// </summary>
    public float      lifeTime;

    /// <summary>
    /// Görselin kaynağı — kayıttan kurulurken aynı sprite'ı çizebilmek için.
    /// ≥ 0: turret uzmanlaşması (TurretSpecType), -1: savaşçı mermisi.
    /// </summary>
    public int        visual;

    Vector2 _dir;
    float   _bornAt;
    bool    _started;

    // Patladı: Destroy kare sonunda işler, aynı karede ikinci bir çarpma
    // (trigger + süpürme) mermiyi iki kez patlatmasın. Kayda girmez — nesne
    // bu kareyi zaten görmeyecek.
    bool    _spent;

    /// <summary>Collider yarıçapı — süpürme mesafesi buna göre uzatılır.</summary>
    const float Radius = 0.07f;

    // Süpürme sonuçları paylaşılan bir tamponda toplanır; her mermi her karede
    // dizi ayırsaydı hızlı ateş eden turretlerde GC yükü olurdu.
    static readonly List<RaycastHit2D> _sweep = new();

    public void TakeDamage(float amount)
    {
        if (hp <= 0f) return;
        hp -= amount;
        if (hp <= 0f) Destroy(gameObject);
    }

    void Awake()
    {
        var col    = gameObject.AddComponent<CircleCollider2D>();
        col.radius    = Radius;
        col.isTrigger = true;

        var rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
    }

    void Start()
    {
        _bornAt  = Time.time;
        _started = true;
        // Şarapnel mermisi ömrü dolunca sessizce kaybolmaz, PATLAR — ömrü
        // Update sayar. Sigorta menzille sınırlı olduğu için bu yol normalde
        // hiç tetiklenmez; emniyettir.
        if (lifeTime > 0f && shrapnel <= 0) Destroy(gameObject, lifeTime);
    }

    public void SetDirection(Vector2 dir)
    {
        _dir = dir.normalized;
        ApplyRotation();
    }

    Transform FindClosestTarget()
    {
        Transform best  = null;
        float     bestD = float.MaxValue;

        foreach (var e in FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
        {
            float d = Vector2.Distance(e.transform.position, transform.position);
            if (d < bestD) { bestD = d; best = e.transform; }
        }

        var boss = FindFirstObjectByType<BossShip>();
        if (boss != null)
        {
            float d = Vector2.Distance(boss.transform.position, transform.position);
            if (d < bestD) best = boss.transform;
        }

        return best;
    }

    void Update()
    {
        if (UpgradeUI.IsPaused) return;
        if (_spent) return;

        if (isGuided)
        {
            if (guidedTarget == null)
                guidedTarget = FindClosestTarget();

            if (guidedTarget != null)
            {
                Vector2 desired = ((Vector2)guidedTarget.position
                                  - (Vector2)transform.position).normalized;
                float maxTurn  = turnRate * Time.deltaTime;
                float angle    = Vector2.SignedAngle(_dir, desired);
                float clamped  = Mathf.Clamp(angle, -maxTurn, maxTurn);
                _dir = Rotate(_dir, clamped).normalized;
                ApplyRotation();
            }
        }

        float step = speed * Time.deltaTime;

        // Sigorta bu karede doluyorsa mermi tam patlama noktasına kadar gider;
        // yolda bir şeye çarparsa orada patlar (Sweep).
        bool fuseDone = fuse > 0f && step >= fuse;
        if (fuseDone) step = fuse;

        if (Sweep(step)) return;

        if (fuse > 0f) fuse -= step;

        bool expired = shrapnel > 0 && lifeTime > 0f && Time.time - _bornAt >= lifeTime;
        if (fuseDone || expired) Detonate(transform.position, null);
    }

    /// <summary>
    /// Yolu SÜPÜREREK ilerler. Mermi Update'te hareket ediyor ama trigger
    /// tespiti fizik adımında (0.02 sn) yapılıyor; hızlı mermi iki adım arasında
    /// hedefin ÜSTÜNDEN atlıyordu.
    ///
    /// Point Defence mermisi (hız 20) fizik adımı başına 0.40 birim gidiyor,
    /// bombayla çakışma penceresi ise (0.07 + 0.10) × 2 = 0.34 birim. Yani
    /// vuruşların çoğu hiç kaydedilmiyor ve PD'nin VARLIK SEBEBİ — bombayı
    /// kalkana varmadan düşürmek — işlemiyordu.
    ///
    /// Çözüm collider'ı şişirmek değil (o, mermiyi her şeye karşı şişmanlatır)
    /// yolun taranmasıdır: mermi ne kadar hızlanırsa hızlansın aradaki her şeyi
    /// görür. OnTriggerEnter2D yerinde kalır — merminin ÜSTÜNE gelen hedefler
    /// için gerekli.
    /// </summary>
    /// <returns>Bir şeye çarpıp yok olduysa true.</returns>
    bool Sweep(float distance)
    {
        if (distance <= 0f) return false;

        int count = Physics2D.Raycast(transform.position, _dir,
                                      ContactFilter2D.noFilter, _sweep, distance + Radius);
        if (count > 0)
        {
            _sweep.Sort((a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < _sweep.Count; i++)
                if (TryHit(_sweep[i].collider, _sweep[i].point)) return true;
        }

        transform.Translate(_dir * distance, Space.World);
        return false;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    void ApplyRotation()
    {
        float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    void OnTriggerEnter2D(Collider2D other) => TryHit(other, transform.position);

    /// <summary>
    /// Bu collider'a vurulabiliyorsa hasarı uygular ve true döner. Hem süpürme
    /// hem trigger yolu buradan geçer — iki ayrı kopya zamanla birbirinden sapardı.
    /// </summary>
    bool TryHit(Collider2D other, Vector2 hitPos)
    {
        if (other == null || _spent) return false;

        var bomb = other.GetComponent<Bomb>();
        if (bomb != null)
        {
            if (shrapnel > 0)     { Detonate(hitPos, other); return true; }
            if (blastRadius > 0f) { Explode(hitPos, other);  return true; }

            bomb.TakeDamage(damage);
            // Bomba tek vuruşta gider: Point Defence'in işini yaptığı görünsün
            HitEffect.SpawnImpact(hitPos, _dir, other.transform.position,
                                  ImpactSurface.Hull, damage, lethal: true);
            Destroy(gameObject);
            return true;
        }

        // Patlayan mermi hedefi AYIRMAZ: çarptığı her şeyde patlar ve hasarı
        // alan hasarı yolundan gider.
        if (shrapnel > 0 && DamageUtil.IsBlastTarget(other))
        {
            Detonate(hitPos, other);
            return true;
        }

        if (blastRadius > 0f && DamageUtil.IsBlastTarget(other))
        {
            Explode(hitPos, other);
            return true;
        }

        var surface = DamageUtil.SurfaceOf(other);

        if (DamageUtil.TryDamage(other, damage, weaponType))
        {
            bool lethal = other.GetComponent<HealthBar>()?.currentHealth <= 0f;

            BalanceLog.Event("shot_hit")
                      .Str("kaynak", "turret")
                      .Str("silah",  weaponType.ToString())
                      .Str("yuzey",  surface.ToString())
                      .Str("hedef",  DamageUtil.TypeNameOf(other))
                      .Num("hasar",  damage)
                      .Num("zoom",   zoomAtFire)
                      .Bool("oldurdu", lethal)
                      .End();

            HitEffect.SpawnImpact(hitPos, _dir, other.transform.position,
                                  surface, damage, lethal);
            if (surface == ImpactSurface.Shield)
                DamageUtil.ShieldFlash(other, hitPos);
            Destroy(gameObject);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Patlar: yarıçaptaki her hedefe hasar uygular ve mermiyi yok eder.
    ///
    /// LOG'A TEK BİR İSABET YAZILIR, kaç hedef yakaladığı ayrı bir alanda.
    /// İsabet oranı <c>shot_hit / shot_fired</c> olarak hesaplanıyor; sekiz
    /// gemi yakalayan bir flak mermisi sekiz satır yazsaydı oran %100'ü aşar ve
    /// metrik sessizce anlamsızlaşırdı. "Atışım tuttu mu" ile "kaç tanesini
    /// yakaladı" iki ayrı sorudur.
    /// </summary>
    void Explode(Vector2 at, Collider2D primary)
    {
        int caught = DamageUtil.AreaDamage(at, blastRadius, damage, weaponType);

        var surface = DamageUtil.SurfaceOf(primary);
        BalanceLog.Event("shot_hit")
                  .Str("kaynak", "turret")
                  .Str("silah",  weaponType.ToString())
                  .Str("yuzey",  surface.ToString())
                  .Str("hedef",  DamageUtil.TypeNameOf(primary))
                  .Num("hasar",  damage)
                  .Num("zoom",   zoomAtFire)
                  .Num("yakalanan", caught)
                  .Bool("oldurdu", false)
                  .End();

        HitEffect.SpawnBlast(at, blastRadius, damage);
        Destroy(gameObject);
    }

    // Patlama noktasını içine alan collider'ı bulmak için paylaşılan tampon.
    static readonly List<Collider2D> _inside = new();

    /// <summary>
    /// Şarapnel patlaması. <paramref name="primary"/> çarpılan hedeftir; null
    /// ise mermi sigortayla HAVADA patlamıştır. Havada patlayan mermi bir
    /// geminin içindeyse o da doğrudan isabet sayılır — yoksa içeriden saçılan
    /// kıymıklar o gemiyi hiç görmezdi.
    ///
    /// Log'a tek satır yazılır, yalnızca en az bir hedef yakalandıysa: hiçbir
    /// şeye değmeyen havada patlama bir ISKALAMADIR, isabet oranı bunu
    /// göstermeli.
    /// </summary>
    void Detonate(Vector2 at, Collider2D primary)
    {
        if (_spent) return;
        _spent = true;

        if (primary == null)
        {
            _inside.Clear();
            Physics2D.OverlapPoint(at, ContactFilter2D.noFilter, _inside);
            foreach (var col in _inside)
                if (DamageUtil.IsBlastTarget(col)) { primary = col; break; }
        }

        var surface = primary != null ? DamageUtil.SurfaceOf(primary) : ImpactSurface.Hull;
        var r = DamageUtil.Shrapnel(at, _dir, primary, shrapnel, blastRadius, damage, weaponType);

        if (r.caught > 0)
            BalanceLog.Event("shot_hit")
                      .Str("kaynak", "turret")
                      .Str("silah",  weaponType.ToString())
                      .Str("yuzey",  surface.ToString())
                      .Str("hedef",  primary != null ? DamageUtil.TypeNameOf(primary) : "havada")
                      .Num("hasar",  damage)
                      .Num("zoom",   zoomAtFire)
                      .Num("yakalanan", r.caught)
                      .Num("kiymik",    r.fragments)
                      .Bool("oldurdu", false)
                      .End();

        Destroy(gameObject);
    }

    // ── Kayıt ─────────────────────────────────────────────────────────────────

    float LifeLeft => !_started || lifeTime <= 0f
        ? lifeTime
        : Mathf.Max(0.01f, lifeTime - (Time.time - _bornAt));

    public TurretBulletState CaptureState() => new TurretBulletState
    {
        pos        = transform.position,
        dir        = _dir,
        speed      = speed,
        damage     = damage,
        turnRate   = turnRate,
        hp         = hp,
        blast      = blastRadius,
        shrapnel   = shrapnel,
        fuse       = fuse,
        zoom       = zoomAtFire,
        life       = LifeLeft,
        weaponType = (int)weaponType,
        visual     = visual,
        guided     = isGuided,
        target     = WorldSave.RefOf(guidedTarget),
    };

    public static TurretBullet Rebuild(TurretBulletState s)
    {
        var go = new GameObject(s.visual < 0 ? "FighterBullet" : "TurretBullet");
        go.transform.position = s.pos;

        var tb = go.AddComponent<TurretBullet>();
        tb.damage       = s.damage;
        tb.speed        = s.speed;
        tb.weaponType   = (WeaponType)s.weaponType;
        tb.isGuided     = s.guided;
        tb.guidedTarget = WorldSave.ResolveTransform(s.target);
        tb.turnRate     = s.turnRate;
        tb.hp           = s.hp;
        tb.blastRadius  = s.blast;
        tb.shrapnel     = s.shrapnel;
        tb.fuse         = s.fuse;
        tb.zoomAtFire   = s.zoom;
        tb.lifeTime     = s.life;
        tb.visual       = s.visual;
        tb.SetDirection(s.dir);

        if (s.visual < 0) FighterShip.BuildBulletVisual(go, s.damage);
        else              TurretController.BuildBulletVisual(go, (TurretSpecType)s.visual, s.damage);
        return tb;
    }
}
