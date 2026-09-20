using MiniBrawl.Gameplay.Combat;
using MiniBrawl.Gameplay.Player;
using UnityEngine;

namespace MiniBrawl.Gameplay.Weapons
{
    /// <summary>
    /// Auto-firing hitscan weapon: aims where the right stick points and fires on the simulation
    /// tick, not the render frame, so the fire rate is frame-rate independent.
    /// </summary>
    [RequireComponent(typeof(PlayerDriver))]
    public sealed class PlayerWeapon : MonoBehaviour
    {
        [Tooltip("Level geometry plus anything shootable.")]
        public LayerMask HitMask = ~0;

        public LineRenderer AimLine;

        [SerializeField] WeaponConfig m_Config = WeaponConfig.Default;

        const float k_TracerDuration = 0.06f;
        static readonly Color k_AimColor   = new Color(1f, 1f, 1f, 0.25f);
        static readonly Color k_TracerColor = new Color(1f, 0.85f, 0.4f, 0.95f);

        PlayerDriver m_Driver;
        IHitscanWorld m_World;
        WeaponState m_State;
        float m_TracerRemaining;
        int m_Hits;

        public int Hits => m_Hits;
        public WeaponConfig Config => m_Config;

        void Reset() => m_Config = WeaponConfig.Default;

        void Awake()
        {
            m_Driver = GetComponent<PlayerDriver>();
            m_World = new Physics2DHitscan(HitMask);
        }

        void OnEnable() => m_Driver.Ticked += OnTick;
        void OnDisable() => m_Driver.Ticked -= OnTick;

        void OnTick(PlayerInput input, float dt)
        {
            if (!WeaponSim.Step(ref m_State, m_Config, input.Fire, dt)) return;

            var hit = Trace(input.AimDirection);
            if (hit.Hit && hit.Collider != null && hit.Collider.TryGetComponent(out Damageable target))
            {
                target.TakeDamage(m_Config.Damage);
                m_Hits++;
            }
            m_TracerRemaining = k_TracerDuration;
        }

        void Update()
        {
            if (AimLine == null) return;

            Vector2 origin = m_Driver.State.Position;
            Vector2 direction = m_Driver.LastInput.AimDirection;
            var hit = Trace(direction);

            AimLine.positionCount = 2;
            AimLine.SetPosition(0, origin);
            AimLine.SetPosition(1, origin + direction * hit.Distance);

            bool firing = m_TracerRemaining > 0f;
            if (firing) m_TracerRemaining -= Time.deltaTime;

            AimLine.startColor = AimLine.endColor = firing ? k_TracerColor : k_AimColor;
            AimLine.widthMultiplier = firing ? 0.14f : 0.05f;
        }

        HitscanHit Trace(Vector2 direction) =>
            m_World.Raycast(m_Driver.State.Position, direction, m_Config.Range);
    }
}
