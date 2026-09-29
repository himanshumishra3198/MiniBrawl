using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using MiniBrawl.Config;
using MiniBrawl.Gameplay.Rules;
using UnityEngine;

namespace MiniBrawl.Networking.Match
{
    /// <summary>
    /// Host-owned single source of truth for the match (§2.3): which phase we are in, who is in the
    /// roster and what the score is. Clients read it and never write to it.
    ///
    /// The rules themselves live in MatchRules, which knows nothing about networking; this class
    /// only decides when to ask them and what to do with the answer.
    /// </summary>
    public sealed class MatchDirector : NetworkBehaviour
    {
        public static MatchDirector Instance { get; private set; }

        [SerializeField] MatchSettings m_Settings = MatchSettings.Default;

        readonly SyncVar<MatchPhase> m_Phase = new();
        readonly SyncVar<float> m_PhaseElapsed = new();
        readonly SyncDictionary<string, PlayerSlot> m_Slots = new();

        public MatchPhase Phase => m_Phase.Value;
        public float PhaseElapsed => m_PhaseElapsed.Value;
        public MatchSettings Settings => m_Settings;

        /// <summary>Seconds left in a timed phase, or 0 when the phase waits on players.</summary>
        public float TimeRemaining =>
            Mathf.Max(0f, MatchRules.PhaseDuration(Phase, m_Settings) - m_PhaseElapsed.Value);

        public IReadOnlyDictionary<string, PlayerSlot> Slots => m_Slots;

        /// <summary>Roster sorted for the scoreboard: most kills first, fewest deaths breaking ties.</summary>
        public List<PlayerSlot> Standings() =>
            m_Slots.Values.OrderByDescending(s => s.Kills).ThenBy(s => s.Deaths).ToList();

        /// <summary>Raised on every machine when a kill is recorded, for the kill feed.</summary>
        public event System.Action<string, string> KillReported;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Reset() => m_Settings = MatchSettings.Default;

        public override void OnStartServer()
        {
            base.OnStartServer();
            m_Phase.Value = MatchPhase.Lobby;
            m_PhaseElapsed.Value = 0f;
        }

        void Update()
        {
            // Only the host advances the match; everyone else is told what happened.
            if (!IsServerStarted) return;

            m_PhaseElapsed.Value += Time.deltaTime;

            int players = m_Slots.Count(pair => pair.Value.Connected);
            int topScore = m_Slots.Count == 0 ? 0 : m_Slots.Max(pair => pair.Value.Kills);

            ExpireAbandonedSlots();

            MatchPhase next = MatchRules.Advance(m_Phase.Value, m_PhaseElapsed.Value, players, topScore, m_Settings);
            if (next == m_Phase.Value) return;

            EnterPhase(next);
        }

        /// <summary>
        /// A held seat is only worth holding for as long as someone might come back to it (§2.7).
        /// Past the window it is released, so the roster reflects who is actually playing.
        /// </summary>
        void ExpireAbandonedSlots()
        {
            float now = Time.time;

            foreach (string id in m_Slots.Keys.ToList())
            {
                PlayerSlot slot = m_Slots[id];
                if (slot.Connected) continue;
                if (now - slot.DisconnectedAt < NetworkConstants.ReconnectWindowSeconds) continue;

                m_Slots.Remove(id);
                Debug.Log($"[Match] {slot.Name} did not return within " +
                          $"{NetworkConstants.ReconnectWindowSeconds}s; seat released");
            }
        }

        void EnterPhase(MatchPhase phase)
        {
            MatchPhase previous = m_Phase.Value;
            m_Phase.Value = phase;
            m_PhaseElapsed.Value = 0f;

            // A new match starts from zero; the scoreboard after one should not.
            if (phase == MatchPhase.Countdown) ResetScores();

            Debug.Log($"[Match] {previous} -> {phase} ({m_Slots.Count} in roster)");
        }

        void ResetScores()
        {
            foreach (string id in m_Slots.Keys.ToList())
                m_Slots[id] = m_Slots[id].WithScore(0, 0);
        }

        /// <summary>
        /// Claims or reclaims a seat. Called by the owning client; RequireOwnership is off because
        /// this object belongs to the server, not to any player.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void ClaimSlot(string playerId, string name, NetworkConnection connection = null)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return;

            int clientId = connection?.ClientId ?? -1;

            if (m_Slots.TryGetValue(playerId, out PlayerSlot existing))
            {
                // Same person, new connection: keep their score (§2.7).
                m_Slots[playerId] = existing.WithConnection(clientId);
                Debug.Log($"[Match] {existing.Name} reclaimed their slot with {existing.Kills} kills");
                return;
            }

            byte color = (byte)(m_Slots.Count % 6);
            m_Slots[playerId] = PlayerSlot.Create(playerId, name, color, clientId);
            Debug.Log($"[Match] {name} joined ({m_Slots.Count} in roster)");
        }

        /// <summary>Server-only. Marks a seat empty but keeps it, so a returning player finds it.</summary>
        public void ReleaseSlot(int clientId)
        {
            if (!IsServerStarted) return;

            foreach (string id in m_Slots.Keys.ToList())
            {
                if (m_Slots[id].ClientId != clientId) continue;
                m_Slots[id] = m_Slots[id].WithConnection(-1, Time.time);
                Debug.Log($"[Match] {m_Slots[id].Name} disconnected; seat held for " +
                          $"{NetworkConstants.ReconnectWindowSeconds}s");
            }
        }

        /// <summary>Server-only. Records a kill, ignoring anything outside play.</summary>
        public void ReportKill(int killerClientId, int victimClientId)
        {
            if (!IsServerStarted || !MatchRules.ScoringEnabled(m_Phase.Value)) return;

            string killerName = "someone";
            string victimName = "someone";

            foreach (string id in m_Slots.Keys.ToList())
            {
                PlayerSlot slot = m_Slots[id];

                // Killing yourself, by your own shot or the world, costs a death and no kill.
                if (slot.ClientId == victimClientId)
                {
                    victimName = slot.Name;
                    m_Slots[id] = slot.WithScore(slot.Kills, slot.Deaths + 1);
                }
                else if (slot.ClientId == killerClientId && killerClientId != victimClientId)
                {
                    killerName = slot.Name;
                    m_Slots[id] = slot.WithScore(slot.Kills + 1, slot.Deaths);
                }
            }

            AnnounceKill(killerClientId == victimClientId ? victimName : killerName, victimName);
        }

        [ObserversRpc(RunLocally = true)]
        void AnnounceKill(string killer, string victim) => KillReported?.Invoke(killer, victim);
    }
}
