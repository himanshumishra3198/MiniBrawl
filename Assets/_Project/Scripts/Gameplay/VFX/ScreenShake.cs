using UnityEngine;

namespace MiniBrawl.Gameplay.VFX
{
    /// <summary>
    /// Camera punch on impact. Purely local and purely visual: it moves the camera, never the
    /// simulation, so two machines shaking differently still agree on where everyone is.
    ///
    /// Deliberately not hit-stop. Freezing time would desync a tick-locked simulation that replays
    /// during reconciliation, which is a high price for a few frames of impact.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ScreenShake : MonoBehaviour
    {
        public static ScreenShake Instance { get; private set; }

        [Tooltip("How quickly a shake decays. Higher is snappier.")]
        public float Damping = 14f;

        [Tooltip("Upper bound in world units, so a burst of hits cannot throw the view off the arena.")]
        public float MaxAmount = 0.35f;

        Vector3 m_Rest;
        float m_Amount;

        void Awake()
        {
            Instance = this;
            m_Rest = transform.localPosition;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Requests a shake; the strongest request in flight wins rather than stacking.</summary>
        public void Shake(float amount) => m_Amount = Mathf.Min(MaxAmount, Mathf.Max(m_Amount, amount));

        void LateUpdate()
        {
            if (m_Amount <= 0.002f)
            {
                transform.localPosition = m_Rest;
                m_Amount = 0f;
                return;
            }

            // LateUpdate so this runs after CameraFitter has sized the view for the frame.
            Vector2 offset = Random.insideUnitCircle * m_Amount;
            transform.localPosition = m_Rest + new Vector3(offset.x, offset.y, 0f);

            m_Amount = Mathf.Lerp(m_Amount, 0f, Damping * Time.unscaledDeltaTime);
        }
    }
}
