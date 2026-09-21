using FishNet;
using FishNet.Managing;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace MiniBrawl.Networking.Discovery
{
    /// <summary>
    /// Broadcasts while this device is hosting, and stops when it is not. Keeps the advertised
    /// player count current so a joiner can see a full room before trying to join it.
    /// </summary>
    [RequireComponent(typeof(LanBeaconBroadcaster))]
    public sealed class HostAdvertiser : MonoBehaviour
    {
        LanBeaconBroadcaster m_Broadcaster;
        NetworkManager m_Manager;

        void Awake() => m_Broadcaster = GetComponent<LanBeaconBroadcaster>();

        void Start() => m_Manager = InstanceFinder.NetworkManager;

        void Update()
        {
            if (m_Manager == null) return;

            bool hosting = m_Manager.IsServerStarted;

            if (hosting && !m_Broadcaster.IsBroadcasting)
            {
                // Advertise the port actually in use, which -port can change.
                if (m_Manager.TransportManager.Transport is Tugboat tugboat)
                    m_Broadcaster.GamePort = tugboat.GetPort();

                m_Broadcaster.StartBroadcasting(DeviceName());
            }
            else if (!hosting && m_Broadcaster.IsBroadcasting)
                m_Broadcaster.StopBroadcasting();

            if (hosting)
                m_Broadcaster.Players = (byte)Mathf.Min(byte.MaxValue, m_Manager.ServerManager.Clients.Count);
        }

        /// <summary>"Galaxy S21" reads better in a room list than an IP address.</summary>
        static string DeviceName()
        {
            string name = SystemInfo.deviceName;
            return string.IsNullOrWhiteSpace(name) || name == "<unknown>" ? "MiniBrawl host" : name;
        }
    }
}
