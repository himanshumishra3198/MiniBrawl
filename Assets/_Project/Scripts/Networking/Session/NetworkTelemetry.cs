using System.Text;
using FishNet;
using FishNet.Managing;
using MiniBrawl.Networking.Replication;
using UnityEngine;

namespace MiniBrawl.Networking.Session
{
    /// <summary>
    /// Periodically logs every player's position. Two processes logging the same tick can be diffed
    /// to prove the client's prediction matches the server, which is the whole point of Phase 2.
    /// </summary>
    public sealed class NetworkTelemetry : MonoBehaviour
    {
        [Tooltip("Ticks between log lines. 30 ticks is one second at the simulation rate.")]
        public uint Interval = 30;

        NetworkManager m_Manager;
        uint m_LastLogged;

        /* Frame timing, sampled every frame and reported once a second. The Phase 5 bar is 60 fps
         * with no spike past 33 ms, and a spike is exactly what an average hides — so the worst
         * frame in each window is reported alongside the mean. */
        int m_Frames;
        float m_FrameSeconds;
        float m_WorstFrameMs;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            m_Frames++;
            m_FrameSeconds += dt;

            float ms = dt * 1000f;
            if (ms > m_WorstFrameMs) m_WorstFrameMs = ms;
        }

        void Start()
        {
            m_Manager = InstanceFinder.NetworkManager;
            if (m_Manager != null) m_Manager.TimeManager.OnPostTick += OnPostTick;
        }

        void OnDestroy()
        {
            if (m_Manager != null) m_Manager.TimeManager.OnPostTick -= OnPostTick;
        }

        void OnPostTick()
        {
            // Tick is synchronised to the server, unlike LocalTick, so two processes logging the
            // same tick number are describing the same moment. That is what makes these lines
            // comparable across machines.
            uint tick = m_Manager.TimeManager.Tick;

            // Sample on exact multiples rather than "every N since I started", so every process
            // logs the same tick numbers and the lines can be diffed against each other.
            if (Interval == 0 || tick % Interval != 0 || tick == m_LastLogged) return;
            m_LastLogged = tick;

            var motors = FindObjectsByType<NetworkPlayerMotor>(FindObjectsSortMode.None);
            bool server = m_Manager.IsServerStarted;
            bool client = m_Manager.IsClientStarted;
            string role = server && client ? "host" : server ? "server" : client ? "client" : "offline";

            float fps = m_FrameSeconds > 0f ? m_Frames / m_FrameSeconds : 0f;
            float worst = m_WorstFrameMs;
            m_Frames = 0;
            m_FrameSeconds = 0f;
            m_WorstFrameMs = 0f;

            var line = new StringBuilder();
            line.Append($"[telemetry] role={role} tick={tick} rtt={m_Manager.TimeManager.RoundTripTime} " +
                        $"fps={fps:0} worstFrame={worst:0.0}ms players={motors.Length}");

            // Roster scores, which are the match's truth — the per-player "hits" below only counts
            // shots landed and says nothing about who is winning.
            Match.MatchDirector director = Match.MatchDirector.Instance;
            if (director != null)
            {
                line.Append($" | phase={director.Phase} left={director.TimeRemaining:0}s");
                foreach (Match.PlayerSlot slot in director.Standings())
                    line.Append($" [{slot.Name} {slot.Kills}k/{slot.Deaths}d{(slot.Connected ? "" : " away")}]");
            }

            foreach (var motor in motors)
            {
                var p = motor.State.Position;
                line.Append($" | owner={motor.OwnerId} pos={p.x:0.00},{p.y:0.00} " +
                            $"{(motor.IsAbsent ? "ABSENT " : "")}" +
                            $"hp={motor.State.Health} respawnIn={motor.State.RespawnIn:0.0} " +
                            $"hits={motor.Hits} deaths={motor.Deaths} " +
                            $"fuel={motor.State.Fuel:0.00} corrections={motor.Corrections}/{motor.Reconciles} " +
                            $"err={motor.LastError:0.0000} maxErr={motor.MaxError:0.0000}");
            }

            Debug.Log(line.ToString());
        }
    }
}
