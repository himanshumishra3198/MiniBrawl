using System;
using System.IO;
using System.Text;
using MiniBrawl.Config;

namespace MiniBrawl.Networking.Discovery
{
    /// <summary>
    /// What a host shouts onto the subnet once a second (§4.1). Deliberately tiny and
    /// version-stamped: a phone running an older build must be able to tell that it cannot join,
    /// rather than connecting and desyncing.
    /// </summary>
    public struct BeaconPacket
    {
        public const int MaxNameLength = 24;

        /// <summary>Longest a valid packet can be; anything bigger is not ours.</summary>
        public const int MaxSize = 4 + 2 + 2 + 1 + 1 + 1 + MaxNameLength * 3;

        public ushort ProtocolVersion;
        public ushort GamePort;
        public byte Players;
        public byte MaxPlayers;
        public string HostName;

        public bool IsCompatible => ProtocolVersion == NetworkConstants.ProtocolVersion;

        /// <param name="gamePort">The port actually being listened on, which is not necessarily the
        /// default — a joiner dials whatever this says.</param>
        public static BeaconPacket Create(string hostName, byte players, ushort gamePort)
        {
            if (string.IsNullOrWhiteSpace(hostName)) hostName = "MiniBrawl";
            if (hostName.Length > MaxNameLength) hostName = hostName.Substring(0, MaxNameLength);

            return new BeaconPacket
            {
                ProtocolVersion = NetworkConstants.ProtocolVersion,
                GamePort = gamePort,
                Players = players,
                MaxPlayers = NetworkConstants.MaxPlayers,
                HostName = hostName,
            };
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            writer.Write(NetworkConstants.BeaconMagic);
            writer.Write(ProtocolVersion);
            writer.Write(GamePort);
            writer.Write(Players);
            writer.Write(MaxPlayers);

            byte[] name = Encoding.UTF8.GetBytes(HostName ?? "");
            writer.Write((byte)name.Length);
            writer.Write(name);

            return stream.ToArray();
        }

        /// <summary>
        /// Parses a datagram. Returns false for anything that is not one of ours — the port is
        /// shared with whatever else is on the network, so malformed input is expected, not
        /// exceptional.
        /// </summary>
        public static bool TryParse(byte[] data, int length, out BeaconPacket packet)
        {
            packet = default;
            if (data == null || length < 11 || length > MaxSize) return false;

            try
            {
                using var stream = new MemoryStream(data, 0, length, writable: false);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                if (reader.ReadUInt32() != NetworkConstants.BeaconMagic) return false;

                packet.ProtocolVersion = reader.ReadUInt16();
                packet.GamePort = reader.ReadUInt16();
                packet.Players = reader.ReadByte();
                packet.MaxPlayers = reader.ReadByte();

                byte nameLength = reader.ReadByte();
                if (nameLength > MaxNameLength * 3 || stream.Position + nameLength > length) return false;

                packet.HostName = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                return true;
            }
            catch (Exception)
            {
                // Truncated or hostile packet; discovery just ignores it.
                return false;
            }
        }
    }
}
