using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using MiniBrawl.Gameplay.Combat;
using MiniBrawl.Gameplay.Player;
using MiniBrawl.Gameplay.Weapons;
using UnityEngine;

namespace MiniBrawl.Networking.Replication
{
    /// <summary>
    /// Wraps the pure simulation in FishNet's replicate/reconcile loop (§2.5). This class owns the
    /// networking; PlayerMotor and WeaponSim own the rules, and know nothing about FishNet.
    /// Reconciliation re-runs the same calls, which is only correct because they are deterministic
    /// and keep no hidden state.
    ///
    /// Movement and shooting live in one behaviour on purpose: both are predicted, and a shot's
    /// cooldown has to roll back in lockstep with the position it was fired from.
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
            public float RespawnIn;
            public float WeaponCooldown;
            public byte Health;
            public bool Grounded;

            uint _tick;

            public StateData(PlayerState state, WeaponState weapon)
            {
                Position = state.Position;
                Velocity = state.Velocity;
                Fuel = state.Fuel;
                RespawnIn = state.RespawnIn;
                Health = state.Health;
                Grounded = state.Grounded;
                WeaponCooldown = weapon.Cooldown;
                _tick = 0;
            }

            public PlayerState ToState() => new PlayerState
            {
                Position = Position,
                Velocity = Velocity,
                Fuel = Fuel,
                RespawnIn = RespawnIn,
                Health = Health,
                Grounded = Grounded,
            };

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        [Tooltip("Layers the motor collides with. Set to 'Level' by the prefab builder.")]
        public LayerMask LevelMask = ~0;

        [Tooltip("Level geometry plus anything shootable.")]
        public LayerMask HitMask = ~0;

        [Tooltip("Seconds a killed player stays down before respawning (§14 item 4).")]
        public float RespawnDelay = 3f;

        [SerializeField] MotorConfig m_Config = MotorConfig.Default;
        [SerializeField] WeaponConfig m_WeaponConfig = WeaponConfig.Default;

        /// <summary>Below this, a correction is float noise rather than a real misprediction.</summary>
        const float k_CorrectionThreshold = 0.01f;

        /// <summary>Two seconds of predicted positions, enough to cover any reconcile that arrives.</summary>
        const int k_HistorySize = 64;

        const float k_TracerDuration = 0.06f;
        static readonly Color k_AimColor = new Color(1f, 1f, 1f, 0.25f);
        static readonly Color k_TracerColor = new Color(1f, 0.85f, 0.4f, 0.95f);

        readonly Vector2[] m_PredictedPositions = new Vector2[k_HistorySize];
        readonly uint[] m_PredictedTicks = new uint[k_HistorySize];

        IPlayerInputSource m_Input;
        IMotorCollision m_World;
        IHitscanWorld m_Hitscan;
        Collider2D m_Collider;
        SpriteRenderer m_Renderer;
        LineRenderer m_AimLine;
        Color m_BaseColor;

        PlayerState m_State;
        WeaponState m_Weapon;
        PlayerInput m_LastInput;
        Vector2 m_SpawnPoint;
        float m_TracerRemaining;

        uint m_Reconciles;
        uint m_Corrections;
        float m_LastError;
        float m_MaxError;

        public PlayerState State => m_State;
        public PlayerInput LastInput => m_LastInput;
        public MotorConfig Config => m_Config;

        /// <summary>Hits this player has landed. Server-side truth; clients see their own guesses.</summary>
        public int Hits { get; private set; }

        /// <summary>Times this player has been killed.</summary>
        public int Deaths { get; private set; }

        /// <summary>Reconcile packets applied. Near one per tick is normal, and means nothing on its own.</summary>
        public uint Reconciles => m_Reconciles;

        /// <summary>Reconciles where the prediction was actually wrong. This is the number to watch.</summary>
        public uint Corrections => m_Corrections;

        /// <summary>How far the last reconcile moved us, in world units.</summary>
        public float LastError => m_LastError;

        public float MaxError => m_MaxError;

        void Reset()
        {
            m_Config = MotorConfig.Default;
            m_WeaponConfig = WeaponConfig.Default;
        }

        void Awake()
        {
            m_Input = Session.NetworkBootstrap.Autopilot
                ? gameObject.AddComponent<AutopilotInputSource>()
                : GetComponent<IPlayerInputSource>();

            m_World = new Physics2DCollision(LevelMask);
            m_Hitscan = new Physics2DHitscan(HitMask);
            m_Collider = GetComponent<Collider2D>();
            m_Renderer = GetComponent<SpriteRenderer>();
            if (m_Renderer != null) m_BaseColor = m_Renderer.color;

            m_SpawnPoint = transform.position;
            m_State = PlayerState.Spawn(m_SpawnPoint, m_Config.FuelMax);

            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            m_AimLine = BuildAimLine();
        }

        void OnDestroy()
        {
            if (m_AimLine != null) Destroy(m_AimLine.gameObject);
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

        public override void CreateReconcile() => PerformReconcile(new StateData(m_State, m_Weapon));

        [Replicate]
        void PerformReplicate(MoveData md, ReplicateState state = ReplicateState.Invalid,
                              Channel channel = Channel.Unreliable)
        {
            // TickDelta, never Time.deltaTime: this runs many times per frame during a replay.
            float delta = (float)TimeManager.TickDelta;

            m_LastInput = md.ToInput();
            m_State = PlayerMotor.Simulate(m_State, m_LastInput, m_Config, m_World, delta);
            transform.position = m_State.Position;

            // The server decides when the body comes back; clients find out by reconciliation.
            if (IsServerStarted && m_State.IsDead && m_State.RespawnIn <= 0f)
                m_State = PlayerState.Spawn(m_SpawnPoint, m_Config.FuelMax);

            if (!m_State.IsDead && WeaponSim.Step(ref m_Weapon, m_WeaponConfig, m_LastInput.Fire, delta))
                FireShot(m_LastInput.AimDirection, state);

            // Record what we predicted for this tick, but only on the live tick — during a replay
            // this same method re-runs past ticks, and those are corrections, not predictions.
            if (state.ContainsTicked())
            {
                uint tick = md.GetTick();
                int slot = (int)(tick % k_HistorySize);
                m_PredictedTicks[slot] = tick;
                m_PredictedPositions[slot] = m_State.Position;
            }
        }

        void FireShot(Vector2 direction, ReplicateState state)
        {
            HitscanHit hit = m_Hitscan.Raycast(m_State.Position, direction, m_WeaponConfig.Range, m_Collider);

            // Only the live tick should flash a tracer; a replay would fire the same shot again.
            if (state.ContainsTicked()) m_TracerRemaining = k_TracerDuration;

            /* §5.3: the host decides damage. Clients run this same code for the visuals but never
             * apply it, so a modified client can draw whatever it likes and still hit nothing. */
            if (!IsServerStarted || !hit.Hit || hit.Collider == null) return;

            if (hit.Collider.TryGetComponent(out NetworkPlayerMotor victim) && victim != this)
            {
                if (victim.m_State.IsDead) return;   // no shooting corpses
                victim.ApplyDamage(m_WeaponConfig.Damage);
                Hits++;
            }
            else if (hit.Collider.TryGetComponent(out Damageable target))
            {
                target.TakeDamage(m_WeaponConfig.Damage);
                Hits++;
            }
        }

        /// <summary>Server-only. Health is part of the reconciled state, so clients learn of it there.</summary>
        void ApplyDamage(int amount)
        {
            if (!IsServerStarted || amount <= 0) return;

            m_State.Health = (byte)Mathf.Max(0, m_State.Health - amount);
            if (m_State.Health > 0) return;

            // Start the countdown; PlayerMotor ticks it down and the replicate step respawns us.
            Deaths++;
            m_State.RespawnIn = RespawnDelay;
            m_State.Velocity = Vector2.zero;
        }

        [Reconcile]
        void PerformReconcile(StateData rd, Channel channel = Channel.Unreliable)
        {
            m_Reconciles++;

            /* Compare against what we predicted for THIS tick, not our current position. The client
             * simulates ahead of the server, so current position describes a later moment and would
             * report a large error even when prediction is perfect. */
            uint tick = rd.GetTick();
            int slot = (int)(tick % k_HistorySize);
            if (m_PredictedTicks[slot] == tick)
            {
                m_LastError = Vector2.Distance(m_PredictedPositions[slot], rd.Position);
                if (m_LastError > m_MaxError) m_MaxError = m_LastError;
                if (m_LastError > k_CorrectionThreshold) m_Corrections++;
            }

            m_State = rd.ToState();
            m_Weapon.Cooldown = rd.WeaponCooldown;
            transform.position = m_State.Position;
        }

        void Update()
        {
            /* Health and the respawn timer are reconciled to everyone, so hiding a dead player
             * needs no extra synchronisation — every machine reaches the same conclusion. */
            bool dead = m_State.IsDead;

            if (m_Renderer != null)
            {
                m_Renderer.enabled = !dead;
                m_Renderer.color = Color.Lerp(new Color(1f, 0.3f, 0.3f), m_BaseColor, m_State.Health / 100f);
            }
            if (m_Collider != null) m_Collider.enabled = !dead;

            if (m_AimLine == null) return;
            m_AimLine.enabled = !dead;
            if (dead) return;

            Vector2 origin = m_State.Position;
            Vector2 direction = m_LastInput.AimDirection;
            HitscanHit hit = m_Hitscan.Raycast(origin, direction, m_WeaponConfig.Range, m_Collider);

            m_AimLine.SetPosition(0, origin);
            m_AimLine.SetPosition(1, origin + direction * hit.Distance);

            bool firing = m_TracerRemaining > 0f;
            if (firing) m_TracerRemaining -= Time.deltaTime;

            m_AimLine.startColor = m_AimLine.endColor = firing ? k_TracerColor : k_AimColor;
            m_AimLine.widthMultiplier = firing ? 0.14f : 0.05f;
        }

        /// <summary>Unparented: as a child it would inherit the player's non-uniform scale.</summary>
        LineRenderer BuildAimLine()
        {
            var line = new GameObject($"AimLine_{OwnerId}").AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.05f;
            line.numCapVertices = 0;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 20;
            line.sharedMaterial = m_Renderer != null ? m_Renderer.sharedMaterial : null;
            return line;
        }
    }
}
