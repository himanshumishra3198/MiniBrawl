using UnityEngine;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>
    /// Collision probe abstraction. Unity supplies a Physics2D-backed implementation;
    /// tests supply a fake. Keeps PlayerMotor deterministic and unit-testable (§2.5).
    /// </summary>
    public interface IMotorCollision
    {
        bool Cast(Vector2 origin, Vector2 size, Vector2 direction, float distance, out float hitDistance);
    }
}
