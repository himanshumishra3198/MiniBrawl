using System;
using MiniBrawl.Gameplay.Player;
using UnityEngine;

namespace MiniBrawl.Gameplay.VFX
{
    /// <summary>
    /// Draws a player from its simulation state. Reads IPlayerView and never writes back, so a
    /// mispredicted frame or a reconciliation snapping the body across the room changes what you
    /// see and nothing else — the same reason HitSparks and ScreenShake need no networking.
    ///
    /// Whoever owns identity (the networked motor, or the scene for the offline prototype) pushes
    /// Seat, SeatColor, Hidden and Flashing in. Those are facts about the match, not about the
    /// simulation, and this component has no way to learn them on its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerVisual : MonoBehaviour
    {
        /// <summary>One seat's frames. Six poses is the whole animation budget, and enough:
        /// at phone scale a player is about 90 pixels tall and reads as pose, not as detail.</summary>
        [Serializable]
        public struct Skin
        {
            public Sprite Stand;
            public Sprite Walk1;
            public Sprite Walk2;
            public Sprite Jump;
            public Sprite Hurt;
        }

        [Tooltip("One per seat, in PlayerColors order.")]
        public Skin[] Skins = Array.Empty<Skin>();

        public SpriteRenderer Body;
        public SpriteRenderer Gun;
        public SpriteRenderer Jet;
        public SpriteRenderer Muzzle;

        [Tooltip("World distance per walk frame. Tied to distance, not time, so the legs keep pace " +
                 "with the body instead of sliding when movement speed changes.")]
        public float Stride = 0.42f;

        public float MuzzleFlashSeconds = 0.05f;

        [Tooltip("Jet flame size in world units. Both particle sprites import one unit tall, so " +
                 "these read directly as length.")]
        public Vector2 JetScale = new Vector2(0.34f, 0.55f);

        public float MuzzleScale = 0.4f;

        [Header("Set by whoever owns match identity")]
        public int Seat;
        public Color SeatColor = Color.white;
        public bool Hidden;
        public bool Flashing;

        IPlayerView m_View;
        float m_WalkPhase;
        float m_MuzzleRemaining;

        /// <summary>Local offsets, resolved once: the gun pivots at the chest and the jet fires
        /// from the feet, both relative to a body whose feet sit at -Size.y/2.</summary>
        static readonly Vector3 k_GunAnchor = new Vector3(0f, -0.05f, 0f);

        void Awake()
        {
            m_View = GetComponent<IPlayerView>();
            if (Muzzle != null) Muzzle.enabled = false;
        }

        /// <summary>Called by the shooter's own code on every machine, so it needs no message.</summary>
        public void FlashMuzzle() => m_MuzzleRemaining = MuzzleFlashSeconds;

        void LateUpdate()
        {
            if (m_View == null) return;

            PlayerState state = m_View.State;
            PlayerInput input = m_View.LastInput;
            float dt = Time.deltaTime;

            bool visible = !Hidden && !state.IsDead;
            if (Body != null) Body.enabled = visible;
            if (Gun != null) Gun.enabled = visible;
            if (!visible)
            {
                if (Jet != null) Jet.enabled = false;
                if (Muzzle != null) Muzzle.enabled = false;
                return;
            }

            Vector2 aim = input.AimDirection;
            bool facingLeft = aim.x < 0f;

            DrawBody(state, dt, facingLeft);
            DrawGun(aim, facingLeft);
            DrawJet(state, input, dt);
            DrawMuzzle(dt);
        }

        void DrawBody(PlayerState state, float dt, bool facingLeft)
        {
            if (Body == null || Skins.Length == 0) return;

            Skin skin = Skins[Mathf.Abs(Seat) % Skins.Length];

            // Advance by distance covered rather than by clock, so the stride matches the speed.
            m_WalkPhase += Mathf.Abs(state.Velocity.x) * dt;

            Sprite pose;
            if (Flashing && skin.Hurt != null)
                pose = skin.Hurt;
            else if (!state.Grounded)
                pose = skin.Jump;
            else if (Mathf.Abs(state.Velocity.x) > 0.15f)
                pose = Mathf.FloorToInt(m_WalkPhase / Stride) % 2 == 0 ? skin.Walk1 : skin.Walk2;
            else
                pose = skin.Stand;

            if (pose != null) Body.sprite = pose;
            Body.flipX = facingLeft;

            /* A white flash reads as "that landed" better than a colour shift does, and the seat
             * tint is what identifies the player, so health is shown by draining towards red only
             * between flashes. */
            Body.color = Flashing
                ? Color.white
                : Color.Lerp(new Color(1f, 0.45f, 0.4f), Color.white, state.Health / 100f);
        }

        void DrawGun(Vector2 aim, bool facingLeft)
        {
            if (Gun == null) return;

            Gun.transform.localPosition = k_GunAnchor;
            Gun.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);

            // Rotating past vertical puts the gun on its back. Mirroring across the barrel keeps
            // the grip under it without moving the muzzle, which is where shots come from.
            Gun.flipY = facingLeft;
        }

        void DrawJet(PlayerState state, PlayerInput input, float dt)
        {
            if (Jet == null) return;

            bool thrusting = input.Jetpack && state.Fuel > 0f;
            Jet.enabled = thrusting;
            if (!thrusting) return;

            // Flicker on a fast triangle wave. A steady flame looks like a decal.
            float pulse = 0.85f + Mathf.PingPong(Time.time * 14f, 0.3f);
            Jet.transform.localScale = new Vector3(JetScale.x, JetScale.y * pulse, 1f);

            // Thins out as the tank empties, which is the cue that matters most in a fight.
            Color flame = Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.85f, 0.45f),
                                     Mathf.Clamp01(state.Fuel * 2f));
            flame.a = Mathf.Clamp01(0.45f + state.Fuel);
            Jet.color = flame;
        }

        void DrawMuzzle(float dt)
        {
            if (Muzzle == null) return;

            if (m_MuzzleRemaining <= 0f)
            {
                Muzzle.enabled = false;
                return;
            }

            m_MuzzleRemaining -= dt;
            Muzzle.enabled = true;

            float t = Mathf.Clamp01(m_MuzzleRemaining / Mathf.Max(0.0001f, MuzzleFlashSeconds));
            Muzzle.color = new Color(1f, 0.9f, 0.55f, t);
            Muzzle.transform.localScale = Vector3.one * (MuzzleScale * (0.7f + t * 0.5f));
        }
    }
}
