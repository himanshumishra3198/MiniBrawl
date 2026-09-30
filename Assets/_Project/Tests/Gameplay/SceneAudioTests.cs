using System.Linq;
using MiniBrawl.Gameplay.Audio;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MiniBrawl.Gameplay.Tests
{
    /// <summary>
    /// That the generated scenes can actually be heard.
    ///
    /// Written after shipping a build with a complete sound system and no AudioListener in either
    /// scene. Every part was correct on its own — clips imported, banks populated, volumes mixed,
    /// calls wired to the right events — and the game was silent, because Unity's GameObject menu
    /// adds a listener beside a camera and constructing one from script does not.
    ///
    /// Nothing logs a warning for this, which is the point: these assert the conditions under
    /// which sound is audible at all, rather than that the audio code compiles and runs.
    /// </summary>
    public sealed class SceneAudioTests
    {
        const string k_Prototype = "Assets/_Project/Scenes/10_Prototype.unity";
        const string k_Network = "Assets/_Project/Scenes/20_Network.unity";

        static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                 .SelectMany(root => root.GetComponentsInChildren<T>(includeInactive: true))
                 .ToArray();

        [TestCase(k_Prototype)]
        [TestCase(k_Network)]
        public void Scene_HasExactlyOneAudioListener(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            AudioListener[] listeners = FindAll<AudioListener>(scene);

            // Two is as broken as none: Unity disables the extras and warns every frame.
            Assert.AreEqual(1, listeners.Length,
                $"{path} has {listeners.Length} AudioListeners. Zero means the game is silent.");
        }

        [TestCase(k_Prototype)]
        [TestCase(k_Network)]
        public void Scene_HasSfxWithEveryBankFilled(string path)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            Sfx[] players = FindAll<Sfx>(scene);
            Assert.AreEqual(1, players.Length, $"{path} should have exactly one Sfx.");

            Sfx sfx = players[0];

            /* Every sound the code can ask for has to exist. A missing bank is silent rather than
             * loud: Play returns early on an empty clip list, so the only symptom is that one
             * event in the game stops making a noise. */
            foreach (SfxId id in System.Enum.GetValues(typeof(SfxId)))
            {
                Sfx.Bank bank = sfx.Banks.FirstOrDefault(b => b.Id == id);

                Assert.IsNotNull(bank.Clips, $"{path}: {id} has no bank.");
                Assert.IsNotEmpty(bank.Clips, $"{path}: {id} has an empty bank.");
                CollectionAssert.DoesNotContain(bank.Clips, null, $"{path}: {id} has a null clip.");
                Assert.Greater(bank.Volume, 0f, $"{path}: {id} is mixed to silence.");
            }
        }
    }
}
