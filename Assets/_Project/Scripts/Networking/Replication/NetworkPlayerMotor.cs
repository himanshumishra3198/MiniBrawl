using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using MiniBrawl.Gameplay.Player;
using UnityEngine;

namespace MiniBrawl.Networking.Replication
{
    /// <summary>
    /// Wraps the pure simulation in FishNet's replicate/reconcile loop (§2.5). This class owns the
    /// networking; PlayerMotor owns the movement, and knows nothing about either FishNet or
    /// MonoBehaviours. Reconciliation re-runs the same Simulate call, which is only correct because
    /// the motor is deterministic and keeps no hidden state.
    /// </summary>
    public sealed class NetworkPlayerMotor : TickNetworkBehaviour
    {
        /// <summary>One tick of intent on the wire — 8 bytes plus FishNet's tick.</summary>
        public struct MoveData : IReplicateData
        {
            public sbyte MoveX;
            public ushort AimAngle;
            public byte Buttons;

            uint _tick;

            public MoveData(PlayerInput input)
            {
                MoveX = input.MoveX;
                AimAngle = input.AimAngle;
                Buttons = input.Buttons;
                _tick = 0;
            }

            public PlayerInput ToInput() => new PlayerInput
            {
                Tick = _tick,
                MoveX = MoveX,
                AimAngle = AimAngle,
                Buttons = Buttons,
            };

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>The authoritative state a client snaps back to when it mispredicts.</summary>
        public struct StateData : IReconcileData
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Fuel;
            public byte Health;
            public bool Grounded;

            uint _tick;

            public StateData(PlayerState state)
            {
                Position = state.Position;
                Velocity = state.Velocity;
                Fuel = state.Fuel;
                Health = state.Health;
                Grounded = state.Grounded;
                _tick = 0;
            }

            public PlayerState ToState() => new PlayerState
            {
                Position = Position,
                Velocity = Velocity,
                Fuel = Fuel,
                Health = Health,
                Grounded = Grounded,
            };

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        [Tooltip("Layers the motor collides with. Set to 'Level' by the scene builder.")]
        public LayerMask LevelMask = ~0;

        [SerializeField] MotorConfig m_Config = MotorConfig.Default;

        IPlayerInputSource m_Input;
        IMotorCollision m_World;
        PlayerState m_State;
        PlayerInput m_LastInput;
        uint m_Reconciles;

        public PlayerState State => m_State;
        public PlayerInput LastInput => m_LastInput;
        public MotorConfig Config => m_Config;

        /// <summary>How many corrections the server has forced on us — the number to watch.</summary>
        public uint Reconciles => m_Reconciles;

        void Reset() => m_Config = MotorConfig.Default;

        void Awake()
        {
            m_Input = Session.NetworkBootstrap.Autopilot
                ? gameObject.AddComponent<AutopilotInputSource>()
                : GetComponent<IPlayerInputSource>();
            m_World = new Physics2DCollision(LevelMask);
            m_State = PlayerState.Spawn(transform.position, m_Config.FuelMax);

            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        protected override void TimeManager_OnTick() => PerformReplicate(BuildMoveData());

        protected override void TimeManager_OnPostTick() => CreateReconcile();

        MoveData BuildMoveData()
        {
            // Only the owner authors input; everyone else replays what arrives over the wire.
            if (!IsOwner) return default;

            m_LastInput = m_Input != null ? m_Input.Read(TimeManager.LocalTick) : default;
            return new MoveData(m_LastInput);
        }

        public override void CreateReconcile() => PerformReconcile(new StateData(m_State));

        [Replicate]
        void PerformReplicate(MoveData md, ReplicateState state = ReplicateState.Invalid,
                              Channel channel = Channel.Unreliable)
        {
            // TickDelta, never Time.deltaTime: this runs many times per frame during a replay.
            float delta = (float)TimeManager.TickDelta;

            m_LastInput = md.ToInput();
            m_State = PlayerMotor.Simulate(m_State, m_LastInput, m_Config, m_World, delta);
            transform.position = m_State.Position;
        }

        [Reconcile]
        void PerformReconcile(StateData rd, Channel channel = Channel.Unreliable)
        {
            m_Reconciles++;
            m_State = rd.ToState();
            transform.position = m_State.Position;
        }
    }
}
