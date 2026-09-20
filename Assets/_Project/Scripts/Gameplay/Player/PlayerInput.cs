using UnityEngine;

namespace MiniBrawl.Gameplay.Player
{
    /// <summary>One tick of player intent. 8 bytes on the wire (§2.4).</summary>
    public struct PlayerInput
    {
        public uint   Tick;
        public sbyte  MoveX;     // -127..127, quantised stick
        public ushort AimAngle;  // 0..65535 maps to 0..360 degrees
        public byte   Buttons;

        public const byte BtnJetpack = 1 << 0;
        public const byte BtnFire    = 1 << 1;
        public const byte BtnGrenade = 1 << 2;

        public bool Jetpack => (Buttons & BtnJetpack) != 0;
        public bool Fire    => (Buttons & BtnFire)    != 0;
        public float MoveAxis => Mathf.Clamp(MoveX / 127f, -1f, 1f);

        /// <summary>Aim direction, unpacked from the quantised angle.</summary>
        public Vector2 AimDirection
        {
            get
            {
                float radians = AimAngle / 65536f * 2f * Mathf.PI;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        /// <summary>Quantises a direction into the 16-bit angle that goes on the wire.</summary>
        public static ushort EncodeAim(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return 0;
            float radians = Mathf.Atan2(direction.y, direction.x);
            if (radians < 0f) radians += 2f * Mathf.PI;
            return (ushort)Mathf.RoundToInt(radians / (2f * Mathf.PI) * 65536f);
        }
    }
}
