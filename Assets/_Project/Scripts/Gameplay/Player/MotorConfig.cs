using System;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>Tuning values for movement. Placeholder numbers — Phase 1 tunes these by feel.</summary>
    [Serializable]
    public struct MotorConfig
    {
        public float Gravity, MoveSpeed, JetpackThrust, MaxFallSpeed, MaxRiseSpeed;
        public float FuelMax, FuelDrainPerSecond, FuelRegenPerSecond;

        public static MotorConfig Default => new MotorConfig
        {
            Gravity = 30f, MoveSpeed = 7f, JetpackThrust = 55f,
            MaxFallSpeed = 18f, MaxRiseSpeed = 9f,
            FuelMax = 1f, FuelDrainPerSecond = 0.5f, FuelRegenPerSecond = 0.35f,
        };
    }
}
