using NUnit.Framework;
using UnityEngine;
using MiniBrawl.Gameplay.Player;

namespace MiniBrawl.Gameplay.Tests
{
    /// <summary>Proves the sim layer runs with no Unity runtime and no networking.</summary>
    public class PlayerMotorTests
    {
        class NoWorld : IMotorCollision
        {
            public bool Cast(Vector2 o, Vector2 s, Vector2 d, float dist, out float hit)
            { hit = 0f; return false; }
        }
        class Floor : IMotorCollision   // solid plane 0.3 units below spawn
        {
            public bool Cast(Vector2 o, Vector2 s, Vector2 d, float dist, out float hit)
            {
                if (d.y < 0f && dist >= 0.3f) { hit = 0.3f; return true; }
                hit = 0f; return false;
            }
        }

        const float Dt = 1f / 30f;

        [Test]
        public void Gravity_PullsPlayerDown()
        {
            var s = PlayerState.Spawn(Vector2.zero, 1f);
            s = PlayerMotor.Simulate(s, default, MotorConfig.Default, new NoWorld(), Dt);
            Assert.Less(s.Velocity.y, 0f, "gravity should produce downward velocity");
        }

        [Test]
        public void Jetpack_BurnsFuel_AndLiftsPlayer()
        {
            var s = PlayerState.Spawn(Vector2.zero, 1f);
            var input = new PlayerInput { Buttons = PlayerInput.BtnJetpack };
            for (int i = 0; i < 10; i++)
                s = PlayerMotor.Simulate(s, input, MotorConfig.Default, new NoWorld(), Dt);

            Assert.Less(s.Fuel, 1f, "jetpack should consume fuel");
            Assert.Greater(s.Velocity.y, 0f, "sustained thrust should overcome gravity");
        }

        [Test]
        public void EmptyFuel_ProducesNoThrust()
        {
            var s = PlayerState.Spawn(Vector2.zero, 0f);
            var input = new PlayerInput { Buttons = PlayerInput.BtnJetpack };
            s = PlayerMotor.Simulate(s, input, MotorConfig.Default, new NoWorld(), Dt);
            Assert.AreEqual(0f, s.Fuel, 1e-4f);
            Assert.Less(s.Velocity.y, 0f, "no fuel means gravity wins");
        }

        [Test]
        public void Simulation_IsDeterministic()
        {
            var a = PlayerState.Spawn(Vector2.zero, 1f);
            var b = PlayerState.Spawn(Vector2.zero, 1f);
            var input = new PlayerInput { MoveX = 64, Buttons = PlayerInput.BtnJetpack };
            for (int i = 0; i < 60; i++)
            {
                a = PlayerMotor.Simulate(a, input, MotorConfig.Default, new NoWorld(), Dt);
                b = PlayerMotor.Simulate(b, input, MotorConfig.Default, new NoWorld(), Dt);
            }
            Assert.AreEqual(a.Position.x, b.Position.x, 0f, "identical inputs must give identical output");
            Assert.AreEqual(a.Position.y, b.Position.y, 0f);
        }

        [Test]
        public void Landing_SetsGrounded_AndZeroesVerticalVelocity()
        {
            var s = PlayerState.Spawn(Vector2.zero, 1f);
            s.Velocity = new Vector2(0f, -40f);
            s = PlayerMotor.Simulate(s, default, MotorConfig.Default, new Floor(), Dt);
            Assert.IsTrue(s.Grounded);
            Assert.AreEqual(0f, s.Velocity.y, 1e-4f);
        }
    }
}
