using UnityEngine;

namespace MiniBrawl.Core
{
    /// <summary>Android caps at 30 fps unless told otherwise. Runs before the first scene loads.</summary>
    static class FrameRateBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply() => Application.targetFrameRate = 60;
    }
}
