using System.Linq;
using FishNet;
using FishNet.Managing;
using MiniBrawl.Networking.Replication;
using UnityEngine;
using UnityEngine.UI;

namespace MiniBrawl.Networking.Session
{
    /// <summary>
    /// The §14 item 6 readout: RTT, tick, reconciles/sec, connection count. Lives in the networking
    /// assembly rather than UI so the UI layer stays free of FishNet types.
    /// </summary>
    public sealed class NetworkDebugOverlay : MonoBehaviour
    {
        public Text Readout;

        NetworkManager m_Manager;
        NetworkPlayerMotor m_Local;
        float m_SmoothedFps;
        uint m_ReconcilesAtWindowStart;
        float m_WindowElapsed;
        float m_ReconcilesPerSecond;

        void Awake() => m_Manager = InstanceFinder.NetworkManager;

        void Update()
        {
            if (Readout == null) return;

            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) m_SmoothedFps = Mathf.Lerp(m_SmoothedFps, 1f / dt, 0.1f);

            if (m_Local == null)
                m_Local = FindObjectsByType<NetworkPlayerMotor>(FindObjectsSortMode.None)
                    .FirstOrDefault(m => m.IsOwner);

            UpdateReconcileRate(dt);

            if (m_Manager == null)
            {
                Readout.text = "no NetworkManager";
                return;
            }

            bool server = m_Manager.IsServerStarted;
            bool client = m_Manager.IsClientStarted;
            string role = server && client ? "host" : server ? "server" : client ? "client" : "offline";
            int connections = server ? m_Manager.ServerManager.Clients.Count : 0;

            string local = m_Local == null
                ? "no local player"
                : $"pos {m_Local.State.Position.x:0.0}, {m_Local.State.Position.y:0.0}  " +
                  $"fuel {m_Local.State.Fuel * 100f:0}%  " +
                  $"corrections {m_Local.Corrections}  err {m_Local.LastError:0.000}";

            Readout.text =
                $"{role}  |  fps {m_SmoothedFps:0}  |  rtt {m_Manager.TimeManager.RoundTripTime} ms\n" +
                $"tick {m_Manager.TimeManager.LocalTick}  |  clients {connections}  |  " +
                $"reconciles/s {m_ReconcilesPerSecond:0.0}\n" +
                local;
        }

        void UpdateReconcileRate(float dt)
        {
            m_WindowElapsed += dt;
            if (m_WindowElapsed < 1f) return;

            uint now = m_Local != null ? m_Local.Reconciles : 0;
            m_ReconcilesPerSecond = (now - m_ReconcilesAtWindowStart) / m_WindowElapsed;
            m_ReconcilesAtWindowStart = now;
            m_WindowElapsed = 0f;
        }
    }
}
