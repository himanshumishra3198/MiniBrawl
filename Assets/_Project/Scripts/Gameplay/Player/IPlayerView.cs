namespace MiniBrawl.Gameplay.Player
{
    /// <summary>
    /// What a renderer needs in order to draw a player, and nothing else.
    ///
    /// PlayerDriver (offline) and NetworkPlayerMotor (replicated) both step the same
    /// PlayerMotor and both end up holding the same two values. Reading them through
    /// this interface is what lets the visuals live in Gameplay: the sprite code never
    /// learns whether the state it is drawing was simulated locally, predicted, or
    /// reconciled from a server, because from here those are the same thing.
    /// </summary>
    public interface IPlayerView
    {
        PlayerState State { get; }
        PlayerInput LastInput { get; }
    }
}
