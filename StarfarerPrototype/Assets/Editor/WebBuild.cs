using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Tarayıcı build'i:
///   Unity.exe -batchmode -projectPath . -buildTarget WebGL -executeMethod WebBuild.Web
///
/// Neden var: testçiye ulaşmanın en ucuz yolu bu. Mağaza, ücret, inceleme ve
/// kurulum yok — link verilir, açılır. Denge verisi toplamak için yeterli;
/// PERFORMANS ölçmek için değil (tarayıcıdaki kare süresi native'i temsil
/// etmez, bkz. PerfSampler).
///
/// AYARLAR BURADA ZORLANIR, proje ayarına güvenilmez. Sıkıştırma kapalı
/// kalırsa indirme boyutu birkaç katına çıkar ve bunu ancak ilk oyuncu fark
/// eder; build script'i her seferinde doğru değeri yazarsa o hata hiç doğmaz.
///
/// Çıktı Builds/Web/ altında, gitignore kapsamında.
/// </summary>
public static class WebBuild
{
    public const string OutDir = "Builds/Web";

    public static void Web()    => Build(development: false);
    public static void WebDev() => Build(development: true);

    static void Build(bool development)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Fail("Build listesinde etkin sahne yok.");
            return;
        }

        ApplySettings();

        var outPath = Path.GetFullPath(OutDir);
        Directory.CreateDirectory(outPath);

        var opts = new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = outPath,
            target           = BuildTarget.WebGL,
            targetGroup      = BuildTargetGroup.WebGL,
            options          = development ? BuildOptions.Development : BuildOptions.None
        };

        var summary = BuildPipeline.BuildPlayer(opts).summary;
        Debug.Log($"[WebBuild] sonuc={summary.result} development={development} " +
                  $"sikistirma={PlayerSettings.WebGL.compressionFormat} " +
                  $"boyut={summary.totalSize} sure={summary.totalTime} yol={outPath}");

        if (summary.result != BuildResult.Succeeded)
        {
            Fail($"Build basarisiz: {summary.result}, {summary.totalErrors} hata");
            return;
        }

        EditorApplication.Exit(0);
    }

    static void ApplySettings()
    {
        // Brotli, gzip'ten belirgin biçimde küçük. İlk açılışta indirilen şey
        // oyunun tamamı olduğu için buradaki fark doğrudan bekleme süresidir.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;

        // Sunucu sıkıştırılmış dosyaları doğru Content-Encoding ile servis
        // etmezse tarayıcı .br dosyasını çözemez ve oyun BOŞ EKRANLA açılır.
        // Fallback, çözmeyi JavaScript'e devreder: açılış birkaç yüz ms yavaşlar
        // ama sunucu yapılandırması yanlışken bile oyun çalışır. IIS'te bu
        // eşlemeyi yapmak ayrı bir iş; sessizce boş ekran vermektense
        // yavaş açılmak yeğdir.
        PlayerSettings.WebGL.decompressionFallback = true;

        // Varlıklar tarayıcı önbelleğinde kalsın: testçi her açılışta oyunun
        // tamamını yeniden indirmesin.
        PlayerSettings.WebGL.dataCaching = true;

        // Unity'nin Default şablonu sabit boyutlu bir kutu çizer; landscape bir
        // oyun telefon tarayıcısında ekranın ortasında küçük bir kare olarak
        // kalırdı. Proje şablonu pencerenin tamamını canvas'a verir.
        PlayerSettings.WebGL.template = "PROJECT:Starfarer";
    }

    static void Fail(string message)
    {
        Debug.LogError("[WebBuild] " + message);
        EditorApplication.Exit(1);
    }
}
