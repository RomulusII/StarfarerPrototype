using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Editör kapalıyken komut satırından Android paketi üretir:
///   Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod AndroidBuild.Aab
///
/// İKİ ÇIKTI VARDIR VE İKİSİ AYNI ŞEY DEĞİLDİR:
///   Aab    — Google Play'e yüklenen paket. Play yeni uygulamalarda APK kabul
///            etmiyor, yani dağıtım paketi budur.
///   Apk    — telefona doğrudan kurulan paket. Play'e yüklenemez ama
///            "adb install" ile saniyeler içinde denenir; yerel test için bu.
///
/// Varsayılan RELEASE üretir. Sebep: denge kaydı artık Development Build'e bağlı
/// değil (bkz. BalanceLog.Enabled) ve bu iddia ancak development OLMAYAN bir
/// build'de sınanırsa doğrulanmış sayılır. Logcat'te yığın izi gerekiyorsa
/// AndroidBuild.ApkDev kullan.
///
/// SÜRÜM NUMARASI: Play aynı versionCode'u iki kez kabul etmez. Yükleme
/// öncesi -sfVersionCode ile geçilebilir; geçilmezse ProjectSettings'teki
/// değer kullanılır ve ikinci yükleme reddedilir.
///
/// Çıktı Builds/Android/ altında, gitignore kapsamında.
/// </summary>
public static class AndroidBuild
{
    public static void Aab()    => Build(development: false, bundle: true);
    public static void Apk()    => Build(development: false, bundle: false);
    public static void ApkDev() => Build(development: true,  bundle: false);

    static void Build(bool development, bool bundle)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Fail("Build listesinde etkin sahne yok.");
            return;
        }

        var name    = bundle      ? "Starfarer.aab"
                    : development ? "Starfarer-dev.apk"
                                  : "Starfarer.apk";
        // Play imzalamayı kendi devralır (Play App Signing) ama YÜKLEME anahtarı
        // yine de bizden gelir: keystore ayarlanmamışsa Unity debug anahtarıyla
        // imzalar ve Play o paketi reddeder. Sessizce reddedilen bir yüklemeyi
        // saatler sonra anlamaktansa burada durmak yeğdir.
        //
        // Kontrol proje ayarlarına DOKUNMADAN ÖNCE yapılır: hata durumunda
        // buildAppBundle ve versionCode değişmiş hâlde kalmasın.
        if (bundle && !PlayerSettings.Android.useCustomKeystore)
        {
            Fail("AAB için imzalama anahtarı ayarlı değil (Player Settings > Publishing Settings). "
               + "Debug anahtarıyla imzalanan paketi Play kabul etmez.");
            return;
        }

        var outPath = Path.GetFullPath("Builds/Android/" + name);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        EditorUserBuildSettings.buildAppBundle = bundle;

        ApplyVersionCodeOverride();

        var opts = new BuildPlayerOptions
        {
            scenes            = scenes,
            locationPathName  = outPath,
            target            = BuildTarget.Android,
            targetGroup       = BuildTargetGroup.Android,
            options           = development ? BuildOptions.Development : BuildOptions.None
        };

        var summary = BuildPipeline.BuildPlayer(opts).summary;
        Debug.Log($"[AndroidBuild] sonuc={summary.result} paket={(bundle ? "aab" : "apk")} " +
                  $"development={development} versionCode={PlayerSettings.Android.bundleVersionCode} " +
                  $"targetSdk={PlayerSettings.Android.targetSdkVersion} " +
                  $"boyut={summary.totalSize} sure={summary.totalTime} yol={outPath}");

        if (summary.result != BuildResult.Succeeded)
        {
            Fail($"Build basarisiz: {summary.result}, {summary.totalErrors} hata");
            return;
        }

        EditorApplication.Exit(0);
    }

    /// <summary>
    /// <c>-sfVersionCode N</c> geçilmişse versionCode'u ezer. Play aynı kodu
    /// iki kez kabul etmediği için her yükleme yeni bir sayı ister; bunu elle
    /// ProjectSettings'ten artırmak, unutulduğunda build'i çöpe atıyor.
    /// Argüman yoksa proje ayarı olduğu gibi kullanılır.
    /// </summary>
    static void ApplyVersionCodeOverride()
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "-sfVersionCode") continue;
            if (!int.TryParse(args[i + 1], out int code) || code <= 0)
            {
                Debug.LogWarning($"[AndroidBuild] -sfVersionCode okunamadi: '{args[i + 1]}', "
                               + "proje ayari kullaniliyor.");
                return;
            }
            PlayerSettings.Android.bundleVersionCode = code;
            return;
        }
    }

    static void Fail(string message)
    {
        Debug.LogError("[AndroidBuild] " + message);
        EditorApplication.Exit(1);
    }
}
