using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for the art and audio, applied from code.
///
/// Unity's defaults are wrong for this project in ways that are invisible in the editor and
/// expensive on a phone: sprites default to a pixels-per-unit that has nothing to do with our
/// world scale, and audio defaults to decompressing everything into memory at load. Setting them
/// here means a fresh clone, or a reimport after a Library wipe, comes back configured — rather
/// than looking subtly wrong with no record of what it should have been.
/// </summary>
public static class ArtImport
{
    /// <summary>
    /// World scale for character and weapon art: 100 pixels to the unit. The commando frames are
    /// 92px tall, which lands a player at 0.92 units against a 0.9-unit collision box — the sprite
    /// reads a touch larger than its hitbox, which is what platformers want.
    /// </summary>
    public const float CharacterPPU = 100f;

    public static Sprite Sprite(string path, float ppu, Vector2 pivot, int maxSize = 2048,
                                FilterMode filter = FilterMode.Bilinear, bool fullRect = false)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"[ArtImport] Missing {path} — run tools/import_assets.py.");
            return null;
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.filterMode = filter;
        importer.mipmapEnabled = false;          // nothing here is ever seen at a distance
        importer.maxTextureSize = maxSize;

        /* maxTextureSize only ever sets the *default* platform. Any per-platform override left on
         * the asset silently wins, which is how the particles ended up shipping at full size. */
        importer.ClearPlatformTextureSettings("Standalone");
        importer.ClearPlatformTextureSettings("Android");
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Compressed;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;

        // Tiled draw mode needs the full quad. With the default tight mesh Unity trims the sprite
        // to its opaque pixels, and a trimmed sprite cannot tile.
        settings.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// A particle sized so the texture is one world unit tall, which makes the transform scale
    /// read directly as length.
    ///
    /// The pivot matters more than it looks. A jet flame and a muzzle flash both grow away from a
    /// fixed point — the feet, the barrel tip — so they pivot at their base and extend outwards.
    /// Pivot them centrally and half the flame grows back through the thing that emitted it.
    /// </summary>
    public static Sprite Particle(string path, Vector2 pivot)
    {
        /* Each particle is one world unit tall, so the transform scales in PlayerVisual read
         * directly as a length.
         *
         * Pixels-per-unit has to come from the *source* image, which is what Unity measures the
         * sprite rect in. Reading it off the imported Texture2D gave whatever the import had most
         * recently produced, and pinning it to the 128px cap assumed a cap that platform overrides
         * were quietly ignoring — both produced particles four world units across.
         * GetSourceTextureWidthAndHeight answers the question the other two were guessing at. */
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        float ppu = 512f;
        if (importer != null)
        {
            importer.GetSourceTextureWidthAndHeight(out int _, out int height);
            if (height > 0) ppu = height;
        }

        // 128 is ample for a soft blurred particle, and it is the difference between a quarter of
        // a megabyte per effect and four. Resolution only; the world size above is unaffected.
        return Sprite(path, ppu, pivot, maxSize: 128);
    }

    /// <summary>
    /// Short one-shots. Decompressed up front because the alternative is a decoder spinning up on
    /// the first gunshot of the match, which is a frame spike exactly when the player is looking.
    /// </summary>
    public static AudioClip Sound(string path, bool longLoop = false)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"[ArtImport] Missing {path} — run tools/import_assets.py.");
            return null;
        }

        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return null;

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        if (longLoop)
        {
            // Five seconds of thruster: kept compressed, since it plays for as long as it plays and
            // one decoder is cheaper than the memory.
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
        }
        else
        {
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
        }

        // Per-platform since Unity 6, not a flag on the importer.
        settings.preloadAudioData = true;

        importer.defaultSampleSettings = settings;
        importer.forceToMono = true;         // the mix is flat 2D; stereo doubles the data for nothing
        importer.loadInBackground = !longLoop;

        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
