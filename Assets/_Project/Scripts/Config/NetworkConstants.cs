namespace MiniBrawl.Config
{
    /// <summary>Wire-level constants. Shared by host and client; must match exactly.</summary>
    public static class NetworkConstants
    {
        public const ushort GamePort      = 7770;  // FishNet Tugboat (UDP)
        public const ushort DiscoveryPort = 7771;  // LAN beacon broadcast (UDP)
        public const uint   BeaconMagic   = 0x4D4D4231; // 'MMB1'
        /// <summary>
        /// Bump on ANY change that makes builds incompatible. That includes the spawnable prefab
        /// collection: FishNet spawns by index, so adding a prefab renumbers the existing ones and
        /// an older build spawns the wrong object or none at all. Version 1 -> 2 was exactly that,
        /// and it was missed, so two builds declared themselves compatible and failed to play.
        /// </summary>
        public const ushort ProtocolVersion = 2;

        public const int SimulationTickRate = 30;   // §5.2 — 30 Hz, mobile thermal budget
        public const int SnapshotSendRate   = 20;
        public const int MaxPlayers         = 6;
        public const float ReconnectWindowSeconds = 45f;
    }
}
