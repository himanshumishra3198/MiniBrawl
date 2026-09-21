using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MiniBrawl.Config;
using MiniBrawl.Platform;
using UnityEngine;

namespace MiniBrawl.Networking.Discovery
{
    /// <summary>
    /// The joiner end of discovery (§4.1): listen for host beacons and keep a live list of rooms.
    ///
    /// Receiving runs on its own thread because UdpClient.Receive blocks, and packets are handed to
    /// the main thread through a queue — Unity objects must not be touched off-thread.
    /// </summary>
    public sealed class LanBeaconListener : MonoBehaviour
    {
        [Tooltip("Seconds without a beacon before a room drops off the list.")]
        public float Timeout = 4f;

        readonly ConcurrentQueue<(string address, BeaconPacket packet)> m_Incoming = new();
        readonly Dictionary<string, RoomInfo> m_Rooms = new();
        readonly List<RoomInfo> m_Snapshot = new();

        UdpClient m_Socket;
        Thread m_Thread;
        volatile bool m_Running;
        IDisposable m_MulticastLock;

        public bool IsListening => m_Running;

        /// <summary>Rooms heard from recently, refreshed each frame while listening.</summary>
        public IReadOnlyList<RoomInfo> Rooms => m_Snapshot;

        public void StartListening()
        {
            if (m_Running) return;

            try
            {
                // Android drops broadcast packets to sleeping apps without this.
                m_MulticastLock = AndroidMulticastLock.Acquire();

                m_Socket = new UdpClient();
                m_Socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                m_Socket.Client.Bind(new IPEndPoint(IPAddress.Any, NetworkConstants.DiscoveryPort));
                m_Socket.Client.ReceiveTimeout = 500;   // so the thread can notice it should stop

                m_Running = true;
                m_Thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "LanBeaconListener" };
                m_Thread.Start();

                Debug.Log($"[LanBeacon] listening on port {NetworkConstants.DiscoveryPort}");
            }
            catch (SocketException e)
            {
                Debug.LogError($"[LanBeacon] could not listen: {e.Message}");
                StopListening();
            }
        }

        public void StopListening()
        {
            m_Running = false;

            m_Socket?.Close();
            m_Socket = null;

            m_Thread = null;   // background thread; it exits on its own once the socket closes

            m_MulticastLock?.Dispose();
            m_MulticastLock = null;

            m_Rooms.Clear();
            m_Snapshot.Clear();
        }

        void OnDestroy() => StopListening();

        void ReceiveLoop()
        {
            var sender = new IPEndPoint(IPAddress.Any, 0);

            while (m_Running)
            {
                try
                {
                    byte[] data = m_Socket.Receive(ref sender);
                    if (BeaconPacket.TryParse(data, data.Length, out BeaconPacket packet))
                        m_Incoming.Enqueue((sender.Address.ToString(), packet));
                }
                catch (SocketException)
                {
                    // Receive timeout, or the socket closed underneath us on shutdown.
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        void Update()
        {
            if (!m_Running) return;

            float now = Time.unscaledTime;

            while (m_Incoming.TryDequeue(out (string address, BeaconPacket packet) received))
            {
                var room = new RoomInfo
                {
                    Address = received.address,
                    Port = received.packet.GamePort,
                    HostName = received.packet.HostName,
                    Players = received.packet.Players,
                    MaxPlayers = received.packet.MaxPlayers,
                    Compatible = received.packet.IsCompatible,
                    LastSeen = now,
                };

                if (!m_Rooms.ContainsKey(room.Key))
                    Debug.Log($"[LanBeacon] found host {room.Describe()}");

                m_Rooms[room.Key] = room;
            }

            RebuildSnapshot(now);
        }

        void RebuildSnapshot(float now)
        {
            m_Snapshot.Clear();

            List<string> stale = null;
            foreach (KeyValuePair<string, RoomInfo> entry in m_Rooms)
            {
                if (now - entry.Value.LastSeen > Timeout)
                {
                    (stale ??= new List<string>()).Add(entry.Key);
                    continue;
                }
                m_Snapshot.Add(entry.Value);
            }

            if (stale == null) return;
            foreach (string key in stale) m_Rooms.Remove(key);
        }
    }
}
