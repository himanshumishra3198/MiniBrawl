using System.IO;
using System.Linq;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using MiniBrawl.Config;
using MiniBrawl.Gameplay.Player;
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
    const string k_PrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

    [MenuItem("MiniBrawl/Build Network Scene")]
    public static void Build()
    {
        int levelLayer = PrototypeSceneBuilder.EnsureLayer(PrototypeSceneBuilder.k_LevelLayer);
        Sprite square = PrototypeSceneBuilder.EnsureSquareSprite();

        NetworkObject prefab = BuildPlayerPrefab(square, levelLayer);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        PrototypeSceneBuilder.BuildCamera();
        PrototypeSceneBuilder.BuildLevel(square, levelLayer);
        BuildNetworkManager(prefab);
        BuildHud(square);

        Directory.CreateDirectory(Path.GetDirectoryName(k_ScenePath));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, k_ScenePath);

        RegisterScene();
        AssetDatabase.SaveAssets();

        Debug.Log($"[NetworkSceneBuilder] Wrote {k_ScenePath} and {k_PrefabPath}.");
    }

    static NetworkObject BuildPlayerPrefab(Sprite square, int levelLayer)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(k_PrefabPath));

        var go = new GameObject("NetworkPlayer");
        go.transform.localScale = new Vector3(PlayerMotor.Size.x, PlayerMotor.Size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = PrototypeSceneBuilder.k_PlayerColor;
        sr.sortingOrder = 10;

        var nob = go.AddComponent<NetworkObject>();
        var motor = go.AddComponent<NetworkPlayerMotor>();
        motor.LevelMask = 1 << levelLayer;
        go.AddComponent<PrototypeInputSource>();

        // Prediction is off by default on NetworkObject, and without it [Replicate]/[Reconcile]
        // silently do nothing. State forwarding (on by default) is what moves spectated players.
        var serialized = new SerializedObject(nob);
        serialized.FindProperty("_enablePrediction").boolValue = true;
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

    static void BuildNetworkManager(NetworkObject playerPrefab)
    {
        var go = new GameObject("NetworkManager", typeof(NetworkManager), typeof(Tugboat));

        var manager = go.GetComponent<NetworkManager>();
        manager.SpawnablePrefabs = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(k_PrefabObjectsPath);

        var tugboat = go.GetComponent<Tugboat>();
        tugboat.SetPort(NetworkConstants.GamePort);
        tugboat.SetClientAddress("127.0.0.1");

        go.AddComponent<NetworkBootstrap>();

        go.AddComponent<NetworkTelemetry>();

        var spawner = go.AddComponent<PlayerSpawner>();
        spawner.PlayerPrefab = playerPrefab;
        spawner.SpawnPoints = new[]
        {
            new Vector2(-4f, -4f), new Vector2(4f, -4f),
            new Vector2(-8f, -4f), new Vector2(8f, -4f),
            new Vector2(-2f, 0f),  new Vector2(2f, 0f),
        };
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

        var readout = PrototypeSceneBuilder.Label("Readout", canvas, 30, TextAnchor.UpperLeft);
        var rt = readout.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(44f, -44f);
        rt.sizeDelta = new Vector2(1100f, 160f);

        canvasGo.AddComponent<NetworkDebugOverlay>().Readout = readout;
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
