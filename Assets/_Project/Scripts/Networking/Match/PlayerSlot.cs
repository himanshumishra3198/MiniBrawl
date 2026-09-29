using System;

namespace MiniBrawl.Networking.Match
{
    /// <summary>
    /// A seat in the match, keyed by the player's own id rather than their connection (§2.7).
    /// The separation is what lets someone drop off Wi-Fi and come back to the same score instead
    /// of arriving as a stranger.
    /// </summary>
    [Serializable]
    public struct PlayerSlot
    {
        /// <summary>Stable across reconnects; survives the connection being replaced.</summary>
        public string PlayerId;

        public string Name;
        public byte ColorIndex;
        public int Kills;
        public int Deaths;

        /// <summary>Ready to start. Cleared between matches so nobody is dragged into a rematch.</summary>
        public bool Ready;

        /// <summary>Current connection, or -1 while the seat is held for someone who dropped.</summary>
        public int ClientId;

        /// <summary>Server time the player dropped; the seat is released once the window passes.</summary>
        public float DisconnectedAt;

        public bool Connected => ClientId >= 0;

        public static PlayerSlot Create(string playerId, string name, byte colorIndex, int clientId) =>
            new PlayerSlot
            {
                PlayerId = playerId,
                Name = string.IsNullOrWhiteSpace(name) ? "Player" : name,
                ColorIndex = colorIndex,
                ClientId = clientId,
                Kills = 0,
                Deaths = 0,
            };

        public PlayerSlot WithScore(int kills, int deaths)
        {
            PlayerSlot copy = this;
            copy.Kills = kills;
            copy.Deaths = deaths;
            return copy;
        }

        public PlayerSlot WithReady(bool ready)
        {
            PlayerSlot copy = this;
            copy.Ready = ready;
            return copy;
        }

        public PlayerSlot WithName(string name, byte colorIndex)
        {
            PlayerSlot copy = this;
            if (!string.IsNullOrWhiteSpace(name)) copy.Name = name;
            copy.ColorIndex = colorIndex;
            return copy;
        }

        public PlayerSlot WithConnection(int clientId, float now = 0f)
        {
            PlayerSlot copy = this;
            copy.ClientId = clientId;
            copy.DisconnectedAt = clientId >= 0 ? 0f : now;
            return copy;
        }
    }
}
