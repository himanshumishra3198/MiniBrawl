using FishNet;
using FishNet.Managing;
using MiniBrawl.Config;
using MiniBrawl.Gameplay.Weapons;
using MiniBrawl.Networking.Replication;
using MiniBrawl.Platform;
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
        string m_LocalIp = "…";
        float m_NextIpResolve;
        float m_SmoothedFps;
        uint m_ReconcilesAtWindowStart;
        float m_WindowElapsed;
        float m_ReconcilesPerSecond;

        void Awake() => m_Manager = InstanceFinder.NetworkManager;

        /// <summary>Redraw rate for the readout; the numbers on it do not move faster than this.</summary>
        const float k_RedrawInterval = 0.1f;

        float m_NextRedraw;

        void Update()
        {
            if (Readout == null) return;

            /* Measured every frame, drawn ten times a second. Both of these accumulate real elapsed
             * time, so throttling them as well would quietly skew the numbers they report. */
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) m_SmoothedFps = Mathf.Lerp(m_SmoothedFps, 1f / dt, 0.1f);
            UpdateReconcileRate(dt);

            if (Time.unscaledTime < m_NextRedraw) return;
            m_NextRedraw = Time.unscaledTime + k_RedrawInterval;

            if (m_Local == null)
            {
                // FindObjectsByType already allocates an array; a LINQ pass on top added a closure
                // and an enumerator on every frame until a local player existed.
                foreach (NetworkPlayerMotor motor in FindObjectsByType<NetworkPlayerMotor>(FindObjectsSortMode.None))
                {
                    if (!motor.IsOwner) continue;
                    m_Local = motor;
                    break;
                }
            }

            if (m_Manager == null)
            {
                /* Hidden rather than merely blanked: a player who has turned this off should not be
             * paying to format a string of telemetry every frame. */
            if (!Core.GameSettings.ShowDebugOverlay)
            {
                if (Readout.enabled) Readout.enabled = false;
                return;
            }

            if (!Readout.enabled) Readout.enabled = true;

            Readout.text = "no NetworkManager";
                return;
            }

            bool server = m_Manager.IsServerStarted;
            bool client = m_Manager.IsClientStarted;
            string role = server && client ? "host" : server ? "server" : client ? "client" : "offline";
            int connections = server ? m_Manager.ServerManager.Clients.Count : 0;

            /* The host's address belongs here rather than on the menu, which hides the moment
             * hosting starts. Re-resolved periodically because turning on a hotspot changes the
             * address underneath a running app. */
            string hosting = "";
            if (server)
            {
                // Absolute time, not accumulated frame deltas: this block runs on the redraw
                // schedule, so subtracting dt here would stretch two seconds into twelve.
                if (Time.unscaledTime >= m_NextIpResolve)
                {
                    m_LocalIp = LocalIpResolver.Resolve();
                    m_NextIpResolve = Time.unscaledTime + 2f;
                }
                hosting = $"\nJOIN THIS: {m_LocalIp}:{NetworkConstants.GamePort}";
            }

            // Ammo earns its place here: a pistol that quietly became a rifle when the magazine
            // ran out is otherwise only noticeable by the damage dropping.
            string weapon = "";
            if (m_Local != null)
            {
                WeaponState held = m_Local.Weapon;
                int rounds = held.AmmoFor(held.Kind);
                weapon = rounds < 0 ? $"  {held.Kind}" : $"  {held.Kind} x{rounds}";
            }

            string local = m_Local == null
                ? "no local player"
                : $"pos {m_Local.State.Position.x:0.0}, {m_Local.State.Position.y:0.0}  " +
                  $"fuel {m_Local.State.Fuel * 100f:0}%{weapon}  " +
                  $"corrections {m_Local.Corrections}  err {m_Local.LastError:0.000}";

            Readout.text =
                $"{role}  |  fps {m_SmoothedFps:0}  |  rtt {m_Manager.TimeManager.RoundTripTime} ms{hosting}\n" +
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
