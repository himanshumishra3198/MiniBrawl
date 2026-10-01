using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using MiniBrawl.Gameplay.Combat;
using MiniBrawl.Gameplay.Audio;
using MiniBrawl.Gameplay.Player;
using MiniBrawl.Gameplay.VFX;
using MiniBrawl.Gameplay.Weapons;
using MiniBrawl.Networking.Identity;
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
    public sealed class NetworkPlayerMotor : TickNetworkBehaviour, IPlayerView
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

            /* The weapon travels in the reconcile with everything else. Without it a replay would
             * re-fire past ticks using whatever is held now — pick up a shotgun and the last ten
             * ticks of rifle fire would be replayed as buckshot. */
            public byte WeaponKind;
            public int ShotgunAmmo;
            public int PistolAmmo;
            public bool SwitchHeld;

            public byte Health;
            public bool Grounded;

            /// <summary>Input has stopped arriving: hidden and untouchable until it resumes.</summary>
            public bool Absent;

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
                WeaponKind = (byte)weapon.Kind;
                ShotgunAmmo = weapon.ShotgunAmmo;
                PistolAmmo = weapon.PistolAmmo;
                SwitchHeld = weapon.SwitchHeld;
                Absent = false;
                _tick = 0;
            }

            public WeaponState ToWeapon() => new WeaponState
            {
                Cooldown = WeaponCooldown,
                // Fully qualified: the field below is also called WeaponKind, and inside this
                // struct the field name wins over the type name.
                Kind = (MiniBrawl.Gameplay.Weapons.WeaponKind)WeaponKind,
                ShotgunAmmo = ShotgunAmmo,
                PistolAmmo = PistolAmmo,
                SwitchHeld = SwitchHeld,
            };

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

        /// <summary>Below this, a correction is float noise rather than a real misprediction.</summary>
        const float k_CorrectionThreshold = 0.01f;

        /// <summary>Two seconds of predicted positions, enough to cover any reconcile that arrives.</summary>
        const int k_HistorySize = 64;

        static readonly Color k_TracerColor = new Color(1f, 0.92f, 0.62f, 0.95f);

        /// <summary>Fallback muzzle offset, used only when there are no visuals to ask — on a
        /// headless server, or in a test. PlayerVisual reports the real one.</summary>
        const float k_MuzzleReach = 1.05f;

        readonly Vector2[] m_PredictedPositions = new Vector2[k_HistorySize];
        readonly uint[] m_PredictedTicks = new uint[k_HistorySize];

        IPlayerInputSource m_Input;
        IMotorCollision m_World;
        IHitscanWorld m_Hitscan;
        Collider2D m_Collider;
        PlayerVisual m_Visual;
        PlayerSfx m_Sfx;
        PlayerNameplate m_Nameplate;
        Color m_BaseColor;

        PlayerState m_State;
        WeaponState m_Weapon = WeaponState.Starting;
        PlayerInput m_LastInput;
        Vector2 m_SpawnPoint;

        /// <summary>
        /// Ticks of silence before a player counts as absent. Roughly a second — long enough that
        /// ordinary packet loss does not trigger it, short enough that a dropped player stops being
        /// a free target well inside the transport's own timeout.
        /// </summary>
        const uint k_AbsentAfterTicks = 30;

        const float k_DamageFlashDuration = 0.09f;

        bool m_SlotClaimed;
        bool m_Absent;
        byte m_LastSeenHealth = 100;
        float m_DamageFlash;
        uint m_LastInputTick;
        uint m_Reconciles;
        uint m_Corrections;
        float m_LastError;
        float m_MaxError;

        static readonly System.Collections.Generic.List<NetworkPlayerMotor> s_All = new();

        /// <summary>Every motor currently in the scene, living or dead. Kept as a registry so
        /// anything that needs all the players each frame does not have to go and find them.</summary>
        public static System.Collections.Generic.IReadOnlyList<NetworkPlayerMotor> All => s_All;

        public PlayerState State => m_State;

        /// <summary>What this player is carrying, for the HUD.</summary>
        public WeaponState Weapon => m_Weapon;
        public PlayerInput LastInput => m_LastInput;

        /// <summary>Hidden because input stopped arriving. Exposed so telemetry can catch false positives.</summary>
        public bool IsAbsent => m_Absent;
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

        /// <summary>Overridden, not hidden: FishNet's Reset auto-adds the required NetworkObject.</summary>
        protected override void Reset()
        {
            base.Reset();
            m_Config = MotorConfig.Default;
        }

        void Awake()
        {
            m_Input = Session.NetworkBootstrap.Autopilot
                ? gameObject.AddComponent<AutopilotInputSource>()
                : GetComponent<IPlayerInputSource>();

            m_World = new Physics2DCollision(LevelMask);
            m_Hitscan = new Physics2DHitscan(HitMask);
            m_Collider = GetComponent<Collider2D>();
            m_Visual = GetComponent<PlayerVisual>();
            m_Sfx = GetComponent<PlayerSfx>();
            m_Nameplate = GetComponentInChildren<PlayerNameplate>(includeInactive: true);

            m_SpawnPoint = transform.position;
            m_State = PlayerState.Spawn(m_SpawnPoint, m_Config.FuelMax);
            s_All.Add(this);

            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        void OnDestroy() => s_All.Remove(this);

        /// <summary>
        /// Server-only. Applies a crate and reports whether it was worth taking, so a crate is
        /// not consumed by somebody who gains nothing from it.
        /// </summary>
        public bool TryTakePickup(Match.PickupKind kind, int heal)
        {
            if (!IsServerStarted || m_State.IsDead || m_Absent) return false;

            if (kind == Match.PickupKind.Health)
            {
                if (m_State.Health >= 100) return false;    // walking over it at full health leaves it
                m_State.Health = (byte)Mathf.Min(100, m_State.Health + heal);
                return true;
            }

            /* Weapon crates are always taken, even by somebody already carrying that weapon —
             * picking one up again refills it, and the crate is gone for fourteen seconds either
             * way, so this cannot be farmed. */
            WeaponSim.Equip(ref m_Weapon,
                kind == Match.PickupKind.Shotgun ? WeaponKind.Shotgun : WeaponKind.Pistol);
            return true;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            TryClaimSlot();
        }

        /// <summary>
        /// Claims our seat by player id, not connection id, so a reconnect finds it again. Retried
        /// from Update because the director is a spawned object and may arrive after we do.
        /// </summary>
        void TryClaimSlot()
        {
            if (m_SlotClaimed || !IsOwner) return;

            Match.MatchDirector director = Match.MatchDirector.Instance;
            if (director == null) return;

            director.ClaimSlot(PlayerIdentity.Id, PlayerIdentity.Name);

            // Bots have no thumbs to tap READY with, and a headless test would sit in the lobby.
            if (Session.NetworkBootstrap.Autopilot) director.SetReady(PlayerIdentity.Id, true);

            m_SlotClaimed = true;
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

        public override void CreateReconcile()
        {
            var data = new StateData(m_State, m_Weapon) { Absent = m_Absent };
            PerformReconcile(data);
        }

        [Replicate]
        void PerformReplicate(MoveData md, ReplicateState state = ReplicateState.Invalid,
                              Channel channel = Channel.Unreliable)
        {
            // TickDelta, never Time.deltaTime: this runs many times per frame during a replay.
            float delta = (float)TimeManager.TickDelta;

            /* A player whose input has stopped arriving stands motionless in the level until the
             * transport gives up on them. Ten seconds is a long time to be an effortless target, so
             * they are marked absent well before that and cannot be shot — losing Wi-Fi should not
             * cost you deaths you had no chance to avoid. */
            if (IsServerStarted && state.ContainsTicked())
            {
                /* Created is the flag that means real data exists for this tick. IsFuture() is
                 * exactly Replayed, a replay concept — testing it here marked input fresh on every
                 * live tick, so absence never triggered and the feature silently did nothing. */
                if (state.ContainsCreated()) m_LastInputTick = TimeManager.Tick;

                bool absent = TimeManager.Tick - m_LastInputTick > k_AbsentAfterTicks;
                if (absent != m_Absent)
                {
                    m_Absent = absent;
                    // Tell the roster, so the scoreboard and feed react now rather than in ten
                    // seconds when the transport finally admits the connection is gone.
                    Match.MatchDirector.Instance?.SetAbsent(OwnerId, absent);
                }
            }

            m_LastInput = md.ToInput();
            m_State = PlayerMotor.Simulate(m_State, m_LastInput, m_Config, m_World, delta);
            transform.position = m_State.Position;

            /* The server decides when the body comes back; clients find out by reconciliation.
             * The weapon resets with it: carrying a shotgun through death would let whoever is
             * already winning keep the thing that is helping them win. */
            if (IsServerStarted && m_State.IsDead && m_State.RespawnIn <= 0f)
            {
                m_State = PlayerState.Spawn(m_SpawnPoint, m_Config.FuelMax);
                m_Weapon = WeaponState.Starting;
            }

            if (!m_State.IsDead &&
                WeaponSim.Step(ref m_Weapon, m_LastInput.Fire, m_LastInput.Switch, delta))
                FireShot(m_LastInput.AimDirection, state, md.GetTick());

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

        void FireShot(Vector2 direction, ReplicateState state, uint tick)
        {
            WeaponConfig config = m_Weapon.Config;
            int pellets = Mathf.Max(1, config.Pellets);

            for (int i = 0; i < pellets; i++)
            {
                // Scatter hashed from the tick, so the client's predicted spread and the server's
                // authoritative one are the same cone.
                Vector2 heading = WeaponSim.Pellet(direction, i, tick, config);
                HitscanHit hit = m_Hitscan.Raycast(m_State.Position, heading, config.Range, m_Collider);

                // Only the live tick should show effects; a replay would fire the same shot again
                // and a reconcile of ten ticks would spray ten bursts from one trigger pull.
                if (state.ContainsTicked()) PlayShotEffects(heading, hit, i == 0);

                /* §5.3: the host decides damage. Clients run this same code for the visuals but
                 * never apply it, so a modified client can draw whatever it likes and hit nothing. */
                if (!IsServerStarted || !hit.Hit || hit.Collider == null) continue;

                if (hit.Collider.TryGetComponent(out NetworkPlayerMotor victim) && victim != this)
                {
                    if (victim.m_State.IsDead || victim.m_Absent) continue;   // no corpses, no absentees
                    victim.ApplyDamage(config.Damage, OwnerId);
                    Hits++;
                }
                else if (hit.Collider.TryGetComponent(out Damageable target))
                {
                    target.TakeDamage(config.Damage);
                    Hits++;
                }
            }
        }

        /// <summary>
        /// Health is reconciled to everyone, so each machine can notice a hit by watching it fall.
        /// No extra message, and it stays correct for spectated players as well as your own.
        /// </summary>
        void ReactToDamage()
        {
            byte health = m_State.Health;

            if (health < m_LastSeenHealth)
            {
                m_DamageFlash = k_DamageFlashDuration;

                HitSparks.Instance?.Burst(m_State.Position, Vector2.up,
                    health == 0 ? new Color(1f, 0.4f, 0.35f) : m_BaseColor,
                    count: health == 0 ? 14 : 4);

                // Being shot shakes your own view harder than shooting does; watching someone else
                // get shot should not shake yours at all.
                if (IsOwner) ScreenShake.Instance?.Shake(health == 0 ? 0.3f : 0.12f);

                /* Quieter for other players than for yourself. Everyone runs this for all six
                 * players, so at equal volume a busy match is a wall of impacts and you cannot
                 * hear that you are the one being shot. */
                Sfx.PlayGlobal(health == 0 ? SfxId.Death : SfxId.HitBody, IsOwner ? 1f : 0.4f);
            }
            else if (health > m_LastSeenHealth && m_LastSeenHealth == 0)
            {
                // Coming back is the same trick in reverse: health is reconciled to everyone, so
                // every machine sees the respawn without anything extra being sent.
                Sfx.PlayGlobal(SfxId.Respawn, IsOwner ? 0.8f : 0.22f);
            }

            m_LastSeenHealth = health;
            if (m_DamageFlash > 0f) m_DamageFlash -= Time.deltaTime;
        }

        /// <summary>
        /// Muzzle flash, impact sparks, and a nudge of the view for the shooter. All local and
        /// visual: every machine runs this same code for the tick, so nothing needs sending.
        /// </summary>
        void PlayShotEffects(Vector2 direction, HitscanHit hit, bool firstPellet)
        {
            /* One muzzle position for every effect, taken from the drawn weapon rather than
             * guessed at. The sparks and the tracer each had their own constant, and both went
             * stale the moment the character was resized. */
            Vector2 muzzle = m_Visual != null
                ? m_Visual.MuzzleWorld
                : m_State.Position + direction * k_MuzzleReach;

            HitSparks sparks = HitSparks.Instance;
            if (sparks != null)
            {
                sparks.Burst(muzzle, direction, new Color(1f, 0.85f, 0.4f), count: 2);

                if (hit.Hit)
                    sparks.Burst(hit.Point, -direction, new Color(1f, 0.75f, 0.35f), count: 5);
            }

            // A shotgun fires six of these at once; the flash, the bang and the recoil belong to
            // the trigger pull, not to each pellet.
            if (firstPellet && m_Visual != null) m_Visual.FlashMuzzle();

            // A streak from the barrel to wherever the shot ended, rather than a line down the
            // whole firing solution.
            Vector2 end = hit.Hit ? hit.Point : muzzle + direction * m_Weapon.Config.Range;
            BulletTracers.Instance?.Fire(muzzle, end, k_TracerColor);

            /* No shake on firing. It fires five times a second, so even a light nudge is a view
             * that never settles while a trigger is held — and it competes with the shake that
             * means something, which is being hit. That one stays. */

            if (firstPellet) Sfx.PlayGlobal(SfxId.Shoot, IsOwner ? 0.75f : 0.28f);
            if (hit.Hit && firstPellet) Sfx.PlayGlobal(SfxId.HitWall, IsOwner ? 0.45f : 0.18f);
        }

        /// <summary>Server-only. Health is part of the reconciled state, so clients learn of it there.</summary>
        void ApplyDamage(int amount, int attackerClientId)
        {
            if (!IsServerStarted || amount <= 0) return;

            m_State.Health = (byte)Mathf.Max(0, m_State.Health - amount);
            if (m_State.Health > 0) return;

            // Start the countdown; PlayerMotor ticks it down and the replicate step respawns us.
            Deaths++;
            m_State.RespawnIn = RespawnDelay;
            m_State.Velocity = Vector2.zero;

            /* Pick where to come back now, at the moment of death, rather than when the timer
             * expires. The respawn itself happens inside the replicate step, which the host also
             * runs during reconciliation replays — choosing randomly in there would hand back a
             * different position every replay. Deciding once, here, keeps it stable.
             *
             * Without this the spawn point was whatever the player was given on joining, for the
             * whole match, so dying returned you to the same square every time. */
            if (Match.PlayerSpawner.Instance != null)
                m_SpawnPoint = Match.PlayerSpawner.Instance.ChooseSpawn();

            Match.MatchDirector.Instance?.ReportKill(attackerClientId, OwnerId);
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
            m_Weapon = rd.ToWeapon();
            m_Absent = rd.Absent;
            transform.position = m_State.Position;
        }

        void Update()
        {
            TryClaimSlot();
            ReactToDamage();

            /* Health and the respawn timer are reconciled to everyone, so hiding a dead player
             * needs no extra synchronisation — every machine reaches the same conclusion. */
            bool dead = m_State.IsDead || m_Absent;

            /* The roster owns seat identity, so every machine paints each player the same. It is
             * pushed into the visuals rather than drawn here: PlayerVisual lives in Gameplay and
             * has no way to reach MatchDirector, which is the separation that keeps the simulation
             * layer free of FishNet. */
            Match.MatchDirector director = Match.MatchDirector.Instance;
            int seat = 0;
            string playerName = "";

            if (director != null && director.TryGetSlot(OwnerId, out Match.PlayerSlot slot))
            {
                seat = slot.ColorIndex;
                playerName = slot.Name;
                m_BaseColor = Match.PlayerColors.Get(seat);
            }

            if (m_Visual != null)
            {
                m_Visual.Weapon = (int)m_Weapon.Kind;
                m_Visual.Seat = seat;
                m_Visual.SeatColor = m_BaseColor;
                m_Visual.Hidden = dead;
                m_Visual.Flashing = m_DamageFlash > 0f;
            }

            if (m_Nameplate != null)
            {
                m_Nameplate.DisplayName = playerName;
                m_Nameplate.Tint = m_BaseColor;
                m_Nameplate.Hidden = dead || m_Absent;
                m_Nameplate.IsLocal = IsOwner;
            }

            if (m_Sfx != null) m_Sfx.IsLocal = IsOwner;

            // The camera follows whoever is playing on this machine.
            if (IsOwner && Gameplay.Map.CameraFollow.Instance != null)
                Gameplay.Map.CameraFollow.Instance.Target = transform;
            if (m_Collider != null) m_Collider.enabled = !dead;
        }
    }
}
