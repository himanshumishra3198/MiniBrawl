using MiniBrawl.Gameplay.Player;
using UnityEngine;

namespace MiniBrawl.Gameplay.Audio
{
    /// <summary>
    /// The jetpack, which is the one sound that is held rather than triggered and so cannot come
    /// from the shared one-shot pool.
    ///
    /// Like PlayerVisual, this reads simulation state and never writes it, so it runs identically
    /// for the player holding the stick and for the five people watching them fly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSfx : MonoBehaviour
    {
        public AudioClip JetpackLoop;

        [Tooltip("Volume for your own jetpack. Other players' thrusters are mixed well below this, " +
                 "or six of them drown out everything that matters.")]
        [Range(0f, 1f)] public float LocalVolume = 0.32f;

        [Range(0f, 1f)] public float RemoteVolume = 0.10f;

        [Tooltip("Seconds to reach full volume. Snapping a loop on and off clicks audibly, and the " +
                 "jetpack is tapped constantly.")]
        public float Fade = 0.08f;

        /// <summary>Set by whoever knows whose screen this is.</summary>
        public bool IsLocal;

        IPlayerView m_View;
        AudioSource m_Source;
        float m_Level;

        void Awake()
        {
            m_View = GetComponent<IPlayerView>();

            m_Source = gameObject.AddComponent<AudioSource>();
            m_Source.clip = JetpackLoop;
            m_Source.loop = true;
            m_Source.playOnAwake = false;
            m_Source.spatialBlend = 0f;
            m_Source.volume = 0f;
        }

        void Update()
        {
            if (m_View == null || m_Source == null || JetpackLoop == null) return;

            PlayerState state = m_View.State;
            bool thrusting = m_View.LastInput.Jetpack && state.Fuel > 0f && !state.IsDead;

            float target = thrusting ? (IsLocal ? LocalVolume : RemoteVolume) : 0f;
            float step = Fade > 0f ? Time.deltaTime / Fade : 1f;
            m_Level = Mathf.MoveTowards(m_Level, target, step);

            m_Source.volume = m_Level;

            /* Kept stopped at silence rather than looping inaudibly: six players each running a
             * silent voice is six voices the mixer still has to pull through every buffer. */
            if (m_Level > 0.001f && !m_Source.isPlaying) m_Source.Play();
            else if (m_Level <= 0.001f && m_Source.isPlaying) m_Source.Stop();
        }
    }
}
