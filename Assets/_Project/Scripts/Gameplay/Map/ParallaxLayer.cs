using UnityEngine;

namespace MiniBrawl.Gameplay.Map
{
    /// <summary>
    /// A background band that drifts with the camera, so the arena reads as a place with distance
    /// in it rather than a flat diagram.
    ///
    /// Depth is the fraction of the camera's movement the layer copies. At 0 it is nailed to the
    /// world and scrolls past at full speed like the level itself; at 1 it follows the camera
    /// exactly and never appears to move, which is what something infinitely far away does.
    /// Everything interesting is in between.
    ///
    /// Vertical drift is deliberately weaker than horizontal. A player on a jetpack climbs far
    /// more of the screen than they ever walk across it, and matching the horizontal rate makes
    /// the horizon pump up and down on every ascent.
    ///
    /// Purely visual, like every other effect here: nothing reads this, so a machine that drew it
    /// wrong still agrees with everyone else about the match.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [Range(0f, 1f)]
        [Tooltip("0 follows the world, 1 follows the camera. Further away is higher.")]
        public float Depth = 0.6f;

        [Range(0f, 1f)]
        public float VerticalDamping = 0.45f;

        Vector3 m_Origin;
        Camera m_Camera;

        void Awake()
        {
            m_Origin = transform.position;
            m_Camera = Camera.main;
        }

        void LateUpdate()
        {
            // The camera is driven in LateUpdate too, so this re-finds it rather than caching a
            // null from a frame before the rig existed.
            if (m_Camera == null)
            {
                m_Camera = Camera.main;
                if (m_Camera == null) return;
            }

            Vector3 eye = m_Camera.transform.position;
            transform.position = new Vector3(
                m_Origin.x + eye.x * Depth,
                m_Origin.y + eye.y * Depth * VerticalDamping,
                m_Origin.z);
        }
    }
}
