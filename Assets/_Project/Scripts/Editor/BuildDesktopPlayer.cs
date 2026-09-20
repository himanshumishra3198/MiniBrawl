using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds a macOS player containing only the network scene. Two of these can be run headlessly from
/// a terminal (-server / -client), which tests real transport and prediction without needing two
/// Editor instances or two phones.
/// </summary>
public static class BuildDesktopPlayer
{
    const string k_Output = "Builds/Desktop/MiniBrawl.app";
    const string k_NetworkScene = "Assets/_Project/Scenes/20_Network.unity";

    [MenuItem("MiniBrawl/Build Desktop Test Player")]
    public static void Build()
    {
        if (!File.Exists(k_NetworkScene))
            throw new InvalidOperationException($"{k_NetworkScene} missing — run MiniBrawl > Build Network Scene first.");

        Directory.CreateDirectory(Path.GetDirectoryName(k_Output));

        var options = new BuildPlayerOptions
        {
            scenes = new[] { k_NetworkScene },
            locationPathName = k_Output,
            target = BuildTarget.StandaloneOSX,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.Development,
        };

        BuildSummary summary = BuildPipeline.BuildPlayer(options).summary;
        Debug.Log($"[BuildDesktopPlayer] {summary.result} · {summary.totalTime.TotalSeconds:0}s · {k_Output}");

        if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }
}
