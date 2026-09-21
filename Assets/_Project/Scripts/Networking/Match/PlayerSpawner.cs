using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace MiniBrawl.Networking.Match
{
    /// <summary>
    /// Server-side spawning. Waits for a connection to finish loading the start scenes, then gives
    /// it a player at the next spawn point. Host-authoritative: clients never spawn themselves.
    /// </summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        public NetworkObject PlayerPrefab;

        [Tooltip("Spawned once when the server starts, before anyone joins.")]
        public NetworkObject MatchDirectorPrefab;
        public Vector2[] SpawnPoints = { new Vector2(-4f, -4f), new Vector2(4f, -4f) };

        NetworkManager m_Manager;
        int m_NextSpawn;

        void Start()
        {
            m_Manager = InstanceFinder.NetworkManager;
            if (m_Manager == null)
            {
                Debug.LogError("[PlayerSpawner] No NetworkManager in the scene.");
                return;
            }
            m_Manager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            m_Manager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            m_Manager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        void OnDestroy()
        {
            if (m_Manager == null) return;
            m_Manager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            m_Manager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            m_Manager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
        }

        /// <summary>The match has to exist before the first player claims a slot in it.</summary>
        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            if (MatchDirectorPrefab == null || MatchDirector.Instance != null) return;

            NetworkObject director = Instantiate(MatchDirectorPrefab);
            m_Manager.ServerManager.Spawn(director);
            Debug.Log("[PlayerSpawner] spawned the match director");
        }

        void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped) return;

            // The seat is held rather than removed: the same player id reclaims it on return.
            MatchDirector.Instance?.ReleaseSlot(connection.ClientId);
        }

        void OnClientLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            if (!asServer) return;   // the server owns spawning

            if (PlayerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] No player prefab assigned.");
                return;
            }

            Vector2 point = SpawnPoints.Length > 0
                ? SpawnPoints[m_NextSpawn++ % SpawnPoints.Length]
                : Vector2.zero;

            NetworkObject player = Instantiate(PlayerPrefab, point, Quaternion.identity);
            m_Manager.ServerManager.Spawn(player, connection);

            Debug.Log($"[PlayerSpawner] spawned player for connection {connection.ClientId} at {point}");
        }
    }
}
