using System;
using System.Net;
using System.Net.Sockets;
using MiniBrawl.Config;
using MiniBrawl.Platform;
using UnityEngine;

namespace MiniBrawl.Networking.Discovery
{
    /// <summary>
    /// The host end of discovery (§4.1): shout a small packet onto the subnet once a second so
    /// joiners can list rooms instead of typing an address.
    ///
    /// Sends to both the global broadcast address and the subnet-directed one, because some
    /// Android builds and routers drop one or the other.
    /// </summary>
    public sealed class LanBeaconBroadcaster : MonoBehaviour
    {
        public float Interval = 1f;

        UdpClient m_Socket;
        IPEndPoint[] m_Targets;
        float m_NextSend;
        string m_HostName;

        public bool IsBroadcasting => m_Socket != null;

        /// <summary>The port the game is actually listening on; joiners dial this, not the default.</summary>
        public ushort GamePort { get; set; } = NetworkConstants.GamePort;

        public void StartBroadcasting(string hostName)
        {
            if (m_Socket != null) return;

            m_HostName = hostName;

            try
            {
                m_Socket = new UdpClient { EnableBroadcast = true };
                m_Targets = BuildTargets();
                m_NextSend = 0f;
                Debug.Log($"[LanBeacon] broadcasting '{hostName}' on port {NetworkConstants.DiscoveryPort}");
            }
            catch (SocketException e)
            {
                Debug.LogError($"[LanBeacon] could not open broadcast socket: {e.Message}");
                m_Socket = null;
            }
        }

        public void StopBroadcasting()
        {
            m_Socket?.Close();
            m_Socket = null;
        }

        void OnDestroy() => StopBroadcasting();

        void Update()
        {
            if (m_Socket == null) return;

            m_NextSend -= Time.unscaledDeltaTime;
            if (m_NextSend > 0f) return;
            m_NextSend = Interval;

            Send();
        }

        /// <summary>Called by the host to keep the advertised player count honest.</summary>
        public byte Players { get; set; }

        void Send()
        {
            byte[] payload = BeaconPacket.Create(m_HostName, Players, GamePort).ToBytes();

            foreach (IPEndPoint target in m_Targets)
            {
                try
                {
                    m_Socket.Send(payload, payload.Length, target);
                }
                catch (SocketException)
                {
                    // One dead route should not stop the others, and networks come and go.
                }
            }
        }

        static IPEndPoint[] BuildTargets()
        {
            var global = new IPEndPoint(IPAddress.Broadcast, NetworkConstants.DiscoveryPort);

            string local = LocalIpResolver.Resolve();
            if (!TryGetSubnetBroadcast(local, out IPAddress subnet))
                return new[] { global };

            return new[] { global, new IPEndPoint(subnet, NetworkConstants.DiscoveryPort) };
        }

        /// <summary>Turns 192.168.1.33 into 192.168.1.255, assuming the /24 that home and hotspot
        /// networks almost always use.</summary>
        static bool TryGetSubnetBroadcast(string address, out IPAddress broadcast)
        {
            broadcast = null;
            if (!IPAddress.TryParse(address, out IPAddress parsed)) return false;

            byte[] octets = parsed.GetAddressBytes();
            if (octets.Length != 4) return false;

            octets[3] = 255;
            broadcast = new IPAddress(octets);
            return true;
        }
    }
}
