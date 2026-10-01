using System.Collections.Generic;
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
            WeaponState state = WeaponState.Starting;
            Assert.IsTrue(WeaponSim.Step(ref state, true, k_Dt));
        }

        [Test]
        public void FireRate_MatchesTheConfiguredInterval()
        {
            WeaponState state = WeaponState.Starting;   // the rifle, 0.2 s between shots
            int shots = 0;

            for (int i = 0; i < 30; i++)                // one second of held trigger
                if (WeaponSim.Step(ref state, true, k_Dt)) shots++;

            Assert.AreEqual(5, shots, "1 s at a 0.2 s interval is 5 shots");
        }

        [Test]
        public void ReleasingTrigger_StopsShots_ButStillCoolsDown()
        {
            WeaponState state = WeaponState.Starting;

            Assert.IsTrue(WeaponSim.Step(ref state, true, k_Dt));
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(WeaponSim.Step(ref state, false, k_Dt), "no trigger, no shot");

            Assert.IsTrue(WeaponSim.Step(ref state, true, k_Dt),
                "cooldown elapsed while the trigger was released, so the next pull fires at once");
        }

        [Test]
        public void Spread_IsTheSameEveryTimeTheSameTickIsSimulated()
        {
            /* The client predicts its own shotgun blast and the server decides what it hit. If the
             * two produced different cones the prediction would be wrong every time anyone fired
             * one, so the scatter is hashed from the tick rather than drawn from a random source
             * that a replay cannot rewind. This is the property that makes that safe. */
            WeaponConfig shotgun = WeaponConfig.For(WeaponKind.Shotgun);
            var aim = new Vector2(0.8f, 0.6f).normalized;

            for (uint tick = 900; tick < 905; tick++)
                for (int pellet = 0; pellet < shotgun.Pellets; pellet++)
                    Assert.AreEqual(WeaponSim.Pellet(aim, pellet, tick, shotgun),
                                    WeaponSim.Pellet(aim, pellet, tick, shotgun),
                                    $"tick {tick} pellet {pellet} must resolve the same way twice");
        }

        [Test]
        public void Spread_SeparatesPelletsWithinTheCone()
        {
            WeaponConfig shotgun = WeaponConfig.For(WeaponKind.Shotgun);
            Vector2 aim = Vector2.right;

            var seen = new List<Vector2>();
            for (int pellet = 0; pellet < shotgun.Pellets; pellet++)
            {
                Vector2 direction = WeaponSim.Pellet(aim, pellet, 1234u, shotgun);

                // Inside the cone it was given, and not simply the aim direction repeated.
                Assert.LessOrEqual(Vector2.Angle(aim, direction), shotgun.Spread * 0.5f + 0.01f);
                CollectionAssert.DoesNotContain(seen, direction, "pellets must not stack");
                seen.Add(direction);
            }
        }

        [Test]
        public void SingleShotWeapons_DoNotScatter()
        {
            WeaponConfig rifle = WeaponConfig.For(WeaponKind.Rifle);
            var aim = new Vector2(-0.3f, 0.95f).normalized;

            Assert.AreEqual(aim, WeaponSim.Pellet(aim, 0, 77u, rifle));
        }

        [Test]
        public void RunningOutOfAmmo_FallsBackToTheRifle()
        {
            WeaponState state = WeaponState.Starting;
            WeaponSim.Equip(ref state, WeaponKind.Pistol);

            int ammo = WeaponConfig.For(WeaponKind.Pistol).Ammo;
            Assert.AreEqual(ammo, state.Ammo, "equipping should fill the magazine");

            int fired = 0;
            for (int i = 0; i < 2000 && state.Kind == WeaponKind.Pistol; i++)
                if (WeaponSim.Step(ref state, true, k_Dt)) fired++;

            Assert.AreEqual(ammo, fired, "the magazine should hold exactly its stated rounds");
            Assert.AreEqual(WeaponKind.Rifle, state.Kind, "an empty weapon falls back to the rifle");
            Assert.AreEqual(-1, state.Ammo, "and the rifle never runs out");
        }

        [Test]
        public void TheRifle_NeverRunsOut()
        {
            WeaponState state = WeaponState.Starting;

            for (int i = 0; i < 600; i++) WeaponSim.Step(ref state, true, k_Dt);

            Assert.AreEqual(WeaponKind.Rifle, state.Kind);
            Assert.AreEqual(-1, state.Ammo);
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
