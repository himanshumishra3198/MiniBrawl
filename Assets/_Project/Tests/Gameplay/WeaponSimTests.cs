using MiniBrawl.Gameplay.Player;
using MiniBrawl.Gameplay.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace MiniBrawl.Gameplay.Tests
{
    public class WeaponSimTests
    {
        const float k_Dt = 1f / 30f;

        [Test]
        public void HoldingFire_ShootsOnTheFirstTick()
        {
            var state = new WeaponState();
            Assert.IsTrue(WeaponSim.Step(ref state, WeaponConfig.Default, true, k_Dt));
        }

        [Test]
        public void FireRate_MatchesTheConfiguredInterval()
        {
            var config = WeaponConfig.Default;   // 0.2 s between shots
            var state = new WeaponState();
            int shots = 0;

            for (int i = 0; i < 30; i++)         // one second of held trigger
                if (WeaponSim.Step(ref state, config, true, k_Dt)) shots++;

            Assert.AreEqual(5, shots, "1 s at a 0.2 s interval is 5 shots");
        }

        [Test]
        public void ReleasingTrigger_StopsShots_ButStillCoolsDown()
        {
            var config = WeaponConfig.Default;
            var state = new WeaponState();

            Assert.IsTrue(WeaponSim.Step(ref state, config, true, k_Dt));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(WeaponSim.Step(ref state, config, false, k_Dt), "no trigger, no shot");

            Assert.IsTrue(WeaponSim.Step(ref state, config, true, k_Dt),
                "cooldown elapsed while the trigger was released, so the next pull fires at once");
        }

        [Test]
        public void AimAngle_SurvivesQuantisation()
        {
            foreach (var direction in new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down,
                                              new Vector2(1f, 1f).normalized, new Vector2(-3f, 1f).normalized })
            {
                var input = new PlayerInput { AimAngle = PlayerInput.EncodeAim(direction) };
                Assert.AreEqual(0f, Vector2.Angle(direction, input.AimDirection), 0.02f,
                    $"round-tripping {direction} should stay within a fiftieth of a degree");
            }
        }

        [Test]
        public void AimAngle_DefaultsToFacingRight()
        {
            Assert.AreEqual(Vector2.right, new PlayerInput().AimDirection);
        }
    }
}
