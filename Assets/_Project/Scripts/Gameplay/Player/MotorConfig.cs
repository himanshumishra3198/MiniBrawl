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
        // Fuel drain went from 0.5 to 0.33, taking a full tank from two seconds of flight to
        // three. Drain is the lever rather than FuelMax: raising the tank would have stretched
        // the refill in the same proportion, so a longer flight would have been paid for with a
        // longer wait on the ground. Regen is deliberately untouched, which does mean the time
        // spent airborne over a whole match rises by more than the fifty percent this looks like.
        public static MotorConfig Default => new MotorConfig
        {
            Gravity = 18f, MoveSpeed = 4.5f, JetpackThrust = 33f,
            MaxFallSpeed = 10f, MaxRiseSpeed = 7.5f,
            FuelMax = 1f, FuelDrainPerSecond = 0.33f, FuelRegenPerSecond = 0.35f,
        };
    }
}
