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

        /// <summary>The floor's top surface, from the transform set up below.</summary>
        const float k_FloorSurface = -1.5f;

        /// <summary>
        /// Derived from PlayerMotor.Size rather than written out. These expectations were
        /// hard-coded to a half-height of 0.45 and broke the moment the character was made taller,
        /// which told us nothing except that a number had been copied — the motor was right both
        /// times.
        /// </summary>
        static float HalfHeight => PlayerMotor.Size.y * 0.5f;

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
            // From origin y=0 the box's underside meets the surface after falling the gap
            // less its own half-height.
            Assert.AreEqual(-k_FloorSurface - HalfHeight, distance, 0.01f);
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
            // The centre rests one half-height above the surface, within the motor's skin width.
            Assert.AreEqual(k_FloorSurface + HalfHeight, state.Position.y, 0.05f);
            Assert.AreEqual(0f, state.Velocity.y, 0.01f);
        }
    }
}
