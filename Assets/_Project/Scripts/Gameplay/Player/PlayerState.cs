using UnityEngine;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>Authoritative per-player simulation state. Snapshot-replicated (§2.4).</summary>
    public struct PlayerState
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float   Fuel;
        public float   RespawnIn;   // seconds left while dead; part of the state so it rolls back
        public byte    Health;
        public bool    Grounded;

        public bool IsDead => Health == 0;

        public static PlayerState Spawn(Vector2 at, float fuel) => new PlayerState
        { Position = at, Velocity = Vector2.zero, Fuel = fuel, Health = 100, Grounded = false, RespawnIn = 0f };
    }
}
