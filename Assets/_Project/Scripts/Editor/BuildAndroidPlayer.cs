using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Headless APK build. Run via -executeMethod BuildAndroidPlayer.BuildDevelopment.</summary>
public static class BuildAndroidPlayer
{
    const string k_OutputDir = "Builds/Android";

    [MenuItem("MiniBrawl/Build Development APK")]
    public static void BuildDevelopment() => Build(true);

    [MenuItem("MiniBrawl/Build Release APK")]
    public static void BuildRelease() => Build(false);

    static void Build(bool development)
    {
        AndroidSetup.Configure();   // keep player settings in one place

        string apk = Path.Combine(k_OutputDir, development ? "MiniBrawl-dev.apk" : "MiniBrawl.apk");
        Directory.CreateDirectory(k_OutputDir);

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes — run MiniBrawl > Build Prototype Scene first.");

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            // Development build keeps the profiler and logcat output available, which is the whole
            // point of the first device build.
            options = development
                ? BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler
                : BuildOptions.None,
        };

        BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
        bool ok = summary.result == BuildResult.Succeeded;

        Debug.Log($"[BuildAndroidPlayer] {summary.result} · {summary.totalSize / (1024 * 1024)} MB · " +
                  $"{summary.totalTime.TotalSeconds:0}s · {apk}");

        if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
    }
}
