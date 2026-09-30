using MiniBrawl.Gameplay.Player;
using UnityEngine;

namespace MiniBrawl.Gameplay.VFX
{
    /// <summary>
    /// The name and health bar floating above a player.
    ///
    /// This matters more now that the camera follows one player instead of framing the whole
    /// arena: you meet people at the edge of the screen with no scoreboard glance to spare, and
    /// the two things worth knowing immediately are who it is and whether they are nearly dead.
    ///
    /// Drawn in world space with a TextMesh and two sprites rather than on the HUD canvas. A
    /// canvas element would need its screen position recomputed from the world every frame for
    /// every player, and would sit on top of geometry it should be behind.
    ///
    /// Health comes from the reconciled state, like everything else here, so a spectated player's
    /// bar is as correct as your own without anything extra being sent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerNameplate : MonoBehaviour
    {
        public TextMesh Label;
        public SpriteRenderer BarBack;
        public SpriteRenderer BarFill;

        public float Width = 1.1f;
        public float Height = 0.13f;

        [Header("Set by whoever owns match identity")]
        public string DisplayName = "";
        public Color Tint = Color.white;
        public bool Hidden;

        /// <summary>Your own plate is dimmed: you know who you are, and it sits over your sights.</summary>
        public bool IsLocal;

        static readonly Color k_Low = new Color(0.95f, 0.30f, 0.25f);
        static readonly Color k_High = new Color(0.45f, 0.90f, 0.40f);

        IPlayerView m_View;

        void Awake() => m_View = GetComponentInParent<IPlayerView>();

        void LateUpdate()
        {
            if (m_View == null) return;

            PlayerState state = m_View.State;
            bool visible = !Hidden && !state.IsDead;

            if (Label != null) Label.gameObject.SetActive(visible);
            if (BarBack != null) BarBack.enabled = visible;
            if (BarFill != null) BarFill.enabled = visible;
            if (!visible) return;

            float alpha = IsLocal ? 0.45f : 0.95f;

            if (Label != null)
            {
                if (Label.text != DisplayName) Label.text = DisplayName;   // rebuilds the mesh
                Label.color = new Color(Tint.r, Tint.g, Tint.b, alpha);
            }

            float health = Mathf.Clamp01(state.Health / 100f);

            if (BarFill != null)
            {
                /* Scaled from the left rather than the centre: a sprite scales about its own
                 * origin, so the bar is also shifted by half of what it lost. Without that it
                 * shrinks towards the middle from both ends, which reads as a progress bar
                 * emptying rather than health being taken off the end. */
                BarFill.transform.localScale = new Vector3(Width * health, Height, 1f);
                BarFill.transform.localPosition = new Vector3(-Width * 0.5f * (1f - health), 0f, 0f);

                Color fill = Color.Lerp(k_Low, k_High, health);
                fill.a = alpha;
                BarFill.color = fill;
            }

            if (BarBack != null)
            {
                BarBack.transform.localScale = new Vector3(Width, Height, 1f);
                BarBack.color = new Color(0.05f, 0.06f, 0.08f, alpha * 0.75f);
            }
        }
    }
}
