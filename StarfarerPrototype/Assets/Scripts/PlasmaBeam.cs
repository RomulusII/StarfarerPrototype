using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plazma beam: namlu ucundan başlayarak genişler (GROWING), enerji bitince
/// baş durur kuyruk yetişir (FADING), uzunluk sıfırlanınca yok olur.
///
/// Geçtiği dikdörtgen alandaki tüm EnemyBot'lara hasar verir.
/// Toplam hasar = totalEnergy. Düşman başına dps hızında tüketilir.
/// </summary>
public class PlasmaBeam : MonoBehaviour
{
    // WeaponController tarafından spawn öncesi atanır
    public float      beamWidth;    // dünya birimi — şarj küresinin çapı
    public float      maxLength;    // dünya birimi — beamWidth * 4
    public float      headSpeed;    // baş ilerleme hızı (dünya birimi / sn)
    public float      totalEnergy;  // toplam hasar havuzu
    public float      dps;          // enerji tüketim hızı (düşman başına / sn)
    public WeaponType weaponType;

    Vector3 _origin;
    Vector3 _firingDir;
    float   _rotAngle;
    float   _headDist;
    float   _tailDist;
    bool    _headStopped;

    SpriteRenderer       _sr;
    readonly List<Collider2D>      _buffer  = new();
    readonly HashSet<int>          _hitSet  = new();  // aynı frame'de çift hasar önleme

    void Awake()
    {
        _origin    = transform.position;
        _firingDir = transform.up;         // WeaponMount local Y = ateş yönü
        _rotAngle  = transform.eulerAngles.z;

        // PPU=1 → localScale dünya boyutunu doğrudan belirler
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1),
                                   new Vector2(0.5f, 0.5f), 1f);

        _sr               = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite        = sprite;
        _sr.color         = new Color(0.5f, 1f, 0.3f, 0.88f);
        _sr.sortingOrder  = 2;
    }

    void Update()
    {
        // ── 1. Baş ilerleme ──────────────────────────────────────────────────
        if (!_headStopped)
        {
            _headDist += headSpeed * Time.deltaTime;
            if (_headDist >= maxLength)
            {
                _headDist    = maxLength;
                _headStopped = true;
            }
            if (totalEnergy <= 0f)
                _headStopped = true;
        }

        // ── 2. Kuyruk ilerlemesi (baş durduğunda) ───────────────────────────
        if (_headStopped)
        {
            _tailDist += headSpeed * Time.deltaTime;
            if (_tailDist >= _headDist)
            {
                Destroy(gameObject);
                return;
            }
        }

        float length = _headDist - _tailDist;
        if (length < 0.001f) { Destroy(gameObject); return; }

        // ── 3. Transform güncelle ────────────────────────────────────────────
        float centerDist = (_tailDist + _headDist) * 0.5f;
        transform.position   = _origin + _firingDir * centerDist;
        transform.localScale = new Vector3(beamWidth, length, 1f);

        // Alfa: enerji azaldıkça solar
        if (totalEnergy > 0f)
        {
            float initEnergy = dps > 0f ? dps / 3f : 1f; // başlangıç tahmini
            // Sadece görsel ipucu — tam hassasiyet gerekmez
            _sr.color = new Color(0.5f, 1f, 0.3f, Mathf.Lerp(0.35f, 0.88f,
                                   Mathf.Clamp01(totalEnergy / Mathf.Max(1f, dps / 3f))));
        }
        else
        {
            _sr.color = new Color(0.5f, 1f, 0.3f, 0.30f);
        }

        // ── 4. Alan hasarı ───────────────────────────────────────────────────
        if (totalEnergy <= 0f) return;

        _hitSet.Clear();
        int count = Physics2D.OverlapBox(
            (Vector2)transform.position,
            new Vector2(beamWidth, length),
            _rotAngle,
            ContactFilter2D.noFilter,
            _buffer);

        for (int i = 0; i < count; i++)
        {
            var col = _buffer[i];

            // Beam yalnızca düşman objelerine hasar verir
            bool isEnemy = col.GetComponent<EnemyBot>()   != null
                        || col.GetComponent<BossHardpoint>() != null
                        || col.GetComponent<BossShip>()    != null;
            if (!isEnemy) continue;

            int id = col.gameObject.GetInstanceID();
            if (!_hitSet.Add(id)) continue;  // bu frame zaten hasar aldı

            float tickDmg = Mathf.Min(dps * Time.deltaTime, totalEnergy);
            if (tickDmg <= 0f) break;

            DamageUtil.TryDamage(col, tickDmg, weaponType);
            totalEnergy -= tickDmg;

            if (totalEnergy <= 0f)
            {
                _headStopped = true;
                break;
            }
        }
    }
}
