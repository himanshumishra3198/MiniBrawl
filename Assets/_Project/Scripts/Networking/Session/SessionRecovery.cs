using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using MiniBrawl.Config;
using MiniBrawl.Networking.Discovery;
using UnityEngine;

namespace MiniBrawl.Networking.Session
{
    /// <summary>
    /// Keeps a dropped client trying to get back in (§2.7). Phones lose Wi-Fi constantly — walking
    /// out of range, the screen locking, Android trimming a backgrounded app — and none of those
    /// should end the match for that player. The host holds their seat and score for the same
    /// window, so a successful return is seamless.
    ///
    /// The hard part is telling "my connection blipped" from "the host is gone". The beacon answers
    /// it: a host still shouting on the subnet is worth waiting for, one that has gone quiet is not.
    /// </summary>
    public sealed class SessionRecovery : MonoBehaviour
    {
        [Tooltip("Seconds between reconnect attempts.")]
        public float RetryInterval = 2f;

        /// <summary>Time to allow before deciding a silent host has actually gone (§4.4).</summary>
        const float k_HostSilenceGrace = 6f;

        NetworkManager m_Manager;
        NetworkBootstrap m_Bootstrap;
        LanBeaconListener m_Listener;

        float m_GiveUpAt;
        float m_NextAttemptAt;
        float m_StartedAt;
        string m_Address;
        ushort m_Port;

        public bool IsRecovering { get; private set; }

        /// <summary>Human-readable state for the menu.</summary>
        public string Status { get; private set; } = "";

        void Start()
        {
            m_Manager = InstanceFinder.NetworkManager;
            m_Bootstrap = FindFirstObjectByType<NetworkBootstrap>();
            m_Listener = FindFirstObjectByType<LanBeaconListener>();

            if (m_Manager != null)
                m_Manager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        void OnDestroy()
        {
            if (m_Manager != null)
                m_Manager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                if (IsRecovering) Debug.Log("[Recovery] back in");
                Stop();
                return;
            }

            if (args.ConnectionState != LocalConnectionState.Stopped || IsRecovering) return;

            // Hosts do not reconnect to themselves, and leaving on purpose should stay left.
            if (m_Manager.IsServerStarted) return;
            if (m_Bootstrap == null || m_Bootstrap.StoppedIntentionally) return;

            Begin();
        }

        void Begin()
        {
            m_Address = m_Bootstrap.Address;
            m_Port = m_Bootstrap.Port;

            IsRecovering = true;
            m_StartedAt = Time.unscaledTime;
            m_GiveUpAt = m_StartedAt + NetworkConstants.ReconnectWindowSeconds;
            m_NextAttemptAt = m_StartedAt + 1f;   // a moment to settle before the first retry

            Debug.Log($"[Recovery] lost the host; retrying {m_Address}:{m_Port} for " +
                      $"{NetworkConstants.ReconnectWindowSeconds}s");
        }

        void Stop()
        {
            IsRecovering = false;
            Status = "";
        }

        void Update()
        {
            if (!IsRecovering) return;

            float now = Time.unscaledTime;

            if (now >= m_GiveUpAt)
            {
                Fail("could not get back in — the match went on without you");
                return;
            }

            // A host that stopped advertising has shut down; waiting out the full window would
            // leave the player staring at a dead screen for no reason.
            if (now - m_StartedAt > k_HostSilenceGrace && !HostIsStillAdvertising())
            {
                Fail("the host left the game");
                return;
            }

            Status = $"reconnecting to {m_Address}… {Mathf.CeilToInt(m_GiveUpAt - now)}s";

            if (now < m_NextAttemptAt) return;
            m_NextAttemptAt = now + RetryInterval;

            m_Bootstrap.StartClient(m_Address, m_Port);
        }

        bool HostIsStillAdvertising()
        {
            // No listener means no evidence either way, so keep trying rather than guessing.
            if (m_Listener == null || !m_Listener.IsListening) return true;

            foreach (RoomInfo room in m_Listener.Rooms)
                if (room.Address == m_Address) return true;

            return false;
        }

        void Fail(string reason)
        {
            Debug.Log($"[Recovery] giving up: {reason}");
            IsRecovering = false;
            Status = reason;
        }

        /// <summary>
        /// Android suspends a backgrounded app, which kills the socket without warning. Coming back
        /// to the foreground is the moment to retry, rather than waiting out the retry interval.
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (paused || !IsRecovering) return;

            m_NextAttemptAt = 0f;
            Debug.Log("[Recovery] resumed from background; retrying now");
        }
    }
}
