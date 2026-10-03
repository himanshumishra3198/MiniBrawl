using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniBrawl.Gameplay.Tests
{
    /// <summary>
    /// That no two touch controls sit on top of each other.
    ///
    /// Written after a weapon-swap button was placed over the move stick, where it swallowed the
    /// drag that was meant to steer. Overlaps are obvious on a phone and invisible in a diff: the
    /// HUD is built from code with anchored positions, so moving one element by a hundred pixels
    /// reads as a number changing and nothing else.
    ///
    /// Only siblings on the canvas are compared. A button's own icon is a child and is supposed to
    /// sit inside it.
    /// </summary>
    public sealed class HudLayoutTests
    {
        const string k_Scene = "Assets/_Project/Scenes/20_Network.unity";

        /// <summary>
        /// Things a thumb aims at, plus the status panel — it takes no input, but a readout
        /// underneath a thumb is a readout nobody can see.
        /// </summary>
        static readonly string[] k_Touchable =
            { "LeftStick", "RightStick", "SwapButton", "ReadyButton", "LeaveButton", "StatusPanel" };

        /// <summary>
        /// Leaves an empty scene behind.
        ///
        /// Opening a scene puts its colliders into the physics world, and they stay there for
        /// every test that runs afterwards. Physics2DCollisionTests builds its own floor and drops
        /// a player onto it; with the arena still loaded the player landed on the arena instead.
        /// A test that changes global state has to put it back.
        /// </summary>
        [TearDown]
        public void CloseScene() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        static Rect ScreenRect(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return new Rect(corners[0].x, corners[0].y,
                            corners[2].x - corners[0].x, corners[2].y - corners[0].y);
        }

        [Test]
        public void TouchControls_DoNotOverlap()
        {
            Scene scene = EditorSceneManager.OpenScene(k_Scene, OpenSceneMode.Single);

            var found = new Dictionary<string, Rect>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
                {
                    if (!k_Touchable.Contains(rt.gameObject.name)) continue;
                    found[rt.gameObject.name] = ScreenRect(rt);
                }
            }

            CollectionAssert.AreEquivalent(k_Touchable, found.Keys,
                "a control was renamed or removed; this list has to follow it");

            var names = found.Keys.ToList();
            for (int i = 0; i < names.Count; i++)
            {
                for (int j = i + 1; j < names.Count; j++)
                {
                    Rect a = found[names[i]];
                    Rect b = found[names[j]];

                    // The ready button only exists in the lobby and the sticks are live in a
                    // match, so those two never compete for the same thumb at the same time.
                    bool lobbyOnly = names[i] == "ReadyButton" || names[j] == "ReadyButton";
                    if (lobbyOnly) continue;

                    Assert.IsFalse(a.Overlaps(b),
                        $"{names[i]} {a} overlaps {names[j]} {b} — one will steal the other's touches");
                }
            }
        }
    }
}
