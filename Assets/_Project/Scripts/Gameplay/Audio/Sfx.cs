using System;
using UnityEngine;

namespace MiniBrawl.Gameplay.Audio
{
    public enum SfxId
    {
        Shoot,
        HitBody,
        HitWall,
        Death,
        Respawn,
        Kill,
        UiClick,
        UiConfirm,
        UiError,
        MatchStart,
        MatchEnd,
    }

    /// <summary>
    /// One-shot sound effects from a fixed pool of voices, built once at startup for the same
    /// reason HitSparks pools its particles: this is the code that runs hardest exactly when the
    /// frame budget is tightest.
    ///
    /// Sound is played flat (2D). The camera frames the whole arena, so every event is equally
    /// "near" and positional audio would only add attenuation bugs.
    ///
    /// Six players firing five shots a second is thirty shots a second. The pool caps how many can
    /// sound at once by stealing its oldest voice, and callers pass a lower volume for other
    /// players' actions than for their own — without both, a firefight is just noise.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }

        /// <summary>Several clips per sound where it repeats often; one picked at random each time.
        /// A single clip machine-guns, which reads as a bug rather than as rapid fire.</summary>
        [Serializable]
        public struct Bank
        {
            public SfxId Id;
            public AudioClip[] Clips;
            [Range(0f, 1f)] public float Volume;
            [Tooltip("Random pitch spread. Zero for UI, where a wobbling click sounds broken.")]
            public float PitchJitter;
        }

        public Bank[] Banks = Array.Empty<Bank>();

        [Tooltip("Simultaneous one-shots. Beyond this the oldest voice is reused.")]
        public int Voices = 16;

        [Range(0f, 1f)] public float MasterVolume = 1f;

        AudioSource[] m_Voices;
        int m_Next;
        Bank[] m_ById;

        void Awake()
        {
            Instance = this;

            // Indexed by enum value rather than by array order, so reordering Banks in the
            // inspector cannot silently swap the death sound for a menu click.
            int count = 0;
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                count = Mathf.Max(count, (int)id + 1);

            m_ById = new Bank[count];
            foreach (Bank bank in Banks)
            {
                int index = (int)bank.Id;
                if (index >= 0 && index < count) m_ById[index] = bank;
            }

            m_Voices = new AudioSource[Mathf.Max(1, Voices)];
            for (int i = 0; i < m_Voices.Length; i++)
            {
                var go = new GameObject($"Voice{i}");
                go.transform.SetParent(transform, false);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                m_Voices[i] = source;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Plays one sound. <paramref name="volume"/> is the caller's scale on top of the bank's —
        /// pass less than one for things that happened to somebody else.
        /// </summary>
        public void Play(SfxId id, float volume = 1f)
        {
            if (m_ById == null || m_Voices == null) return;

            int index = (int)id;
            if (index < 0 || index >= m_ById.Length) return;

            Bank bank = m_ById[index];
            if (bank.Clips == null || bank.Clips.Length == 0) return;

            AudioClip clip = bank.Clips[UnityEngine.Random.Range(0, bank.Clips.Length)];
            if (clip == null) return;

            AudioSource source = m_Voices[m_Next];
            m_Next = (m_Next + 1) % m_Voices.Length;

            source.clip = clip;
            source.volume = Mathf.Clamp01(bank.Volume * volume * MasterVolume);
            source.pitch = 1f + UnityEngine.Random.Range(-bank.PitchJitter, bank.PitchJitter);
            source.Play();
        }

        /// <summary>Null-safe shorthand: effects must never be the reason a match crashes.</summary>
        public static void PlayGlobal(SfxId id, float volume = 1f) => Instance?.Play(id, volume);
    }
}
