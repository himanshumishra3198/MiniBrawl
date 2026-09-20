using MiniBrawl.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace MiniBrawl.Gameplay.Tests
{
    /// <summary>
    /// Exercises the real Physics2D probe against real colliders — the fake world in
    /// PlayerMotorTests can't catch a wrong layer mask or a bad cast origin.
    /// </summary>
    public class Physics2DCollisionTests
    {
        const int k_LevelLayer = 8;      // "Level", created by PrototypeSceneBuilder
        const int k_OtherLayer = 9;

        GameObject m_Floor;

        [SetUp]
        public void SetUp()
        {
            m_Floor = new GameObject("TestFloor") { layer = k_LevelLayer };
            m_Floor.transform.position = new Vector3(0f, -2f, 0f);   // top surface at y = -1.5
            m_Floor.transform.localScale = new Vector3(10f, 1f, 1f);
            m_Floor.AddComponent<BoxCollider2D>().size = Vector2.one;
            Physics2D.SyncTransforms();                              // auto-sync is off in this project
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(m_Floor);

        [Test]
        public void Cast_Down_ReportsDistanceToFloorSurface()
        {
            var probe = new Physics2DCollision(1 << k_LevelLayer);

            bool hit = probe.Cast(Vector2.zero, PlayerMotor.Size, Vector2.down, 5f, out float distance);

            Assert.IsTrue(hit, "a box cast straight down should find the floor");
            // Box half-height 0.45 from origin y=0, floor surface at y=-1.5.
            Assert.AreEqual(1.05f, distance, 0.01f);
        }

        [Test]
        public void Cast_IgnoresCollidersOutsideTheLayerMask()
        {
            var probe = new Physics2DCollision(1 << k_OtherLayer);

            bool hit = probe.Cast(Vector2.zero, PlayerMotor.Size, Vector2.down, 5f, out _);

            Assert.IsFalse(hit, "the mask must exclude everything that isn't level geometry");
        }

        [Test]
        public void Cast_ShorterThanTheGap_Misses()
        {
            var probe = new Physics2DCollision(1 << k_LevelLayer);

            bool hit = probe.Cast(Vector2.zero, PlayerMotor.Size, Vector2.down, 0.5f, out _);

            Assert.IsFalse(hit, "a cast that stops short of the floor must not report a hit");
        }

        [Test]
        public void Motor_FallsAndSettlesOnTheFloor()
        {
            var probe = new Physics2DCollision(1 << k_LevelLayer);
            var state = PlayerState.Spawn(new Vector2(0f, 3f), 1f);

            for (int i = 0; i < 120; i++)   // 4 seconds at 30 Hz
                state = PlayerMotor.Simulate(state, default, MotorConfig.Default, probe, 1f / 30f);

            Assert.IsTrue(state.Grounded, "the player should be resting on the floor");
            // Floor surface -1.5 plus the box's 0.45 half-height, within the motor's skin width.
            Assert.AreEqual(-1.05f, state.Position.y, 0.05f);
            Assert.AreEqual(0f, state.Velocity.y, 0.01f);
        }
    }
}
