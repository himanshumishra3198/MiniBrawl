using UnityEngine;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>
    /// Scripted input for headless testing: no device, no human. The pattern is a pure function of
    /// the tick, so two processes simulating the same tick must produce the same movement — which
    /// is exactly what makes it useful for checking that prediction agrees with the server.
    /// </summary>
    public sealed class AutopilotInputSource : MonoBehaviour, IPlayerInputSource
    {
        public PlayerInput Read(uint tick)
        {
            uint phase = tick % 120;

            // Walk right for two seconds, then left, thrusting in bursts throughout.
            sbyte moveX = phase < 60 ? (sbyte)100 : (sbyte)-100;
            bool jetpack = phase % 40 < 16;

            return new PlayerInput
            {
                Tick = tick,
                MoveX = moveX,
                AimAngle = PlayerInput.EncodeAim(moveX > 0 ? Vector2.right : Vector2.left),
                Buttons = jetpack ? PlayerInput.BtnJetpack : (byte)0,
            };
        }
    }
}
