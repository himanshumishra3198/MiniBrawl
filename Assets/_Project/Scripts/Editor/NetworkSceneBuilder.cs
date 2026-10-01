using System.IO;
using System.Linq;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using MiniBrawl.Config;
using MiniBrawl.Gameplay.Audio;
using MiniBrawl.Gameplay.Player;
using MiniBrawl.Networking.Discovery;
using MiniBrawl.Networking.Match;
using MiniBrawl.Networking.Replication;
using MiniBrawl.Networking.Session;
using MiniBrawl.UI.Widgets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Generates the networked test scene and the player prefab. Shares the level and touch controls
/// with the offline prototype so the only difference under test is the networking.
/// </summary>
public static class NetworkSceneBuilder
{
    const string k_ScenePath  = "Assets/_Project/Scenes/20_Network.unity";
    const string k_PrefabPath = "Assets/_Project/Prefabs/Player/NetworkPlayer.prefab";
    const string k_DirectorPrefabPath = "Assets/_Project/Prefabs/Network/MatchDirector.prefab";
    const string k_PrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

    [MenuItem("MiniBrawl/Build Network Scene")]
    public static void Build()
    {
        int levelLayer = PrototypeSceneBuilder.EnsureLayer(PrototypeSceneBuilder.k_LevelLayer);
        int hittableLayer = PrototypeSceneBuilder.EnsureLayer(PrototypeSceneBuilder.k_HittableLayer);
        Sprite square = PrototypeSceneBuilder.EnsureSquareSprite();

        NetworkObject prefab = BuildPlayerPrefab(square, levelLayer, hittableLayer);
        NetworkObject director = BuildMatchDirectorPrefab();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        PrototypeSceneBuilder.BuildCamera();
        PrototypeSceneBuilder.BuildEffects(square);
        PrototypeSceneBuilder.BuildLevel(square, levelLayer);
        BuildNetworkManager(prefab, director);
        BuildHud(square);

        Directory.CreateDirectory(Path.GetDirectoryName(k_ScenePath));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, k_ScenePath);

        RegisterScene();
        AssetDatabase.SaveAssets();

        Debug.Log($"[NetworkSceneBuilder] Wrote {k_ScenePath} and {k_PrefabPath}.");
    }

    static NetworkObject BuildPlayerPrefab(Sprite square, int levelLayer, int hittableLayer)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(k_PrefabPath));

        // On the Hittable layer so shots can find players, and off the Level layer so the motor's
        // own sweep never collides with the player it is moving.
        var go = new GameObject("NetworkPlayer") { layer = hittableLayer };

        PrototypeSceneBuilder.BuildPlayerVisual(go);
        go.AddComponent<PlayerSfx>().JetpackLoop = GameAssets.JetpackLoop();

        /* The root stays at unit scale and the collider carries the size, where before the root
         * was squashed to PlayerMotor.Size around a 1x1 collider. The world-space box is identical,
         * so hitscan is unaffected — but the sprite rig hanging off this root is not squashed. */
        go.AddComponent<BoxCollider2D>().size = PlayerMotor.Size;

        var nob = go.AddComponent<NetworkObject>();
        var motor = go.AddComponent<NetworkPlayerMotor>();
        motor.LevelMask = 1 << levelLayer;
        motor.HitMask = (1 << levelLayer) | (1 << hittableLayer);
        go.AddComponent<PrototypeInputSource>();

        /* Prediction is off by default on NetworkObject, and without it [Replicate]/[Reconcile]
         * silently do nothing. State forwarding (on by default) is what moves spectated players.
         *
         * _graphicalObject is the other half of that, and it was left null: FishNet's prediction
         * smoothing only runs on an assigned child, so the interpolation settings sitting right
         * beside it did nothing at all and the body snapped once per tick. Nothing warns about
         * this — it just looks like a stuttering game. */
        var serialized = new SerializedObject(nob);
        serialized.FindProperty("_enablePrediction").boolValue = true;

        Transform graphical = go.transform.Find("Graphical");
        if (graphical == null)
            Debug.LogError("[NetworkSceneBuilder] No Graphical child — movement will not be smoothed.");
        else
            serialized.FindProperty("_graphicalObject").objectReferenceValue = graphical;

        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, k_PrefabPath);
        Object.DestroyImmediate(go);

        RegisterPrefab(saved.GetComponent<NetworkObject>());
        return saved.GetComponent<NetworkObject>();
    }

    /// <summary>FishNet spawns by prefab id, so the prefab has to be in the collection.</summary>
    static void RegisterPrefab(NetworkObject prefab)
    {
        var collection = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(k_PrefabObjectsPath);
        if (collection == null)
        {
            Debug.LogWarning($"[NetworkSceneBuilder] {k_PrefabObjectsPath} missing — prefab not registered.");
            return;
        }

        if (collection.Prefabs.Contains(prefab)) return;

        collection.AddObject(prefab, checkForDuplicates: true);
        EditorUtility.SetDirty(collection);
    }

    static void BuildNetworkManager(NetworkObject playerPrefab, NetworkObject directorPrefab)
    {
        var go = new GameObject("NetworkManager", typeof(NetworkManager), typeof(Tugboat));

        var manager = go.GetComponent<NetworkManager>();
        manager.SpawnablePrefabs = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(k_PrefabObjectsPath);

        var tugboat = go.GetComponent<Tugboat>();
        tugboat.SetPort(NetworkConstants.GamePort);
        tugboat.SetClientAddress("127.0.0.1");

        go.AddComponent<NetworkBootstrap>();

        // Discovery: the host advertises itself, every device listens while its menu is open.
        go.AddComponent<LanBeaconBroadcaster>();
        go.AddComponent<HostAdvertiser>();
        go.AddComponent<LanBeaconListener>();

        go.AddComponent<SessionRecovery>();
        go.AddComponent<SessionMenu>();
        go.AddComponent<NetworkTelemetry>();

        var spawner = go.AddComponent<PlayerSpawner>();
        spawner.PlayerPrefab = playerPrefab;
        spawner.MatchDirectorPrefab = directorPrefab;
        /* Spread across the 44-unit arena and all in open air, so a spawn drops onto whatever is
         * below rather than risking a start inside a ledge. Six seats, six corners of the map —
         * spawning two players on top of each other is how a match opens with a free kill. */
        spawner.SpawnPoints = new[]
        {
            new Vector2(-18f, -8f), new Vector2(18f, -8f),
            new Vector2(-14f, -3f), new Vector2(14f, -3f),
            new Vector2(0f, -1f),   new Vector2(0f, 6f),
        };
    }

    /// <summary>
    /// A prefab the server spawns, not a scene object. FishNet assigns scene ids from editor
    /// callbacks that never fire for objects created by script, so a generated scene's
    /// NetworkObjects end up with SceneId 0 and are silently never spawned.
    /// </summary>
    static NetworkObject BuildMatchDirectorPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(k_DirectorPrefabPath));

        var go = new GameObject("MatchDirector", typeof(NetworkObject), typeof(MatchDirector));
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, k_DirectorPrefabPath);
        Object.DestroyImmediate(go);

        var prefab = saved.GetComponent<NetworkObject>();
        RegisterPrefab(prefab);
        return prefab;
    }

    static void BuildHud(Sprite square)
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGo.transform;

        PrototypeSceneBuilder.Stick("LeftStick", "<Gamepad>/leftStick", canvas, square,
            new Vector2(0f, 0f), new Vector2(300f, 300f));
        PrototypeSceneBuilder.Stick("RightStick", "<Gamepad>/rightStick", canvas, square,
            new Vector2(1f, 0f), new Vector2(-300f, 300f));

        // Sits below the banner and clear of the scoreboard: the match clock is centred along the
        // top, and at 1100 wide starting at the left edge this text ran straight through it.
        var readout = PrototypeSceneBuilder.Label("Readout", canvas, 30, TextAnchor.UpperLeft);
        var rt = readout.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(44f, -140f);
        rt.sizeDelta = new Vector2(1000f, 160f);

        canvasGo.AddComponent<NetworkDebugOverlay>().Readout = readout;

        // Match banner across the top, scoreboard right, kill feed under it.
        Text banner = PrototypeSceneBuilder.Label("Banner", canvas, 54, TextAnchor.UpperCenter);
        Anchor(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1200f, 80f));

        Text scoreboard = PrototypeSceneBuilder.Label("Scoreboard", canvas, 30, TextAnchor.UpperRight);
        Anchor(scoreboard.rectTransform, new Vector2(1f, 1f), new Vector2(-44f, -140f), new Vector2(760f, 260f));

        Text killFeed = PrototypeSceneBuilder.Label("KillFeed", canvas, 28, TextAnchor.UpperRight);
        Anchor(killFeed.rectTransform, new Vector2(1f, 1f), new Vector2(-44f, -420f), new Vector2(760f, 200f));

        // Ready button sits centre-bottom, clear of both thumbsticks.
        Image readyImage = PrototypeSceneBuilder.MakeImage("ReadyButton", canvas, GameAssets.Ui("button_wide"),
            new Color(1f, 1f, 1f, 0.55f), new Vector2(0.5f, 0f), new Vector2(0f, 190f),
            new Vector2(640f, 130f));
        var readyButton = readyImage.gameObject.AddComponent<Button>();
        Text readyLabel = PrototypeSceneBuilder.Label("Label", readyImage.transform, 40, TextAnchor.MiddleCenter);
        PrototypeSceneBuilder.Stretch(readyLabel.rectTransform);

        /* Top-right corner, above the scoreboard. It was top-left first, which put it underneath
         * four lines of debug readout at a quarter opacity — present in the scene, invisible on a
         * phone. Solid enough to read as a button, and far from both thumbsticks. */
        Image leaveImage = PrototypeSceneBuilder.MakeImage("LeaveButton", canvas, GameAssets.Ui("button_wide"),
            new Color(1f, 0.45f, 0.4f, 0.8f), new Vector2(1f, 1f), new Vector2(-150f, -70f),
            new Vector2(240f, 90f));
        var leaveButton = leaveImage.gameObject.AddComponent<Button>();
        Text leaveLabel = PrototypeSceneBuilder.Label("Label", leaveImage.transform, 34, TextAnchor.MiddleCenter);
        PrototypeSceneBuilder.Stretch(leaveLabel.rectTransform);
        leaveLabel.text = "LEAVE";

        var matchHud = canvasGo.AddComponent<MatchHud>();
        matchHud.LeaveButton = leaveButton;
        matchHud.Banner = banner;
        matchHud.Scoreboard = scoreboard;
        matchHud.KillFeed = killFeed;
        matchHud.ReadyButton = readyButton;
        matchHud.ReadyLabel = readyLabel;
    }

    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 dimensions)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = dimensions;
    }

    /// <summary>Keeps the phone scene at index 0 and adds the network scene after it.</summary>
    static void RegisterScene()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == k_ScenePath)) return;

        scenes.Add(new EditorBuildSettingsScene(k_ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
