using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ÇEVRİMDIŞI DENGE MODELİ — zorluk eğrisini oyuncunun oynayışına göre değil,
/// hesapla belirlemek için.
///
///   Unity.exe -batchmode -projectPath . -executeMethod CurveModel.Rapor
///             [-sfIsabet 0.30] [-sfTurretIsabet 0.70] [-sfToplama 0.75]
///             [-sfLevel 100]
///
/// NEDEN EDİTÖR SCRIPT'İ, NEDEN AYRI BİR ARAÇ DEĞİL: düşman tipleri, komponent
/// fiyatları, level eğrisi ve dalga kompozisyonu KODDA tanımlı. Formülleri
/// başka bir dile kopyalasaydık model ile oyun ilk değişiklikte ayrışır ve tam
/// da ayrıştığı yerde yanlış sayı üretirdi — üstelik bunu kimse fark etmezdi.
/// Model oyunun kendi nesnelerini çağırıyor: BalanceConfig, LevelCurve,
/// ChapterData, ComponentCatalog, ChapterManager.FillByBudget.
///
/// NE HESAPLAR (v1):
///   1. Bir levelin GELİRİ — bütçe × drop + asteroit, toplama verimiyle
///   2. O levele kadar BİRİKEN kaynak
///   3. Bu kaynakla alınabilecek EN YÜKSEK hasar çıktısı (üst sınır)
///   4. Leveldeki dalganın toplam ETKİN CANI (zırh ve tip zafiyetleri dahil)
///   5. İkisinden çıkan TEMİZLEME SÜRESİ
///
/// NE HESAPLAMAZ (v1): oyuncunun aldığı hasar. Onun için düşmanın ateş etme
/// süresini modellemek gerekiyor ve o model doğrulanmadan sayı üretmek
/// yanıltıcı olur. Temizleme süresi ise log'daki gerçek level süreleriyle
/// DOĞRUDAN karşılaştırılabilir; modelin sınavı bu.
///
/// REFERANS OYUNCU: model bir isabet oranı olmadan sayı üretemez ve ölçülen
/// oran %23-60 arasında değişiyor. Eğri, referans oyuncunun kıl payı geçeceği
/// yere kurulur; ondan iyisi rahat geçer, kötüsü bir yerde ölür. Varsayılan
/// %30 — bu bir tasarım kararıdır, ölçüm değil.
/// </summary>
public static class CurveModel
{
    /// <summary>Kompozisyon kaç kez örneklenip ortalanacak (FillByBudget rastgele).</summary>
    const int Ornek = 200;

    public static void Rapor()
    {
        float isabet       = Arg("-sfIsabet",       0.30f);
        float turretIsabet = Arg("-sfTurretIsabet", 0.70f);
        float toplama      = Arg("-sfToplama",      0.75f);
        int   maxLevel     = (int)Arg("-sfLevel",   100f);

        var cfg      = BalanceConfig.Instance;
        var chapters = ChapterData.CreateDefaultChapters();
        var satir    = new StringBuilder();

        satir.AppendLine("level;butce;dalga;metal_gelir;kristal_gelir;metal_birikim;kristal_birikim;" +
                         "dalga_ehp;oyuncu_dps;temizleme_sn;kadro");

        float metalBirikim = 0f, kristalBirikim = 0f;

        Debug.Log($"[CurveModel] referans: ana isabet {isabet:P0}, turret {turretIsabet:P0}, " +
                  $"toplama {toplama:P0}, {maxLevel} level");

        for (int L = 1; L <= maxLevel; L++)
        {
            float butce   = cfg.ThreatBudget(L);
            int   dalga   = L < 50 ? 3 : 4;
            var   pool    = PoolFor(chapters, L);
            if (pool == null || pool.Length == 0) continue;

            // ── Kompozisyon: gerçek kurucudan örneklenir ──────────────────────
            var sayim = new Dictionary<EnemyTypeData, float>();
            for (int i = 0; i < Ornek; i++)
            {
                var liste = new List<EnemyTypeData>();
                ChapterManager.FillByBudget(liste, pool, Mathf.RoundToInt(butce));
                foreach (var t in liste)
                {
                    if (t == null) continue;
                    sayim.TryGetValue(t, out float n);
                    sayim[t] = n + 1f / Ornek;
                }
            }

            // ── Gelir ────────────────────────────────────────────────────────
            // Kaynak türü düşman başına farklı (debrisResourceType), o yüzden
            // kompozisyondan türetiliyor — tek bir ortalama sayı yanlış olurdu.
            float drop = cfg.DropPerThreat(L);
            float metal = 0f, kristal = 0f;
            foreach (var kv in sayim)
            {
                float miktar = kv.Key.threatScore * drop * kv.Value * toplama;
                if (kv.Key.debrisResourceType == ResourceType.EnergyCrystal) kristal += miktar;
                else                                                        metal   += miktar;
            }
            metal   += cfg.AsteroidYieldPerLevel(L) * toplama;
            metalBirikim   += metal;
            kristalBirikim += kristal;

            // ── Dalganın etkin canı ──────────────────────────────────────────
            var  olcek = EnemyScaling.ForLevel(L);
            float ehp  = 0f;
            foreach (var kv in sayim)
                ehp += kv.Value * (kv.Key.maxHP * olcek.hp + kv.Key.maxShield);

            // ── Oyuncunun alabileceği en yüksek DPS ──────────────────────────
            float dps = EnAyiyiDps(metalBirikim, kristalBirikim, isabet, turretIsabet, sayim);

            float sure = dps > 0.01f ? ehp / dps : -1f;

            var kadro = string.Join(" ", sayim.OrderByDescending(k => k.Value)
                                             .Take(4)
                                             .Select(k => $"{k.Key.displayName}×{k.Value:0.0}"));

            satir.AppendLine(string.Join(";", new[]
            {
                L.ToString(), F(butce), dalga.ToString(), F(metal), F(kristal),
                F(metalBirikim), F(kristalBirikim), F(ehp), F(dps), F(sure), kadro
            }));

            if (L <= 5 || L % 10 == 0)
                Debug.Log($"[CurveModel] L{L,3}  butce {butce,6:0.0}  gelir {metal,6:0}m/{kristal,5:0}k  " +
                          $"birikim {metalBirikim,7:0}m/{kristalBirikim,6:0}k  " +
                          $"dalga_ehp {ehp,7:0}  dps {dps,6:0.0}  temizleme {sure,6:0.0} sn");
        }

        var yol = Path.GetFullPath("Tools/Balance/curve.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(yol));
        File.WriteAllText(yol, satir.ToString());
        Debug.Log($"[CurveModel] tablo yazildi: {yol}");

        EditorApplication.Exit(0);
    }

    /// <summary>
    /// Biriken kaynakla ulaşılabilecek EN YÜKSEK hasar çıktısı — üst sınır.
    ///
    /// Kasten iyimser: soru "oyuncu en fazla ne kadar güçlenebilir", yani
    /// eğrinin altından geçmesi gereken tavan. Gerçek oyuncu bunun altında
    /// kalır (kalkana ve tamire de harcar), dolayısıyla model temizleme
    /// süresini OLDUĞUNDAN KISA tahmin eder. Bu yön bilinçli: modelin "bu
    /// level kolay" dediği yer gerçekten kolaydır.
    ///
    /// Tip zafiyetleri hesaba katılır (bkz. EnemyTypeData damage modifiers):
    /// Swarm kinetiğe 0.30, plazmaya 1.80 tepki veriyor. Silah seçimi bu
    /// yüzden tek bir "DPS" sayısına indirgenemez — kompozisyona göre
    /// ağırlıklandırılıyor.
    /// </summary>
    static float EnAyiyiDps(float metal, float kristal, float isabet, float turretIsabet,
                            Dictionary<EnemyTypeData, float> kadro)
    {
        float enIyi = 0f;

        // Üç silahın hepsi denenir ve en iyisi seçilir: hangisinin "en iyi"
        // olduğu levelin KADROSUNA bağlı (Swarm kinetiğe 0.30, plazmaya 1.80
        // tepki veriyor), yani tek bir silahı varsaymak yanlış cevap verirdi.
        foreach (WeaponType wt in new[] { WeaponType.Kinetic, WeaponType.Laser, WeaponType.Plasma })
        {
            var def = ComponentCatalog.Weapon(wt);
            if (def == null) continue;

            float m = metal, k = kristal;

            // Kinetik başlangıç silahıdır (bkz. WeaponController varsayılanı),
            // diğerleri satın alınır. Parası yetmiyorsa o silah bu levelde yok.
            if (wt != WeaponType.Kinetic)
            {
                if (def.costResource == ResourceType.EnergyCrystal) { if (k < def.cost) continue; k -= def.cost; }
                else                                               { if (m < def.cost) continue; m -= def.cost; }
            }

            float dps = SilahDpsi(def, wt, ref m, ref k, isabet, kadro);
            if (dps > enIyi) enIyi = dps;
        }

        return enIyi;
    }

    /// <summary>
    /// Bir silahın parası bitene kadar yükseltilmiş hâlinin DPS'i.
    ///
    /// Hasar ve atış hızı izleri AÇGÖZLÜ seçilir: her adımda birim maliyet
    /// başına daha çok DPS getiren alınır. İkisi de çarpan olduğu için
    /// (DPS = hasar × çarpan_h × çarpan_a / aralık) tek bir ize yatırmak her
    /// zaman daha kötüdür; modelin üst sınır iddiası ancak ikisi birlikte
    /// optimize edilirse doğru olur.
    /// </summary>
    static float SilahDpsi(ComponentDefinition def, WeaponType wt, ref float metal, ref float kristal,
                           float isabet, Dictionary<EnemyTypeData, float> kadro)
    {
        var cfg = BalanceConfig.Instance;
        bool kristalle = def.costResource == ResourceType.EnergyCrystal;

        int hKademe = 0, aKademe = 0;
        for (int adim = 0; adim < 200; adim++)
        {
            int hFiyat = ComponentCatalog.StatUpgradeCost(def, hKademe, "damage");
            int aFiyat = ComponentCatalog.StatUpgradeCost(def, aKademe, "fireRate");

            float simdi  = cfg.StatMultiplier(hKademe) * cfg.StatMultiplier(aKademe);
            float hKazanc = (cfg.StatMultiplier(hKademe + 1) * cfg.StatMultiplier(aKademe) - simdi) / Mathf.Max(1, hFiyat);
            float aKazanc = (cfg.StatMultiplier(hKademe) * cfg.StatMultiplier(aKademe + 1) - simdi) / Mathf.Max(1, aFiyat);

            bool hasarAl = hKazanc >= aKazanc;
            int  fiyat   = hasarAl ? hFiyat : aFiyat;
            float kasa   = kristalle ? kristal : metal;
            if (fiyat > kasa) break;

            if (kristalle) kristal -= fiyat; else metal -= fiyat;
            if (hasarAl) hKademe++; else aKademe++;
        }

        float hasar  = def.weaponDamage * cfg.StatMultiplier(hKademe);
        float aralik = def.weaponFireRate > 0.01f ? def.weaponFireRate / cfg.StatMultiplier(aKademe) : 1f;

        // Kompozisyona göre ağırlıklı tip çarpanı
        float carpan = 0f, agirlik = 0f;
        foreach (var kv in kadro) { carpan += kv.Value * TipCarpani(kv.Key, wt); agirlik += kv.Value; }
        carpan = agirlik > 0.01f ? carpan / agirlik : 1f;

        return hasar / Mathf.Max(0.01f, aralik) * isabet * carpan;
    }

    /// <summary>
    /// Düşmanın o silah tipine karşı hasar çarpanı; tanımsızsa 1.
    ///
    /// Gövde ve kalkan dirençleri AYRI tablolar. Model gövdeyi esas alıyor:
    /// kalkanlı tipler azınlıkta ve kalkan zaten yenilendiği için ayrı bir
    /// zaman modeli isterdi — v1'in kapsamı dışında, ama sayının hangi
    /// tablodan geldiği bilinsin.
    /// </summary>
    static float TipCarpani(EnemyTypeData d, WeaponType tip)
    {
        if (d.hullResistances == null) return 1f;
        foreach (var m in d.hullResistances)
            if (m.weaponType == tip) return m.multiplier;
        return 1f;
    }

    static EnemyTypeData[] PoolFor(ChapterData[] chapters, int level)
    {
        int idx = Mathf.Clamp((level - 1) / LevelCurve.Instance.levelsPerChapter, 0, chapters.Length - 1);
        return chapters[idx]?.enemyPool;
    }

    static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    static float Arg(string ad, float varsayilan)
    {
        var raw = BuildStamp.Arg(ad);
        return raw != null && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
             ? v : varsayilan;
    }
}
