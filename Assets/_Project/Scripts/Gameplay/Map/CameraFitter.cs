using UnityEngine;

namespace MiniBrawl.Gameplay.Map
{
    /// <summary>
    /// Keeps the whole arena in frame on any screen shape.
    ///
    /// A fixed orthographic size only frames the room at one aspect ratio. At 6.5 the room fits a
    /// phone held sideways but not a 16:9 window, where the outer 0.4 units of each side fall off
    /// screen — so a player who flew at a wall was simply not drawn. That went unnoticed with two
    /// players spawning near the middle, and showed up as "the third player is invisible".
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public sealed class CameraFitter : MonoBehaviour
    {
        [Tooltip("World size that must always be visible, including the walls.")]
        public Vector2 WorldSize = new Vector2(26f, 13.5f);

        [Tooltip("Extra margin so players are not drawn flush against the screen edge.")]
        public float Padding = 0.5f;

        Camera m_Camera;
        int m_LastWidth;
        int m_LastHeight;

        void Awake() => m_Camera = GetComponent<Camera>();

        void OnEnable() => Fit();

        void Update()
        {
            // Cheap guard: only recompute when the window actually changes, including on a phone
            // rotating or a desktop window being resized mid-match.
            if (Screen.width == m_LastWidth && Screen.height == m_LastHeight) return;
            Fit();
        }

        void Fit()
        {
            if (m_Camera == null) m_Camera = GetComponent<Camera>();
            if (m_Camera == null || !m_Camera.orthographic) return;

            m_LastWidth = Screen.width;
            m_LastHeight = Screen.height;

            float aspect = m_Camera.aspect;
            if (aspect <= 0f) return;

            float halfHeight = WorldSize.y * 0.5f + Padding;
            float halfWidth = WorldSize.x * 0.5f + Padding;

            // Whichever constraint is tighter decides the zoom.
            m_Camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);
        }
    }
}
