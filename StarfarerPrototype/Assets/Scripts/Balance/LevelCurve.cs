using UnityEngine;

/// <summary>
/// Düşman ölçeklemesinin tek sahibi. 100 level elle ayarlanamaz; zorluk sürekli
/// bir formülden gelir, bölüm sınırları yalnızca tema ve yeni düşman tipi getirir.
///
/// ÇÖP GEMİLERİN STATI ARTIK LEVELLE BÜYÜMEZ. Bir Swarm 1. levelde neyse
/// 100. levelde de odur: aynı HP, aynı hasar, aynı zırh. Levelin zorluğu
/// yalnızca KAÇ TANE geldiğinden ve HANGİ TİPLERİN geldiğinden gelir.
///
/// Neden: <c>threatScore</c> tip başına SABİT bir sayıdır ve oyunun en çok iş
/// yapan para birimidir — dalga bütçesini harcar, geliri belirler, serbest
/// modun rampasını ilerletir, valfin saha tavanını ölçer. Stat levelle
/// büyüyünce o birim sessizce enflasyona uğruyordu: Lv100'de aynı gemi 9.8× HP,
/// 4× hasar ve +20 zırh taşıyordu, yani "1 puan" Lv1'de bir şey, Lv100'de
/// ~39 katı bir şey demekti. Dört sistem birden şişmiş bir birimle hesap
/// yapıyordu ve tehdit formülünün veriyle doğrulanması (aynı tipin her levelde
/// tek bir α/β'ya oturması) yapısal olarak imkânsızdı.
///
/// Kaybolan büyüme bütçeye devredildi: <see cref="BalanceConfig.budgetGrowth"/>
/// 1.027 -> 1.051 (= 1.027 × 1.0233), yani bir levele gelen TOPLAM efektif HP
/// birebir korundu — yalnızca uzun HP barları yerine daha çok gemi olarak
/// geliyor. Toplam gelir de korundu: <see cref="BalanceConfig.dropGrowth"/>
/// 1.0'a indi, çünkü tehdit dürüst bir sabit olunca tehdit başına drop da
/// sabit olmalı.
///
/// ZIRH DA DÜZLEŞTİ. Yarısı düzleşmiş bir düşman düzleşmemiştir: Lv100'ün
/// 20 zırhı, 10 hasarlı raylı topa karşı %90 kesinti demekti — hpGrowth'un
/// ürettiğinden daha büyük bir efektif HP çarpanı, üstelik tehdit formülünün
/// "dayanıklılık" terimi doğrudan efektif HP'den türüyor. Zırh eşiğinin amacı
/// (atış başına hasarı ödüllendirmek) kaybolmadı, KOMPOZİSYONA taşındı: zırh
/// artık yalnızca tipin kendi özelliğidir (Kaleci +12, Obüs +3) ve baskı o
/// tipler sahneye çıktığında gelir. Oyuncuya "aynı Swarm gizemli biçimde
/// sertleşti" diye değil, "artık Kaleci yolluyorlar" diye görünür.
///
/// DOKUNULMAYAN: kaçamak manevra ve manevra kabiliyeti eğrileri. Erken
/// levellerin düz ve hantal uçması bir STAT değil bir ÖĞRENME rampasıdır;
/// oyuncu nişan almayı yavaş hedeflerde öğrenir. Bu yüzden <c>evasion</c> ve
/// <c>mobility</c> eskisi gibi levelden gelir.
///
/// BOSS BU KURALIN DIŞINDA — <see cref="BossHullHP"/> hâlâ levelle büyür.
/// Sonuç bilinçli: çöp 20-200 HP'de kalırken boss Lv100'de 4.900'e çıkar, yani
/// kampanya iki ayrı oyuna ayrışır — çöp kalabalık kontrolü ister (turret,
/// point defence, alan hasarı), boss odaklı manuel ateş ister.
/// </summary>
[CreateAssetMenu(fileName = "LevelCurve", menuName = "Starfarer/Level Curve")]
public class LevelCurve : ScriptableObject
{
    [Header("Kapsam")]
    public int totalLevels     = 100;
    public int levelsPerChapter = 10;

    [Header("Ölçekleme")]
    [Tooltip("Level başına HP büyümesi. Lv100 = 9.8×.\n\n" +
             "YALNIZCA BOSS KULLANIR. Çöp gemiler levelle büyümez (bkz. sınıf " +
             "dokümanı); bu çarpan onlardan sökülünce bütçeye devredildi " +
             "(BalanceConfig.budgetGrowth 1.027 -> 1.051 = 1.027 × 1.0233). " +
             "Buradaki sayıyı değiştirirsen bütçe katsayısı da aynı oranda " +
             "değişmeli, yoksa boss ile çöp arasındaki mesafe sessizce kayar.")]
    public float hpGrowth = 1.0233f;

    [Header("Zırh")]
    [Tooltip("Oyundaki en yüksek zırh referansı. İKİ yerde kullanılır: bölüm " +
             "10 boss'unun zırhı ve düşman bilgi kutusundaki zırh barının " +
             "ölçeği.\n\n" +
             "Eskiden 'son leveldeki TABAN zırh' idi ve her düşmanın üstüne " +
             "levelden binerdi. Artık zırh yalnızca tipin kendi özelliğidir " +
             "(Kaleci +12, Obüs +3) — levelden gelen taban zırh kaldırıldı, " +
             "Armor(n) eğrisi ve armorExponent ile birlikte silindi.")]
    public float maxArmor = 20f;

    [Header("Kaçamak Manevra")]
    [Tooltip("Kaçamak davranışın tam açıldığı level. Öncesinde doğrusal artar — " +
             "oyuncu nişan almayı öğrenirken düz uçan hedeflerle başlar.")]
    public int evasionFullLevel = 25;

    [Tooltip("1. levelde manevra kabiliyetinin çarpanı — hem hıza hem çevikliğe " +
             "uygulanır. Dönüş hızı ikisinin ÇARPIMI olduğu için kavis kabiliyeti " +
             "bu sayının KARESİ kadar düşer: 0.7 ile Swarm 135°/sn yerine " +
             "66°/sn döner. Kaçamak açısı 1. levelde zaten 0'dı ama gemi hâlâ " +
             "kıvraktı — 'ilk leveller düz uçar' kuralı yalnızca SALINIMA " +
             "uygulanıyordu, uçuşun kendisine değil.")]
    public float startMobility = 0.7f;

    // ── Singleton ─────────────────────────────────────────────────────────────

    static LevelCurve _instance;

    public static LevelCurve Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<LevelCurve>("LevelCurve");
            if (_instance == null) _instance = CreateInstance<LevelCurve>();
            return _instance;
        }
    }

    /// <summary>
    /// Singleton'ı koşuya özgü bir KOPYAYA çevirir.
    ///
    /// Simülasyon parametre ezmesi (--set) uygularken asıl asset'e yazamaz:
    /// editörde koşulduğunda Resources/LevelCurve.asset diske kirlenir ve bir
    /// duyarlılık koşusunun ±%20'si projenin kalıcı dengesi hâline gelirdi.
    /// Kopya yalnızca bellekte yaşar.
    /// </summary>
    public static void UseRuntimeCopy()
    {
        _instance = Instantiate(Instance);
    }

    // ── Formüller ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Level başına HP çarpanı — YALNIZCA BOSS için. Çöp gemiler düz statla
    /// gelir (bkz. sınıf dokümanı).
    /// </summary>
    public float HpMultiplier(int n) => Mathf.Pow(hpGrowth, Mathf.Max(0, n - 1));

    public float EvasionMultiplier(int n)
        => Mathf.InverseLerp(1f, Mathf.Max(2, evasionFullLevel), n);

    /// <summary>
    /// Manevra kabiliyeti — hız ve çeviklik ORTAK çarpanı. Kaçamak salınımıyla
    /// aynı eğriyi kullanır: "erken leveller düz ve hantal uçar" tek bir kural
    /// olmalı, iki ayrı eğri zamanla birbirinden sapardı.
    ///
    /// Neden gerekti: salınım açısı 1. levelde 0'a iniyordu ama gemi hâlâ
    /// 135°/sn dönen, 3 birim/sn giden bir Swarm'dı. Oyuncunun gördüğü kaçamak
    /// davranış salınım değil, DAR KAVİSTİ — "ilk levellerde düz uçarlar"
    /// kuralı yalnızca kağıt üstünde geçerliydi.
    /// </summary>
    public float MobilityMultiplier(int n)
        => Mathf.Lerp(Mathf.Clamp(startMobility, 0.2f, 1f), 1f, EvasionMultiplier(n));

    /// <summary>Boss gövde HP'si — bölüm kapanış dövüşü.</summary>
    public float BossHullHP(int n) => 500f * HpMultiplier(n);

    /// <summary>Boss hardpoint HP'si.</summary>
    public float BossHardpointHP(int n) => 120f * HpMultiplier(n);

    /// <summary>Bölüm numarasına göre hardpoint adedi — boss'lar giderek karmaşıklaşır.</summary>
    public int BossHardpointCount(int chapter) => 2 + chapter / 2;
}

/// <summary>
/// Bir düşmana uygulanacak ölçekleme katsayıları. Kampanya bunları
/// <see cref="LevelCurve"/>'den, serbest mod kendi rampasından üretir —
/// ama ikisi de aynı yoldan geçer, ayrı formül yoktur.
///
/// HP VE HASAR ARTIK YALNIZCA ZORLUK ÇARPANINI TAŞIR (Kolay ×0.8 / Zor ×1.2);
/// levelden gelen büyüme kaldırıldı. ZIRH ALANI HİÇ YOK: zırh yalnızca tipin
/// kendi özelliğidir. Her zaman 0 olan bir alan bırakmak, ileride birinin onu
/// yeniden beslemesi ve düzleştirmenin sessizce geri alınması demekti —
/// alanın olmaması kuralı yapısal kılar.
///
/// Levelden gelmeye DEVAM eden tek şey uçuş karakteridir: <c>evasion</c> ve
/// <c>mobility</c>. Onlar stat değil öğrenme rampasıdır.
/// </summary>
public struct EnemyScaling
{
    /// <summary>HP çarpanı — yalnızca zorluk. Levelden büyüme YOK.</summary>
    public float hp;

    /// <summary>Hasar çarpanı — yalnızca zorluk. Levelden büyüme YOK.</summary>
    public float damage;

    public float evasion;

    /// <summary>Hız ve çevikliğin ortak çarpanı. 1 = tipin kendi değeri.</summary>
    public float mobility;

    public static EnemyScaling None => new EnemyScaling
    {
        hp = 1f, damage = 1f, evasion = 1f, mobility = 1f,
    };

    public static EnemyScaling ForLevel(int gameLevel)
    {
        var c = LevelCurve.Instance;

        // Zorluk BURADA uygulanır: kampanya da serbest mod da düşmanını bu
        // yoldan kurar (EnemySpawner.Spawn), yani tek satır iki modu birden
        // kapsar. Boss bu yoldan geçmez — onun çarpanı BossShipData'da.
        //
        // Level ARTIK HP'ye ve hasara dokunmuyor: levelin zorluğu dalga
        // bütçesinden (kaç gemi) ve kompozisyondan (hangi tipler) gelir.
        float zorluk = DifficultyManager.EnemyMultiplier;

        return new EnemyScaling
        {
            hp       = zorluk,
            damage   = zorluk,
            evasion  = c.EvasionMultiplier(gameLevel),
            mobility = c.MobilityMultiplier(gameLevel),
        };
    }
}
