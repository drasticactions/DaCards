namespace DaCards.Tests;

[TestClass]
public sealed class SolitaireGameTests
{
    [TestMethod]
    public void SolitaireGame_Constructor_InitializesPiles()
    {
        var game = new SolitaireGame();

        Assert.AreEqual(4, game.Foundations.Count);
        Assert.AreEqual(7, game.Tableaus.Count);
        Assert.IsNotNull(game.Stock);
        Assert.IsNotNull(game.Waste);
    }

    [TestMethod]
    public void SolitaireGame_NewGame_DealsCardsCorrectly()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        // Tableau 0 has 1 card, Tableau 1 has 2, etc.
        for (int i = 0; i < 7; i++)
        {
            Assert.AreEqual(i + 1, game.Tableaus[i].Count, $"Tableau {i} should have {i + 1} cards");
        }

        // Total in tableaus: 1+2+3+4+5+6+7 = 28 cards
        // Remaining in stock: 52 - 28 = 24 cards
        Assert.AreEqual(24, game.Stock.Count);
        Assert.AreEqual(0, game.Waste.Count);
    }

    [TestMethod]
    public void SolitaireGame_NewGame_TopCardsFaceUp()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        foreach (var tableau in game.Tableaus)
        {
            Assert.IsTrue(tableau.TopCard!.IsFaceUp);
            // All other cards should be face down
            for (int i = 0; i < tableau.Count - 1; i++)
            {
                Assert.IsFalse(tableau.Cards[i].IsFaceUp);
            }
        }
    }

    [TestMethod]
    public void SolitaireGame_NewGame_ValidatesSuccessfully()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        Assert.IsTrue(game.ValidateGame());
    }

    [TestMethod]
    public void SolitaireGame_DrawFromStock_MovesCardToWaste()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        var stockBefore = game.Stock.Count;

        Assert.IsTrue(game.DrawFromStock());
        Assert.AreEqual(stockBefore - 1, game.Stock.Count);
        Assert.AreEqual(1, game.Waste.Count);
        Assert.IsTrue(game.Waste.TopCard!.IsFaceUp);
    }

    [TestMethod]
    public void SolitaireGame_DrawFromStock_DrawsMultipleCardsWhenSet()
    {
        var game = new SolitaireGame();
        game.DrawCount = 3;
        game.NewGame(42);

        var stockBefore = game.Stock.Count;

        Assert.IsTrue(game.DrawFromStock());
        Assert.AreEqual(stockBefore - 3, game.Stock.Count);
        Assert.AreEqual(3, game.Waste.Count);
    }

    [TestMethod]
    public void SolitaireGame_DrawFromStock_FailsWhenEmpty()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        // Empty the stock
        while (game.Stock.Count > 0)
        {
            game.DrawFromStock();
        }

        Assert.IsFalse(game.DrawFromStock());
    }

    [TestMethod]
    public void SolitaireGame_ResetStock_MovesWasteBackToStock()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        // Draw all cards to waste
        while (game.Stock.Count > 0)
        {
            game.DrawFromStock();
        }

        var wasteCount = game.Waste.Count;
        Assert.IsTrue(game.ResetStock());
        Assert.AreEqual(wasteCount, game.Stock.Count);
        Assert.AreEqual(0, game.Waste.Count);
    }

    [TestMethod]
    public void SolitaireGame_ResetStock_FailsWhenStockNotEmpty()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        Assert.IsFalse(game.ResetStock());
    }

    [TestMethod]
    public void SolitaireGame_MoveTableauToTableau_MovesValidCards()
    {
        var game = new SolitaireGame();

        // Set up a specific scenario: King on tableau 0, Queen on tableau 1
        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        var redKing = new Card(Suit.Hearts, Rank.King, true);
        var redQueen = new Card(Suit.Diamonds, Rank.Queen, true);

        game.Tableaus[0].AddCard(redKing);
        game.Tableaus[1].AddCard(redQueen);

        // Cannot move red queen onto red king (same color)
        Assert.IsFalse(game.MoveTableauToTableau(1, 0, 0));

        // Set up valid scenario: black queen can go on red king
        game.Tableaus[1].Clear();
        var blackQueen = new Card(Suit.Spades, Rank.Queen, true);
        game.Tableaus[1].AddCard(blackQueen);

        // Move black queen onto red king
        Assert.IsTrue(game.MoveTableauToTableau(1, 0, 0));
        Assert.AreEqual(2, game.Tableaus[0].Count);
        Assert.AreEqual(0, game.Tableaus[1].Count);
    }

    [TestMethod]
    public void SolitaireGame_MoveTableauToTableau_MovesMultipleCards()
    {
        var game = new SolitaireGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        // Create a sequence
        game.Tableaus[0].AddCard(new Card(Suit.Hearts, Rank.King, true));
        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Queen, true));
        game.Tableaus[0].AddCard(new Card(Suit.Hearts, Rank.Jack, true));

        // Move King + sequence to empty tableau
        Assert.IsTrue(game.MoveTableauToTableau(0, 0, 1));
        Assert.AreEqual(0, game.Tableaus[0].Count);
        Assert.AreEqual(3, game.Tableaus[1].Count);
    }

    [TestMethod]
    public void SolitaireGame_MoveTableauToFoundation_MovesAce()
    {
        var game = new SolitaireGame();
        game.Tableaus[0].Clear();

        var ace = new Card(Suit.Hearts, Rank.Ace, true);
        game.Tableaus[0].AddCard(ace);

        Assert.IsTrue(game.MoveTableauToFoundation(0, 0));
        Assert.AreEqual(0, game.Tableaus[0].Count);
        Assert.AreEqual(1, game.Foundations[0].Count);
    }

    [TestMethod]
    public void SolitaireGame_MoveTableauToFoundation_FlipsNewTopCard()
    {
        var game = new SolitaireGame();
        game.Tableaus[0].Clear();

        var bottomCard = new Card(Suit.Spades, Rank.King, false);
        var ace = new Card(Suit.Hearts, Rank.Ace, true);
        game.Tableaus[0].AddCard(bottomCard);
        game.Tableaus[0].AddCard(ace);

        Assert.IsFalse(game.Tableaus[0].Cards[0].IsFaceUp);

        game.MoveTableauToFoundation(0, 0);

        Assert.IsTrue(game.Tableaus[0].TopCard!.IsFaceUp);
    }

    [TestMethod]
    public void SolitaireGame_MoveWasteToTableau_MovesCard()
    {
        var game = new SolitaireGame();
        game.Tableaus[0].Clear();

        var king = new Card(Suit.Hearts, Rank.King, true);
        game.Waste.AddCard(king);

        Assert.IsTrue(game.MoveWasteToTableau(0));
        Assert.AreEqual(0, game.Waste.Count);
        Assert.AreEqual(1, game.Tableaus[0].Count);
    }

    [TestMethod]
    public void SolitaireGame_MoveWasteToFoundation_MovesAce()
    {
        var game = new SolitaireGame();

        var ace = new Card(Suit.Hearts, Rank.Ace, true);
        game.Waste.AddCard(ace);

        Assert.IsTrue(game.MoveWasteToFoundation(0));
        Assert.AreEqual(0, game.Waste.Count);
        Assert.AreEqual(1, game.Foundations[0].Count);
    }

    [TestMethod]
    public void SolitaireGame_GetValidMoves_ReturnsAllValidMoves()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        var moves = game.GetValidMoves();

        // At minimum, we should be able to draw from stock
        Assert.IsTrue(moves.Count > 0);
        Assert.IsTrue(moves.Any(m => m.Type == MoveType.DrawFromStock));
    }

    [TestMethod]
    public void SolitaireGame_IsGameWon_ReturnsTrueWhenAllFoundationsFull()
    {
        var game = new SolitaireGame();

        // Fill all foundations
        for (int f = 0; f < 4; f++)
        {
            var suit = (Suit)f;
            for (int r = 1; r <= 13; r++)
            {
                game.Foundations[f].AddCard(new Card(suit, (Rank)r));
            }
        }

        Assert.IsTrue(game.IsGameWon);
    }

    [TestMethod]
    public void SolitaireGame_IsGameWon_ReturnsFalseWhenIncomplete()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        Assert.IsFalse(game.IsGameWon);
    }

    [TestMethod]
    public void SolitaireGame_SaveAndLoad_PreservesState()
    {
        var game = new SolitaireGame();
        game.NewGame(42);
        game.DrawFromStock();
        game.DrawFromStock();

        var saveData = game.SaveGame();
        var loadedGame = SolitaireGame.LoadGame(saveData) as SolitaireGame;

        Assert.IsNotNull(loadedGame);
        Assert.AreEqual(game.Stock.Count, loadedGame.Stock.Count);
        Assert.AreEqual(game.Waste.Count, loadedGame.Waste.Count);
        Assert.AreEqual(game.MoveHistory.Count, loadedGame.MoveHistory.Count);
    }

    [TestMethod]
    public void SolitaireGame_ValidateGame_DetectsDuplicateCards()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        // Add a duplicate card
        game.Waste.AddCard(new Card(Suit.Hearts, Rank.Ace));

        Assert.IsFalse(game.ValidateGame());
    }

    [TestMethod]
    public void SolitaireGame_AutoMoveToFoundation_FindsValidFoundation()
    {
        var game = new SolitaireGame();
        game.Tableaus[0].Clear();
        game.Tableaus[0].AddCard(new Card(Suit.Hearts, Rank.Ace, true));

        Assert.IsTrue(game.AutoMoveToFoundation(0));
        Assert.AreEqual(1, game.Foundations.Sum(f => f.Count));
    }

    [TestMethod]
    public void SolitaireGame_AutoMoveWasteToFoundation_FindsValidFoundation()
    {
        var game = new SolitaireGame();
        game.Waste.AddCard(new Card(Suit.Spades, Rank.Ace, true));

        Assert.IsTrue(game.AutoMoveWasteToFoundation());
        Assert.AreEqual(1, game.Foundations.Sum(f => f.Count));
    }

    [TestMethod]
    public void SolitaireGame_MoveHistory_TracksAllMoves()
    {
        var game = new SolitaireGame();
        game.NewGame(42);

        game.DrawFromStock();
        game.DrawFromStock();

        Assert.AreEqual(2, game.MoveHistory.Count);
        Assert.IsTrue(game.MoveHistory.All(m => m.Type == MoveType.DrawFromStock));
    }

    [TestMethod]
    public void SolitaireGame_SameSeed_ProducesSameGame()
    {
        var game1 = new SolitaireGame();
        var game2 = new SolitaireGame();

        game1.NewGame(123);
        game2.NewGame(123);

        // Compare tableau cards
        for (int t = 0; t < 7; t++)
        {
            Assert.AreEqual(game1.Tableaus[t].Count, game2.Tableaus[t].Count);
            for (int c = 0; c < game1.Tableaus[t].Count; c++)
            {
                Assert.AreEqual(game1.Tableaus[t].Cards[c].Suit, game2.Tableaus[t].Cards[c].Suit);
                Assert.AreEqual(game1.Tableaus[t].Cards[c].Rank, game2.Tableaus[t].Cards[c].Rank);
            }
        }
    }
}
