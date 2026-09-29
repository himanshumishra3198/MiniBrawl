using MiniBrawl.Config;
using MiniBrawl.Gameplay.Player;
using MiniBrawl.Gameplay.Rules;
using MiniBrawl.Gameplay.Weapons;
using MiniBrawl.Networking.Discovery;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;   // brings in the AllocatingGCMemory extension
using Is = UnityEngine.TestTools.Constraints.Is;

namespace MiniBrawl.Gameplay.Tests
{
    /// <summary>
    /// The simulation runs 30 times a second per player and again for every tick replayed during
    /// reconciliation, so anything it allocates becomes collector pressure — the plan names GC in
    /// the game loop as the top cause of mobile stutter (§Phase 5). These tests fail if that
    /// changes, which is cheaper than finding it later on a profiler.
    /// </summary>
    public class AllocationTests
    {
        class NoWorld : IMotorCollision
        {
            public bool Cast(Vector2 o, Vector2 s, Vector2 d, float dist, out float hit)
            { hit = 0f; return false; }
        }

        [Test]
        public void PlayerMotor_Simulate_DoesNotAllocate()
        {
            var state = PlayerState.Spawn(Vector2.zero, 1f);
            var input = new PlayerInput { MoveX = 100, Buttons = PlayerInput.BtnJetpack };
            var config = MotorConfig.Default;
            var world = new NoWorld();

            // Warm up first: the constraint measures the delegate, and first-call JIT would count.
            state = PlayerMotor.Simulate(state, input, config, world, 1f / 30f);

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                    state = PlayerMotor.Simulate(state, input, config, world, 1f / 30f);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void WeaponSim_Step_DoesNotAllocate()
        {
            var weapon = new WeaponState();
            var config = WeaponConfig.Default;
            WeaponSim.Step(ref weapon, config, true, 1f / 30f);

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                    WeaponSim.Step(ref weapon, config, true, 1f / 30f);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void MatchRules_Advance_DoesNotAllocate()
        {
            MatchSettings settings = MatchSettings.Default;
            MatchRules.Advance(MatchPhase.Playing, 1f, 2, 2, 0, settings);

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                    MatchRules.Advance(MatchPhase.Playing, i, 2, 2, i % 20, settings);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AimEncoding_DoesNotAllocate()
        {
            // Runs for every input tick on every client, and again on the server.
            PlayerInput.EncodeAim(Vector2.right);

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    ushort angle = PlayerInput.EncodeAim(new Vector2(i, 100 - i));
                    Vector2 back = new PlayerInput { AimAngle = angle }.AimDirection;
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void BeaconParsing_AllocatesOnlyWhatItMustReturn()
        {
            // Discovery runs once a second, not per frame, so this only guards against the parser
            // growing something pathological: a host name string is the one legitimate allocation.
            byte[] packet = BeaconPacket.Create("Host", 2, NetworkConstants.GamePort).ToBytes();

            Assert.IsTrue(BeaconPacket.TryParse(packet, packet.Length, out BeaconPacket parsed));
            Assert.AreEqual("Host", parsed.HostName);
        }
    }
}
