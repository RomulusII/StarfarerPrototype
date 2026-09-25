using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mermi çarpışmalarında hedef tespiti için ortak yardımcı.
/// EnemyBot, BossHardpoint, BossShip gövdesi ve Asteroid'i tek noktadan yönetir.
/// </summary>
public static class DamageUtil
{
    /// <summary>
    /// Bu collider bir KALKAN yüzeyi mi, öyleyse sahibi kim? İki kalkan tipi
    /// var — <see cref="BarrierShield"/> (yay) ve <see cref="BubbleShield"/>
    /// (küre) — ve ikisi de gövdeden AYRI collider taşır. Dört ayrı yerde
    /// "önce yayı sor, sonra küreyi sor" yazmak yerine soru burada bir kez
    /// cevaplanır; yeni bir kalkan şekli eklendiğinde de tek yer değişir.
    /// </summary>
    static EnemyBot ShieldOwnerOf(Collider2D other)
    {
        if (other == null) return null;

        var barrier = other.GetComponent<BarrierShield>();
        if (barrier != null) return barrier.owner;

        var bubble = other.GetComponent<BubbleShield>();
        return bubble != null ? bubble.owner : null;
    }

    /// <summary>
    /// Bu collider hangi yüzey? Kıvılcım rengi buradan gelir.
    /// Hedef tespitiyle aynı dosyada durur: iki soru da "bu collider ne"
    /// sorusunun parçası ve ayrı yerlerde yaşasalardı biri diğerinden sapardı.
    /// </summary>
    /// <summary>
    /// Denge kaydı için hedefin TİP ADI. "Bu silah neyi vuruyor" sorusu isabet
    /// oranını hedef tipine göre ayırmak için gerekli — kıvrak bir Avcı ile
    /// duran bir asteroit aynı sayıya karışmamalı.
    ///
    /// Yüzey tespitiyle (<see cref="SurfaceOf"/>) aynı dosyada durur: ikisi de
    /// "bu collider ne" sorusunun parçası.
    /// </summary>
    public static string TypeNameOf(Collider2D other)
    {
        if (other == null) return "?";

        var shieldOwner = ShieldOwnerOf(other);
        if (shieldOwner != null && shieldOwner.data != null) return shieldOwner.data.name;

        var enemy = other.GetComponent<EnemyBot>();
        if (enemy != null && enemy.data != null) return enemy.data.name;

        if (other.GetComponent<BossHardpoint>() != null) return "Hardpoint";
        if (other.GetComponent<BossShip>()      != null) return "Boss";
        if (other.GetComponent<Asteroid>()      != null) return "Asteroit";
        if (other.GetComponent<Bomb>()          != null) return "Bomba";

        return other.name;
    }

    public static ImpactSurface SurfaceOf(Collider2D other)
    {
        if (other == null) return ImpactSurface.Hull;
        if (ShieldOwnerOf(other) != null) return ImpactSurface.Shield;

        var bot = other.GetComponent<EnemyBot>();
        if (bot != null && bot.HasActiveShield) return ImpactSurface.Shield;

        return other.GetComponent<Asteroid>() != null ? ImpactSurface.Rock
                                                      : ImpactSurface.Hull;
    }

    /// <summary>
    /// Kalkana isabet hilalini tetikler. Çarpma NOKTASI yalnızca merminin
    /// kendisinde biliniyor; TryDamage'a bir parametre daha eklemek yerine
    /// çağıran taraf zaten elindeki konumu buraya veriyor.
    /// </summary>
    public static void ShieldFlash(Collider2D other, Vector2 hitPos)
    {
        if (other == null) return;

        var shieldOwner = ShieldOwnerOf(other);
        if (shieldOwner != null) { shieldOwner.ShieldFlash(hitPos); return; }

        other.GetComponent<EnemyBot>()?.ShieldFlash(hitPos);
    }

    /// <summary>Hedefin zırhı. Zırh kavramı olmayan hedeflerde 0.</summary>
    public static float ArmorOf(Collider2D other)
    {
        if (other == null) return 0f;

        var shieldOwner = ShieldOwnerOf(other);
        if (shieldOwner != null) return shieldOwner.ArmorValue;

        var enemy = other.GetComponent<EnemyBot>();
        if (enemy != null) return enemy.ArmorValue;

        var boss = other.GetComponent<BossShip>();
        if (boss != null) return boss.ArmorValue;

        return 0f;   // hardpoint ve asteroit zırhsız
    }

    /// <summary>
    /// Çarpışılan collider'a hasar uygular.
    /// Sıra: kalkan yüzeyi → BossHardpoint → BossShip gövdesi → EnemyBot → Asteroid
    /// Hasar uygulandıysa true döner.
    /// </summary>
    /// <param name="armorPreApplied">
    /// Işınlar için true. Zırh eşiği atış başına işler; sürekli bir kaynak onu
    /// kendisi ORAN olarak hesaplar (BalanceConfig.BeamArmorEfficiency) ve
    /// hedefin bir kez daha kesmesini istemez.
    /// </param>
    public static bool TryDamage(Collider2D other, float damage, WeaponType weaponType,
                                 bool armorPreApplied = false)
    {
        // Kalkan yüzeyi — gövdeden AYRI bir collider, gövdeden önce sorulur.
        // Yay kalkanında yalnızca önden gelen mermi buraya çarpar, yandan gelen
        // onu ıskalayıp aşağıdaki gövde dalına düşer. Küresel kalkanda ise her
        // yönden gelen çarpar — kabuğu kesip gövdeyi ıskalayan mermi artık
        // öbür taraftan çıkmıyor.
        var shieldOwner = ShieldOwnerOf(other);
        if (shieldOwner != null)
        {
            shieldOwner.TakeShieldDamage(damage, weaponType, armorPreApplied);
            return true;
        }

        // Boss hardpoint (collider doğrudan hardpoint GO'sunda)
        var hardpoint = other.GetComponent<BossHardpoint>();
        if (hardpoint != null && hardpoint.IsAlive)
        {
            hardpoint.TakeDamage(damage);
            return true;
        }

        // Boss gövdesi (collider boss'un ana GO'sunda, hardpoint değil)
        var boss = other.GetComponent<BossShip>();
        if (boss != null)
        {
            boss.TakeDamage(damage, weaponType, armorPreApplied);
            return true;
        }

        // Normal düşman
        var enemy = other.GetComponent<EnemyBot>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage, weaponType, armorPreApplied);
            return true;
        }

        // Asteroit — düşman değil ama vurulabilir ve parçalanır
        var asteroid = other.GetComponent<Asteroid>();
        if (asteroid != null)
        {
            asteroid.TakeDamage(damage, weaponType);
            return true;
        }

        return false;
    }

    // ── Alan hasarı ───────────────────────────────────────────────────────────

    /// <summary>Patlama merkezindeki hasarın yayın KENARINDA kalan oranı.</summary>
    /// <remarks>
    /// Sönüm olmasaydı patlama sert kenarlı bir daire olurdu: menzile giren
    /// her hedef aynı hasarı alır, yani atışı kalabalığın MERKEZİNE koymanın
    /// kenarına koymaya göre hiçbir üstünlüğü kalmazdı. Nişan almanın karşılığı
    /// olmalı. Kenarda tamamen sıfırlamak da yanlış: o zaman efektif yarıçap
    /// gösterilenden küçük olur ve oyuncu "değdi ama saymadı" hissi yaşar.
    /// </remarks>
    public const float BlastEdgeFalloff = 0.4f;

    // Paylaşılan tampon: her patlama dizi ayırsaydı yoğun bir dalgada GC yükü
    // olurdu (TurretBullet'in süpürme tamponuyla aynı gerekçe).
    static readonly List<Collider2D> _blastHits = new();
    static readonly HashSet<Object>  _blastSeen = new();

    /// <summary>
    /// Bir yarıçap içindeki HER hedefe hasar uygular. Patlayan roket ve flak
    /// mermisi buradan geçer.
    ///
    /// AYNI HEDEFE İKİ KEZ VURMAZ. Kalkanlı bir geminin gövdesi ve kalkanı AYRI
    /// collider'lardır; ikisi de yarıçapın içindeyse naif bir tarama o gemiye
    /// iki kez hasar verirdi. Alıcılar kimliğe göre tekilleştirilir ve kalkan
    /// gövdeye TERCİH EDİLİR — dışarıdan gelen bir patlama önce kabuğa çarpar,
    /// tıpkı <see cref="TryDamage"/>'ın sırası gibi.
    ///
    /// Boss gövdesi ile hardpoint'leri AYRI alıcılardır ve ikisi de vurulur:
    /// onlar uzayda farklı parçalar, aynı şeyin iki collider'ı değil.
    ///
    /// Mesafe hedefin MERKEZİNDEN değil, collider'ının en yakın noktasından
    /// ölçülür: boss gibi iri bir gövdede merkez ölçüsü, patlama gövdenin
    /// üstündeyken bile "uzak" derdi.
    /// </summary>
    /// <returns>Hasar alan AYRI hedef sayısı.</returns>
    public static int AreaDamage(Vector2 center, float radius, float damage,
                                 WeaponType weaponType)
    {
        if (radius <= 0f || damage <= 0f) return 0;

        _blastHits.Clear();
        _blastSeen.Clear();
        Physics2D.OverlapCircle(center, radius, new ContactFilter2D().NoFilter(), _blastHits);
        if (_blastHits.Count == 0) return 0;

        // İki geçiş: önce kalkan yüzeyleri kaydedilir, sonra gövdeler. Tek
        // geçişte sonuç collider sırasına bağlı olurdu — aynı patlama bazen
        // kalkana bazen gövdeye vururdu.
        int hit = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < _blastHits.Count; i++)
            {
                var col = _blastHits[i];
                if (col == null) continue;

                var shieldOwner = ShieldOwnerOf(col);
                bool isShield   = shieldOwner != null;
                if (pass == 0 != isShield) continue;

                Object key = isShield ? (Object)shieldOwner : ReceiverKey(col);
                if (key == null || !_blastSeen.Add(key)) continue;

                float dist  = Vector2.Distance(center, col.ClosestPoint(center));
                float scale = Mathf.Lerp(1f, BlastEdgeFalloff,
                                         Mathf.Clamp01(dist / radius));

                // Bomba TryDamage'ın bilmediği tek alıcı: çağıranlar onu
                // kendileri özel olarak ele alıyor (bkz. TurretBullet.TryHit).
                // Patlamada ele alınması şart — patlamanın İÇİNDE kalan bir
                // bombanın sağ çıkması, oyuncunun GÖRDÜĞÜ ile oyunun BİLDİĞİ
                // arasında fark demekti.
                var bomb = col.GetComponent<Bomb>();
                if (bomb != null) { bomb.TakeDamage(damage * scale); hit++; continue; }

                if (TryDamage(col, damage * scale, weaponType))
                {
                    hit++;
                    if (isShield) ShieldFlash(col, col.ClosestPoint(center));
                }
                else _blastSeen.Remove(key);   // vurulamadıysa kimliği tutma
            }
        }
        return hit;
    }

    // ── Şarapnel ──────────────────────────────────────────────────────────────

    /// <summary>Doğrudan isabette kıymıkların çarpılan hedefe giden payı.</summary>
    public const float ShrapnelDirectShare = 0.4f;

    /// <summary>
    /// Doğrudan isabette kalan kıymıkların saçıldığı koninin YARI açısı.
    /// Kıymıklar merminin geliş yönünde, hedefin ARKASINA saçılır: mermi
    /// hedefi delip geçen bir enerji taşıyor. 50° ile koni 100°; hedefin
    /// hemen arkasındaki gemileri yakalar, yandakileri kaçırır.
    /// </summary>
    public const float ShrapnelConeHalfAngle = 50f;

    public struct ShrapnelResult
    {
        public int caught;      // kıymık yiyen AYRI hedef sayısı
        public int fragments;   // bir hedefe değen kıymık sayısı
    }

    // Hedef başına toplanan isabet — efekt ve kalkan parlaması hedef başına BİR
    // kez oynar. Kıymık başına oynasaydı 12 kıymık yiyen gemi 12 ayrı kıvılcım
    // patlaması ve 12 hilal doğururdu.
    struct ShrapnelHit
    {
        public Collider2D    col;
        public Vector2       point, dir;
        public ImpactSurface surface;
        public float         damage;
        public int           fragments;
    }

    static readonly List<RaycastHit2D>   _shrapnelRay  = new();
    static readonly List<ShrapnelHit>    _shrapnelHits = new();
    static readonly Dictionary<Object, int> _shrapnelIdx = new();

    /// <summary>
    /// Şarapnel patlaması. Alan hasarından (<see cref="AreaDamage"/>) farkı:
    /// yarıçaptaki her hedefe tek bir hasar yazılmaz, <paramref name="count"/>
    /// kıymığın her biri ayrı bir ışın olarak atılır ve yolundaki İLK hedefe
    /// ayrı bir atış olarak vurur.
    ///
    /// Bunun iki sonucu tasarımın kendisidir:
    /// - **Zırh eşiği KIYMIK başına işler.** 3 hasarlı kıymık zırhsız gemiye
    ///   3 vurur, zırh 2'ye 1, Muhafız aurasındaki gemiye (+6) %10. Kalabalığı
    ///   biçer, zırhı çizer.
    /// - **Hasar mesafeyle KENDİLİĞİNDEN söner.** Ayrı bir kenar sönümü yok;
    ///   uzaktaki hedef daha dar bir açı kapladığı için daha az kıymık yakalar.
    ///   Öndeki gemi arkadakine SİPER olur, kalkan kıymık emer.
    ///
    /// Kıymıklar anlıktır (hitscan): 30 ışın bir kerede atılır, kıymık başına
    /// nesne kurulmaz ve kayda girecek bir şey kalmaz. Görseli aynı ışının
    /// üstünden gider (<see cref="HitEffect.SpawnShrapnel"/>), yani oyuncunun
    /// gördüğü kıymık hasar veren kıymığın ta kendisidir.
    ///
    /// <paramref name="primary"/> doluysa DOĞRUDAN İSABETTİR: kıymıkların
    /// <see cref="ShrapnelDirectShare"/> kadarı o hedefe gider, kalanı merminin
    /// geliş yönünde bir koni hâlinde arkasına saçılır ve hedefin kendi
    /// collider'larını (kalkan kabuğu dahil) delip geçer. Boşsa mermi havada
    /// patlamıştır ve kıymıklar 360° saçılır.
    /// </summary>
    public static ShrapnelResult Shrapnel(Vector2 origin, Vector2 dir, Collider2D primary,
                                          int count, float range, float damage,
                                          WeaponType weaponType)
    {
        var result = new ShrapnelResult();
        if (count <= 0 || range <= 0f) return result;

        _shrapnelHits.Clear();
        _shrapnelIdx.Clear();
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;

        Object primaryKey = primary != null ? ReceiverOf(primary) : null;
        int direct = primaryKey != null ? Mathf.RoundToInt(count * ShrapnelDirectShare) : 0;

        for (int i = 0; i < direct; i++)
            ShrapnelStrike(primary, primaryKey, origin, dir, damage, weaponType);

        // Açılar TAMAMEN rastgele. Önce düzgün aralık + küçük sapma denendi:
        // bir çark gibi, fazla düzenli okunuyordu. Rastgele dağılım kümelenip
        // delik açar — ve bu kabul: gerçek şarapnel de öyle, bir gemi bazen
        // şanslı çıkar. Ortalamada yoğunluk yine 1/mesafe ile düşer.
        int   rest   = count - direct;
        bool  cone   = primaryKey != null;

        for (int i = 0; i < rest; i++)
        {
            float   angle = cone ? Random.Range(-ShrapnelConeHalfAngle, ShrapnelConeHalfAngle)
                                 : Random.Range(0f, 360f);
            Vector2 d     = Rotate(dir, angle);
            float   reach = range;

            int n = Physics2D.Raycast(origin, d, ContactFilter2D.noFilter, _shrapnelRay, range);
            if (n > 0)
            {
                _shrapnelRay.Sort((a, b) => a.distance.CompareTo(b.distance));
                for (int k = 0; k < _shrapnelRay.Count; k++)
                {
                    var col = _shrapnelRay[k].collider;
                    var key = ReceiverOf(col);
                    // Hedef olmayan (oyuncu gemisi, mermiler, enkaz) kıymığı
                    // durdurmaz; doğrudan isabet alan hedef de — kıymık onu
                    // delip geçiyor.
                    if (key == null || key == primaryKey) continue;

                    if (ShrapnelStrike(col, key, _shrapnelRay[k].point, d, damage, weaponType))
                    {
                        reach = _shrapnelRay[k].distance;
                        break;
                    }
                }
            }

            HitEffect.SpawnShrapnel(origin, d, reach, range);
        }

        // Efektler hasardan SONRA ama yüzey hasardan ÖNCE okundu (ShrapnelStrike):
        // bu patlama kalkanı düşürse bile ilk kıymık kalkana çarpmıştır.
        foreach (var h in _shrapnelHits)
        {
            result.caught++;
            result.fragments += h.fragments;
            if (h.col == null) continue;

            bool lethal = h.col.GetComponent<HealthBar>()?.currentHealth <= 0f;
            HitEffect.SpawnImpact(h.point, h.dir, h.col.transform.position,
                                  h.surface, h.damage, lethal);
            if (h.surface == ImpactSurface.Shield) ShieldFlash(h.col, h.point);
        }

        return result;
    }

    /// <summary>Tek bir kıymığı uygular; vurulabildiyse true.</summary>
    static bool ShrapnelStrike(Collider2D col, Object key, Vector2 point, Vector2 dir,
                               float damage, WeaponType weaponType)
    {
        bool first   = !_shrapnelIdx.TryGetValue(key, out int idx);
        var  surface = first ? SurfaceOf(col) : _shrapnelHits[idx].surface;

        // Bomba TryDamage'ın bilmediği tek alıcı (bkz. AreaDamage).
        var bomb = col.GetComponent<Bomb>();
        if (bomb != null) bomb.TakeDamage(damage);
        else if (!TryDamage(col, damage, weaponType)) return false;

        if (first)
        {
            _shrapnelIdx[key] = _shrapnelHits.Count;
            _shrapnelHits.Add(new ShrapnelHit
            {
                col = col, point = point, dir = dir, surface = surface,
                damage = damage, fragments = 1,
            });
        }
        else
        {
            var h = _shrapnelHits[idx];
            h.damage += damage;
            h.fragments++;
            _shrapnelHits[idx] = h;
        }
        return true;
    }

    /// <summary>
    /// Collider'ın hasar alıcısı; kalkan yüzeyi için kalkanın SAHİBİ. Bir
    /// geminin kalkanı ve gövdesi aynı alıcıdır — kıymık kabuğu delip aynı
    /// geminin gövdesine ikinci kez vurmasın diye.
    /// </summary>
    static Object ReceiverOf(Collider2D col)
    {
        var owner = ShieldOwnerOf(col);
        return owner != null ? owner : ReceiverKey(col);
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    /// <summary>
    /// Bu collider patlamayı TETİKLER mi? Patlayan mermi yalnızca gerçek bir
    /// hedefe çarpınca patlamalı; yoksa sahnedeki alakasız bir collider'a
    /// değip havada patlar ve menzili sessizce kısalırdı.
    /// </summary>
    public static bool IsBlastTarget(Collider2D col)
        => col != null && (ShieldOwnerOf(col) != null || ReceiverKey(col) != null);

    /// <summary>
    /// Bu collider'ın hasar ALICISI kim? Tekilleştirme anahtarı budur; bir
    /// nesnenin kaç collider'ı olursa olsun tek bir alıcıya karşılık gelir.
    /// </summary>
    static Object ReceiverKey(Collider2D col)
    {
        var hardpoint = col.GetComponent<BossHardpoint>();
        if (hardpoint != null) return hardpoint;

        var boss = col.GetComponent<BossShip>();
        if (boss != null) return boss;

        var enemy = col.GetComponent<EnemyBot>();
        if (enemy != null) return enemy;

        var asteroid = col.GetComponent<Asteroid>();
        if (asteroid != null) return asteroid;

        var bomb = col.GetComponent<Bomb>();
        if (bomb != null) return bomb;

        return null;   // oyuncu gemisi, komponentler, mermiler: patlama onlara işlemez
    }
}
