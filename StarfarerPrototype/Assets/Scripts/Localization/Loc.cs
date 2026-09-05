using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Desteklenen diller. Sıra <c>strings.txt</c>'deki sütun sırasıdır; enum
/// değerleri PlayerPrefs'e yazıldığı için mevcut değerler değiştirilemez.
/// </summary>
public enum Lang { Tr = 0, En = 1, De = 2 }

/// <summary>
/// Oyun metinlerinin dil katmanı. Bütün metinler
/// <c>Resources/Locale/strings.txt</c> içinde tek bir sekme ayraçlı tabloda
/// durur: her satır bir anahtar ve üç dildeki karşılığıdır.
///
/// Unity'nin resmi Localization paketi yerine bu seçildi. Paket, Locale ve
/// String Table asset'lerinin editör pencerelerinden kurulmasını ve
/// Addressables'ı gerektiriyor; bu projede metin taşıyan tek bir prefab yok
/// (bütün UI koddan kuruluyor), yani paketin asıl gücü olan inspector
/// bağlamaları boşa gidiyordu. Üç Latin alfabeli dil, RTL yok, çoğul kuralı
/// yok — karşılığı olmayan bir altyapı maliyeti.
///
/// <see cref="CultureInfo.CurrentCulture"/> ASLA değiştirilmez. Dil değişince
/// kültür de değişseydi string interpolasyonlarının ondalık ayracı kayardı ve
/// telemetri (<see cref="BalanceLog"/>) ile kayıt dosyaları bozulurdu. Kültüre
/// ihtiyaç duyan yerler <see cref="Culture"/> özelliğini açıkça alır.
/// </summary>
public static class Loc
{
    const string PrefKey     = "starfarer.language";
    const string TablePath   = "Locale/strings";   // Resources/Locale/strings.txt
    const int    ColumnCount = 3;                  // Lang enum'ıyla aynı sayıda

    /// <summary>Menüde dil değiştiğinde tetiklenir.</summary>
    public static event Action OnLanguageChanged;

    /// <summary>Dil seçimi kuran UI'lar bunun üzerinden döner.</summary>
    public static readonly Lang[] All = { Lang.Tr, Lang.En, Lang.De };

    static Dictionary<string, string[]> _table;
    static CultureInfo _culture;
    static Lang _language;
    static bool _tableLoaded;
    static bool _languageResolved;

    // ── Dil ───────────────────────────────────────────────────────────────────

    public static Lang Language
    {
        get { ResolveLanguage(); return _language; }
        set
        {
            ResolveLanguage();
            if (_language == value) return;

            _language = value;
            _culture  = null;
            PlayerPrefs.SetInt(PrefKey, (int)value);
            PlayerPrefs.Save();
            OnLanguageChanged?.Invoke();
        }
    }

    /// <summary>
    /// Aktif dilin kültürü — sayı biçimlemek ve <see cref="ToUpper"/> için.
    /// Süreç kültürüne dokunulmaz.
    /// </summary>
    public static CultureInfo Culture
    {
        get
        {
            if (_culture != null) return _culture;

            string name = Language switch
            {
                Lang.Tr => "tr-TR",
                Lang.De => "de-DE",
                _       => "en-US",
            };

            // IL2CPP build'inde kültür verisi kırpılmış olabilir; metnin hiç
            // görünmemesindense yanlış büyük harf yeğdir.
            try                              { _culture = CultureInfo.GetCultureInfo(name); }
            catch (CultureNotFoundException) { _culture = CultureInfo.InvariantCulture; }

            return _culture;
        }
    }

    /// <summary>
    /// Dil adları her dilde kendi adıyla yazılır ("Deutsch", "Türkçe"), yani
    /// çevrilmezler; tabloya girmemelerinin sebebi bu.
    /// </summary>
    public static string NameOf(Lang l) => l switch
    {
        Lang.Tr => "Türkçe",
        Lang.De => "Deutsch",
        _       => "English",
    };

    static void ResolveLanguage()
    {
        if (_languageResolved) return;
        _languageResolved = true;

        int saved = PlayerPrefs.GetInt(PrefKey, -1);
        _language = saved >= 0 && saved < ColumnCount
                  ? (Lang)saved
                  : Application.systemLanguage switch
                    {
                        SystemLanguage.Turkish => Lang.Tr,
                        SystemLanguage.German  => Lang.De,
                        _                      => Lang.En,
                    };
    }

    // ── Metin ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Anahtarın aktif dildeki karşılığı. Karşılık yoksa sırayla İngilizce ve
    /// Türkçe denenir; o da yoksa anahtarın kendisi döner — eksik çeviri
    /// ekranda boşluk değil, gözle görülür bir iz bırakmalı.
    /// </summary>
    public static string T(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        EnsureTable();

        if (_table.TryGetValue(key, out var row))
        {
            string v = row[(int)Language] ?? row[(int)Lang.En] ?? row[(int)Lang.Tr];
            if (!string.IsNullOrEmpty(v)) return v;
        }

        WarnMissing(key);
        return key;
    }

    /// <summary>
    /// Yer tutuculu metin: <c>Loc.T("menu.continue", level)</c>. Biçimleme
    /// aktif dilin kültürüyle yapılır, süreç kültürüyle değil.
    /// </summary>
    public static string T(string key, params object[] args)
    {
        string s = T(key);
        if (args == null || args.Length == 0) return s;

        try
        {
            return string.Format(Culture, s, args);
        }
        catch (FormatException)
        {
            // Çeviride yer tutucu bozulmuş olabilir; bu oyunu düşürmemeli.
            Debug.LogWarning($"[Loc] Yer tutucu hatası: '{key}' -> \"{s}\"");
            return s;
        }
    }

    /// <summary>
    /// Dile göre büyük harf. <c>ToUpperInvariant</c> Türkçe'de küçük i harfini
    /// noktasız I yapıyor; "Devriye Hattı" başlığı ekranda "DEVRIYE HATTI"
    /// görünüyordu.
    /// </summary>
    public static string ToUpper(string s) =>
        string.IsNullOrEmpty(s) ? s : Culture.TextInfo.ToUpper(s);

    // ── Tablo yükleme ─────────────────────────────────────────────────────────

    static void EnsureTable()
    {
        if (_tableLoaded) return;

        // Hata durumunda da işaretlenir: Resources.Load'ı her T() çağrısında
        // yeniden denemek kare başına yüzlerce disk aramasına dönerdi.
        _tableLoaded = true;
        _table       = new Dictionary<string, string[]>(512, StringComparer.Ordinal);

        var asset = Resources.Load<TextAsset>(TablePath);
        if (asset == null)
        {
            Debug.LogError($"[Loc] Metin tablosu bulunamadı: Resources/{TablePath}");
            return;
        }

        Parse(asset.text);
    }

    /// <summary>
    /// Sekme ayraçlı tablo. Virgül yerine sekme, çünkü oyun metninde virgül ve
    /// tırnak var ama sekme yok — CSV'nin kaçış kuralları hiç doğmuyor.
    /// </summary>
    static void Parse(string text)
    {
        var  lines      = text.Split('\n');
        bool headerSeen = false;

        foreach (var raw in lines)
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;

            if (!headerSeen) { headerSeen = true; continue; }   // key/tr/en/de başlığı

            var cells = line.Split('\t');
            if (cells.Length < 2) continue;

            string key = cells[0].Trim();
            if (key.Length == 0) continue;

            var row = new string[ColumnCount];
            for (int i = 0; i < ColumnCount; i++)
            {
                string cell = i + 1 < cells.Length ? cells[i + 1] : null;
                row[i] = string.IsNullOrEmpty(cell) ? null : Unescape(cell);
            }

            _table[key] = row;
        }
    }

    /// <summary>Her metin tek satıra sığmak zorunda; satır sonu \n yazılır.</summary>
    static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;

        var sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n switch { 'n' => '\n', 't' => '\t', _ => n });
                continue;
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    // ── Teşhis ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    static readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
#endif

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    static void WarnMissing(string key)
    {
#if UNITY_EDITOR
        if (_warned.Add(key))
            Debug.LogWarning($"[Loc] Karşılığı yok: '{key}' ({Language})");
#endif
    }
}
