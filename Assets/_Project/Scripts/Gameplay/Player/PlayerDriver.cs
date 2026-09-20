using MiniBrawl.Config;
using UnityEngine;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>
    /// Offline driver: steps PlayerMotor at the fixed simulation rate and writes the result to the
    /// transform. Phase 2 replaces this with FishNet's replicate/reconcile, which calls the same
    /// PlayerMotor.Simulate — that is the point of keeping the motor free of MonoBehaviour state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDriver : MonoBehaviour
    {
        [Tooltip("Layers the motor collides with. Set to 'Level' by the scene builder.")]
        public LayerMask LevelMask = ~0;

        [SerializeField] MotorConfig m_Config = MotorConfig.Default;

        IPlayerInputSource m_Input;
        IMotorCollision m_World;
        PlayerState m_State;
        PlayerInput m_LastInput;
        float m_Accumulator;
        uint m_Tick;
        int m_TicksThisFrame;

        public PlayerState State => m_State;
        public PlayerInput LastInput => m_LastInput;
        public uint Tick => m_Tick;
        public int TicksThisFrame => m_TicksThisFrame;
        public MotorConfig Config => m_Config;

        void Reset() => m_Config = MotorConfig.Default;

        void Awake()
        {
            m_Input = GetComponent<IPlayerInputSource>();
            m_World = new Physics2DCollision(LevelMask);
            m_State = PlayerState.Spawn(transform.position, m_Config.FuelMax);
        }

        void Update()
        {
            const int maxCatchUpTicks = 5;   // a hitch must not spiral into a long simulation burst
            float dt = 1f / NetworkConstants.SimulationTickRate;

            m_Accumulator += Time.deltaTime;
            m_TicksThisFrame = 0;

            while (m_Accumulator >= dt && m_TicksThisFrame < maxCatchUpTicks)
            {
                m_Accumulator -= dt;
                m_LastInput = m_Input != null ? m_Input.Read(m_Tick) : default;
                m_State = PlayerMotor.Simulate(m_State, m_LastInput, m_Config, m_World, dt);
                m_Tick++;
                m_TicksThisFrame++;
            }

            if (m_TicksThisFrame == maxCatchUpTicks) m_Accumulator = 0f;

            // Snapped, not interpolated: remote-player smoothing arrives with prediction in Phase 2.
            transform.position = m_State.Position;
        }

        /// <summary>Puts the player back at a spawn point with a full tank.</summary>
        public void Respawn(Vector2 at)
        {
            m_State = PlayerState.Spawn(at, m_Config.FuelMax);
            transform.position = at;
        }
    }
}
