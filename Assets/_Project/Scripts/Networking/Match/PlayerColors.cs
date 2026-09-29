using UnityEngine;

namespace MiniBrawl.Networking.Match
{
    /// <summary>
    /// One colour per seat. Chosen to stay distinguishable as small moving squares against the
    /// dark level, and to stay apart from the red damage flash and the yellow tracer.
    /// </summary>
    public static class PlayerColors
    {
        static readonly Color[] k_Colors =
        {
            new Color(0.35f, 0.85f, 1.00f),   // cyan
            new Color(0.55f, 0.95f, 0.45f),   // green
            new Color(1.00f, 0.75f, 0.30f),   // amber
            new Color(0.80f, 0.55f, 1.00f),   // violet
            new Color(1.00f, 0.55f, 0.75f),   // pink
            new Color(0.95f, 0.95f, 0.95f),   // white
        };

        public static int Count => k_Colors.Length;

        public static Color Get(int index) => k_Colors[Mathf.Abs(index) % k_Colors.Length];
    }
}
