using System.Collections.Generic;
using System.Text;
using MiniBrawl.Gameplay.Rules;
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
                    return;
                }
                m_Director.KillReported += OnKillReported;
            }

            DrawBanner();
            DrawScoreboard();
            DrawKillFeed();
        }

        void OnDestroy()
        {
            if (m_Director != null) m_Director.KillReported -= OnKillReported;
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
                MatchPhase.Lobby => $"WAITING FOR PLAYERS  ({CountConnected()}/{m_Director.Settings.MinimumPlayers})",
                MatchPhase.Countdown => $"STARTING IN {Mathf.CeilToInt(remaining)}",
                MatchPhase.Playing => $"{Mathf.FloorToInt(remaining / 60f)}:{Mathf.FloorToInt(remaining % 60f):00}",
                MatchPhase.MatchEnd => WinnerLine(),
                _ => "",
            };
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
                string name = slot.Connected ? slot.Name : $"{slot.Name} (away)";
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
