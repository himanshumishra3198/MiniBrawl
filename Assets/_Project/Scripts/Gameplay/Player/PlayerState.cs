using UnityEngine;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>Authoritative per-player simulation state. Snapshot-replicated (§2.4).</summary>
    public struct PlayerState
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float   Fuel;
        public byte    Health;
        public bool    Grounded;

        public static PlayerState Spawn(Vector2 at, float fuel) => new PlayerState
        { Position = at, Velocity = Vector2.zero, Fuel = fuel, Health = 100, Grounded = false };
    }
}
