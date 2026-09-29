using UnityEngine;

namespace MiniBrawl.Gameplay.VFX
{
    /// <summary>
    /// Little squares thrown off an impact. A fixed pool built once at startup: particles are the
    /// classic way to leak allocations into a 60 fps loop, and the plan puts GC pressure at the top
    /// of the stutter list (§Phase 5).
    ///
    /// Visual only, spawned from the same code path on every machine, so it needs no networking.
    /// </summary>
    public sealed class HitSparks : MonoBehaviour
    {
        public static HitSparks Instance { get; private set; }

        public Sprite Sprite;

        [Tooltip("Sparks alive at once. Oldest are recycled, so a firefight degrades rather than allocates.")]
        public int PoolSize = 96;

        public float Lifetime = 0.35f;
        public float Speed = 6f;
        public float Gravity = 14f;
        public float Size = 0.12f;

        struct Spark
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Remaining;
            public Color Color;
        }

        Spark[] m_Sparks;
        int m_Next;

        void Awake()
        {
            Instance = this;
            m_Sparks = new Spark[PoolSize];

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Spark");
                go.transform.SetParent(transform, false);
                go.SetActive(false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = Sprite;
                renderer.sortingOrder = 15;

                m_Sparks[i] = new Spark { Transform = go.transform, Renderer = renderer };
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Throws a few sparks away from a surface, roughly along the reflected direction.</summary>
        public void Burst(Vector2 position, Vector2 away, Color color, int count = 5)
        {
            if (m_Sparks == null) return;

            for (int i = 0; i < count; i++)
            {
                ref Spark spark = ref m_Sparks[m_Next];
                m_Next = (m_Next + 1) % m_Sparks.Length;

                Vector2 direction = (away + Random.insideUnitCircle * 0.8f).normalized;

                spark.Velocity = direction * (Speed * Random.Range(0.4f, 1f));
                spark.Remaining = Lifetime * Random.Range(0.6f, 1f);
                spark.Color = color;

                spark.Transform.position = position;
                spark.Transform.localScale = new Vector3(Size, Size, 1f);
                spark.Transform.gameObject.SetActive(true);
                spark.Renderer.color = color;
            }
        }

        void Update()
        {
            if (m_Sparks == null) return;

            float dt = Time.deltaTime;

            for (int i = 0; i < m_Sparks.Length; i++)
            {
                ref Spark spark = ref m_Sparks[i];
                if (spark.Remaining <= 0f) continue;

                spark.Remaining -= dt;
                if (spark.Remaining <= 0f)
                {
                    spark.Transform.gameObject.SetActive(false);
                    continue;
                }

                spark.Velocity.y -= Gravity * dt;
                spark.Transform.position += (Vector3)(spark.Velocity * dt);

                // Fade out over the tail of the life so they vanish rather than blink.
                Color color = spark.Color;
                color.a = Mathf.Clamp01(spark.Remaining / Lifetime);
                spark.Renderer.color = color;
            }
        }
    }
}
