using MiniBrawl.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

// UnityEngine.InputSystem also defines a PlayerInput; ours is the per-tick intent struct.
using PlayerInput = MiniBrawl.Gameplay.Player.PlayerInput;

namespace MiniBrawl.UI.Widgets
{
    /// <summary>
    /// Turns the on-screen stick and jetpack button into one tick of PlayerInput. The on-screen
    /// controls feed a virtual Gamepad, so a real controller works too; the keyboard fallback is
    /// there so the game is playable in the Editor without a touchscreen.
    /// </summary>
    public sealed class PrototypeInputSource : MonoBehaviour, IPlayerInputSource
    {
        public PlayerInput Read(uint tick)
        {
            float moveX = 0f;
            bool jetpack = false;

            var pad = Gamepad.current;
            if (pad != null)
            {
                moveX += pad.leftStick.x.ReadValue();
                jetpack |= pad.buttonSouth.isPressed || pad.rightTrigger.isPressed;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
                jetpack |= keyboard.spaceKey.isPressed || keyboard.wKey.isPressed;
            }

            moveX = Mathf.Clamp(moveX, -1f, 1f);

            return new PlayerInput
            {
                Tick = tick,
                MoveX = (sbyte)Mathf.Clamp(Mathf.RoundToInt(moveX * 127f), -127, 127),
                Buttons = (byte)(jetpack ? PlayerInput.BtnJetpack : 0),
            };
        }
    }
}
