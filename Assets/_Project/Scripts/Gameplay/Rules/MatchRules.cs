using System;

namespace MiniBrawl.Gameplay.Rules
{
    /// <summary>Where a session is in its cycle (§2.3).</summary>
    public enum MatchPhase : byte
    {
        /// <summary>Waiting for players; nobody is scoring.</summary>
        Lobby = 0,
        Countdown = 1,
        Playing = 2,
        /// <summary>Scoreboard is up and a rematch can be called.</summary>
        MatchEnd = 3,
    }

    [Serializable]
    public struct MatchSettings
    {
        public float CountdownSeconds;
        public float MatchSeconds;
        public float EndSeconds;
        public int KillTarget;
        public int MinimumPlayers;

        /// <summary>Short matches with an easy rematch loop, per the MVP scope (§7).</summary>
        public static MatchSettings Default => new MatchSettings
        {
            CountdownSeconds = 3f,
            MatchSeconds = 180f,
            EndSeconds = 8f,
            KillTarget = 15,
            MinimumPlayers = 2,
        };
    }

    /// <summary>
    /// The match state machine, as pure functions. Kept out of the networking layer so the rules
    /// can be tested without a server — the same split that lets PlayerMotor be re-simulated.
    /// </summary>
    public static class MatchRules
    {
        /// <summary>
        /// Decides the phase after <paramref name="elapsed"/> seconds in the current one.
        /// Returns the same phase when nothing should change.
        /// </summary>
        public static MatchPhase Advance(MatchPhase current, float elapsed, int players, int ready,
                                         int topScore, in MatchSettings settings)
        {
            switch (current)
            {
                case MatchPhase.Lobby:
                    // Everyone present has to be ready, so nobody is dropped into a match while
                    // still picking a name.
                    return players >= settings.MinimumPlayers && ready >= players
                        ? MatchPhase.Countdown
                        : MatchPhase.Lobby;

                case MatchPhase.Countdown:
                    // Someone leaving during the countdown sends everyone back to waiting.
                    if (players < settings.MinimumPlayers) return MatchPhase.Lobby;
                    return elapsed >= settings.CountdownSeconds ? MatchPhase.Playing : MatchPhase.Countdown;

                case MatchPhase.Playing:
                    if (players < settings.MinimumPlayers) return MatchPhase.MatchEnd;
                    if (settings.KillTarget > 0 && topScore >= settings.KillTarget) return MatchPhase.MatchEnd;
                    return elapsed >= settings.MatchSeconds ? MatchPhase.MatchEnd : MatchPhase.Playing;

                case MatchPhase.MatchEnd:
                    return elapsed >= settings.EndSeconds ? MatchPhase.Lobby : MatchPhase.MatchEnd;

                default:
                    return MatchPhase.Lobby;
            }
        }

        /// <summary>How long this phase lasts, or 0 when it waits on players rather than the clock.</summary>
        public static float PhaseDuration(MatchPhase phase, in MatchSettings settings) => phase switch
        {
            MatchPhase.Countdown => settings.CountdownSeconds,
            MatchPhase.Playing => settings.MatchSeconds,
            MatchPhase.MatchEnd => settings.EndSeconds,
            _ => 0f,
        };

        /// <summary>Scoring only counts while playing, so a stray shot during the countdown is free.</summary>
        public static bool ScoringEnabled(MatchPhase phase) => phase == MatchPhase.Playing;

        /// <summary>Players are frozen outside play, which keeps the scoreboard readable.</summary>
        public static bool MovementEnabled(MatchPhase phase) =>
            phase == MatchPhase.Playing || phase == MatchPhase.Lobby;
    }
}
