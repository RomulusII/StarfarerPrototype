using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data-driven düşman gemisi. Tüm istatistikler ve davranış parametreleri
/// EnemyTypeData ScriptableObject'inden okunur — yeni tip eklemek kod değişikliği gerektirmez.
///
/// data null ise EnemySpawner tarafından atanmadan önce Start() bekler;
/// null kalırsa CreateSwarm() ile fallback oluşturulur.
///
/// ÇARPIŞMA HASARI YOKTUR — düşmanlar ana geminin üstünden geçer. Ana gemi
/// kaçamıyor, dolayısıyla temas hasarı kaçınılamaz bir sızıntı olurdu:
/// oyuncunun hiçbir kararı onu engelleyemezdi. Tehdit menzilli silahlardan
/// gelir. (Asteroitler ayrı: onlar VURULABİLİR, yani çarpmaları engellenebilir
/// bir olaydır ve hasar verirler.)
/// </summary>
public class EnemyBot : MonoBehaviour, ITurretTarget
{
    public EnemyTypeData data;

    PlayerShip   _playerShip;
    HealthBar    _healthBar;
    ShipMovement _movement;
    ShipBrain    _brain;

    // Kalkan
    float         _shieldHP;
    float         _maxShieldHP;
    GameObject    _shieldVisual;   // küresel kalkan kabuğu
    float         _shieldRadius;   // kabuğun dünya yarıçapı — çarpma parlaması buna oturur
    BarrierShield _barrier;        // yönlü yay kalkanı (Bariyer tipi)
    float         _shieldRechargeTimer;

    // Siper (Screen) durum makinesi
    // Manevra iticileri sayesinde "yaklaşma" ile "tutma" arasında bir fark
    // kalmadı: gemi her iki durumda da yuvasına doğru süzülüyor ve varınca
    // MoveToward zaten frenliyor. Ayrı bir Holding durumu hiçbir şey yapmıyordu.
    enum ScreenPhase { Guarding, Retreating, Leaving }
    ScreenPhase _screenPhase;

    // Korunacak yön: ana gemiden korunan filonun ağırlık merkezine. Sahne
    // taraması pahalı olduğu için aralıklarla tazelenir.
    Vector2 _guardDir = Vector2.right;
    float   _guardScanTimer;

    // Yuvanın koruma eksenine DİK kayması — sabit pay + yavaş salınım
    float _screenLateral;
    float _screenSwayPhase;

    /// <summary>Çekildikten sonra geri dönmek için gereken kalkan oranı.</summary>
    const float ScreenReturnRatio = 0.9f;

    /// <summary>Çekilirken bu mesafeye ulaşınca durup şarj bekler.</summary>
    const float ScreenRetreatDistance = 13f;

    const float GuardScanInterval   = 0.5f;
    const float ScreenSwayAmplitude = 1.6f;

    // Periyot geminin HIZ BÜTÇESİNDEN türer. 1.6 birimlik genlikte 7 sn'lik bir
    // periyot tepe noktada 1.44 birim/sn ister; siperin max hızı 1.5, yani gemi
    // salınımı kovalarken koruma eksenini takip edecek gücü kalmazdı ve strafe
    // 'yavaş' değil sendeleyerek görünürdü. 10 sn'de tepe hız 1.01, bütçenin
    // üçte ikisi.
    const float ScreenSwayPeriod    = 10f;

    // Ateş etme
    float _fireTimer;
    float _fireRateBase;

    // Namlu + dolum göstergesi
    Transform      _barrelTransform;
    Transform      _reloadFillTransform;
    SpriteRenderer _reloadFillSR;
    bool           _fireFlash;

    // Formasyon — dalga hâlinde gelirken grup yuvasını tutar, çapa oyuncuya
    // yaklaşınca grup dağılır ve gemi kendi taktik AI'sına döner.
    FormationGroup _formation;

    // Hedef tarama
    float _targetScanTimer;

    // Hareket hedefi (_brain) ile ateş hedefi AYRI tutulur. Ağır bir gemi ana
    // gemiye yönelmeye devam ederken, menziline giren bir savaşçıya ateş
    // edebilmeli — ama onun peşine düşmemeli.
    Transform _fireTarget;
    float     _fireScanTimer;

    // Ateş hedefi sahne taraması gerektirir; her karede yapılmaz. 0.25 sn,
    // en hızlı ateş eden tipin (Sülük, 1.4 sn) çok altında kalır — yani
    // hedef seçimi hiçbir atışı geciktirmez.
    const float FireScanInterval = 0.25f;

    // Approach (Bomber tipi) state machine
    enum ApproachPhase { Approaching, Hovering, Retreating }
    enum ArPhase { Engaging, Disengaging, Braking }
    ApproachPhase _approachPhase;
    float         _approachFireTimer;
    int           _approachShotsLeft;
    Vector2       _approachHoverPos;
    Vector2       _approachEscapeDir = Vector2.right;
    const float   ApproachHoverX  = 2.0f;
    const int     ApproachShotMax = 3;

    // BombRun state
    float       _bombRunFireTimer;
    const float BombRunSpeed = 1.5f;

    // AttackRun state — hareket ShipMovement uçuş modeline devredilmiştir
    float             _arDisengageTimer;
    float             _arEscapeAngle;
    ArPhase           _arPhase;
    ShipComponentBase _arTarget;
    const float ArFireAngle     = 20f;   // ateş için max açı sapması
    const float ArDisengageTime = 1.0f;  // ateş sonrası düz uçuş süresi (sn)
    const float ArAimSpeed      = 0.4f;  // saldırıya geçmek için max hız
    const float ArAimAngle      = 12f;   // saldırıya geçmek için max açı hatası
    const float ArAimDistanceFactor = 2f; // fireRange × bu mesafede nişana geçer, salınım kesilir
    const float ArEscapeFactor       = 1.25f; // escapeAngle × bu = AttackRun kaçış sapması
    const float ApproachEscapeFactor = 1.1f;  // escapeAngle × bu = Approach geri çekilme sapması

    // Velocity tracking
    Vector2        _prevPos;
    public Vector2 Velocity { get; private set; }

    // Spawn anındaki burun yönü — InitMovement belirler, Initialize uygular
    float _initialFacing = 180f;

    // Kaçış tarafı her sortide değişir — deterministik imza manevrası
    int _escapeSide = 1;

    // Kalkan kapasitesinin kaçta kaçı kristal olarak düşer
    const float CrystalPerShieldPoint = 0.1f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        var col        = gameObject.AddComponent<BoxCollider2D>();
        col.size       = new Vector2(0.6f, 0.4f);
        var rb         = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType    = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        _movement      = gameObject.AddComponent<ShipMovement>();
    }

    void Start()
    {
        if (data == null)
            data = EnemyTypeData.CreateSwarm();

        _healthBar  = GetComponent<HealthBar>();
        _playerShip = FindFirstObjectByType<PlayerShip>();

        _movement.mass        = data.mass;
        _movement.enginePower = data.enginePower;
        _movement.agility     = data.agility;
        _movement.grip        = data.grip;
        _movement.wanderAngle  = data.evasionAngle;
        _movement.wanderPeriod = data.evasionPeriod;

        ApplyStats();

        if (data.maxShield > 0f)
            InitShield();

        _fireRateBase = data.fireRate;
        _fireTimer    = Random.Range(0f, _fireRateBase);
        _escapeSide   = Random.Range(0, 2) == 0 ? 1 : -1;   // yalnızca başlangıç tarafı
        _prevPos      = transform.position;

        InitMovement();

        bool needsBarrel = data.weaponKind != EnemyWeaponKind.None
                        && data.weaponKind != EnemyWeaponKind.ComponentBurst
                        && data.movementKind != EnemyMovementKind.Approach
                        && data.movementKind != EnemyMovementKind.BombRun
                        && data.movementKind != EnemyMovementKind.AttackRun;
        if (needsBarrel)
            BuildBarrel(data.barrelColor);

        _movement.Initialize(_initialFacing);
    }

    void ApplyStats()
    {
        if (_healthBar != null)
        {
            _healthBar.maxHealth     = data.maxHP;
            _healthBar.currentHealth = data.maxHP;
            _healthBar.barWidth      = data.bodyWidth  / 100f * 1.3f;
            _healthBar.barOffsetY    = data.bodyHeight / 100f * 0.8f;
        }

        // Hitbox sprite siluetinden TÜRER — bağımsız değil, kasten daraltılmış
        // türevidir (SkinEntry.hitboxScale). Skin yoksa aşağıdaki kutuya düşer ve
        // bu oturumda konan denge sayıları aynen korunur: skin ile hitbox birlikte
        // açılıp kapanır, yarı yolda kalmış bir durum oluşmaz.
        if (!SkinLibrary.TryApplyCollider(gameObject, data.SkinId))
        {
            GetComponent<BoxCollider2D>().size = new Vector2(
                data.EffectiveHitboxWidth  / 100f,
                data.EffectiveHitboxHeight / 100f);
        }

        BuildBody(data.bodyWidth, data.bodyHeight, data.bodyColor);
    }

    void InitShield()
    {
        _shieldHP = _maxShieldHP = data.maxShield;

        if (data.HasDirectionalShield)
            _barrier = BarrierShield.Attach(this, data.shieldArcRadius, data.shieldArcDegrees);
        else
            BuildShieldVisual(data.bodyWidth + 20, data.bodyHeight + 15);

        if (_healthBar != null)
        {
            _healthBar.maxShield     = _maxShieldHP;
            _healthBar.currentShield = _shieldHP;
        }
    }

    void InitMovement()
    {
        switch (data.movementKind)
        {
            case EnemyMovementKind.Charge:
                SetupBrain(CombatPattern.Orbit,
                    data.engageRange, data.fireRange, data.orbitRadius, data.engageDuration);
                break;

            case EnemyMovementKind.HoverFire:
                SetupBrain(CombatPattern.HoverFire,
                    data.engageRange, data.fireRange, data.orbitRadius, data.engageDuration);
                break;

            case EnemyMovementKind.Approach:
                _approachPhase     = ApproachPhase.Approaching;
                _approachFireTimer = 1.2f;
                _approachShotsLeft = ApproachShotMax;
                _approachHoverPos  = new Vector2(ApproachHoverX, transform.position.y);
                break;

            case EnemyMovementKind.Strafe:
                SetupBrain(CombatPattern.Strafe,
                    data.engageRange, data.fireRange, data.orbitRadius, data.engageDuration);
                break;

            case EnemyMovementKind.Stationary:
                break;

            case EnemyMovementKind.Screen:
                // ShipBrain KURULMAZ: siper gemisinin taktiği yörünge/dalış
                // değil, tek bir noktayı tutmaktır. _initialFacing 180'de kalır,
                // yani burun (ve yay kalkanı) daha doğduğu an oyuncuya dönüktür.
                //
                // MANEVRA İTİCİLERİ: roket modelinde gemi yalnızca burnu
                // doğrultusunda itebilir, yani yana kaymak için burnunu çevirmesi
                // gerekir — ve o an yay kalkanı oyuncudan kayardı. Siper gemisi
                // burnunu hedefte tutup yana süzülebilmeli; kalkanı yönlü olan
                // bir gemi için bu bir süs değil, işleyişinin ön koşulu.
                _movement.omniThrust = true;
                //
                // Faz ve yanal pay gemi başına ayrılır: aynı dalgada iki siper
                // varsa aynı noktayı paylaşmasınlar ve senkron salınmasınlar.
                _screenSwayPhase = Random.Range(0f, Mathf.PI * 2f);
                _screenLateral   = Random.Range(-1.5f, 1.5f);
                break;

            case EnemyMovementKind.BombRun:
                _bombRunFireTimer = _fireRateBase * 0.5f;
                break;

            case EnemyMovementKind.AttackRun:
                _arTarget         = PickComponentTarget();
                Vector2 initDir   = _arTarget != null
                    ? ((Vector2)_arTarget.transform.position - (Vector2)transform.position).normalized
                    : Vector2.left;
                _initialFacing    = Mathf.Atan2(initDir.y, initDir.x) * Mathf.Rad2Deg;
                _arPhase          = ArPhase.Engaging;
                _arDisengageTimer = 0f;
                break;
        }
    }

    void SetupBrain(CombatPattern pattern, float engageRange, float fireRange,
                    float orbitRadius, float engageDuration)
    {
        _brain                 = gameObject.AddComponent<ShipBrain>();
        _brain.pattern         = pattern;
        _brain.engageRange     = engageRange;
        _brain.fireRange       = fireRange;
        _brain.orbitRadius     = orbitRadius;
        _brain.engageDuration  = engageDuration;
        _brain.repositionDelay = 1f;
        _brain.escapeAngle     = data.escapeAngle;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Formasyon grubuna katılır. Grup dağılana kadar HAREKET grubun; tipe özgü
    /// davranış (yörünge, dalış, bomba koşusu) ancak dağılınca devreye girer.
    /// </summary>
    public void AssignFormation(FormationGroup group) => _formation = group;

    /// <summary>Grup hızını belirlemek için: bu geminin seyir hızı.</summary>
    public float CruiseSpeed => _movement != null ? _movement.MaxSpeed
                              : (data != null ? data.enginePower / Mathf.Max(data.mass, 0.01f) : 1f);

    void Update()
    {
        if (UpgradeUI.IsPaused) return;

        if (Time.deltaTime > 0f)
            Velocity = ((Vector2)transform.position - _prevPos) / Time.deltaTime;
        _prevPos = transform.position;

        UpdateSpecialBehaviours();

        if (_formation != null)
        {
            if (_formation.Active) { UpdateFormationFlight(); return; }
            _formation = null;   // grup dağıldı, kendi AI'na dön
        }

        if (data.movementKind == EnemyMovementKind.Approach)
        {
            UpdateApproach();
            return;
        }

        if (data.movementKind == EnemyMovementKind.BombRun)
        {
            UpdateBombRun();
            return;
        }

        if (data.movementKind == EnemyMovementKind.AttackRun)
        {
            UpdateAttackRun();
            return;
        }

        if (data.movementKind == EnemyMovementKind.Screen)
        {
            UpdateScreen();
            return;
        }

        _targetScanTimer -= Time.deltaTime;
        if (_targetScanTimer <= 0f || (_brain != null && !_brain.HasTarget))
        {
            _targetScanTimer = 1.5f;
            var threat = FindClosestThreat();
            if (threat != null) _brain?.SetTarget(threat);
        }

        // Tarama YALNIZCA sayaçla yapılır. "_fireTarget == null ise hemen tekrar
        // tara" eklemek cazip ama tuzak: menzilde hiçbir şey olmaması normal
        // durumdur ve o hâlde her düşman her karede sahneyi taramaya başlar.
        _fireScanTimer -= Time.deltaTime;
        if (_fireScanTimer <= 0f)
        {
            _fireScanTimer = FireScanInterval;
            _fireTarget    = SelectFireTarget();
        }

        _fireTimer -= Time.deltaTime;
        if (_fireTarget != null && _fireTimer <= 0f)
        {
            _fireTimer = _fireRateBase;
            FireAtTarget();
        }

        UpdateBarrel();

        if (data.maxShield > 0f)
            UpdateShieldRecharge();

        if (Vector2.Distance(transform.position, Vector2.zero) > ViewBounds.DespawnRadius)
            Destroy(gameObject);
    }

    /// <summary>
    /// HAREKET hedefi. Yalnızca kıvrak ve hafif gemiler savaşçı kovalar
    /// (bkz. EnemyTypeData.PursuesFighters); ağır olanlar ana gemide kalır.
    ///
    /// Eskiden burada tip ayrımı yoktu ve en yakın tehdit ne ise ona kilitleniliyordu.
    /// Bir avuç savaşçı, bir Kaleci'yi sahnenin dışına kadar çekebiliyordu:
    /// düşman gemisi kavis üstüne kavis çizip ana gemiden uzaklaşıyor, oyuncu
    /// hiç baskı hissetmiyordu.
    /// </summary>
    /// <summary>
    /// Grup yuvasını tut. Salınım KAPALI: kaçamak manevra formasyonu bozar ve
    /// düzenli gelen bir filo, dağınık gelen bir sürüden çok daha tehditkâr
    /// görünür. Silahı uygun olan menzile giren hedefe ateş etmeye devam eder.
    /// </summary>
    void UpdateFormationFlight()
    {
        _movement.MoveToward(_formation.SlotOf(this));

        // Bomba ve komponent burst'ü yaklaşma sırasında anlamsız — onlar
        // tipin kendi davranışına ait, formasyon dağılınca başlar.
        bool canFire = data.weaponKind != EnemyWeaponKind.None
                    && data.weaponKind != EnemyWeaponKind.ComponentBurst;

        if (canFire)
        {
            _fireScanTimer -= Time.deltaTime;
            if (_fireScanTimer <= 0f)
            {
                _fireScanTimer = FireScanInterval;
                _fireTarget    = SelectFireTarget();
            }

            _fireTimer -= Time.deltaTime;
            if (_fireTarget != null && _fireTimer <= 0f)
            {
                _fireTimer = _fireRateBase;
                FireAtTarget();
            }
        }

        UpdateBarrel();
        if (data.maxShield > 0f) UpdateShieldRecharge();
    }

    Transform FindClosestThreat()
    {
        Transform ship = _playerShip != null ? _playerShip.transform : null;
        if (data == null || !data.PursuesFighters) return ship;

        Transform best  = ship;
        float     bestD = ship != null
            ? Vector2.Distance(transform.position, ship.position)
            : float.MaxValue;

        foreach (var f in FindObjectsByType<FighterShip>(FindObjectsSortMode.None))
        {
            float d = Vector2.Distance(transform.position, f.transform.position);
            if (d < bestD) { bestD = d; best = f.transform; }
        }

        return best;
    }

    /// <summary>
    /// ATEŞ hedefi. Ana hedef menzildeyse odur; silahı küçük hedefe uygunsa
    /// (bkz. EnemyTypeData.CanEngageFighters) menzildeki daha yakın bir savaşçı
    /// önceliği alır.
    ///
    /// Kovalamamak ile ateş etmemek AYRI şeyler: hantal bir lazer gemisi
    /// dibindeki avcıyı görmezden gelmemeli, ama peşinden de gitmemeli. Öte
    /// yandan bir bombardıman topunu tek bir avcıya harcamak, o atışın ana
    /// gemiye gitmemesi demektir — o yüzden ağır silahlar savaşçıyı hiç
    /// hedeflemez.
    /// </summary>
    Transform SelectFireTarget()
    {
        float     range = data != null ? data.fireRange : 0f;
        Transform best  = null;
        float     bestD = range;

        var brainTarget = _brain != null ? _brain.TargetTransform : null;
        if (brainTarget != null)
        {
            float d = Vector2.Distance(transform.position, brainTarget.position);
            if (d <= bestD) { bestD = d; best = brainTarget; }
        }

        if (data != null && data.CanEngageFighters)
        {
            foreach (var f in FindObjectsByType<FighterShip>(FindObjectsSortMode.None))
            {
                float d = Vector2.Distance(transform.position, f.transform.position);
                if (d <= bestD) { bestD = d; best = f.transform; }
            }
        }

        return best;
    }

    void UpdateApproach()
    {
        switch (_approachPhase)
        {
            case ApproachPhase.Approaching:
                _movement.MoveToward(_approachHoverPos, evasive: true);
                if (_movement.IsNear(_approachHoverPos, 0.2f))
                {
                    _approachPhase     = ApproachPhase.Hovering;
                    _approachFireTimer = 1.2f;
                }
                break;

            case ApproachPhase.Hovering:
                _movement.Brake();
                _approachFireTimer -= Time.deltaTime;
                if (_approachFireTimer <= 0f)
                {
                    FireAtComponent();
                    _approachShotsLeft--;
                    _approachFireTimer = _fireRateBase;
                    if (_approachShotsLeft <= 0)
                    {
                        _approachPhase = ApproachPhase.Retreating;
                        // Düz geri değil, sabit açıyla çapraz kaç; taraf sortiden
                        // sortiye değişir — kalıp öğrenilebilir kalsın
                        _escapeSide = -_escapeSide;
                        _approachEscapeDir = ShipMovement.Rotate(
                            Vector2.right, data.escapeAngle * ApproachEscapeFactor * _escapeSide);
                    }
                }
                break;

            case ApproachPhase.Retreating:
                _movement.MoveInDirection(_approachEscapeDir, evasive: true);
                break;
        }

        // Sabit 20 sınırı, ViewBounds.SpawnX'in (~35) çok altındaydı: Approach
        // tipi düşman doğduğu KARE yok oluyordu.
        float x = transform.position.x;
        if (x < ViewBounds.DespawnX || x > ViewBounds.SpawnX + ViewBounds.SpawnMargin)
            Destroy(gameObject);
    }

    void UpdateBombRun()
    {
        transform.Translate(Vector2.left * BombRunSpeed * Time.deltaTime, Space.World);

        _bombRunFireTimer -= Time.deltaTime;
        if (_bombRunFireTimer <= 0f)
        {
            _bombRunFireTimer = _fireRateBase;
            DropBomb();
        }

        if (transform.position.x < ViewBounds.DespawnX) Destroy(gameObject);
    }

    /// <summary>
    /// Saldırı sortisi: hedefe dalış → ateş → kaçış → fren + yeni hedefe nişan.
    /// Hareket tamamen ShipMovement uçuş modeline aittir; burada sadece
    /// hangi yöne bakılacağı ve gaz durumu belirlenir.
    /// </summary>
    void UpdateAttackRun()
    {
        // Hedef component geçersizse yenisini seç
        if (_arTarget == null || !_arTarget.IsOperational)
            _arTarget = PickComponentTarget();
        if (_arTarget == null)
        {
            // Operasyonel komponent yok — motor kapalı süzül, hedef bekle
            _movement.Coast();
            if (Vector2.Distance(transform.position, Vector2.zero) > 30f)
                Destroy(gameObject);
            return;
        }

        Vector2 toTarget  = (Vector2)_arTarget.transform.position - (Vector2)transform.position;
        float   angleDiff = _movement.HeadingErrorTo(toTarget);
        float   dist      = toTarget.magnitude;

        switch (_arPhase)
        {
            case ArPhase.Engaging:
            {
                // Burnu hedefe çevir, tam gaz dal. Burun sapmışsa uçuş modeli
                // gazı zaten kısar — gemi önce toparlanır, sonra ivmelenir.
                // Nişan mesafesine girene kadar kaçamak salınım açık kalır.
                bool lining = dist <= data.fireRange * ArAimDistanceFactor;
                _movement.MoveInDirection(toTarget, evasive: !lining);

                _fireTimer -= Time.deltaTime;
                if (dist <= data.fireRange && angleDiff <= ArFireAngle && _fireTimer <= 0f)
                {
                    _fireTimer        = _fireRateBase;
                    _arPhase          = ArPhase.Disengaging;
                    _arDisengageTimer = ArDisengageTime;

                    // Kaçış açısını kaydet — Disengaging fazında bu açıya dönülür
                    // İmza kaçışı: sabit açı, her sortide ters taraf
                    _escapeSide    = -_escapeSide;
                    _arEscapeAngle = _movement.FacingAngle
                                   + data.escapeAngle * ArEscapeFactor * _escapeSide;

                    var go = new GameObject("EnemyBullet_Comp");
                    go.transform.position = transform.position;
                    var eb                = go.AddComponent<EnemyBullet>();
                    eb.damage             = data.fireDamage;
                    eb.speed              = data.bulletSpeed;
                    eb.targetComponent    = _arTarget;
                }
                break;
            }

            case ArPhase.Disengaging:
            {
                // Kaçış açısına dön ve gazla uzaklaş — hızlı olduğu için geniş kavis
                float   rad     = _arEscapeAngle * Mathf.Deg2Rad;
                Vector2 escape  = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                _movement.MoveInDirection(escape, evasive: true);

                _arDisengageTimer -= Time.deltaTime;
                if (_arDisengageTimer <= 0f)
                {
                    _arPhase  = ArPhase.Braking;
                    _arTarget = PickComponentTarget(); // bir sonraki saldırı için yeni hedef
                }
                break;
            }

            case ArPhase.Braking:
            {
                // Burun yeni hedefte, retro ile yavaşla. Hız düştükçe dönüş hızı
                // arttığı için gemi burada dar kavis çizip nişan alabilir.
                _movement.FaceAndBrake(toTarget);

                if (_movement.Speed <= ArAimSpeed && angleDiff <= ArAimAngle)
                {
                    _arPhase   = ArPhase.Engaging;
                    _fireTimer = 0f; // her saldırı sortisi ateşe hazır başlar
                }
                break;
            }
        }

        if (Vector2.Distance(transform.position, Vector2.zero) > ViewBounds.DespawnRadius)
            Destroy(gameObject);
    }

    /// <summary>
    /// Siper manevrası: ana gemi ile KORUNAN FİLO arasına geç, dur, bekle.
    /// Kalkan bitince çekil, dolunca geri gel.
    ///
    /// Burun DAİMA oyuncuya dönüktür — yay kalkanı geminin +X yönünde durduğu
    /// için burnun yönü kalkanın yönüdür. Çekilirken bile burun geride kalır ve
    /// gemi retro itkiyle uzaklaşır: sırtını dönseydi kalkan işe yaramaz olur ve
    /// çekilme bir ölüm cezasına dönerdi.
    ///
    /// Bu tip HİÇ ateş etmez; tehdidi tamamen oyuncunun ateş hattını kapatmasıdır.
    /// </summary>
    void UpdateScreen()
    {
        Vector2 self    = transform.position;
        Vector2 shipPos = _playerShip != null ? (Vector2)_playerShip.transform.position : Vector2.zero;
        Vector2 toShip  = shipPos - self;

        UpdateShieldRecharge();

        // Burun (ve yay kalkanı) HER DURUMDA oyuncuda; çekilirken de.
        _movement.AimAt(toShip);

        bool depleted  = _maxShieldHP > 0f && _shieldHP <= 0f;
        bool recovered = _maxShieldHP > 0f && _shieldHP >= _maxShieldHP * ScreenReturnRatio;

        switch (_screenPhase)
        {
            case ScreenPhase.Guarding:
            {
                // Yuva yavaşça kayar (strafe) ve korunan filo hareket ettikçe
                // koruma ekseni döner; gemi yana süzülerek yuvasını takip eder.
                // Burun AimAt ile hep oyuncuda — kalkan hattı hiç açılmaz.
                _movement.MoveToward(ScreenHoldPosition(shipPos));

                if (depleted) _screenPhase = ScreenPhase.Retreating;
                break;
            }

            case ScreenPhase.Retreating:
            {
                // Yeterince uzaklaştıysa dur ve şarj ol; yoksa uzaklaşmaya devam.
                // Burun oyuncuda kaldığı için bu bir GERİ ÇEKİLME, kaçış değil.
                if (toShip.magnitude < ScreenRetreatDistance)
                    _movement.MoveInDirection(-toShip);   // burun oyuncuda, itki geriye
                else
                    _movement.Brake();

                if (recovered)
                {
                    // Dönerken yeni bir yanal pay: hep aynı noktaya dönmek
                    // oyuncuya bedava bir nişan hattı verirdi
                    _screenLateral = Random.Range(-1.5f, 1.5f);
                    _screenPhase   = ScreenPhase.Guarding;
                }
                break;
            }

            case ScreenPhase.Leaving:
            {
                // Koruyacak filo kalmadı — sahneden çekil (bkz. Withdraw)
                _movement.MoveInDirection(Vector2.right);
                break;
            }
        }

        if (Vector2.Distance(transform.position, Vector2.zero) > ViewBounds.DespawnRadius)
            Destroy(gameObject);
    }

    /// <summary>
    /// Siperin tutmaya çalıştığı nokta: ana gemi ile korunan filonun arasında,
    /// gemiden <c>engageRange</c> kadar uzakta.
    ///
    /// Sabit "geminin sağı" değil: siperin işi kendini korumak değil,
    /// ARKASINDAKİLERİ korumak. Filo yukarıdan geliyorsa siper de yukarı
    /// kayar, yoksa oyuncunun ateş hattı zaten açık kalır ve gemi bir işe
    /// yaramaz.
    ///
    /// Üstüne yavaş bir yanal salınım biner. Salınım DETERMİNİSTİKTİR (sinüs) —
    /// oyunun geri kalanındaki kaçamak manevralarla aynı gerekçe: oyuncu deseni
    /// öğrenip önünü kesebilmeli.
    /// </summary>
    Vector2 ScreenHoldPosition(Vector2 shipPos)
    {
        _guardScanTimer -= Time.deltaTime;
        if (_guardScanTimer <= 0f)
        {
            _guardScanTimer = GuardScanInterval;
            _guardDir       = GuardDirection(shipPos);
        }

        Vector2 lateral = new Vector2(-_guardDir.y, _guardDir.x);
        float   sway    = Mathf.Sin(Time.time * (Mathf.PI * 2f / ScreenSwayPeriod) + _screenSwayPhase);

        return shipPos + _guardDir * data.engageRange
                       + lateral  * (_screenLateral + sway * ScreenSwayAmplitude);
    }

    /// <summary>
    /// Ana gemiden korunan filonun ağırlık merkezine bakan birim vektör.
    /// Diğer siperler sayılmaz — siper sipere siper olmaz.
    /// Korunacak kimse yoksa geminin tam önü.
    /// </summary>
    Vector2 GuardDirection(Vector2 shipPos)
    {
        Vector2 sum = Vector2.zero;
        int     n   = 0;

        foreach (var e in FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
        {
            if (e == this || e.data == null) continue;
            if (e.data.role == EnemyRole.Barrier) continue;
            sum += (Vector2)e.transform.position;
            n++;
        }

        if (n == 0) return Vector2.right;

        Vector2 dir = (sum / n) - shipPos;
        return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
    }

    /// <summary>
    /// Sahneden çekil. ChapterManager, dalgada tehdit üreten kimse kalmayınca
    /// çağırır: koruyacak filo yoksa siperin sahnede durmasının anlamı yok ve
    /// dalga dalga birikip oyuncunun ateş hattını kalıcı olarak kapatırlardı.
    /// </summary>
    public void Withdraw()
    {
        if (data == null || data.movementKind != EnemyMovementKind.Screen) return;
        _screenPhase = ScreenPhase.Leaving;
    }

    void DropBomb()
    {
        var go = new GameObject("Bomb");
        go.transform.position = transform.position;

        var bomb   = go.AddComponent<Bomb>();
        bomb.damage = data.fireDamage;
        bomb.speed  = data.bulletSpeed;

        Vector2 dir = _playerShip != null
            ? ((Vector2)_playerShip.transform.position - (Vector2)transform.position).normalized
            : Vector2.left;
        bomb.SetDirection(dir);
    }

    // ── Ateş etme ─────────────────────────────────────────────────────────────

    void UpdateBarrel()
    {
        if (_barrelTransform == null) return;

        if (_fireFlash)
        {
            _fireFlash = false;
            if (_reloadFillSR != null)
                _reloadFillSR.color = ReloadColor(0f);
            if (_reloadFillTransform != null)
                _reloadFillTransform.localScale = new Vector3(0f, 1f, 1f);
        }

        Transform target = _fireTarget != null ? _fireTarget : _brain?.TargetTransform;
        if (target != null)
        {
            var   dir   = (target.position - _barrelTransform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            _barrelTransform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
        else
        {
            _barrelTransform.rotation = transform.rotation;
        }

        if (_reloadFillTransform != null && !_fireFlash)
        {
            float ratio = _fireRateBase > 0f ? 1f - Mathf.Clamp01(_fireTimer / _fireRateBase) : 1f;
            _reloadFillTransform.localScale = new Vector3(ratio, 1f, 1f);
            if (_reloadFillSR != null) _reloadFillSR.color = ReloadColor(ratio);
        }
    }

    static Color ReloadColor(float t) =>
        new Color(1f, Mathf.Lerp(0.1f, 0.55f, t), 0f, Mathf.Lerp(0.1f, 0.65f, t));

    void FireAtTarget()
    {
        Transform t = _fireTarget ?? _brain?.TargetTransform ?? _playerShip?.transform;
        if (t == null) return;

        var dir = (t.position - transform.position).normalized;

        if (data.weaponKind == EnemyWeaponKind.Laser)
        {
            FireLaserBeam(dir);
        }
        else
        {
            var spawnPos = _barrelTransform != null
                ? _barrelTransform.position + _barrelTransform.right * 0.18f
                : transform.position;

            var go = new GameObject("EnemyBullet");
            go.transform.position   = spawnPos;
            float bulletScale       = Mathf.Clamp(data.fireDamage / 10f, 0.3f, 1.5f);
            go.transform.localScale = Vector3.one * bulletScale;

            var eb    = go.AddComponent<EnemyBullet>();
            eb.damage = data.fireDamage;
            eb.speed  = data.bulletSpeed;
            eb.SetDirection(dir);
        }

        _fireFlash = true;
        if (_reloadFillSR != null)
            _reloadFillSR.color = new Color(1f, 1f, 0.8f, 0.95f);
        if (_reloadFillTransform != null)
            _reloadFillTransform.localScale = new Vector3(1f, 1f, 1f);
    }

    void FireLaserBeam(Vector3 dir)
    {
        // Düşman ile birlikte hareket etmesi için barrel'ın (veya enemy'nin) child'ı olarak spawn et
        var parent   = _barrelTransform != null ? _barrelTransform : transform;
        var localOff = _barrelTransform != null ? Vector3.right * 0.18f : Vector3.zero;

        var go = new GameObject("EnemyLaserBeam");
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.localPosition = localOff;
        go.transform.up            = dir; // world-space yön; parent pozisyon değişince beam de taşınır

        var beam             = go.AddComponent<LaserBeam>();
        beam.damage          = data.fireDamage;
        beam.weaponType      = WeaponType.Laser;
        beam.continuous      = false;
        beam.burnDuration    = 1.5f;
        beam.energyPerSecond = 0f; // düşmanlar enerji sistemi kullanmaz
        beam.hitsPlayer      = true;
        beam.maxRange        = data.fireRange + 2f;
        beam.Init();
    }

    ShipComponentBase PickComponentTarget()
    {
        var all         = FindObjectsByType<ShipComponentBase>(FindObjectsSortMode.None);
        var operational = System.Array.FindAll(all, c => c.IsOperational);
        if (operational.Length == 0) return null;
        return operational[Random.Range(0, operational.Length)];
    }

    /// <summary>AttackRun için: gemi burnuna doğru ilerleyen, kalkan bypass eden mermi.</summary>
    void FireForward()
    {
        Vector2 dir = _movement.Facing;

        var go = new GameObject("EnemyBullet_AR");
        go.transform.position = transform.position;
        var eb = go.AddComponent<EnemyBullet>();
        eb.damage        = data.fireDamage;
        eb.speed         = data.bulletSpeed;
        eb.bypassShields = true;
        eb.SetDirection(dir);
    }

    void FireAtComponent()
    {
        var all         = FindObjectsByType<ShipComponentBase>(FindObjectsSortMode.None);
        var operational = new List<ShipComponentBase>();
        foreach (var c in all)
            if (c.IsOperational) operational.Add(c);
        if (operational.Count == 0) return;

        var target = operational[Random.Range(0, operational.Count)];
        var go     = new GameObject("EnemyBullet_Comp");
        go.transform.position = transform.position;
        var eb = go.AddComponent<EnemyBullet>();
        eb.damage          = data.fireDamage;
        eb.speed           = data.bulletSpeed;
        eb.targetComponent = target;
    }

    // ── Teşhis / HUD erişimi ──────────────────────────────────────────────────
    // EnemyInfoHUD bu değerleri okur. Alanlar private kalır; dışarıya yalnızca
    // okunur bir görünüm verilir.

    public EnemyTypeData Data          => data;
    public float         CurrentHP     => _healthBar != null ? _healthBar.currentHealth : 0f;
    public float         MaxHP         => _healthBar != null ? _healthBar.maxHealth     : 0f;
    public float         CurrentShield => _shieldHP;
    public float         MaxShield     => _maxShieldHP;

    // ── ITurretTarget ─────────────────────────────────────────────────────────

    public Transform TargetTransform => transform;
    public Vector2   TargetVelocity  => Velocity;
    // Faz hâlindeki hayalet geçerli hedef değildir — turretler ona kilitlenip
    // mermilerini boşa harcamasın.
    public bool      IsValidTarget   => this != null && isActiveAndEnabled && !IsPhased
                                     && _healthBar != null && _healthBar.currentHealth > 0f;

    public float ThreatValue => data != null ? Mathf.Max(1, data.threatScore) : 1f;

    public float ArmorValue => EffectiveArmor;

    /// <summary>Kalkanın içine girmiş küçük/yakın saldırganlar PD'nin işi.</summary>
    public bool IsPointDefencePriority =>
        data != null && (data.movementKind == EnemyMovementKind.Approach
                      || data.movementKind == EnemyMovementKind.BombRun
                      || data.movementKind == EnemyMovementKind.AttackRun);

    /// <summary>
    /// Kalkan + gövdeyi bu silah tipiyle bitirmek için gereken ham hasar.
    /// Direnci yüksek katman ham hasarı büyütür, düşük katman küçültür.
    ///
    /// Zırh burada hesaba KATILMAZ çünkü zırhın etkisi atış başına hasara
    /// bağlıdır ve o bilgi çağıran turrette durur; turret kendi atış hasarını
    /// bilerek <see cref="ArmorPenaltyFor"/> ile çarpar. Zırhı buraya sabit
    /// gömseydik, zayıf atışlı turretler vuramadıkları hedefe kilitlenirdi.
    /// </summary>
    public float RawDamageToKill(WeaponType weaponType)
    {
        float raw = 0f;

        if (data != null && data.maxShield > 0f && _shieldHP > 0f)
        {
            var mods = data.shieldResistances != null && data.shieldResistances.Length > 0
                ? data.shieldResistances
                : DefaultShieldResistances;
            raw += _shieldHP / Mathf.Max(MultiplierFor(weaponType, mods), 0.01f);
        }

        float hull = _healthBar != null ? _healthBar.currentHealth : 0f;
        raw += hull / Mathf.Max(MultiplierFor(weaponType, data?.hullResistances), 0.01f);

        return raw;
    }

    static float MultiplierFor(WeaponType wt, DamageModifier[] mods)
    {
        if (mods == null) return 1f;
        foreach (var m in mods)
            if (m.weaponType == wt) return m.multiplier;
        return 1f;
    }

    // ── Hasar alma ────────────────────────────────────────────────────────────

    /// <param name="armorPreApplied">
    /// Işınlar true geçer: zırhı kendileri ORAN olarak uygulamıştır
    /// (bkz. BalanceConfig.BeamArmorEfficiency).
    /// </param>
    public void TakeDamage(float amount, WeaponType weaponType = WeaponType.Kinetic,
                           bool armorPreApplied = false)
    {
        if (_healthBar == null) return;
        if (IsPhased) return;   // Hayalet: faz sırasında hiçbir şey geçmez

        // Zırh EŞİĞİ dirençlerden ÖNCE, atış başına uygulanır. Sıra önemlidir:
        // zırh ham atışı budar, dirençler kalanı ölçekler. Ters sırada olsaydı
        // dirençli düşmanlara karşı zırh iki kez cezalandırırdı.
        float shot = armorPreApplied
            ? amount
            : BalanceConfig.Instance.ApplyArmor(amount, EffectiveArmor);

        // YÖNLÜ kalkan burada devreye GİRMEZ: bu çağrı gövde collider'ından
        // geliyor, yani mermi yayı ıskalamış demektir. Kalkanı kenarından
        // dolanmanın ödülü tam da budur.
        float hull = data.maxShield > 0f && _shieldHP > 0f && !data.HasDirectionalShield
            ? ApplyShieldLayer(shot, weaponType)
            : ApplyResistances(shot, weaponType, data.hullResistances);

        ApplyHullDamage(hull);
    }

    /// <summary>
    /// Yay kalkanına isabet — yalnızca <see cref="BarrierShield"/> çağırır.
    /// Kalkanı aşan fazlalık gövdeye geçer.
    /// </summary>
    public void TakeShieldDamage(float amount, WeaponType weaponType,
                                 bool armorPreApplied = false)
    {
        if (_healthBar == null) return;
        if (IsPhased) return;

        float shot = armorPreApplied
            ? amount
            : BalanceConfig.Instance.ApplyArmor(amount, EffectiveArmor);
        ApplyHullDamage(ApplyShieldLayer(shot, weaponType));
    }

    void ApplyHullDamage(float hull)
    {
        if (hull <= 0f) return;

        _healthBar.TakeDamage(hull);
        if (_healthBar.currentHealth <= 0f)
        {
            SpawnSplitFragments();
            SpawnDebris();
            Destroy(gameObject);
        }
    }

    // ── Zırh ve özel davranışlar ──────────────────────────────────────────────

    /// <summary>
    /// Bu düşmanın toplam zırhı: levelin taban zırhı + tipin bonusu.
    /// EnemySpawner ölçekleme sırasında data.armor'a zaten leveli eklemiştir.
    /// </summary>
    public float EffectiveArmor => data != null ? Mathf.Max(0f, data.armor) : 0f;

    /// <summary>Hayalet fazı — vurulamaz olduğu pencere.</summary>
    public bool IsPhased => _phaseTimer > 0f;

    float _phaseTimer;      // > 0 iken vurulamaz
    float _phaseCooldown;   // bir sonraki faza kalan süre
    float _auraTimer;
    static float _jamRefreshTimer;

    void UpdateSpecialBehaviours()
    {
        if (data == null) return;

        UpdatePhasing();
        UpdateRepairAura();
    }

    /// <summary>
    /// Periyodik vurulamazlık. Sürekli DPS'i cezalandırır, burst'ü ödüllendirir —
    /// oyuncunun faz penceresini öğrenmesi gerekir.
    /// </summary>
    void UpdatePhasing()
    {
        if (data.phaseInterval <= 0f) return;

        if (_phaseTimer > 0f)
        {
            _phaseTimer -= Time.deltaTime;
            if (_phaseTimer <= 0f) SetPhaseVisual(false);
            return;
        }

        _phaseCooldown -= Time.deltaTime;
        if (_phaseCooldown > 0f) return;

        _phaseCooldown = data.phaseInterval;
        _phaseTimer    = Mathf.Max(0.1f, data.phaseDuration);
        SetPhaseVisual(true);
    }

    void SetPhaseVisual(bool phased)
    {
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
        {
            var c = sr.color;
            c.a      = phased ? 0.25f : 1f;
            sr.color = c;
        }
    }

    /// <summary>
    /// Onarıcı aurası — menzildeki düşmanların HP'sini geri getirir. Oyuncunun
    /// DPS'i aurayı aşamıyorsa hedefler hiç ölmez; öncelik hedeflemeyi zorunlu kılar.
    /// </summary>
    void UpdateRepairAura()
    {
        if (data.repairAura <= 0f) return;

        _auraTimer -= Time.deltaTime;
        if (_auraTimer > 0f) return;
        _auraTimer = 0.25f;   // her karede tarama yapmaya değmez

        float heal = data.repairAura * 0.25f;
        float r2   = data.repairAuraRange * data.repairAuraRange;

        foreach (var other in FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
        {
            if (other == this || other._healthBar == null) continue;
            if (((Vector2)other.transform.position - (Vector2)transform.position).sqrMagnitude > r2)
                continue;
            other._healthBar.currentHealth =
                Mathf.Min(other._healthBar.maxHealth, other._healthBar.currentHealth + heal);
        }
    }

    /// <summary>
    /// Bölünen düşman — ölünce iki küçük parçaya ayrılır. Tek hedefe odaklanan
    /// yüksek hasarlı build'i cezalandırır, alan hasarı talebi yaratır.
    /// </summary>
    void SpawnSplitFragments()
    {
        if (data == null || data.splitInto == null) return;

        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), 0f);
            var frag = EnemySpawner.Spawn(data.splitInto, transform.position + offset);
            if (frag == null) continue;

            // Parçalar zayıflar; yoksa bölünmek düşmanı güçlendirirdi
            var hb = frag.GetComponent<HealthBar>();
            if (hb != null)
            {
                hb.maxHealth     *= data.splitHpRatio;
                hb.currentHealth  = hb.maxHealth;
            }
        }
    }

    /// <summary>
    /// Karıştırıcıların toplam enerji kısıtı (0–1). EnergyBus üretimi bu oranda düşer.
    /// Statik sorgu: sahada kaç jammer varsa etkileri toplanır, %80'de tavanlanır.
    /// </summary>
    public static float TotalEnergyJam(Vector3 shipPos)
    {
        float jam = 0f;
        foreach (var e in FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))
        {
            if (e.data == null || e.data.energyDrain <= 0f) continue;
            float d = Vector2.Distance(shipPos, e.transform.position);
            if (d > e.data.energyDrainRange) continue;
            jam += e.data.energyDrain;
        }
        return Mathf.Min(jam, 0.8f);
    }

    float ApplyShieldLayer(float amount, WeaponType wt)
    {
        float effective = ApplyResistances(amount, wt,
            data.shieldResistances != null && data.shieldResistances.Length > 0
                ? data.shieldResistances
                : DefaultShieldResistances);

        _shieldRechargeTimer = data.shieldRechargeDelay;

        if (_shieldHP >= effective)
        {
            _shieldHP -= effective;
            SyncShieldBar();
            RefreshShieldVisual();
            return 0f;
        }

        float overflow = effective - _shieldHP;
        _shieldHP = 0f;
        SyncShieldBar();
        RefreshShieldVisual();
        return ApplyResistances(overflow, wt, data.hullResistances);
    }

    // Kalkan direnci tanımlanmamışsa kullanılan oyun sabitleri
    static readonly DamageModifier[] DefaultShieldResistances =
    {
        new DamageModifier { weaponType = WeaponType.Kinetic, multiplier = 1.5f  },
        new DamageModifier { weaponType = WeaponType.Laser,   multiplier = 0.25f },
    };

    static float ApplyResistances(float amount, WeaponType wt, DamageModifier[] mods)
    {
        if (mods == null) return amount;
        foreach (var m in mods)
            if (m.weaponType == wt) return amount * m.multiplier;
        return amount;
    }

    void UpdateShieldRecharge()
    {
        if (_maxShieldHP <= 0f || _shieldHP >= _maxShieldHP) return;
        _shieldRechargeTimer -= Time.deltaTime;
        if (_shieldRechargeTimer > 0f) return;

        _shieldHP = Mathf.Min(_shieldHP + data.shieldRechargeRate * Time.deltaTime, _maxShieldHP);
        SyncShieldBar();
        RefreshShieldVisual();
    }

    void SyncShieldBar()
    {
        if (_healthBar != null) _healthBar.currentShield = _shieldHP;
    }

    void SpawnDebris()
    {
        // Parçalanma görsel efekti
        if (data != null)
            DeathEffect.Spawn(transform.position, data.bodyColor, data.bodyWidth, data.bodyHeight);

        // Toplam miktar = tehdit puanı × levelin drop oranı.
        // Eskiden sabit ×4'tü; gelir bu yüzden yalnızca wave bütçesiyle büyüyordu
        // ve 100. levelde 125× düşman spawn etmek gerekirdi. Artık iki bileşen
        // ayrı: bütçe yavaş (×1.018/level), düşman başına değer hızlı (×1.031).
        float perThreat   = BalanceConfig.Instance.DropPerThreat(GameProgress.CurrentLevel);
        float total       = (data != null ? data.threatScore : 1) * perThreat;
        var   resType     = data != null ? data.debrisResourceType : ResourceType.RawMaterial;
        int   pieceCount  = Random.Range(2, 5);

        // Toplam miktarı rastgele boyutlarda parçalara böl
        float[] weights = new float[pieceCount];
        float   wSum    = 0f;
        for (int i = 0; i < pieceCount; i++) { weights[i] = Random.value + 0.1f; wSum += weights[i]; }

        for (int i = 0; i < pieceCount; i++)
        {
            float amount = total * (weights[i] / wSum);
            var   go     = new GameObject("Debris");
            go.transform.position = transform.position;
            var d = go.AddComponent<Debris>();
            d.Init(Random.insideUnitCircle.normalized * Random.Range(0.3f, 1.2f), amount, resType);
        }

        SpawnCrystalDebris();
    }

    /// <summary>
    /// Kalkanı olan her gemi, gövde enkazına ek olarak enerji kristali bırakır —
    /// kalkan teknolojisi kristal tabanlıdır. Miktar kalkan kapasitesiyle orantılıdır,
    /// yani bölüm HP çarpanı arttıkça kristal getirisi de artar.
    /// </summary>
    void SpawnCrystalDebris()
    {
        if (data == null || data.maxShield <= 0f) return;

        float amount = data.maxShield * CrystalPerShieldPoint;
        if (amount < 0.5f) return;

        var go = new GameObject("Debris_Crystal");
        go.transform.position = transform.position;
        go.AddComponent<Debris>().Init(
            Random.insideUnitCircle.normalized * Random.Range(0.3f, 1.2f),
            amount, ResourceType.EnergyCrystal);
    }

    // ── Görsel kurulum ────────────────────────────────────────────────────────

    void BuildBody(int w, int h, Color c)
    {
        var body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.Get(data.SkinId, w, h, c);
        sr.sortingOrder = data.sizeOrder;
    }

    void BuildBarrel(Color barrelColor)
    {
        var root = new GameObject("Barrel");
        root.transform.SetParent(transform, false);
        _barrelTransform = root.transform;

        var barrelSR = root.AddComponent<SpriteRenderer>();
        barrelSR.sprite       = SkinLibrary.Get(data.SkinId + ".barrel", SkinId.EnemyBarrel,
                                    18, 3, barrelColor, new Vector2(0f, 0.5f));
        barrelSR.sortingOrder = data.sizeOrder + 1;

        var fillGO = new GameObject("ReloadFill");
        fillGO.transform.SetParent(root.transform, false);
        fillGO.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        _reloadFillTransform = fillGO.transform;
        _reloadFillTransform.localScale = new Vector3(0f, 1f, 1f);

        // Reload göstergesi bir HUD öğesidir, skin'e tabi değil — hep prosedürel
        _reloadFillSR = fillGO.AddComponent<SpriteRenderer>();
        _reloadFillSR.sprite       = SkinLibrary.Rect(18, 2, Color.white, new Vector2(0f, 0.5f));
        _reloadFillSR.sortingOrder = data.sizeOrder + 2;
        _reloadFillSR.color        = ReloadColor(0f);
    }

    /// <summary>
    /// Küresel kalkan kabuğu. Yarıçapı gövdeden türer ve DAİREDİR.
    ///
    /// Eskiden SkinLibrary.Get(...) çağrılıyordu; "fx.shield" hiçbir SkinSet'te
    /// olmadığı için prosedürel yedeğe düşüyordu — ve o yedek bir DİKDÖRTGEN.
    /// Yani oyundaki her kalkanlı düşman, kalkanını kare bir levha olarak
    /// taşıyordu. Yuvarlak yedek burada üretilir (ShipComponentBase halkasıyla
    /// aynı desen: skin varsa o, yoksa çağıranın kendi şekli).
    /// </summary>
    void BuildShieldVisual(int w, int h)
    {
        _shieldRadius = Mathf.Max(w, h) / 100f * 0.62f;

        _shieldVisual = new GameObject("ShieldVisual");
        _shieldVisual.transform.SetParent(transform, false);

        var sr = _shieldVisual.AddComponent<SpriteRenderer>();
        sr.sprite       = SkinLibrary.GetOrNull(SkinId.ShieldBubble) ?? BubbleSprite();
        sr.sortingOrder = data.sizeOrder + 1;
        sr.color        = BarrierShield.ArcColor;

        // Sprite dış kenarı 1 birim (ppu = yarıçap) → ölçek doğrudan yarıçap
        _shieldVisual.transform.localScale = Vector3.one * _shieldRadius;
    }

    /// <summary>
    /// Yumuşak kenarlı kalkan kabuğu: merkeze doğru şeffaf, kenarda parlak.
    /// Arkasındaki gemi görünmeli — kalkan bir duvar değil bir yüzey.
    /// </summary>
    static Sprite _bubbleSprite;

    static Sprite BubbleSprite()
    {
        if (_bubbleSprite != null) return _bubbleSprite;

        const int   Sz   = 128;
        const float OutR = 62f;              // ppu olarak da kullanılır → 1 birim
        const float InR  = OutR * 0.55f;
        const float C    = Sz * 0.5f;

        var tex = new Texture2D(Sz, Sz, TextureFormat.RGBA32, false)
                  { filterMode = FilterMode.Bilinear };
        var px = new Color[Sz * Sz];

        for (int i = 0; i < px.Length; i++)
        {
            float dx = (i % Sz) + 0.5f - C;
            float dy = (i / Sz) + 0.5f - C;
            float r  = Mathf.Sqrt(dx * dx + dy * dy);

            if (r > OutR) { px[i] = Color.clear; continue; }

            // İçeride soluk bir dolgu, kenara doğru güçlenen bir halka
            float rim  = Mathf.Clamp01((r - InR) / (OutR - InR));
            float edge = Mathf.Clamp01((OutR - r) / 2.5f);   // dış kenar yumuşatma
            px[i] = new Color(1f, 1f, 1f, (0.18f + 0.82f * rim * rim) * edge);
        }

        tex.SetPixels(px);
        tex.Apply();
        _bubbleSprite = Sprite.Create(tex, new Rect(0, 0, Sz, Sz), Vector2.one * 0.5f, OutR);
        return _bubbleSprite;
    }

    void RefreshShieldVisual()
    {
        float ratio = _maxShieldHP > 0f ? Mathf.Clamp01(_shieldHP / _maxShieldHP) : 0f;

        if (_barrier != null) { _barrier.Refresh(ratio); return; }

        if (_shieldVisual == null) return;
        if (_shieldHP <= 0f) { _shieldVisual.SetActive(false); return; }
        _shieldVisual.SetActive(true);
        var sr = _shieldVisual.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var c = BarrierShield.ArcColor;
            sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.12f, c.a, ratio));
        }
    }

    /// <summary>Kalkan ayakta mı? Çarpma efektinin rengini/parlamasını belirler.</summary>
    public bool HasActiveShield => _maxShieldHP > 0f && _shieldHP > 0f;

    /// <summary>
    /// Kalkana isabet parlaması — ana geminin kalkanındaki hilalin aynısı.
    /// Yay kalkanında kırpma BarrierShield'in işi; küre kalkanda kırpma gerekmez.
    /// </summary>
    public void ShieldFlash(Vector2 worldHitPos)
    {
        if (!HasActiveShield) return;

        if (_barrier != null) { _barrier.Flash(worldHitPos); return; }
        if (_shieldVisual == null) return;

        // Küre kalkanın bandı (BubbleSprite: InR = 0.55×R) parlamanın
        // varsayılan 0.60'ına zaten yakın; ayrıca oran vermeye gerek yok.
        ShieldEffect.Spawn(worldHitPos, transform.position, _shieldRadius,
                           BarrierShield.FlashColor, 55f, 0.60f, transform);
    }
}
