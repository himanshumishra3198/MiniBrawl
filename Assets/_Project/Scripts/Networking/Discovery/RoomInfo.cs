namespace MiniBrawl.Networking.Discovery
{
    /// <summary>A host we have heard from recently.</summary>
    public struct RoomInfo
    {
        public string Address;
        public ushort Port;
        public string HostName;
        public byte Players;
        public byte MaxPlayers;
        public bool Compatible;

        /// <summary>Unscaled time of the last beacon, used to drop hosts that went away.</summary>
        public float LastSeen;

        public string Key => $"{Address}:{Port}";

        public bool IsFull => Players >= MaxPlayers;

        public string Describe()
        {
            string status = !Compatible ? "different version" : IsFull ? "full" : $"{Players}/{MaxPlayers}";
            return $"{HostName}   {Address}   {status}";
        }
    }
}
