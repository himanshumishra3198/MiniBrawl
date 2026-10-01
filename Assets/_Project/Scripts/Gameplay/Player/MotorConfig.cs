using System;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>Tuning values for movement, in world units per second. The player is 1.6 tall.</summary>
    [Serializable]
    public struct MotorConfig
    {
        public float Gravity, MoveSpeed, JetpackThrust, MaxFallSpeed, MaxRiseSpeed;
        public float FuelMax, FuelDrainPerSecond, FuelRegenPerSecond;

        // Tuned by feel on device: walking and falling were both too quick. Gravity and thrust
        // came down together (the ratio is what makes the jetpack feel right), and terminal fall
        // speed dropped hardest, since that is what "falling like a stone" actually is.
        //
        // Fuel drain has come down twice, 0.5 to 0.33 to 0.22, taking a full tank from two
        // seconds of flight to four and a half. Drain is the lever rather than FuelMax: raising
        // the tank would stretch the refill in the same proportion, so a longer flight would be
        // paid for with a longer wait on the ground. Regen is deliberately untouched, so the tank
        // now empties slower than it fills and the jetpack is close to always available -- which
        // is what the 44-unit map wants, since crossing it on foot is a long walk.
        public static MotorConfig Default => new MotorConfig
        {
            Gravity = 18f, MoveSpeed = 4.5f, JetpackThrust = 33f,
            MaxFallSpeed = 10f, MaxRiseSpeed = 7.5f,
            FuelMax = 1f, FuelDrainPerSecond = 0.22f, FuelRegenPerSecond = 0.35f,
        };
    }
}
