using System.IO;
using MiniBrawl.Gameplay.Combat;
using MiniBrawl.Gameplay.Player;
using MiniBrawl.Gameplay.Weapons;
using MiniBrawl.UI.Widgets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Generates the §14 prototype scene: a boxed-in room, one player square, dual touch controls and
/// the debug readout. Generated rather than hand-built so it can be rebuilt headlessly and reviewed
/// as code — the scene asset is disposable.
/// </summary>
public static class PrototypeSceneBuilder
{
    const string k_ScenePath  = "Assets/_Project/Scenes/10_Prototype.unity";
    const string k_SpritePath = "Assets/_Project/Art/Sprites/Square.png";
    internal const string k_LevelLayer = "Level";
    internal const string k_HittableLayer = "Hittable";

    static readonly Color k_Background = new Color(0.09f, 0.10f, 0.13f);
    static readonly Color k_LevelColor = new Color(0.30f, 0.34f, 0.42f);
    internal static readonly Color k_PlayerColor = new Color(0.35f, 0.85f, 1f);
    static readonly Color k_TargetColor = new Color(1f, 0.45f, 0.4f);

    [MenuItem("MiniBrawl/Build Prototype Scene")]
    public static void Build()
    {
        int levelLayer = EnsureLayer(k_LevelLayer);
        int hittableLayer = EnsureLayer(k_HittableLayer);
        Sprite square = EnsureSquareSprite();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildCamera();
        BuildLevel(square, levelLayer);
        BuildTargets(square, hittableLayer);
        var driver = BuildPlayer(square, levelLayer, hittableLayer);
        BuildHud(square);

        Directory.CreateDirectory(Path.GetDirectoryName(k_ScenePath));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, k_ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(k_ScenePath, true) };
        AssetDatabase.SaveAssets();

        Debug.Log($"[PrototypeSceneBuilder] Wrote {k_ScenePath} (player on layer mask {driver.LevelMask.value}).");
    }

    internal static void BuildCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = k_Background;
        go.transform.position = new Vector3(0f, 0f, -10f);

        // The 2D renderer's default sprite material is lit, so without a global light the scene
        // renders black on device.
        var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
    }

    internal static void BuildLevel(Sprite square, int layer)
    {
        var root = new GameObject("Level").transform;

        // A closed box: the jetpack is only tunable if you can hit a ceiling and slide off walls.
        Block("Floor",      new Vector2(0f, -6f),    new Vector2(26f, 1f), square, layer, root);
        Block("Ceiling",    new Vector2(0f, 6f),     new Vector2(26f, 1f), square, layer, root);
        Block("WallLeft",   new Vector2(-12.5f, 0f), new Vector2(1f, 13f), square, layer, root);
        Block("WallRight",  new Vector2(12.5f, 0f),  new Vector2(1f, 13f), square, layer, root);

        Block("Platform_A", new Vector2(-6f, -2.5f), new Vector2(6f, 0.6f), square, layer, root);
        Block("Platform_B", new Vector2(6f, 0.5f),   new Vector2(6f, 0.6f), square, layer, root);
        Block("Platform_C", new Vector2(0f, 3.2f),   new Vector2(4f, 0.6f), square, layer, root);
    }

    internal static void Block(string name, Vector2 pos, Vector2 size, Sprite square, int layer, Transform parent)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = k_LevelColor;

        go.AddComponent<BoxCollider2D>().size = Vector2.one;   // 1x1 sprite, so scale sets world size
    }

    internal static void BuildTargets(Sprite square, int hittableLayer)
    {
        var root = new GameObject("Targets").transform;

        // Standing on Platform_A and Platform_B, so shots have to clear the geometry.
        Target("Target_A", new Vector2(-6f, -1.6f), square, hittableLayer, root);
        Target("Target_B", new Vector2(6f, 1.4f), square, hittableLayer, root);
    }

    internal static void Target(string name, Vector2 pos, Sprite square, int layer, Transform parent)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(0.8f, 1.2f, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = k_TargetColor;
        sr.sortingOrder = 5;

        go.AddComponent<BoxCollider2D>().size = Vector2.one;
        go.AddComponent<Damageable>();
    }

    static PlayerDriver BuildPlayer(Sprite square, int levelLayer, int hittableLayer)
    {
        var go = new GameObject("Player");
        go.transform.position = new Vector3(0f, -4f, 0f);
        go.transform.localScale = new Vector3(PlayerMotor.Size.x, PlayerMotor.Size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = k_PlayerColor;
        sr.sortingOrder = 10;

        // No Collider2D on the player: the motor sweeps its own box, so a collider would only make
        // the cast hit itself.
        var driver = go.AddComponent<PlayerDriver>();
        driver.LevelMask = 1 << levelLayer;
        go.AddComponent<PrototypeInputSource>();

        var weapon = go.AddComponent<PlayerWeapon>();
        weapon.HitMask = (1 << levelLayer) | (1 << hittableLayer);
        weapon.AimLine = BuildAimLine();
        return driver;
    }

    static LineRenderer BuildAimLine()
    {
        // Unparented: as a child it would inherit the player's non-uniform scale and skew the line.
        var line = new GameObject("AimLine").AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = 0.05f;
        line.numCapVertices = 0;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 20;
        // Built-in sprite material: unlit, always included in the build.
        line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        return line;
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

        // Mini Militia's scheme: left stick walks and, pushed up, flies. Right stick aims and fires.
        // No separate buttons, so each thumb owns one control.
        Stick("LeftStick", "<Gamepad>/leftStick", canvas, square, new Vector2(0f, 0f), new Vector2(300f, 300f));
        Stick("RightStick", "<Gamepad>/rightStick", canvas, square, new Vector2(1f, 0f), new Vector2(-300f, 300f));

        var fuelBack = MakeImage("FuelBar", canvas, square, new Color(1f, 1f, 1f, 0.12f),
            new Vector2(0f, 1f), new Vector2(300f, -70f), new Vector2(520f, 46f));
        var fuelFill = MakeImage("Fill", fuelBack.transform, square, new Color(0.35f, 0.8f, 1f),
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(fuelFill.rectTransform);
        fuelFill.type = Image.Type.Filled;
        fuelFill.fillMethod = Image.FillMethod.Horizontal;
        fuelFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fuelFill.fillAmount = 1f;

        var readout = Label("Readout", canvas, 30, TextAnchor.UpperLeft);
        var rt = readout.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(44f, -110f);
        rt.sizeDelta = new Vector2(900f, 160f);

        var hud = canvasGo.AddComponent<PrototypeHud>();
        hud.FuelFill = fuelFill;
        hud.Readout = readout;
    }

    internal static void Stick(string name, string controlPath, Transform canvas, Sprite square,
                      Vector2 anchor, Vector2 anchoredPos)
    {
        var stickBase = MakeImage(name, canvas, square, new Color(1f, 1f, 1f, 0.10f),
            anchor, anchoredPos, new Vector2(320f, 320f));
        var handle = MakeImage("Handle", stickBase.transform, square, new Color(1f, 1f, 1f, 0.35f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));

        var stick = handle.gameObject.AddComponent<OnScreenStick>();
        stick.controlPath = controlPath;
        stick.movementRange = 110f;
    }

    internal static Image MakeImage(string name, Transform parent, Sprite sprite, Color color,
                       Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    internal static Text Label(string name, Transform parent, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    internal static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    internal static Sprite EnsureSquareSprite()
    {
        if (!File.Exists(k_SpritePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(k_SpritePath));
            var tex = new Texture2D(4, 4);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(k_SpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(k_SpritePath, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(k_SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 4f;   // 4px texture at 4 ppu = exactly 1x1 world unit
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(k_SpritePath);
    }

    internal static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing != -1) return existing;

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)   // 0-7 are Unity's own
        {
            var element = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(element.stringValue)) continue;
            element.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return i;
        }
        throw new System.InvalidOperationException($"No free user layer slot for '{name}'.");
    }
}
