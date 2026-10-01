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

        /// <summary>How many points to sample before picking. Four out of six is enough to
        /// usually find a quiet corner without making spawns predictable.</summary>
        const int k_Candidates = 4;

        public static PlayerSpawner Instance { get; private set; }

        NetworkManager m_Manager;

        void Awake() => Instance = this;

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
            if (Instance == this) Instance = null;
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

        /// <summary>
        /// A spawn point, chosen at random but biased away from whoever is already alive.
        ///
        /// Points used to be handed out in order, which meant a six-player match always began the
        /// same way and a player always came back exactly where they died — easy to sit on. Pure
        /// randomness fixes the predictability and introduces a worse problem, which is arriving
        /// on top of somebody. Sampling a few and keeping the one furthest from the nearest living
        /// player costs nothing at the rate this is called and avoids both.
        ///
        /// Server-only. The result reaches clients through reconciliation like any other position,
        /// so nothing here has to be deterministic across machines.
        /// </summary>
        public Vector2 ChooseSpawn()
        {
            if (SpawnPoints == null || SpawnPoints.Length == 0) return Vector2.zero;
            if (SpawnPoints.Length == 1) return SpawnPoints[0];

            var motors = FindObjectsByType<Replication.NetworkPlayerMotor>(FindObjectsSortMode.None);

            Vector2 best = SpawnPoints[Random.Range(0, SpawnPoints.Length)];
            float bestClearance = -1f;

            for (int attempt = 0; attempt < k_Candidates; attempt++)
            {
                Vector2 candidate = SpawnPoints[Random.Range(0, SpawnPoints.Length)];
                float nearest = float.MaxValue;

                foreach (Replication.NetworkPlayerMotor motor in motors)
                {
                    if (motor.State.IsDead) continue;   // a corpse is not a threat to spawn beside
                    nearest = Mathf.Min(nearest, Vector2.Distance(candidate, motor.State.Position));
                }

                if (nearest <= bestClearance) continue;
                bestClearance = nearest;
                best = candidate;
            }

            return best;
        }

        void OnClientLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            if (!asServer) return;   // the server owns spawning

            if (PlayerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] No player prefab assigned.");
                return;
            }

            Vector2 point = ChooseSpawn();

            NetworkObject player = Instantiate(PlayerPrefab, point, Quaternion.identity);
            m_Manager.ServerManager.Spawn(player, connection);

            Debug.Log($"[PlayerSpawner] spawned player for connection {connection.ClientId} at {point}");
        }
    }
}
