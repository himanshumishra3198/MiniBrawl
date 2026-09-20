namespace MiniBrawl.Gameplay.Player
{
    /// <summary>One tick of player intent. ~10 bytes on the wire (§2.4).</summary>
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
        public float MoveAxis => MoveX / 127f;
    }
}
