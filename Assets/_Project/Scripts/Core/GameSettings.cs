using UnityEngine;

namespace MiniBrawl.Core
{
    /// <summary>
    /// Player preferences that outlive a session: volume, and whether the developer readout is on.
    ///
    /// Lives in Core rather than next to the audio code because settings are not an audio feature —
    /// they are the thing every layer reads and nothing owns. Core is referenced by everything and
    /// references nothing, which is exactly the shape this needs.
    ///
    /// Applied through AudioListener rather than by walking every AudioSource. One global volume is
    /// what the player is actually asking for, and it keeps the per-sound mix in Sfx intact: a
    /// player turning the volume down should not flatten the balance between a gunshot and a death.
    /// </summary>
    public static class GameSettings
    {
        const string k_Master = "minibrawl.volume.master";
        const string k_Sfx = "minibrawl.volume.sfx";
        const string k_Debug = "minibrawl.ui.debug";

        static float s_Master = 1f;
        static float s_Sfx = 1f;
        static bool s_Debug = true;
        static bool s_Loaded;

        /// <summary>Everything the game plays, 0 to 1.</summary>
        public static float MasterVolume
        {
            get { Load(); return s_Master; }
            set { Load(); s_Master = Mathf.Clamp01(value); Save(); Apply(); }
        }

        /// <summary>Effects only, on top of the master. Reserved for when music exists.</summary>
        public static float SfxVolume
        {
            get { Load(); return s_Sfx; }
            set { Load(); s_Sfx = Mathf.Clamp01(value); Save(); Apply(); }
        }

        /// <summary>
        /// The tick counter, round-trip time and prediction readout. On by default while this is a
        /// development build — it is the first thing worth looking at when something is wrong — and
        /// off by default once it is not, because a release player should not be reading telemetry.
        /// </summary>
        public static bool ShowDebugOverlay
        {
            get { Load(); return s_Debug; }
            set { Load(); s_Debug = value; Save(); }
        }

        static void Load()
        {
            if (s_Loaded) return;
            s_Loaded = true;

            s_Master = PlayerPrefs.GetFloat(k_Master, 1f);
            s_Sfx = PlayerPrefs.GetFloat(k_Sfx, 1f);
            s_Debug = PlayerPrefs.GetInt(k_Debug, Debug.isDebugBuild ? 1 : 0) != 0;
        }

        static void Save()
        {
            PlayerPrefs.SetFloat(k_Master, s_Master);
            PlayerPrefs.SetFloat(k_Sfx, s_Sfx);
            PlayerPrefs.SetInt(k_Debug, s_Debug ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Pushes the current volume at the engine. Safe to call repeatedly.</summary>
        public static void Apply()
        {
            Load();
            AudioListener.volume = s_Master;
        }

        /// <summary>
        /// Applied before the first scene loads, so a player who muted the game last time does not
        /// get one frame of full-volume menu music before the setting takes hold.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOnLaunch() => Apply();
    }
}
