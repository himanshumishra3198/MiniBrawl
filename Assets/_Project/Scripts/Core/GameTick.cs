namespace MiniBrawl.Core
{
    /// <summary>Fixed-timestep helper shared by the simulation and the network layer.</summary>
    public struct GameTick
    {
        public readonly int Rate;
        public GameTick(int rate) { Rate = rate; }
        public float DeltaTime => 1f / Rate;
        public uint SecondsToTicks(float seconds) => (uint)(seconds * Rate);
        public float TicksToSeconds(uint ticks) => ticks / (float)Rate;
    }
}
