using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Editör kapalıyken komut satırından APK üretir:
///   Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod AndroidBuild.Apk
///
/// Varsayılan RELEASE üretir. Sebep: denge kaydı artık Development Build'e bağlı
/// değil (bkz. BalanceLog.Enabled) ve bu iddia ancak development OLMAYAN bir
/// build'de sınanırsa doğrulanmış sayılır. Logcat'te yığın izi gerekiyorsa
/// AndroidBuild.ApkDev kullan.
///
/// Çıktı Builds/Android/ altında, gitignore kapsamında.
/// </summary>
public static class AndroidBuild
{
    public static void Apk()    => Build(development: false);
    public static void ApkDev() => Build(development: true);

    static void Build(bool development)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Fail("Build listesinde etkin sahne yok.");
            return;
        }

        var name    = development ? "Starfarer-dev.apk" : "Starfarer.apk";
        var outPath = Path.GetFullPath("Builds/Android/" + name);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        EditorUserBuildSettings.buildAppBundle = false;

        var opts = new BuildPlayerOptions
        {
            scenes            = scenes,
            locationPathName  = outPath,
            target            = BuildTarget.Android,
            targetGroup       = BuildTargetGroup.Android,
            options           = development ? BuildOptions.Development : BuildOptions.None
        };

        var summary = BuildPipeline.BuildPlayer(opts).summary;
        Debug.Log($"[AndroidBuild] sonuc={summary.result} development={development} " +
                  $"boyut={summary.totalSize} sure={summary.totalTime} yol={outPath}");

        if (summary.result != BuildResult.Succeeded)
        {
            Fail($"Build basarisiz: {summary.result}, {summary.totalErrors} hata");
            return;
        }

        EditorApplication.Exit(0);
    }

    static void Fail(string message)
    {
        Debug.LogError("[AndroidBuild] " + message);
        EditorApplication.Exit(1);
    }
}
