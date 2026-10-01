using FishNet.Object;
using FishNet.Object.Synchronizing;
using MiniBrawl.Gameplay.Audio;
using MiniBrawl.Gameplay.Weapons;
using MiniBrawl.Networking.Replication;
using UnityEngine;

namespace MiniBrawl.Networking.Match
{
    public enum PickupKind : byte
    {
        Health = 0,
        Shotgun = 1,
        Pistol = 2,
    }

    /// <summary>
    /// A crate on the ground that heals you or hands you a weapon, in the manner of Mini Militia.
    ///
    /// Deliberately not predicted. Everything else a player does is simulated locally and corrected
    /// later, but two players reaching one crate is a question only the server can answer, and a
    /// client that guessed wrong would heal and then visibly un-heal. The effect lands in the
    /// reconciled state instead, so it arrives a round trip late and is never wrong.
    ///
    /// Taken crates do not despawn. They hide, wait, and come back somewhere else — the map should
    /// not quietly run out of the thing people are fighting over.
    /// </summary>
    public sealed class Pickup : NetworkBehaviour
    {
        public SpriteRenderer Body;
        public SpriteRenderer Icon;

        [Tooltip("Picked up within this distance of a player's centre.")]
        public float Radius = 0.9f;

        public float RespawnSeconds = 14f;

        [Tooltip("Health restored. Weapon crates ignore this.")]
        public int HealAmount = 35;

        readonly SyncVar<PickupKind> m_Kind = new();
        readonly SyncVar<bool> m_Available = new();
        readonly SyncVar<Vector2> m_Where = new();

        float m_Hidden;
        float m_Bob;

        static readonly Color k_Health = new Color(0.45f, 0.92f, 0.45f);
        static readonly Color k_Shotgun = new Color(1f, 0.62f, 0.3f);
        static readonly Color k_Pistol = new Color(0.55f, 0.78f, 1f);

        public PickupKind Kind => m_Kind.Value;

        /// <summary>Server-side setup, before the first tick.</summary>
        public void Place(PickupKind kind, Vector2 at)
        {
            m_Kind.Value = kind;
            m_Where.Value = at;
            m_Available.Value = true;
            transform.position = at;
        }

        void Update()
        {
            // Position is synchronised rather than moved, so a client that joins mid-match finds
            // the crate where everyone else sees it.
            transform.position = m_Where.Value;

            bool visible = m_Available.Value;
            if (Body != null) Body.enabled = visible;
            if (Icon != null) Icon.enabled = visible;

            if (visible) Draw();
            if (IsServerStarted) ServerTick();
        }

        void Draw()
        {
            Color tint = m_Kind.Value switch
            {
                PickupKind.Shotgun => k_Shotgun,
                PickupKind.Pistol => k_Pistol,
                _ => k_Health,
            };

            if (Body != null) Body.color = new Color(tint.r, tint.g, tint.b, 0.9f);
            if (Icon != null) Icon.color = Color.white;

            // A slow bob, so a crate on a dark ledge is not mistaken for scenery.
            m_Bob += Time.deltaTime;
            transform.position = m_Where.Value + new Vector2(0f, Mathf.Sin(m_Bob * 2.2f) * 0.09f);
        }

        void ServerTick()
        {
            if (!m_Available.Value)
            {
                m_Hidden -= Time.deltaTime;
                if (m_Hidden > 0f) return;

                // Back, but somewhere else: a crate that always returns to the same ledge belongs
                // to whoever is standing on that ledge.
                PickupSpawner spawner = PickupSpawner.Instance;
                if (spawner != null) m_Where.Value = spawner.RandomPoint();

                m_Available.Value = true;
                return;
            }

            foreach (NetworkPlayerMotor motor in PickupSpawner.LivePlayers())
            {
                if (Vector2.Distance(motor.State.Position, m_Where.Value) > Radius) continue;
                if (!motor.TryTakePickup(m_Kind.Value, HealAmount)) continue;

                m_Available.Value = false;
                m_Hidden = RespawnSeconds;
                AnnounceTaken();
                return;
            }
        }

        /// <summary>
        /// The sound is sent rather than derived, unlike most effects here. Nothing in the
        /// reconciled state says "this crate was collected" — only that somebody's health or
        /// weapon changed — so there is nothing for a client to notice on its own.
        /// </summary>
        [ObserversRpc(RunLocally = true)]
        void AnnounceTaken()
        {
            Sfx.PlayGlobal(m_Kind.Value == PickupKind.Health ? SfxId.Respawn : SfxId.UiConfirm, 0.7f);
        }
    }
}
