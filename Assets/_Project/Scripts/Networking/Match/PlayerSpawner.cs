using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
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
        }

        void OnDestroy()
        {
            if (m_Manager != null)
                m_Manager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
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
