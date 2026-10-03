using System.IO;
using MiniBrawl.Gameplay.Audio;
using MiniBrawl.Gameplay.Combat;
using MiniBrawl.Gameplay.Map;
using MiniBrawl.Gameplay.Player;
using MiniBrawl.Gameplay.VFX;
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

    // Island at dusk. Kept dark on purpose: six camouflaged players have to stay readable
    // against it, and a bright daytime sky would put pastel uniforms on a pale background.
    static readonly Color k_Background = new Color(0.055f, 0.105f, 0.155f);
    static readonly Color k_LevelColor = new Color(0.27f, 0.23f, 0.18f);
    static readonly Color k_GrassColor = new Color(0.22f, 0.42f, 0.24f);
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
        BuildEffects(square);
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
        /* A rig that follows, with the camera as its child. ScreenShake writes the camera's
         * localPosition, so the follow has to write something else or the two fight over the
         * transform and whichever runs second wins. */
        var rig = new GameObject("CameraRig");
        rig.transform.position = new Vector3(0f, 0f, -10f);

        var go = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
        go.transform.SetParent(rig.transform, false);

        var cam = go.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 4f;     // a starting value; CameraFollow sets it per screen shape
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = k_Background;

        var follow = rig.AddComponent<CameraFollow>();
        follow.View = cam;
        follow.WorldSize = new Vector2(44f, 24f);   // must match the level built below

        go.AddComponent<ScreenShake>();

        /* Without this the game is silent, however correct everything downstream is. Unity's
         * GameObject menu adds one alongside a camera; constructing the camera from script does
         * not, and nothing warns you — the mixer simply has nobody to play to. */
        go.AddComponent<AudioListener>();

        // The 2D renderer's default sprite material is lit, so without a global light the scene
        // renders black on device.
        var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
    }

    /// <summary>Shared effect pools, built once so nothing allocates mid-match.</summary>
    internal static void BuildEffects(Sprite square)
    {
        var go = new GameObject("Effects");

        var sparks = go.AddComponent<HitSparks>();
        sparks.Sprite = GameAssets.Particle("spark");
        sparks.Size = 0.16f;

        var sfx = go.AddComponent<Sfx>();
        sfx.Banks = GameAssets.Banks();

        var tracers = go.AddComponent<BulletTracers>();
        tracers.Sprite = GameAssets.Particle("tracer");
    }

    /// <summary>
    /// The sprite rig: a body that stands on the floor, a weapon that turns with the aim stick, a
    /// thruster flame and a muzzle flash.
    ///
    /// All four hang off the player root as children, and the root is left at unit scale. The old
    /// single-square player squashed its transform to the collision box, which would have squashed
    /// every one of these along with it.
    /// </summary>
    internal static PlayerVisual BuildPlayerVisual(GameObject root)
    {
        var visual = root.AddComponent<PlayerVisual>();
        visual.Skins = GameAssets.Skins();

        float feet = -PlayerMotor.Size.y * 0.5f;

        /* Everything drawn hangs off one child, which FishNet smooths between ticks.
         *
         * The simulation writes the root's position once per tick and nowhere else, so at 30Hz on
         * a 120Hz screen the same position renders four frames running and then jumps. That is the
         * stutter, and no number of animation frames would have hidden it.
         *
         * The root is deliberately left snapping: the collider rides on it and the server's
         * hitscan reads colliders, so smoothing the hitbox would land shots where a player is
         * drawn rather than where they are. */
        var graphical = new GameObject("Graphical");
        graphical.transform.SetParent(root.transform, false);
        Transform g = graphical.transform;

        visual.Body = MakeRenderer("Body", g, GameAssets.Character(0, "stand"), 10);
        visual.Body.transform.localPosition = new Vector3(0f, feet, 0f);

        // Seat 0's arm is a placeholder; PlayerVisual swaps in the right seat's once the roster
        // says which seat this is.
        visual.Gun = MakeRenderer("Gun", g, GameAssets.Arm(0, 0), 11);

        // Pivoted at its base and turned to hang downwards, so lengthening the flame grows it away
        // from the feet instead of up through the body.
        visual.Jet = MakeRenderer("Jet", g, GameAssets.Emitted("flame"), 9);
        visual.Jet.transform.localPosition = new Vector3(0f, feet + 0.05f, 0f);
        visual.Jet.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        /* A child of the arm, so it follows the barrel around without anyone having to recompute
         * where the barrel is. Turned a quarter circle because the sprite points up and the barrel
         * points along +x, and placed at the muzzle: 0.995 units past the shoulder pivot, measured
         * off the rifle in tools/commando.py. This object is also what PlayerVisual reports as the
         * muzzle position, so sparks and tracers follow it without a second constant. */
        BuildNameplate(graphical);

        visual.Muzzle = MakeRenderer("Muzzle", visual.Gun.transform, GameAssets.Emitted("muzzle"), 12);
        visual.Muzzle.transform.localPosition = new Vector3(0.995f, 0.01f, 0f);
        visual.Muzzle.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        visual.Muzzle.enabled = false;

        return visual;
    }

    /// <summary>
    /// Name and health bar above a player, in world space.
    ///
    /// A child of the root rather than of the body, so it does not mirror when the body flips to
    /// face the other way — a reversed name is worse than no name.
    /// </summary>
    static void BuildNameplate(GameObject graphical)
    {
        Sprite square = EnsureSquareSprite();
        var plate = new GameObject("Nameplate");
        plate.transform.SetParent(graphical.transform, false);
        plate.transform.localPosition = new Vector3(0f, PlayerMotor.Size.y * 0.5f + 0.30f, 0f);

        var nameplate = plate.AddComponent<PlayerNameplate>();

        nameplate.BarBack = MakeRenderer("BarBack", plate.transform, square, 30);
        nameplate.BarFill = MakeRenderer("BarFill", plate.transform, square, 31);

        var labelGo = new GameObject("Name");
        labelGo.transform.SetParent(plate.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0.16f, 0f);

        var label = labelGo.AddComponent<TextMesh>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 64;            // rendered large and scaled down, so it stays crisp
        label.characterSize = 0.022f;
        label.anchor = TextAnchor.LowerCenter;
        label.alignment = TextAlignment.Center;

        // TextMesh brings its own MeshRenderer, which defaults to the wrong material and sorts
        // behind every sprite in the scene.
        var meshRenderer = labelGo.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = label.font.material;
        meshRenderer.sortingOrder = 32;

        nameplate.Label = label;
    }

    static SpriteRenderer MakeRenderer(string name, Transform parent, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    internal static void BuildLevel(Sprite square, int layer)
    {
        var root = new GameObject("Level").transform;

        /* 44 x 24 including walls, up from 26 x 13. The old room was built when the camera
         * framed all of it at once and a player was 0.9 units tall; now the camera follows and a
         * player is 1.6, so the same room was two screens wide and felt like a corridor.
         *
         * Every vertical gap below clears 1.6 with room to spare — the tightest is 2.7 between
         * the top platforms and the ceiling. */
        Block("Floor",     new Vector2(0f, -11f),    new Vector2(44f, 1f), square, layer, root);
        Block("Ceiling",   new Vector2(0f, 11f),     new Vector2(44f, 1f), square, layer, root);
        Block("WallLeft",  new Vector2(-21.5f, 0f),  new Vector2(1f, 23f), square, layer, root);
        Block("WallRight", new Vector2(21.5f, 0f),   new Vector2(1f, 23f), square, layer, root);

        // Low ledges, mid tier, then a scattered upper tier: enough footing that a jetpack is
        // worth using to cross, without so much that the floor stops mattering.
        Block("Ledge_L",   new Vector2(-14f, -6.5f), new Vector2(8f, 0.6f), square, layer, root);
        Block("Ledge_R",   new Vector2(14f, -6.5f),  new Vector2(8f, 0.6f), square, layer, root);
        Block("Ledge_C",   new Vector2(0f, -4f),     new Vector2(7f, 0.6f), square, layer, root);

        Block("Mid_L",     new Vector2(-8f, -0.5f),  new Vector2(7f, 0.6f), square, layer, root);
        Block("Mid_R",     new Vector2(8f, -0.5f),   new Vector2(7f, 0.6f), square, layer, root);
        Block("Mid_C",     new Vector2(0f, 3.5f),    new Vector2(9f, 0.6f), square, layer, root);

        Block("High_LL",   new Vector2(-16f, 4f),    new Vector2(6f, 0.6f), square, layer, root);
        Block("High_RR",   new Vector2(16f, 4f),     new Vector2(6f, 0.6f), square, layer, root);
        Block("High_L",    new Vector2(-7f, 7.5f),   new Vector2(6f, 0.6f), square, layer, root);
        Block("High_R",    new Vector2(7f, 7.5f),    new Vector2(6f, 0.6f), square, layer, root);

        BuildIsland(root);
    }

    /// <summary>
    /// Palms, grass and clouds. Decoration only — no colliders, and nothing here is consulted by
    /// the simulation, so a machine that failed to draw a single palm still agrees about the match.
    /// </summary>
    static void BuildIsland(Transform root)
    {
        var decor = new GameObject("Island").transform;
        decor.SetParent(root, false);

        BuildBackdrop(decor);

        // Standing on the floor and on the ledges. Sorted in front of the terrain they stand on
        // and behind the players, so nobody is ever hidden by scenery.
        Palm(decor, 0, new Vector2(-19f, -10.5f), 2.6f);
        Palm(decor, 2, new Vector2(-16.5f, -10.5f), 3.2f);
        Palm(decor, 1, new Vector2(17.5f, -10.5f), 2.4f);
        Palm(decor, 0, new Vector2(20f, -10.5f), 3.0f);
        Palm(decor, 2, new Vector2(-12f, -6.2f), 2.3f);
        Palm(decor, 1, new Vector2(15.5f, -6.2f), 2.1f);
        Palm(decor, 0, new Vector2(-17f, 4.3f), 2.0f);
        Palm(decor, 2, new Vector2(17f, 4.3f), 2.2f);

        float[] floorTufts = { -9f, -5.5f, -2f, 3f, 7f, 11f };
        foreach (float x in floorTufts) Tuft(decor, new Vector2(x, -10.5f));

        Tuft(decor, new Vector2(-15f, -6.2f));
        Tuft(decor, new Vector2(13f, -6.2f));
        Tuft(decor, new Vector2(-1.5f, -3.7f));
        Tuft(decor, new Vector2(-9f, -0.2f));
        Tuft(decor, new Vector2(9f, -0.2f));
        Tuft(decor, new Vector2(2f, 3.8f));

        Cloud(decor, 0, new Vector2(-13f, 8.5f), 3.2f);
        Cloud(decor, 1, new Vector2(3f, 9.5f), 4.0f);
        Cloud(decor, 2, new Vector2(15f, 7.5f), 2.8f);
    }

    /// <summary>
    /// Sea and two bands of hills behind the arena.
    ///
    /// Depths are spread rather than evenly spaced: the gap between the near hills and the level
    /// does most of the work, because that is the one a player sees move against something solid.
    /// Two layers at 0.55 and 0.75 would read as a single sheet.
    ///
    /// The far band is darker than the near one, which inverts the usual rule. Haze pulls distant
    /// things towards the colour of the sky, and this sky is a dark dusk blue.
    /// </summary>
    static void BuildBackdrop(Transform parent)
    {
        Band(parent, "Sea", "sea", footing: -12.5f, width: 86f, depth: 0.25f, order: -40);
        Band(parent, "HillsNear", "hills_near", footing: -9.5f, width: 78f, depth: 0.5f, order: -45);
        Band(parent, "HillsFar", "hills_far", footing: -7.5f, width: 96f, depth: 0.72f, order: -50);
    }

    static void Band(Transform parent, string name, string sprite, float footing, float width,
                     float depth, int order)
    {
        Sprite art = GameAssets.Backdrop(sprite);
        if (art == null) return;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art;
        sr.sortingOrder = order;

        /* Scaled from the sprite's own size so the band is as wide as asked for whatever the
         * source happens to be. Wider than the arena on purpose: a layer that drifts with the
         * camera has to cover the arena plus however far it drifts, or its edge walks into view.
         *
         * Pivoted at the bottom, so "footing" is where the band sits rather than its centre. */
        float scale = width / art.bounds.size.x;
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.transform.position = new Vector3(0f, footing, 0f);

        go.AddComponent<ParallaxLayer>().Depth = depth;
    }

    static void Palm(Transform parent, int variant, Vector2 footing, float height)
    {
        Sprite sprite = GameAssets.Island($"palm{variant}");
        if (sprite == null) return;

        var sr = MakeRenderer($"Palm_{variant}_{footing.x}", parent, sprite, 1);
        float scale = height / sprite.bounds.size.y;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.transform.position = new Vector3(footing.x, footing.y + height * 0.5f, 0f);
    }

    static void Tuft(Transform parent, Vector2 footing)
    {
        Sprite sprite = GameAssets.Island($"grass{Mathf.Abs((int)footing.x) % 3}");
        if (sprite == null) return;

        var sr = MakeRenderer($"Grass_{footing.x}", parent, sprite, 2);
        float scale = 0.55f / sprite.bounds.size.y;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.transform.position = new Vector3(footing.x, footing.y + 0.275f, 0f);
    }

    static void Cloud(Transform parent, int variant, Vector2 at, float width)
    {
        Sprite sprite = GameAssets.Island($"cloud{variant}");
        if (sprite == null) return;

        var sr = MakeRenderer($"Cloud_{variant}_{at.x}", parent, sprite, -30);
        float scale = width / sprite.bounds.size.x;
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.transform.position = new Vector3(at.x, at.y, 0f);
        sr.color = new Color(1f, 1f, 1f, 0.16f);   // far off, and must not compete with players
    }

    internal static void Block(string name, Vector2 pos, Vector2 size, Sprite square, int layer, Transform parent)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        // Unit scale with the size on the renderers, not a squashed transform: scaling stretches
        // one tile over the whole block, where Tiled repeats it.
        go.AddComponent<BoxCollider2D>().size = size;

        /* Built from four pieces rather than one repeated square: a dirt body, a grass surface
         * along the top, and a rounded cap at each end. A platform drawn as a single tiled sprite
         * has no surface and no ends, which is what made the level read as a stack of slabs.
         *
         * All of it is children of the collider rather than baked into it — scenery that changed
         * where you could stand would be a bug, not decoration. */
        Slab(go.transform, "Fill", GameAssets.Tile("ground_fill"), Vector2.zero, size, 0);

        bool tall = size.y > size.x;      // a wall, not a platform
        if (tall)
        {
            // Walls get no grass: a green line down a cliff face reads as moss, and these run
            // from the floor to the ceiling where no sunlight story makes sense.
            return;
        }

        const float surface = 0.5f;       // one tile of grass-over-dirt
        float cap = Mathf.Min(0.25f, size.x * 0.2f);

        Slab(go.transform, "Surface", GameAssets.Tile("ground_top"),
             new Vector2(0f, size.y * 0.5f - surface * 0.5f), new Vector2(size.x, surface), 1);

        Slab(go.transform, "CapL", GameAssets.Tile("edge_left"),
             new Vector2(-size.x * 0.5f + cap * 0.5f, 0f), new Vector2(cap, size.y), 2);

        Slab(go.transform, "CapR", GameAssets.Tile("edge_right"),
             new Vector2(size.x * 0.5f - cap * 0.5f, 0f), new Vector2(cap, size.y), 2);
    }

    /// <summary>One tiled piece of a block.</summary>
    static void Slab(Transform parent, string name, Sprite sprite, Vector2 offset, Vector2 size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;
        sr.sortingOrder = order;
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

        // Left at unit scale, unlike the old square: the sprite rig is made of children, and a
        // squashed root would squash the character, the weapon and the flame along with it.
        BuildPlayerVisual(go);
        go.AddComponent<PlayerSfx>().JetpackLoop = GameAssets.JetpackLoop();

        // No Collider2D on the player: the motor sweeps its own box, so a collider would only make
        // the cast hit itself.
        var driver = go.AddComponent<PlayerDriver>();
        driver.LevelMask = 1 << levelLayer;
        go.AddComponent<PrototypeInputSource>();

        // Offline there is only ever one player, so the rig can be pointed at it directly.
        CameraFollow follow = Object.FindFirstObjectByType<CameraFollow>();
        if (follow != null) follow.Target = go.transform;

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
        /* Outlined art rather than flat translucent squares. Both thumbs sit over live gameplay,
         * and an outline stays findable against a bright wall and a dark floor alike — a plain
         * alpha fill disappears against whichever of the two it happens to match. */
        var stickBase = MakeImage(name, canvas, GameAssets.Ui("stick_pad"), new Color(1f, 1f, 1f, 0.32f),
            anchor, anchoredPos, new Vector2(320f, 320f));
        var handle = MakeImage("Handle", stickBase.transform, GameAssets.Ui("stick_nub"), new Color(1f, 1f, 1f, 0.55f),
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
