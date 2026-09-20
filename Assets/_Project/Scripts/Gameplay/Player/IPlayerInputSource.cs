namespace MiniBrawl.Gameplay.Player
{
    /// <summary>
    /// Supplies one tick of intent. Keeps the input device (touch sticks now, replicated network
    /// input in Phase 2) out of the simulation layer.
    /// </summary>
    public interface IPlayerInputSource
    {
        PlayerInput Read(uint tick);
    }
}
