using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bir dalganın birlikte uçan gemi grubu.
///
/// Formasyon sistemi yazılmıştı ama HİÇ ÇALIŞMIYORDU; üç ayrı sebepten:
///
///   1. Dalganın gemileri <c>spawnInterval</c> (3 sn) arayla TEK TEK doğuyordu.
///      Altı gemilik bir dalga 18 saniyeye yayılıyordu; formasyonun var olduğu
///      bir an hiç oluşmuyordu.
///   2. <c>RoleSlot.offset.x</c> hiç okunmuyordu — yalnızca y kullanılıyordu.
///      Ok formasyonu (x: 0.6 / 0.2 / 0 / -0.4) dikey bir çizgiye çöküyordu.
///   3. Doğan gemi anında kendi <see cref="ShipBrain"/>'ini çalıştırıyor ve
///      RASTGELE bir yaklaşma açısı seçiyordu. Formasyon yalnızca bir doğum
///      ofsetiydi; uçuş sırasında korunmuyordu.
///
/// Artık grup gerçek: tek bir "çapa" noktası hedefe doğru ilerler, gemiler o
/// çapaya göre kendi yuvalarını tutar. Çapa grubun EN YAVAŞ üyesinin hızıyla
/// gider — yoksa hızlılar öne fırlar ve formasyon daha ilk saniyede dağılır.
///
/// Grup, çapa oyuncuya <see cref="BreakDistance"/> kadar yaklaşınca DAĞILIR ve
/// her gemi kendi taktik AI'sına döner. Formasyon bir yaklaşma düzenidir,
/// bir dövüş düzeni değil: yakın dövüşte tipe özgü davranış (yörünge, dalış,
/// bomba koşusu) formasyondan çok daha ilginç.
/// </summary>
public class FormationGroup : MonoBehaviour
{
    /// <summary>Çapa hedefe bu kadar yaklaşınca formasyon dağılır.</summary>
    public const float BreakDistance = 9f;

    /// <summary>
    /// Emniyet: grup bu kadar süre içinde varamadıysa yine de dağılır.
    /// Hedefe ulaşamayan bir çapa yüzünden dalga sonsuza dek formasyonda
    /// kalmasın (dalga temizlenmeyince level de ilerlemez).
    /// </summary>
    const float MaxFormationTime = 30f;

    /// <summary>Normalize ofsetin dünya birimine çevrimi.</summary>
    public const float SpreadX = 3.2f;
    public const float SpreadY = 2.6f;

    readonly List<EnemyBot> _members = new();
    readonly Dictionary<EnemyBot, Vector2> _offsets = new();

    Vector2 _anchor;
    Vector2 _target;
    float   _speed;
    float   _timer;
    bool    _broken;

    /// <summary>Formasyon hâlâ geçerli mi? Dağıldıysa gemiler kendi AI'sına döner.</summary>
    public bool Active => !_broken;

    public Vector2 Anchor => _anchor;
    public float   Timer  => _timer;

    // ── Kayıt ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Yalnızca AKTİF grup kaydedilir: dağılmış bir grup bir saniye sonra zaten
    /// yok oluyor ve üyeleri kendi AI'larına dönmüş durumda.
    /// Ofsetler dünya biriminde yazılır (Add'in Spread çarpımından SONRA).
    /// </summary>
    public FormationState CaptureState()
    {
        var s = new FormationState
        {
            id     = WorldSave.IdOf(this),
            anchor = _anchor,
            target = _target,
            speed  = _speed,
            timer  = _timer,
        };

        foreach (var m in _members)
        {
            if (m == null) continue;
            s.members.Add(WorldSave.IdOf(m));
            s.offsets.Add(_offsets.TryGetValue(m, out var off) ? off : Vector2.zero);
        }
        return s;
    }

    /// <summary>Grubu kurar; üyeler düşmanlar kurulduktan SONRA bağlanır.</summary>
    public static FormationGroup Rebuild(FormationState s)
    {
        var g = Create(s.anchor, s.target);
        g._speed = s.speed;
        g._timer = s.timer;
        return g;
    }

    public void RestoreMembers(FormationState s)
    {
        for (int i = 0; i < s.members.Count && i < s.offsets.Count; i++)
        {
            var bot = WorldSave.Resolve<EnemyBot>(s.members[i]);
            if (bot == null) continue;
            _members.Add(bot);
            _offsets[bot] = s.offsets[i];
            bot.AssignFormation(this);
        }

        if (_members.Count == 0) Break();
    }

    public static FormationGroup Create(Vector2 spawnAnchor, Vector2 target)
    {
        var go = new GameObject("FormationGroup");
        var g  = go.AddComponent<FormationGroup>();
        g._anchor = spawnAnchor;
        g._target = target;
        g._speed  = 0f;
        return g;
    }

    /// <param name="offset">Normalize yuva ofseti (-1..1).</param>
    public void Add(EnemyBot bot, Vector2 offset)
    {
        if (bot == null) return;
        _members.Add(bot);
        _offsets[bot] = new Vector2(offset.x * SpreadX, offset.y * SpreadY);
        bot.AssignFormation(this);
    }

    /// <summary>Tüm üyeler eklendikten sonra çağrılır; grup hızını sabitler.</summary>
    public void Seal()
    {
        // En yavaş üyenin hızı: daha hızlısı formasyonu koparır. %85 pay
        // bırakılır ki en yavaş gemi yuvasını yakalayacak kadar rezerv bulsun.
        float slowest = float.MaxValue;
        foreach (var m in _members)
        {
            if (m == null) continue;
            slowest = Mathf.Min(slowest, m.CruiseSpeed);
        }
        _speed = slowest < float.MaxValue ? slowest * 0.85f : 1f;

        if (_members.Count == 0) Break();
    }

    /// <summary>Bir geminin tutması gereken dünya konumu.</summary>
    public Vector2 SlotOf(EnemyBot bot)
        => _offsets.TryGetValue(bot, out var off) ? _anchor + off : _anchor;

    public void Break()
    {
        if (_broken) return;
        _broken = true;
        Destroy(gameObject, 1f);   // üyeler bu kareyi bitirebilsin
    }

    void Update()
    {
        if (_broken || UpgradeUI.IsPaused) return;

        _timer += Time.deltaTime;

        // Ölen üyeleri düş; kimse kalmadıysa grup biter
        _members.RemoveAll(m => m == null);
        if (_members.Count == 0) { Break(); return; }

        _anchor = Vector2.MoveTowards(_anchor, _target, _speed * Time.deltaTime);

        if (Vector2.Distance(_anchor, _target) <= BreakDistance || _timer >= MaxFormationTime)
            Break();
    }
}
