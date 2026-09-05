using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OneHistory.ColdBrewTwin.Editor
{
    public static class SimulationBuild
    {
        [MenuItem("Cold Brew/Build Windows Simulation %#&b")]
        public static void BuildWindows()
        { BuildAt("Builds/Windows/ColdBrewTwin.exe"); }

        [MenuItem("Cold Brew/Build Water Flow Demo %#&w")]
        public static void BuildWaterFlow()
        { BuildAt("Builds/WaterFlow/ColdBrewTwin.exe"); }

        [MenuItem("Cold Brew/Build Studio Appearance %#&s")]
        public static void BuildStudio()
        { BuildAt("Builds/Studio/ColdBrewTwin.exe"); }

        static void BuildAt(string output)
        {
            PlayerSettings.companyName = "OneHistory";
            PlayerSettings.productName = "ColdBrewTwin";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/ColdBrewTwin.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Directory.CreateDirectory("Docs");
            File.WriteAllText("Docs/build-result.txt", $"{DateTime.Now:O}\nResult: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nBytes: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\n");
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Simulation build failed.");
            Debug.Log("ColdBrewTwin simulation built: " + Path.GetFullPath(output));
        }
    }
}
