using UnityEngine;

namespace MiniBrawl.Gameplay.Map
{
    /// <summary>
    /// Follows the local player at a fixed zoom, the way Mini Militia does.
    ///
    /// This replaces a camera that framed the whole 26-unit arena on every screen. That guaranteed
    /// nobody could be off-screen — it was written after a bug where a player flying at a wall
    /// simply was not drawn — but it is also the reason the characters looked tiny: the room is
    /// fourteen times a player's height, so showing all of it makes each player a speck. Zooming
    /// in trades knowing where everyone is for being able to see anyone at all.
    ///
    /// The view is still clamped inside the arena, so the camera never shows the void outside the
    /// walls; near an edge it stops following and the player walks towards the side of the screen.
    ///
    /// Lives on a parent rig rather than on the Camera itself, because ScreenShake owns the
    /// camera's localPosition. Two components writing the same transform would fight, and the one
    /// that ran second would win at random.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        [Tooltip("The child camera this rig drives.")]
        public Camera View;

        [Tooltip("Set by whoever knows which player is on this screen. Null frames the whole arena.")]
        public Transform Target;

        [Tooltip("The arena, including walls. The view is never allowed outside it.")]
        public Vector2 WorldSize = new Vector2(26f, 13.5f);

        [Tooltip("World units visible top to bottom. A 1.6-unit player against 8 is about an " +
                 "eighth of the screen, which is roughly where Mini Militia sits.")]
        public float ViewHeight = 8f;

        [Tooltip("Seconds to catch up. Too low and the camera jitters on every prediction " +
                 "correction; too high and it lags behind a jetpack climb.")]
        public float Smoothing = 0.16f;

        [Tooltip("Seconds of movement to lead by, so you see where you are going rather than " +
                 "where you have been.")]
        public float LookAhead = 0.18f;

        public float MaxLead = 1.8f;

        Vector3 m_Velocity;
        Vector2 m_LastTarget;
        Vector2 m_Lead;
        bool m_Primed;

        void Awake()
        {
            Instance = this;
            if (View == null) View = GetComponentInChildren<Camera>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            if (View == null || !View.orthographic) return;

            float aspect = View.aspect;
            if (aspect <= 0f) return;

            float half = ViewHeight * 0.5f;

            /* Never wider or taller than the room. On a very wide screen the horizontal limit is
             * the binding one, which is the same calculation the old whole-arena camera used —
             * only now it is a ceiling on the zoom rather than the zoom itself. */
            half = Mathf.Min(half, WorldSize.y * 0.5f);
            half = Mathf.Min(half, WorldSize.x * 0.5f / aspect);
            View.orthographicSize = half;

            float halfWidth = half * aspect;
            float limitX = Mathf.Max(0f, WorldSize.x * 0.5f - halfWidth);
            float limitY = Mathf.Max(0f, WorldSize.y * 0.5f - half);

            Vector2 desired;
            if (Target == null)
            {
                desired = Vector2.zero;      // no local player yet: sit on the middle of the room
                m_Primed = false;
            }
            else
            {
                Vector2 here = Target.position;

                if (!m_Primed)
                {
                    // Snap on the first frame. Smoothing in from wherever the rig happened to be
                    // would sweep the camera across the arena when a player spawns.
                    m_LastTarget = here;
                    m_Primed = true;
                    transform.position = new Vector3(
                        Mathf.Clamp(here.x, -limitX, limitX),
                        Mathf.Clamp(here.y, -limitY, limitY),
                        transform.position.z);
                }

                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                Vector2 velocity = (here - m_LastTarget) / dt;
                m_LastTarget = here;

                /* The lead is smoothed separately from the position. Derived straight from frame
                 * velocity it snaps about on every reconciliation, because a corrected position
                 * looks like a very large instantaneous speed. */
                Vector2 wanted = Vector2.ClampMagnitude(velocity * LookAhead, MaxLead);
                m_Lead = Vector2.Lerp(m_Lead, wanted, 1f - Mathf.Exp(-6f * dt));

                desired = here + m_Lead;
            }

            desired.x = Mathf.Clamp(desired.x, -limitX, limitX);
            desired.y = Mathf.Clamp(desired.y, -limitY, limitY);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                new Vector3(desired.x, desired.y, transform.position.z),
                ref m_Velocity,
                Smoothing);
        }
    }
}
