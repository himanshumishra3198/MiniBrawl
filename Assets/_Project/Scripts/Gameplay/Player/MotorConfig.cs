using System;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>Tuning values for movement, in world units per second. The player is 0.9 tall.</summary>
    [Serializable]
    public struct MotorConfig
    {
        public float Gravity, MoveSpeed, JetpackThrust, MaxFallSpeed, MaxRiseSpeed;
        public float FuelMax, FuelDrainPerSecond, FuelRegenPerSecond;

        // Tuned by feel on device: walking and falling were both too quick. Gravity and thrust
        // came down together (the ratio is what makes the jetpack feel right), and terminal fall
        // speed dropped hardest, since that is what "falling like a stone" actually is.
        public static MotorConfig Default => new MotorConfig
        {
            Gravity = 18f, MoveSpeed = 4.5f, JetpackThrust = 33f,
            MaxFallSpeed = 10f, MaxRiseSpeed = 7.5f,
            FuelMax = 1f, FuelDrainPerSecond = 0.5f, FuelRegenPerSecond = 0.35f,
        };
    }
}
