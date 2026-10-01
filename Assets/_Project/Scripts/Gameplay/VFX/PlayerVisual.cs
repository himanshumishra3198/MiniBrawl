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
        /// <summary>One seat's frames. Five poses is the whole animation budget, and enough:
        /// at phone scale a player is about 90 pixels tall and reads as pose, not as detail.
        ///
        /// The arm carries the rifle and is per-seat rather than shared, because it wears the
        /// same uniform colour as the body — a shared grey weapon would have broken the one rule
        /// that matters with six players on screen, which is that a player is one colour.</summary>
        [Serializable]
        public struct Skin
        {
            public Sprite Stand;

            /// <summary>The walk cycle, in order. Two frames read as a shuffle however far apart
            /// the legs are, because a walk passes through positions two frames cannot express.</summary>
            public Sprite[] Walk;

            public Sprite Jump;
            public Sprite Hurt;
            public Sprite Arm;
        }

        [Tooltip("One per seat, in PlayerColors order.")]
        public Skin[] Skins = Array.Empty<Skin>();

        public SpriteRenderer Body;
        public SpriteRenderer Gun;
        public SpriteRenderer Jet;
        public SpriteRenderer Muzzle;

        [Tooltip("World distance per walk frame. Tied to distance, not time, so the legs keep pace " +
                 "with the body instead of sliding when movement speed changes.")]
        public float Stride = 0.19f;

        public float MuzzleFlashSeconds = 0.05f;

        [Tooltip("Jet flame size in world units. Both particle sprites import one unit tall, so " +
                 "these read directly as length.")]
        public Vector2 JetScale = new Vector2(0.34f, 0.55f);

        public float MuzzleScale = 0.4f;

        [Tooltip("How fast the drawn weapon catches up to the aim, per second. The aim itself " +
                 "only changes once per tick, so without this the gun steps at the tick rate.")]
        public float GunResponse = 22f;

        [Header("Set by whoever owns match identity")]
        public int Seat;
        public Color SeatColor = Color.white;
        public bool Hidden;
        public bool Flashing;

        IPlayerView m_View;
        float m_WalkPhase;
        float m_MuzzleRemaining;
        bool m_FacingLeft;
        bool m_Walking;
        float m_GunAngle;
        bool m_GunPrimed;

        /// <summary>
        /// The shoulder, in root-local space. Measured off the drawing: the body sprite is 160px
        /// tall at 100 pixels per unit with its feet on the bottom edge, and the shoulder sits
        /// 113px up from there, against a body whose feet are at -Size.y/2.
        ///
        /// It is on the centre line on purpose. The real shoulder is a pixel forward of centre,
        /// but the body mirrors when aiming left and an off-centre anchor would make the weapon
        /// jump sideways as the player turned.
        /// </summary>
        static readonly Vector3 k_GunAnchor = new Vector3(0f, 0.33f, 0f);

        void Awake()
        {
            m_View = GetComponent<IPlayerView>();
            if (Muzzle != null) Muzzle.enabled = false;
        }

        /// <summary>Called by the shooter's own code on every machine, so it needs no message.</summary>
        public void FlashMuzzle() => m_MuzzleRemaining = MuzzleFlashSeconds;

        /// <summary>
        /// Where the barrel actually ends, in world space.
        ///
        /// Read off the transform rather than reconstructed from a reach constant. That constant
        /// has been wrong twice already — once for the sparks and once for the tracers — because
        /// it has to be re-measured every time the character is resized, and nothing fails when
        /// somebody forgets. The muzzle object is parented to the weapon, so it is correct for
        /// every aim angle by construction. Its renderer is disabled between shots; the transform
        /// is still live.
        /// </summary>
        public Vector2 MuzzleWorld => Muzzle != null
            ? (Vector2)Muzzle.transform.position
            : (Vector2)transform.position;

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

            /* Facing only flips once the aim is clearly to one side. Flipping on the sign of
              * aim.x alone meant that aiming anywhere near straight up or down turned the body
              * over on whichever side of zero the stick landed that frame — which is most of what
              * "flickering" was. The deadzone is wide enough to cover a thumb holding vertical. */
            Vector2 aim = input.AimDirection;
            if (aim.x > 0.12f) m_FacingLeft = false;
            else if (aim.x < -0.12f) m_FacingLeft = true;

            DrawBody(state, dt, m_FacingLeft);
            DrawGun(aim, m_FacingLeft);
            DrawJet(state, input, dt);
            DrawMuzzle(dt);
        }

        void DrawBody(PlayerState state, float dt, bool facingLeft)
        {
            if (Body == null || Skins.Length == 0) return;

            Skin skin = Skins[Mathf.Abs(Seat) % Skins.Length];

            // Advance by distance covered rather than by clock, so the stride matches the speed.
            m_WalkPhase += Mathf.Abs(state.Velocity.x) * dt;

            /* Separate thresholds to start and stop walking. A single one chattered between the
             * stand frame and the walk cycle whenever speed hovered on it, which happens every
             * time a player eases off the stick or brushes a wall. */
            float speed = Mathf.Abs(state.Velocity.x);
            if (speed > 0.35f) m_Walking = true;
            else if (speed < 0.12f) m_Walking = false;

            Sprite pose;
            if (Flashing && skin.Hurt != null)
                pose = skin.Hurt;
            else if (!state.Grounded)
                pose = skin.Jump;
            else if (m_Walking && skin.Walk != null && skin.Walk.Length > 0)
                pose = skin.Walk[Mathf.Abs(Mathf.FloorToInt(m_WalkPhase / Stride)) % skin.Walk.Length];
            else
                pose = skin.Stand;

            if (pose != null) Body.sprite = pose;
            Body.flipX = facingLeft;

            // The arm belongs to the same seat as the body it is attached to.
            if (Gun != null && skin.Arm != null) Gun.sprite = skin.Arm;

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

            /* The weapon angle is eased rather than set.
             *
             * Aim arrives with the rest of the input, once per simulation tick, so setting the
             * rotation straight from it steps the gun thirty times a second however fast the
             * screen is. FishNet's smoothing does not cover this: it interpolates the graphical
             * child's position, and this is a rotation set locally underneath it.
             *
             * LerpAngle takes the short way round, so crossing from +179 to -179 does not spin
             * the weapon the long way. The first frame snaps, because easing in from a default
             * of zero would sweep the barrel up from due east on every spawn. */
            float target = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            if (!m_GunPrimed)
            {
                m_GunAngle = target;
                m_GunPrimed = true;
            }
            else
            {
                m_GunAngle = Mathf.LerpAngle(m_GunAngle, target,
                    1f - Mathf.Exp(-GunResponse * Time.deltaTime));
            }

            Gun.transform.localRotation = Quaternion.Euler(0f, 0f, m_GunAngle);

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
