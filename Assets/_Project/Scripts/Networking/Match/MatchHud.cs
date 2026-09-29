using System.Collections.Generic;
using System.Text;
using MiniBrawl.Gameplay.Rules;
using MiniBrawl.Networking.Identity;
using UnityEngine;
using UnityEngine.UI;

namespace MiniBrawl.Networking.Match
{
    /// <summary>
    /// Shows the match: phase banner, clock, scoreboard and kill feed. Reads the director rather
    /// than tracking anything itself, so every device shows the same thing without extra syncing.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        public Text Banner;
        public Text Scoreboard;
        public Text KillFeed;

        [Tooltip("Shown in the lobby only; lets the local player opt into the next match.")]
        public Button ReadyButton;
        public Text ReadyLabel;

        [Tooltip("Always available: leaving a session should never need force-quitting the app.")]
        public Button LeaveButton;

        const int k_FeedLength = 4;
        const float k_FeedEntrySeconds = 6f;

        readonly List<(string text, float time)> m_Feed = new();
        MatchDirector m_Director;

        void Update()
        {
            if (m_Director == null)
            {
                m_Director = MatchDirector.Instance;
                if (m_Director == null)
                {
                    if (Banner != null) Banner.text = "";
                    if (ReadyButton != null) ReadyButton.gameObject.SetActive(false);
                    return;
                }
                m_Director.KillReported += OnKillReported;
                m_Director.PlayerEventReported += OnPlayerEvent;
                if (ReadyButton != null) ReadyButton.onClick.AddListener(OnReadyClicked);
                if (LeaveButton != null) LeaveButton.onClick.AddListener(OnLeaveClicked);
            }

            DrawBanner();
            DrawScoreboard();
            DrawKillFeed();
            DrawReadyButton();
        }

        void DrawReadyButton()
        {
            if (ReadyButton == null) return;

            // Only meaningful in the lobby; during a match it would just be in the way.
            bool inLobby = m_Director.Phase == MatchPhase.Lobby;
            ReadyButton.gameObject.SetActive(inLobby);
            if (!inLobby) return;

            bool ready = m_Director.Slots.TryGetValue(PlayerIdentity.Id, out PlayerSlot mine) && mine.Ready;
            if (ReadyLabel != null) ReadyLabel.text = ready ? "READY ✓" : "TAP WHEN READY";

            var image = ReadyButton.GetComponent<Image>();
            if (image != null)
                image.color = ready ? new Color(0.55f, 0.95f, 0.45f, 0.35f) : new Color(1f, 1f, 1f, 0.15f);
        }

        /// <summary>
        /// Stops the session and returns to the menu. Hosting stops the server too, which tells
        /// every client the host has gone rather than leaving them guessing.
        /// </summary>
        void OnLeaveClicked()
        {
            Session.NetworkBootstrap bootstrap = FindFirstObjectByType<Session.NetworkBootstrap>();
            bootstrap?.Stop();
        }

        void OnReadyClicked()
        {
            if (m_Director == null) return;

            bool ready = m_Director.Slots.TryGetValue(PlayerIdentity.Id, out PlayerSlot mine) && mine.Ready;
            m_Director.SetReady(PlayerIdentity.Id, !ready);
        }

        void OnDestroy()
        {
            if (m_Director == null) return;
            m_Director.KillReported -= OnKillReported;
            m_Director.PlayerEventReported -= OnPlayerEvent;
        }

        void OnPlayerEvent(string message)
        {
            m_Feed.Add((message, Time.unscaledTime));
            if (m_Feed.Count > k_FeedLength) m_Feed.RemoveAt(0);
        }

        void OnKillReported(string killer, string victim)
        {
            string line = killer == victim ? $"{victim} died" : $"{killer} killed {victim}";
            m_Feed.Add((line, Time.unscaledTime));

            if (m_Feed.Count > k_FeedLength) m_Feed.RemoveAt(0);
        }

        void DrawBanner()
        {
            if (Banner == null) return;

            float remaining = m_Director.TimeRemaining;

            Banner.text = m_Director.Phase switch
            {
                MatchPhase.Lobby => LobbyLine(),
                MatchPhase.Countdown => $"STARTING IN {Mathf.CeilToInt(remaining)}",
                MatchPhase.Playing => $"{Mathf.FloorToInt(remaining / 60f)}:{Mathf.FloorToInt(remaining % 60f):00}",
                MatchPhase.MatchEnd => WinnerLine(),
                _ => "",
            };
        }

        string LobbyLine()
        {
            int connected = CountConnected();
            int minimum = m_Director.Settings.MinimumPlayers;

            if (connected < minimum) return $"WAITING FOR PLAYERS  ({connected}/{minimum})";

            int ready = 0;
            foreach (KeyValuePair<string, PlayerSlot> pair in m_Director.Slots)
                if (pair.Value.Connected && pair.Value.Ready) ready++;

            return $"READY  {ready}/{connected}";
        }

        string WinnerLine()
        {
            List<PlayerSlot> standings = m_Director.Standings();
            if (standings.Count == 0) return "MATCH OVER";

            PlayerSlot top = standings[0];
            bool draw = standings.Count > 1 && standings[1].Kills == top.Kills;

            return draw ? "DRAW" : $"{top.Name} WINS  ({top.Kills})";
        }

        int CountConnected()
        {
            int count = 0;
            foreach (KeyValuePair<string, PlayerSlot> pair in m_Director.Slots)
                if (pair.Value.Connected) count++;
            return count;
        }

        void DrawScoreboard()
        {
            if (Scoreboard == null) return;

            // Only worth the screen space once the match is over or nearly decided.
            bool detailed = m_Director.Phase == MatchPhase.MatchEnd;

            var text = new StringBuilder();
            foreach (PlayerSlot slot in m_Director.Standings())
            {
                // Three states worth distinguishing: playing, silent but expected back, and gone.
                string name = slot.Name;
                if (!slot.Connected) name = $"{slot.Name} (left)";
                else if (slot.Absent) name = $"{slot.Name} (no signal)";

                // In the lobby the useful column is who is ready, not who is winning.
                if (m_Director.Phase == MatchPhase.Lobby)
                {
                    text.AppendLine($"{name,-16} {(slot.Ready ? "ready" : "…")}");
                    continue;
                }

                text.AppendLine(detailed
                    ? $"{name,-16} {slot.Kills,3} kills  {slot.Deaths,3} deaths"
                    : $"{name,-16} {slot.Kills,3}");
            }

            Scoreboard.text = text.ToString();
        }

        void DrawKillFeed()
        {
            if (KillFeed == null) return;

            float now = Time.unscaledTime;
            m_Feed.RemoveAll(entry => now - entry.time > k_FeedEntrySeconds);

            var text = new StringBuilder();
            foreach ((string line, float _) in m_Feed) text.AppendLine(line);

            KillFeed.text = text.ToString();
        }
    }
}
