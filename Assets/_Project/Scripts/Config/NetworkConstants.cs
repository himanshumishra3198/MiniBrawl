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
        /// Version 2 -> 3 is the pickup crate joining the collection, for the same reason.
        /// </summary>
        public const ushort ProtocolVersion = 3;

        public const int SimulationTickRate = 30;   // §5.2 — 30 Hz, mobile thermal budget
        public const int SnapshotSendRate   = 20;
        public const int MaxPlayers         = 6;
        public const float ReconnectWindowSeconds = 45f;

        /// <summary>
        /// How long a silent peer is tolerated before the transport calls it gone. Tugboat defaults
        /// to 1800 (thirty minutes), which on a phone means someone who walks out of Wi-Fi range
        /// keeps a body standing in the level and a seat in the roster for the rest of the session.
        /// Ten seconds rides out a brief blip, and the reconnect window above covers the rest.
        /// </summary>
        public const float ConnectionTimeoutSeconds = 10f;
    }
}
