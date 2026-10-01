using UnityEngine;

namespace MiniBrawl.Gameplay.VFX
{
    /// <summary>
    /// Bullets, drawn as short streaks that travel from the muzzle to wherever the shot landed.
    ///
    /// This replaces a line drawn the whole length of the shot. A permanent ray from every player
    /// reads as a laser sight rather than as gunfire, and it gave away the outcome before the shot
    /// arrived — the line was already touching you. A streak with travel time makes a shot an event
    /// with a beginning and an end.
    ///
    /// The travel is cosmetic. Hitscan already decided the outcome on the server the moment the
    /// trigger was pulled, so the streak is chasing a result that is settled; that is why it can be
    /// this fast and still never disagree with the damage.
    ///
    /// A fixed pool built once at startup, for the same reason HitSparks uses one: this runs
    /// hardest exactly when the frame budget is tightest.
    /// </summary>
    public sealed class BulletTracers : MonoBehaviour
    {
        public static BulletTracers Instance { get; private set; }

        public Sprite Sprite;

        [Tooltip("Streaks alive at once. Six players firing five a second is thirty a second; " +
                 "the oldest is recycled, so a firefight degrades rather than allocates.")]
        public int PoolSize = 64;

        [Tooltip("World units per second. Fast enough to feel like a bullet, slow enough to see.")]
        public float Speed = 115f;

        [Tooltip("The streak is drawn with a fading tail, so this is its full extent and the " +
                 "visible part is shorter.")]
        public float Length = 0.45f;

        public float Thickness = 0.09f;

        struct Tracer
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 From;
            public Vector2 Direction;
            public float Distance;
            public float Travelled;
        }

        Tracer[] m_Tracers;
        int m_Next;

        void Awake()
        {
            Instance = this;
            m_Tracers = new Tracer[Mathf.Max(1, PoolSize)];

            for (int i = 0; i < m_Tracers.Length; i++)
            {
                var go = new GameObject("Tracer");
                go.transform.SetParent(transform, false);
                go.SetActive(false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = Sprite;
                renderer.sortingOrder = 14;   // over players, under the muzzle flash

                m_Tracers[i] = new Tracer { Transform = go.transform, Renderer = renderer };
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Sends one streak from the muzzle towards where the shot ended.</summary>
        public void Fire(Vector2 from, Vector2 to, Color color)
        {
            if (m_Tracers == null) return;

            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f) return;

            ref Tracer tracer = ref m_Tracers[m_Next];
            m_Next = (m_Next + 1) % m_Tracers.Length;

            tracer.From = from;
            tracer.Direction = delta / distance;
            tracer.Distance = distance;
            tracer.Travelled = 0f;

            tracer.Renderer.color = color;
            tracer.Transform.gameObject.SetActive(true);

            Place(ref tracer);
        }

        void Place(ref Tracer tracer)
        {
            /* The streak is clipped at both ends: it does not start before the muzzle, and it does
             * not overshoot the thing it hit. Without the clamp a long streak visibly pokes out the
             * far side of a wall at the moment of impact. */
            float head = Mathf.Min(tracer.Travelled, tracer.Distance);
            float tail = Mathf.Max(0f, head - Length);
            float length = head - tail;
            if (length <= 0f) length = 0.01f;

            Vector2 centre = tracer.From + tracer.Direction * (tail + length * 0.5f);

            tracer.Transform.position = centre;
            tracer.Transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(tracer.Direction.y, tracer.Direction.x) * Mathf.Rad2Deg);
            tracer.Transform.localScale = new Vector3(length, Thickness, 1f);
        }

        void Update()
        {
            if (m_Tracers == null) return;

            float step = Speed * Time.deltaTime;

            for (int i = 0; i < m_Tracers.Length; i++)
            {
                ref Tracer tracer = ref m_Tracers[i];
                if (!tracer.Transform.gameObject.activeSelf) continue;

                tracer.Travelled += step;

                // Alive until the tail reaches the end, so the streak is not cut off mid-flight.
                if (tracer.Travelled - Length > tracer.Distance)
                {
                    tracer.Transform.gameObject.SetActive(false);
                    continue;
                }

                Place(ref tracer);
            }
        }
    }
}
