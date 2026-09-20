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

    /// <summary>The phone boots into the networked scene; its host/join menu covers solo play too.</summary>
    const string k_BootScene = "Assets/_Project/Scenes/20_Network.unity";

    [MenuItem("MiniBrawl/Build Development APK")]
    public static void BuildDevelopment() => Build(true);

    /// <summary>Slower, but repacks from scratch — see the note on the cached APK below.</summary>
    [MenuItem("MiniBrawl/Build Development APK (clean)")]
    public static void BuildDevelopmentClean() => Build(true, clean: true);

    [MenuItem("MiniBrawl/Build Release APK")]
    public static void BuildRelease() => Build(false, clean: true);

    static void Build(bool development, bool clean = false)
    {
        AndroidSetup.Configure();   // keep player settings in one place

        string apk = Path.Combine(k_OutputDir, development ? "MiniBrawl-dev.apk" : "MiniBrawl.apk");
        Directory.CreateDirectory(k_OutputDir);

        // An incremental rebuild patches the APK in place and leaves the previous libil2cpp.so
        // orphaned inside it - ~60 MB of dead weight that never goes away, because deleting the
        // file just restores it from the build cache. Only CleanBuildCache actually repacks.
        if (File.Exists(apk)) File.Delete(apk);

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
        if (scenes.Count == 0)
            throw new InvalidOperationException("No enabled scenes — run MiniBrawl > Build Prototype Scene first.");

        // Scene 0 is what launches, so the networked scene has to lead.
        if (scenes.Remove(k_BootScene)) scenes.Insert(0, k_BootScene);

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = apk,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            // Development build keeps the profiler and logcat output available, which is the whole
            // point of the first device build.
            options = (development
                    ? BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler
                    : BuildOptions.None)
                | (clean ? BuildOptions.CleanBuildCache : BuildOptions.None),
        };

        BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
        bool ok = summary.result == BuildResult.Succeeded;

        Debug.Log($"[BuildAndroidPlayer] {summary.result} · {summary.totalSize / (1024 * 1024)} MB · " +
                  $"{summary.totalTime.TotalSeconds:0}s · {apk}");

        if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
    }
}
