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
        uint m_NextTick;

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
            uint tick = m_Manager.TimeManager.LocalTick;
            if (tick < m_NextTick) return;
            m_NextTick = tick + Interval;

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
                            $"fuel={motor.State.Fuel:0.00} reconciles={motor.Reconciles}");
            }

            Debug.Log(line.ToString());
        }
    }
}
