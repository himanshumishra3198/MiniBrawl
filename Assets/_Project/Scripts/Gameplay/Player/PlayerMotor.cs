using UnityEngine;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>
    /// Kinematic 2D character movement. Pure C#, no Rigidbody2D, no networking types.
    /// Deterministic and cheap enough to re-run ~10x per frame during reconciliation (§2.5).
    /// </summary>
    public static class PlayerMotor
    {
        public static readonly Vector2 Size = new Vector2(0.6f, 0.9f);
        const float Skin = 0.02f;

        public static PlayerState Simulate(in PlayerState prev, in PlayerInput input,
                                           in MotorConfig cfg, IMotorCollision world, float dt)
        {
            PlayerState s = prev;

            s.Velocity.x = input.MoveAxis * cfg.MoveSpeed;

            if (input.Jetpack)
            {
                // Holding the button on an empty tank must NOT refuel, otherwise the
                // player gets a 1-frame regen/burn sputter. Release to refuel.
                if (s.Fuel > 0f)
                {
                    s.Velocity.y += cfg.JetpackThrust * dt;
                    s.Fuel = Mathf.Max(0f, s.Fuel - cfg.FuelDrainPerSecond * dt);
                }
            }
            else
            {
                s.Fuel = Mathf.Min(cfg.FuelMax, s.Fuel + cfg.FuelRegenPerSecond * dt);
            }

            s.Velocity.y -= cfg.Gravity * dt;
            s.Velocity.y = Mathf.Clamp(s.Velocity.y, -cfg.MaxFallSpeed, cfg.MaxRiseSpeed);

            s.Grounded = false;
            Move(ref s, new Vector2(s.Velocity.x * dt, 0f), world);
            Move(ref s, new Vector2(0f, s.Velocity.y * dt), world);
            return s;
        }

        static void Move(ref PlayerState s, Vector2 delta, IMotorCollision world)
        {
            float dist = delta.magnitude;
            if (dist < 1e-6f) return;

            Vector2 dir = delta / dist;
            if (world != null && world.Cast(s.Position, Size, dir, dist + Skin, out float hit))
            {
                s.Position += dir * Mathf.Max(0f, hit - Skin);
                if (dir.y < 0f) s.Grounded = true;
                if (dir.y != 0f) s.Velocity.y = 0f; else s.Velocity.x = 0f;
            }
            else
            {
                s.Position += delta;
            }
        }
    }
}
