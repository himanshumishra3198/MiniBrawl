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

            var line = new StringBuilder();
            line.Append($"[telemetry] role={role} tick={tick} rtt={m_Manager.TimeManager.RoundTripTime} players={motors.Length}");

            foreach (var motor in motors)
            {
                var p = motor.State.Position;
                line.Append($" | owner={motor.OwnerId} pos={p.x:0.00},{p.y:0.00} " +
                            $"fuel={motor.State.Fuel:0.00} corrections={motor.Corrections}/{motor.Reconciles} " +
                            $"err={motor.LastError:0.0000} maxErr={motor.MaxError:0.0000}");
            }

            Debug.Log(line.ToString());
        }
    }
}
