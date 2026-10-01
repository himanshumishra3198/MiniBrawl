using System.Collections.Generic;
using FishNet;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using MiniBrawl.Networking.Replication;
using UnityEngine;

namespace MiniBrawl.Networking.Match
{
    /// <summary>
    /// Puts the crates out, and keeps a list of the places they are allowed to be.
    ///
    /// Server-side. Crates are spawned prefabs rather than objects placed in the scene, for the
    /// same reason the match director is: FishNet assigns scene ids from editor callbacks that
    /// never fire for objects a script created, so a generated scene's NetworkObjects end up with
    /// SceneId 0 and are silently never spawned.
    /// </summary>
    public sealed class PickupSpawner : MonoBehaviour
    {
        public static PickupSpawner Instance { get; private set; }

        public NetworkObject PickupPrefab;

        [Tooltip("Ledges and floor spots crates may appear on. Chosen at random, so there are " +
                 "more of these than there are crates.")]
        public Vector2[] Points = System.Array.Empty<Vector2>();

        [Tooltip("What goes out, in order. Two health to one of each weapon: healing is the thing " +
                 "a losing player needs, and a weapon is a bonus rather than a lifeline.")]
        public PickupKind[] Layout =
        {
            PickupKind.Health, PickupKind.Health,
            PickupKind.Shotgun, PickupKind.Pistol,
        };

        static readonly List<NetworkPlayerMotor> s_Live = new();
        NetworkManager m_Manager;
        bool m_Spawned;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            m_Manager = InstanceFinder.NetworkManager;
            if (m_Manager == null) return;
            m_Manager.ServerManager.OnServerConnectionState += OnServerConnectionState;
        }

        void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started) return;
            if (m_Spawned || PickupPrefab == null || Points.Length == 0) return;

            m_Spawned = true;

            // Spread the opening layout across distinct points, so a match does not begin with two
            // crates stacked on one ledge.
            var taken = new List<Vector2>();
            foreach (PickupKind kind in Layout)
            {
                Vector2 at = RandomPoint(taken);
                taken.Add(at);

                NetworkObject crate = Instantiate(PickupPrefab, at, Quaternion.identity);
                m_Manager.ServerManager.Spawn(crate);
                crate.GetComponent<Pickup>()?.Place(kind, at);
            }

            Debug.Log($"[PickupSpawner] placed {Layout.Length} crates");
        }

        /// <summary>A point from the list, avoiding any already in use.</summary>
        public Vector2 RandomPoint(List<Vector2> avoid = null)
        {
            if (Points.Length == 0) return Vector2.zero;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 candidate = Points[Random.Range(0, Points.Length)];
                if (avoid == null || !avoid.Contains(candidate)) return candidate;
            }

            return Points[Random.Range(0, Points.Length)];
        }

        /// <summary>
        /// Living players, filtered into a reused list.
        ///
        /// Reads the registry the motors keep rather than calling FindObjectsByType: four crates
        /// testing every player every frame would otherwise allocate an array two hundred times a
        /// second, which is exactly the garbage the allocation tests exist to keep out of the loop.
        /// </summary>
        public static IReadOnlyList<NetworkPlayerMotor> LivePlayers()
        {
            s_Live.Clear();

            IReadOnlyList<NetworkPlayerMotor> all = NetworkPlayerMotor.All;
            for (int i = 0; i < all.Count; i++)
            {
                NetworkPlayerMotor motor = all[i];
                if (motor == null || motor.State.IsDead || motor.IsAbsent) continue;
                s_Live.Add(motor);
            }

            return s_Live;
        }
    }
}
