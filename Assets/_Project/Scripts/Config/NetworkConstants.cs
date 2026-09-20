namespace MiniBrawl.Config
{
    /// <summary>Wire-level constants. Shared by host and client; must match exactly.</summary>
    public static class NetworkConstants
    {
        public const ushort GamePort      = 7770;  // FishNet Tugboat (UDP)
        public const ushort DiscoveryPort = 7771;  // LAN beacon broadcast (UDP)
        public const uint   BeaconMagic   = 0x4D4D4231; // 'MMB1'
        public const ushort ProtocolVersion = 1;    // bump on any wire change

        public const int SimulationTickRate = 30;   // §5.2 — 30 Hz, mobile thermal budget
        public const int SnapshotSendRate   = 20;
        public const int MaxPlayers         = 6;
        public const float ReconnectWindowSeconds = 45f;
    }
}
