using MiniBrawl.Gameplay.Rules;
using NUnit.Framework;

namespace MiniBrawl.Gameplay.Tests
{
    public class MatchRulesTests
    {
        static MatchSettings Settings => MatchSettings.Default;   // 3s countdown, 180s match, 15 kills

        [Test]
        public void Lobby_WaitsForEnoughPlayers()
        {
            Assert.AreEqual(MatchPhase.Lobby,
                MatchRules.Advance(MatchPhase.Lobby, elapsed: 999f, players: 1, topScore: 0, Settings),
                "one player alone should wait however long it takes");

            Assert.AreEqual(MatchPhase.Countdown,
                MatchRules.Advance(MatchPhase.Lobby, elapsed: 0f, players: 2, topScore: 0, Settings));
        }

        [Test]
        public void Countdown_RunsItsClockThenPlays()
        {
            Assert.AreEqual(MatchPhase.Countdown,
                MatchRules.Advance(MatchPhase.Countdown, 2.9f, players: 2, topScore: 0, Settings));

            Assert.AreEqual(MatchPhase.Playing,
                MatchRules.Advance(MatchPhase.Countdown, 3f, players: 2, topScore: 0, Settings));
        }

        [Test]
        public void Countdown_AbortsIfSomeoneLeaves()
        {
            Assert.AreEqual(MatchPhase.Lobby,
                MatchRules.Advance(MatchPhase.Countdown, 1f, players: 1, topScore: 0, Settings),
                "dropping below the minimum mid-countdown should go back to waiting");
        }

        [Test]
        public void Playing_EndsOnTheKillTarget()
        {
            Assert.AreEqual(MatchPhase.Playing,
                MatchRules.Advance(MatchPhase.Playing, 10f, players: 2, topScore: 14, Settings));

            Assert.AreEqual(MatchPhase.MatchEnd,
                MatchRules.Advance(MatchPhase.Playing, 10f, players: 2, topScore: 15, Settings),
                "reaching the target should end the match immediately, not at the clock");
        }

        [Test]
        public void Playing_EndsOnTheClock()
        {
            Assert.AreEqual(MatchPhase.MatchEnd,
                MatchRules.Advance(MatchPhase.Playing, 180f, players: 2, topScore: 3, Settings));
        }

        [Test]
        public void Playing_EndsIfEveryoneElseLeaves()
        {
            Assert.AreEqual(MatchPhase.MatchEnd,
                MatchRules.Advance(MatchPhase.Playing, 5f, players: 1, topScore: 2, Settings),
                "a one-player match is over, however much time is left");
        }

        [Test]
        public void MatchEnd_ReturnsToLobby_SoRematchesCanLoop()
        {
            Assert.AreEqual(MatchPhase.MatchEnd,
                MatchRules.Advance(MatchPhase.MatchEnd, 7f, players: 2, topScore: 15, Settings));

            Assert.AreEqual(MatchPhase.Lobby,
                MatchRules.Advance(MatchPhase.MatchEnd, 8f, players: 2, topScore: 15, Settings));
        }

        [Test]
        public void FiveMatchesInARow_NeedNoIntervention()
        {
            // The Phase 4 bar: five consecutive matches without leaving the session.
            MatchPhase phase = MatchPhase.Lobby;
            int completed = 0;

            for (int step = 0; step < 5000 && completed < 5; step++)
            {
                float duration = MatchRules.PhaseDuration(phase, Settings);
                MatchPhase next = MatchRules.Advance(phase, duration, players: 2, topScore: 15, Settings);

                if (phase == MatchPhase.MatchEnd && next == MatchPhase.Lobby) completed++;
                phase = next;
            }

            Assert.AreEqual(5, completed, "the loop should cycle without getting stuck in a phase");
        }

        [Test]
        public void ScoringOnlyCountsWhilePlaying()
        {
            Assert.IsTrue(MatchRules.ScoringEnabled(MatchPhase.Playing));
            Assert.IsFalse(MatchRules.ScoringEnabled(MatchPhase.Countdown));
            Assert.IsFalse(MatchRules.ScoringEnabled(MatchPhase.MatchEnd));
            Assert.IsFalse(MatchRules.ScoringEnabled(MatchPhase.Lobby));
        }

        [Test]
        public void ZeroKillTarget_MeansTimeOnly()
        {
            MatchSettings timed = Settings;
            timed.KillTarget = 0;

            Assert.AreEqual(MatchPhase.Playing,
                MatchRules.Advance(MatchPhase.Playing, 10f, players: 2, topScore: 99, timed));
        }
    }
}
