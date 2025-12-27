namespace DaCards;

public interface ICardGame
{
    /// <summary>
    /// Saves the current state of the game to a string.
    /// </summary>
    /// <returns>A string representing the saved game state.</returns>
    string SaveGame();

    /// <summary>
    /// Validates the current state of the game.
    /// </summary>
    /// <returns>True if the game state is valid; otherwise, false.</returns>
    bool ValidateGame();

    /// <summary>
    /// Loads the game state from a string.
    /// </summary>
    /// <param name="saveData">A string representing the saved game state.</param>
    /// <returns>True if the game was successfully loaded; otherwise, false.</returns>
    static abstract ICardGame? LoadGame(string saveData);
}