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
        const float k_AimDeadzone = 0.2f;

        Vector2 m_Aim = Vector2.right;   // aim persists when the stick is released

        public PlayerInput Read(uint tick)
        {
            float moveX = 0f;
            bool jetpack = false;
            bool fire = false;

            var pad = Gamepad.current;
            if (pad != null)
            {
                moveX += pad.leftStick.x.ReadValue();
                jetpack |= pad.buttonSouth.isPressed || pad.rightTrigger.isPressed;

                // §14 item 3: the aim stick also pulls the trigger.
                Vector2 aimStick = pad.rightStick.ReadValue();
                if (aimStick.sqrMagnitude > k_AimDeadzone * k_AimDeadzone)
                {
                    m_Aim = aimStick.normalized;
                    fire = true;
                }
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed && Camera.main != null)
            {
                Vector3 screen = mouse.position.ReadValue();
                screen.z = -Camera.main.transform.position.z;
                Vector2 toCursor = (Vector2)Camera.main.ScreenToWorldPoint(screen) - (Vector2)transform.position;
                if (toCursor.sqrMagnitude > 1e-4f)
                {
                    m_Aim = toCursor.normalized;
                    fire = true;
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveX -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveX += 1f;
                jetpack |= keyboard.spaceKey.isPressed || keyboard.wKey.isPressed;
            }

            moveX = Mathf.Clamp(moveX, -1f, 1f);

            byte buttons = 0;
            if (jetpack) buttons |= PlayerInput.BtnJetpack;
            if (fire) buttons |= PlayerInput.BtnFire;

            return new PlayerInput
            {
                Tick = tick,
                MoveX = (sbyte)Mathf.Clamp(Mathf.RoundToInt(moveX * 127f), -127, 127),
                AimAngle = PlayerInput.EncodeAim(m_Aim),
                Buttons = buttons,
            };
        }
    }
}
