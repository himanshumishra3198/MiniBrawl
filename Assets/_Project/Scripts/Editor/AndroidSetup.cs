using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>Headless Android player configuration. Run via -executeMethod.</summary>
public static class AndroidSetup
{
    public static void Configure()
    {
        var ng = NamedBuildTarget.Android;

        PlayerSettings.companyName = "Mishra";
        PlayerSettings.productName = "MiniBrawl";
        PlayerSettings.SetApplicationIdentifier(ng, "com.mishra.minibrawl");

        // §12 risk 6: 30 Hz sim, IL2CPP + ARM64 only (Play Store requires 64-bit).
        PlayerSettings.SetScriptingBackend(ng, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetIl2CppCompilerConfiguration(ng, Il2CppCompilerConfiguration.Release);

        // Side-scroller: landscape only, both ways.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToLandscapeLeft  = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait       = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.useAnimatedAutorotation = true;

        PlayerSettings.Android.minSdkVersion    = AndroidSdkVersions.AndroidApiLevel25; // Unity 6.3's floor
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

        // Mobile 2D: gamma is cheaper and visually fine for sprites.
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
                                                                   UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.Android.forceSDCardPermission = false;
        PlayerSettings.Android.useCustomKeystore = false;

        // LAN discovery needs no special permission, but internet access does.
        PlayerSettings.Android.forceInternetPermission = true;

        // Frame rate is set at runtime by MiniBrawl.Core.FrameRateBootstrap; it can't be baked in here.
        QualitySettings.vSyncCount = 0;

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        AssetDatabase.SaveAssets();
        Debug.Log("[AndroidSetup] Configured: IL2CPP / ARM64 / landscape / minSdk 25 / Gamma / Vulkan+GLES3");
    }
}
