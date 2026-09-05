using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Simülasyon koşucusunun Windows player'ını üretir:
///   Unity.exe -batchmode -projectPath . -executeMethod SimBuild.Player -quit
///
/// NEDEN PLAYER, NEDEN EDİTÖR PLAYMODE DEĞİL:
///   • Paralellik. Simülasyon binlerce koşudur ve bu makinede 12 iş parçacığı
///     var; player exe'si aynı anda N süreç olarak koşar. Editör tek örnektir
///     ve batchmode'da playmode'a girmesi ayrıca kırılgandır.
///   • Determinizm. Player döngüsü editörün inspector/asset yeniden yükleme
///     gürültüsünü taşımaz; aynı tohum aynı kare dizisini verir.
///   • Hız. Editör her koşuda derleme ve asset veritabanı açar.
///
/// Build BİR KEZ alınır, koşular parametrelerini komut satırından okur
/// (bkz. SimConfig). Denge sayısını değiştirmek için yeniden derlemek
/// gerekseydi duyarlılık analizi (her parametre ±%20) pratikte imkânsız olurdu.
///
/// Çıktı Builds/Sim/ altında, gitignore kapsamında.
/// </summary>
public static class SimBuild
{
    public const string OutDir  = "Builds/Sim";
    public const string ExeName = "Starfarer-sim.exe";

    public static void Player()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Fail("Build listesinde etkin sahne yok.");
            return;
        }

        var outPath = Path.GetFullPath(Path.Combine(OutDir, ExeName));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        var opts = new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = outPath,
            target           = BuildTarget.StandaloneWindows64,
            targetGroup      = BuildTargetGroup.Standalone,
            // RELEASE. Development build profiler ve derin yığın izi taşır;
            // koşu sayısı yüzlerceyken bunun bedeli ölçümün kendisinden büyük.
            options          = BuildOptions.None,
        };

        var summary = BuildPipeline.BuildPlayer(opts).summary;
        Debug.Log($"[SimBuild] sonuc={summary.result} boyut={summary.totalSize} " +
                  $"sure={summary.totalTime} yol={outPath}");

        if (summary.result != BuildResult.Succeeded)
        {
            Fail($"Build basarisiz: {summary.result}, {summary.totalErrors} hata");
            return;
        }

        EditorApplication.Exit(0);
    }

    static void Fail(string message)
    {
        Debug.LogError("[SimBuild] " + message);
        EditorApplication.Exit(1);
    }
}
