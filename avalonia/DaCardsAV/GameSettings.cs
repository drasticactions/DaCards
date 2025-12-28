namespace DaCardsAV;

/// <summary>
/// Global game settings shared across all game boards.
/// </summary>
public static class GameSettings
{
    /// <summary>
    /// When enabled, cards are automatically moved to foundations when safe.
    /// Default is true.
    /// </summary>
    public static bool AutoMoveEnabled { get; set; } = true;
}
