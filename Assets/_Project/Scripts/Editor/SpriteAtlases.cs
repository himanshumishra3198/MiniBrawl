using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Packs the sprites into atlases, which is how the draw-call count comes down.
///
/// A SpriteRenderer can only batch with its neighbours when they share a texture. Six players is
/// thirty renderers — body, arm, jet, muzzle and two nameplate bars each — and every one of those
/// sits on a different file, so every one is its own draw call before the scenery is counted.
/// Packing them into shared pages lets Unity collapse that into a handful.
///
/// Grouped by what is on screen together rather than by folder. The characters are drawn every
/// frame of every match; the menu art is not drawn during a match at all. Mixing them would put
/// pages in memory that nothing is using.
/// </summary>
public static class SpriteAtlases
{
    const string k_Dir = "Assets/_Project/Art/Atlases";

    [MenuItem("MiniBrawl/Pack Sprite Atlases")]
    public static void Pack()
    {
        Directory.CreateDirectory(k_Dir);

        Build("Characters", "Assets/_Project/Art/Characters", "Assets/_Project/Art/Weapons");
        Build("Effects", "Assets/_Project/Art/Particles", "Assets/_Project/Art/Items");
        Build("World", "Assets/_Project/Art/Tiles", "Assets/_Project/Art/Island");
        Build("Interface", "Assets/_Project/Art/UI");

        AssetDatabase.SaveAssets();
        SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);

        Debug.Log("[SpriteAtlases] packed 4 atlases.");
    }

    static void Build(string name, params string[] folders)
    {
        string path = $"{k_Dir}/{name}.spriteatlasv2";

        /* Rebuilt from scratch each run rather than updated in place. Adding packables to an
         * existing atlas appends to them, so repacking twice would list every folder twice. */
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

        var atlas = new SpriteAtlasAsset();

        /* Padding of 8, and the number is not arbitrary.
         *
         * Two things reach past a sprite's own edge. Bilinear sampling does, because these are
         * drawn well below their native size. And ASTC compresses in 6x6 pixel blocks, so a gap
         * narrower than a block means two sprites share one — whatever is packed next door leaks
         * through as an artefact that no amount of correct UVs will remove.
         *
         * Padding of 4 shipped exactly that: a checkmark icon bled onto the thumbstick beside it
         * in the atlas. Anything below 6 is broken by construction for this format; 8 leaves a
         * whole block of margin. */
        atlas.SetPackingSettings(new SpriteAtlasPackingSettings
        {
            padding = 8,
            enableRotation = false,     // rotated sprites confuse nothing here, but save nothing either
            enableTightPacking = false, // tight packing costs vertices to save space we are not short of
            blockOffset = 1,
        });

        atlas.SetTextureSettings(new SpriteAtlasTextureSettings
        {
            filterMode = FilterMode.Bilinear,
            generateMipMaps = false,    // nothing here is ever seen at a distance
            readable = false,
        });

        var platform = atlas.GetPlatformSettings("Android");
        platform.overridden = true;
        platform.maxTextureSize = 2048;
        platform.format = TextureImporterFormat.ASTC_6x6;   // mobile-native, alpha preserved
        platform.textureCompression = TextureImporterCompression.Compressed;
        atlas.SetPlatformSettings(platform);

        // Folders rather than individual sprites, so art added later is packed without this
        // file having to learn about it.
        var packable = new Object[folders.Length];
        for (int i = 0; i < folders.Length; i++)
            packable[i] = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folders[i]);

        atlas.Add(packable);

        SpriteAtlasAsset.Save(atlas, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        Debug.Log($"[SpriteAtlases] {name}: {folders.Length} folder(s)");
    }
}
