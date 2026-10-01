using MiniBrawl.Gameplay.Audio;
using MiniBrawl.Gameplay.VFX;
using MiniBrawl.Networking.Match;
using UnityEngine;

/// <summary>
/// Every game asset, loaded with its import settings applied, in one place.
///
/// Both scene builders pull from here rather than each naming paths of their own. The offline
/// prototype and the networked scene are supposed to differ only in their networking; letting
/// them drift apart on art would quietly undo that.
/// </summary>
public static class GameAssets
{
    const string k_Art = "Assets/_Project/Art";
    const string k_Audio = "Assets/_Project/Audio/SFX";

    /// <summary>Feet-on-the-ground pivot, so frames of different heights still stand on the floor.</summary>
    static readonly Vector2 k_Feet = new Vector2(0.5f, 0f);

    /// <summary>
    /// The shoulder, measured off the drawing in tools/commando.py: pixel (24, 27) of a 136x53
    /// sprite, normalised. The arm turns a full circle with the aim stick, and a pivot anywhere
    /// else makes the whole weapon orbit the body instead of swinging from it.
    /// </summary>
    static readonly Vector2 k_Shoulder = new Vector2(24f / 136f, 26f / 53f);

    public static Sprite Character(int seat, string pose) =>
        ArtImport.Sprite($"{k_Art}/Characters/player{seat}_{pose}.png", ArtImport.CharacterPPU, k_Feet);

    /// <summary>Weapon names, in WeaponKind order.</summary>
    static readonly string[] k_Weapons = { "rifle", "shotgun", "pistol" };

    /// <summary>The firing arm, holding one weapon, pivoting at the shoulder.</summary>
    public static Sprite Arm(int seat, int weapon) =>
        ArtImport.Sprite($"{k_Art}/Weapons/arm{seat}_{k_Weapons[weapon]}.png",
                         ArtImport.CharacterPPU, k_Shoulder);

    /// <summary>Every weapon this seat could be holding, in WeaponKind order.</summary>
    public static Sprite[] Arms(int seat)
    {
        var arms = new Sprite[k_Weapons.Length];
        for (int i = 0; i < arms.Length; i++) arms[i] = Arm(seat, i);
        return arms;
    }

    /// <summary>A crate icon: the medikit, or one of the weapons.</summary>
    public static Sprite Item(string name) =>
        ArtImport.Sprite($"{k_Art}/Items/item_{name}.png", 100f, new Vector2(0.5f, 0.5f));

    public static Sprite Particle(string name) => Particle(name, new Vector2(0.5f, 0.5f));

    public static Sprite Particle(string name, Vector2 pivot) =>
        ArtImport.Particle($"{k_Art}/Particles/{name}.png", pivot);

    /// <summary>Base-pivoted, for effects that grow away from where they are attached.</summary>
    public static Sprite Emitted(string name) => Particle(name, new Vector2(0.5f, 0f));

    /// <summary>
    /// The level surface, tiled across each block rather than stretched over it.
    ///
    /// 140 pixels to the unit makes one tile half a world unit — half a player's height — so a
    /// one-unit wall is two tiles deep and the arena reads as built rather than as filled in.
    /// FullRect is not cosmetic: a tiled SpriteRenderer silently refuses to tile a sprite with
    /// the default tight mesh.
    /// </summary>
    public static Sprite Tile() =>
        ArtImport.Sprite($"{k_Art}/Tiles/platform.png", 140f, new Vector2(0.5f, 0.5f),
                         fullRect: true);

    /// <summary>Island scenery: palms, grass tufts, clouds. Pivoted centrally; the builder
    /// positions them by their footing.</summary>
    public static Sprite Island(string name) =>
        ArtImport.Sprite($"{k_Art}/Island/{name}.png", 100f, new Vector2(0.5f, 0.5f));

    public static Sprite Ui(string name) =>
        ArtImport.Sprite($"{k_Art}/UI/{name}.png", 100f, new Vector2(0.5f, 0.5f));

    /// <summary>One skin per seat, in PlayerColors order.</summary>
    public static PlayerVisual.Skin[] Skins()
    {
        var skins = new PlayerVisual.Skin[PlayerColors.Count];
        for (int seat = 0; seat < skins.Length; seat++)
        {
            skins[seat] = new PlayerVisual.Skin
            {
                Stand = Character(seat, "stand"),
                Walk  = WalkCycle(seat),
                Jump  = Character(seat, "jump"),
                Hurt  = Character(seat, "hurt"),
                Arms  = Arms(seat),
            };
        }
        return skins;
    }

    /// <summary>Must match WALK_FRAMES in tools/commando.py.</summary>
    const int k_WalkFrames = 8;

    /// <summary>The walk frames in cycle order.</summary>
    static Sprite[] WalkCycle(int seat)
    {
        var frames = new Sprite[k_WalkFrames];
        for (int i = 0; i < frames.Length; i++) frames[i] = Character(seat, $"walk{i + 1}");
        return frames;
    }

    /// <summary>
    /// A sound by name, whichever extension it arrived with. The Kenney packs ship .ogg; the
    /// gunshots are synthesised by tools/gunshot.py and land as .wav, because writing Vorbis from
    /// a build script would mean carrying an encoder for three files.
    /// </summary>
    public static AudioClip Sound(string name, bool longLoop = false)
    {
        string ogg = $"{k_Audio}/{name}.ogg";
        string path = System.IO.File.Exists(ogg) ? ogg : $"{k_Audio}/{name}.wav";
        return ArtImport.Sound(path, longLoop);
    }

    public static AudioClip JetpackLoop() => Sound("jetpack_loop", longLoop: true);

    /// <summary>
    /// Volumes are set here rather than left at 1, because they are a mix rather than a list.
    /// Shooting happens five times a second per player and has to sit under the sounds that
    /// change the match — being hit, dying, the clock running out.
    /// </summary>
    public static Sfx.Bank[] Banks() => new[]
    {
        Bank(SfxId.Shoot,   0.55f, 0.10f, "shoot_0", "shoot_1", "shoot_2"),
        Bank(SfxId.HitBody, 0.85f, 0.08f, "hit_body_0", "hit_body_1"),
        Bank(SfxId.HitWall, 0.40f, 0.12f, "hit_wall_0", "hit_wall_1"),
        Bank(SfxId.Death,   0.90f, 0.06f, "death_0", "death_1"),
        Bank(SfxId.Respawn, 0.70f, 0f,    "respawn"),
        Bank(SfxId.Kill,    0.80f, 0f,    "kill"),
        Bank(SfxId.UiClick,   0.60f, 0f, "ui_click"),
        Bank(SfxId.UiConfirm, 0.70f, 0f, "ui_confirm"),
        Bank(SfxId.UiError,   0.70f, 0f, "ui_error"),
        Bank(SfxId.MatchStart, 0.75f, 0f, "match_start"),
        Bank(SfxId.MatchEnd,   0.80f, 0f, "match_end"),
    };

    static Sfx.Bank Bank(SfxId id, float volume, float jitter, params string[] names)
    {
        var clips = new AudioClip[names.Length];
        for (int i = 0; i < names.Length; i++) clips[i] = Sound(names[i]);
        return new Sfx.Bank { Id = id, Clips = clips, Volume = volume, PitchJitter = jitter };
    }
}
