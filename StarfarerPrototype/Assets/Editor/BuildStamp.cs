using UnityEditor;
using UnityEngine;

/// <summary>
/// Paketin sürüm numarasını yazar. Üç build script'i de (Web/Android/Sim)
/// buradan geçer.
///
/// İKİ FARKLI SORU, İKİ FARKLI ALAN:
///   <see cref="GameVersion.Surum"/> — insanın gördüğü sürüm, koddaki tek
///        doğruluk kaynağı. Elle iki yerde tutulduğu sürece biri unutulur.
///   <c>-sfBuild N</c>              — O PAKET. Aynı sürümden onlarca paket
///        üretilir ve testçinin hangisini oynadığı ancak bu sayıyla bilinir.
///
/// SAYI NEDEN ŞART (tarayıcı tarafı): Unity'nin WebGL çıktısında dosya adları
/// SABİTTİR — `Build/xxx.wasm.br` her build'de aynı ad. Yeni bir paket
/// yüklendiğinde tarayıcının eski kopyayı vermesini engelleyen tek şey
/// önbellek başlıkları ve Unity'nin IndexedDB anahtarıdır; ikinci anahtar
/// `productVersion`, yani BU değer. Sabit kalırsa testçi "düzelttim" denen
/// ayarı eski pakette oynar, kayıt yeni sürüm gibi görünür ve iki farklı
/// ayarın verisi tek havuzda toplanır — kaybı fark etmek de mümkün olmaz.
/// Aynı sebeple <c>BalanceLog</c> oturum satırına `build` alanını yazıyor.
///
/// Argüman geçilmezse sürüm sade kalır (`1.0.0`): elle alınan bir build,
/// numarası olmadığı için kendini ele verir.
/// </summary>
public static class BuildStamp
{
    /// <summary>
    /// <c>PlayerSettings.bundleVersion</c>'ı yazar ve yazdığı değeri döndürür.
    /// <c>-sfBuild N</c> geçilmişse sürüme <c>+bN</c> eklenir.
    /// </summary>
    public static string Apply()
    {
        var version = GameVersion.Surum;

        var raw = Arg("-sfBuild");
        if (raw != null)
        {
            if (int.TryParse(raw, out int n) && n > 0) version += "+b" + n;
            else Debug.LogWarning($"[BuildStamp] -sfBuild okunamadi: '{raw}', " +
                                  "surum numarasiz damgalaniyor.");
        }

        PlayerSettings.bundleVersion = version;
        Debug.Log($"[BuildStamp] surum={version}");
        return version;
    }

    /// <summary>
    /// Damgayı geri alır: <c>bundleVersion</c> yeniden <see cref="GameVersion.Surum"/>
    /// olur. Build BİTTİKTEN sonra çağrılır, paket numarayı çoktan almış olur.
    ///
    /// NEDEN GEREKLİ: numara <c>ProjectSettings.asset</c>'e yazılıyor ve o dosya
    /// git'te izleniyor. Geri alınmazsa her dağıtımdan sonra çalışma ağacında
    /// bir değişiklik kalır; birkaç dağıtım sonra <c>git status</c> sinyal
    /// olmaktan çıkar ve içine gerçek bir ayar değişikliği karışırsa fark
    /// edilmez. (Nitekim bir kez karıştı: UnityConnectSettings'teki gerçek bir
    /// ayar, "build artığı" sanılıp aylarca commit'lenmeden bekleyebilirdi.)
    ///
    /// Numaranın TEK doğruluk kaynağı <c>Tools/Deploy/build-number.txt</c>.
    /// Aynı sayıyı iki yerde tutmak, er geç ikisinin ayrışması demek.
    ///
    /// Yan faydası: numarasız bir sürüm (<c>1.0.0</c>) gören, o paketin ELLE
    /// alındığını anlar — pipeline her zaman numara geçiyor.
    /// </summary>
    public static void Restore()
    {
        PlayerSettings.bundleVersion = GameVersion.Surum;

        // Batchmode kapanışında ayarların diske yazılacağına güvenmiyoruz:
        // yazılmazsa dosya numaralı hâliyle kalır ve bu metodun varlık sebebi
        // ortadan kalkar.
        AssetDatabase.SaveAssets();

        Debug.Log($"[BuildStamp] surum geri alindi: {GameVersion.Surum}");
    }

    /// <summary>
    /// Komut satırından <paramref name="name"/> argümanının değerini okur;
    /// yoksa null. Batchmode'da Unity kendi bayraklarıyla birlikte bizimkileri
    /// de taşır, ayrıştırmak bize kalır.
    /// </summary>
    public static string Arg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
