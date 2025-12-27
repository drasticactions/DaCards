namespace DaCards.Tests;

[TestClass]
public sealed class SpiderSolitaireGameTests
{
    [TestMethod]
    public void SpiderSolitaireGame_Constructor_InitializesTenTableaus()
    {
        var game = new SpiderSolitaireGame();

        Assert.AreEqual(10, game.Tableaus.Count);
    }

    [TestMethod]
    public void SpiderSolitaireGame_NewGame_DealsCardsCorrectly()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        // First 4 tableaus get 6 cards each
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(6, game.Tableaus[i].Count, $"Tableau {i} should have 6 cards");
        }

        // Last 6 tableaus get 5 cards each
        for (int i = 4; i < 10; i++)
        {
            Assert.AreEqual(5, game.Tableaus[i].Count, $"Tableau {i} should have 5 cards");
        }

        // Total dealt: 4*6 + 6*5 = 24 + 30 = 54 cards
        // Remaining in stock: 104 - 54 = 50 cards
        Assert.AreEqual(50, game.Stock.Count);
    }

    [TestMethod]
    public void SpiderSolitaireGame_NewGame_TopCardsFaceUp()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        foreach (var tableau in game.Tableaus)
        {
            Assert.IsTrue(tableau.TopCard!.IsFaceUp, "Top card should be face up");

            // All other cards should be face down
            for (int i = 0; i < tableau.Count - 1; i++)
            {
                Assert.IsFalse(tableau.Cards[i].IsFaceUp, $"Card at index {i} should be face down");
            }
        }
    }

    [TestMethod]
    public void SpiderSolitaireGame_NewGame_ValidatesSuccessfully()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        Assert.IsTrue(game.ValidateGame());
    }

    [TestMethod]
    [DataRow(SpiderDifficulty.OneSuit)]
    [DataRow(SpiderDifficulty.TwoSuits)]
    [DataRow(SpiderDifficulty.FourSuits)]
    public void SpiderSolitaireGame_NewGame_AllDifficultiesValidate(SpiderDifficulty difficulty)
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(difficulty, 42);

        Assert.IsTrue(game.ValidateGame());
        Assert.AreEqual(difficulty, game.Difficulty);
    }

    [TestMethod]
    public void SpiderSolitaireGame_OneSuit_UsesOnlySpades()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.OneSuit, 42);

        foreach (var tableau in game.Tableaus)
        {
            foreach (var card in tableau.Cards)
            {
                Assert.AreEqual(Suit.Spades, card.Suit);
            }
        }

        foreach (var card in game.Stock)
        {
            Assert.AreEqual(Suit.Spades, card.Suit);
        }
    }

    [TestMethod]
    public void SpiderSolitaireGame_TwoSuits_UsesSpadesAndHearts()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.TwoSuits, 42);

        var allCards = game.Tableaus.SelectMany(t => t.Cards).Concat(game.Stock);

        foreach (var card in allCards)
        {
            Assert.IsTrue(card.Suit == Suit.Spades || card.Suit == Suit.Hearts);
        }
    }

    [TestMethod]
    public void SpiderSolitaireGame_DealFromStock_DealsToAllTableaus()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        var stockBefore = game.Stock.Count;
        var tableauCountsBefore = game.Tableaus.Select(t => t.Count).ToArray();

        Assert.IsTrue(game.DealFromStock());

        Assert.AreEqual(stockBefore - 10, game.Stock.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.AreEqual(tableauCountsBefore[i] + 1, game.Tableaus[i].Count);
            Assert.IsTrue(game.Tableaus[i].TopCard!.IsFaceUp);
        }
    }

    [TestMethod]
    public void SpiderSolitaireGame_DealFromStock_FailsWhenTableauEmpty()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        // Clear one tableau
        game.Tableaus[0].Clear();

        Assert.IsFalse(game.DealFromStock());
    }

    [TestMethod]
    public void SpiderSolitaireGame_DealFromStock_FailsWhenStockEmpty()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        // Deal all stock (5 deals of 10 cards = 50 cards)
        for (int i = 0; i < 5; i++)
        {
            game.DealFromStock();
        }

        Assert.AreEqual(0, game.Stock.Count);
        Assert.IsFalse(game.DealFromStock());
    }

    [TestMethod]
    public void SpiderSolitaireGame_StockDealsRemaining_CalculatesCorrectly()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        Assert.AreEqual(5, game.StockDealsRemaining);

        game.DealFromStock();
        Assert.AreEqual(4, game.StockDealsRemaining);

        game.DealFromStock();
        Assert.AreEqual(3, game.StockDealsRemaining);
    }

    [TestMethod]
    public void SpiderTableauPile_CanPickupFrom_AllowsSameSuitSequence()
    {
        var tableau = new SpiderTableauPile();
        tableau.AddCard(new Card(Suit.Spades, Rank.King, true));
        tableau.AddCard(new Card(Suit.Spades, Rank.Queen, true));
        tableau.AddCard(new Card(Suit.Spades, Rank.Jack, true));

        Assert.IsTrue(tableau.CanPickupFrom(0));
        Assert.IsTrue(tableau.CanPickupFrom(1));
        Assert.IsTrue(tableau.CanPickupFrom(2));
    }

    [TestMethod]
    public void SpiderTableauPile_CanPickupFrom_RejectsMixedSuitSequence()
    {
        var tableau = new SpiderTableauPile();
        tableau.AddCard(new Card(Suit.Spades, Rank.King, true));
        tableau.AddCard(new Card(Suit.Hearts, Rank.Queen, true)); // Different suit
        tableau.AddCard(new Card(Suit.Spades, Rank.Jack, true));

        Assert.IsFalse(tableau.CanPickupFrom(0)); // Can't pick up from King (sequence broken)
        Assert.IsFalse(tableau.CanPickupFrom(1)); // Can't pick up Queen+Jack (different suits)
        Assert.IsTrue(tableau.CanPickupFrom(2));  // Can pick up just Jack
    }

    [TestMethod]
    public void SpiderTableauPile_CanPickupFrom_RejectsFaceDownCards()
    {
        var tableau = new SpiderTableauPile();
        tableau.AddCard(new Card(Suit.Spades, Rank.King, false)); // Face down
        tableau.AddCard(new Card(Suit.Spades, Rank.Queen, true));

        Assert.IsFalse(tableau.CanPickupFrom(0));
        Assert.IsTrue(tableau.CanPickupFrom(1));
    }

    [TestMethod]
    public void SpiderTableauPile_CanAcceptCard_AcceptsAnyCardOnEmpty()
    {
        var tableau = new SpiderTableauPile();

        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Hearts, Rank.Five)));
        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Spades, Rank.King)));
        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Diamonds, Rank.Ace)));
    }

    [TestMethod]
    public void SpiderTableauPile_CanAcceptCard_AcceptsOneLowerRank()
    {
        var tableau = new SpiderTableauPile();
        tableau.AddCard(new Card(Suit.Hearts, Rank.Six, true));

        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Hearts, Rank.Five)));
        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Spades, Rank.Five))); // Different suit OK for placement
        Assert.IsFalse(tableau.CanAcceptCard(new Card(Suit.Hearts, Rank.Four)));
        Assert.IsFalse(tableau.CanAcceptCard(new Card(Suit.Hearts, Rank.Seven)));
    }

    [TestMethod]
    public void SpiderSolitaireGame_MoveCards_MovesSingleCard()
    {
        var game = new SpiderSolitaireGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Six, true));
        game.Tableaus[1].AddCard(new Card(Suit.Hearts, Rank.Seven, true));

        Assert.IsTrue(game.MoveCards(0, 0, 1));
        Assert.AreEqual(0, game.Tableaus[0].Count);
        Assert.AreEqual(2, game.Tableaus[1].Count);
    }

    [TestMethod]
    public void SpiderSolitaireGame_MoveCards_MovesSequence()
    {
        var game = new SpiderSolitaireGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        // Create a same-suit sequence
        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Six, true));
        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Five, true));
        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Four, true));
        game.Tableaus[1].AddCard(new Card(Suit.Hearts, Rank.Seven, true));

        Assert.IsTrue(game.MoveCards(0, 0, 1)); // Move all 3 cards
        Assert.AreEqual(0, game.Tableaus[0].Count);
        Assert.AreEqual(4, game.Tableaus[1].Count);
    }

    [TestMethod]
    public void SpiderSolitaireGame_MoveCards_RejectsBrokenSequence()
    {
        var game = new SpiderSolitaireGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        // Create a mixed-suit sequence (can't be moved together)
        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Six, true));
        game.Tableaus[0].AddCard(new Card(Suit.Hearts, Rank.Five, true)); // Different suit
        game.Tableaus[1].AddCard(new Card(Suit.Clubs, Rank.Seven, true));

        Assert.IsFalse(game.MoveCards(0, 0, 1)); // Can't move from Spades 6
    }

    [TestMethod]
    public void SpiderSolitaireGame_MoveCards_FlipsNewTopCard()
    {
        var game = new SpiderSolitaireGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.King, false)); // Face down
        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Six, true));
        game.Tableaus[1].AddCard(new Card(Suit.Hearts, Rank.Seven, true));

        Assert.IsFalse(game.Tableaus[0].Cards[0].IsFaceUp);

        game.MoveCards(0, 1, 1);

        Assert.IsTrue(game.Tableaus[0].TopCard!.IsFaceUp);
    }

    [TestMethod]
    public void SpiderTableauPile_HasCompleteSequence_DetectsComplete()
    {
        var tableau = new SpiderTableauPile();

        // Add a complete K-A sequence
        for (int rank = 13; rank >= 1; rank--)
        {
            tableau.AddCard(new Card(Suit.Spades, (Rank)rank, true));
        }

        Assert.IsTrue(tableau.HasCompleteSequence());
    }

    [TestMethod]
    public void SpiderTableauPile_HasCompleteSequence_RejectsIncomplete()
    {
        var tableau = new SpiderTableauPile();

        // Add incomplete sequence (missing Ace)
        for (int rank = 13; rank >= 2; rank--)
        {
            tableau.AddCard(new Card(Suit.Spades, (Rank)rank, true));
        }

        Assert.IsFalse(tableau.HasCompleteSequence());
    }

    [TestMethod]
    public void SpiderTableauPile_HasCompleteSequence_RejectsMixedSuit()
    {
        var tableau = new SpiderTableauPile();

        // Add sequence with one different suit
        for (int rank = 13; rank >= 1; rank--)
        {
            var suit = rank == 7 ? Suit.Hearts : Suit.Spades;
            tableau.AddCard(new Card(suit, (Rank)rank, true));
        }

        Assert.IsFalse(tableau.HasCompleteSequence());
    }

    [TestMethod]
    public void SpiderSolitaireGame_CompletedSequence_IsRemovedAutomatically()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.OneSuit, 42);

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        // Set up a nearly complete sequence on tableau 0
        for (int rank = 13; rank >= 2; rank--)
        {
            game.Tableaus[0].AddCard(new Card(Suit.Spades, (Rank)rank, true));
        }

        // Put Ace on tableau 1
        game.Tableaus[1].AddCard(new Card(Suit.Spades, Rank.Two, true));
        game.Tableaus[1].AddCard(new Card(Suit.Spades, Rank.Ace, true));

        var completedBefore = game.CompletedSequences;

        // Move Ace to complete the sequence
        game.MoveCards(1, 1, 0);

        Assert.AreEqual(completedBefore + 1, game.CompletedSequences);
        Assert.AreEqual(0, game.Tableaus[0].Count); // Sequence was removed
    }

    [TestMethod]
    public void SpiderSolitaireGame_IsGameWon_WhenEightSequencesComplete()
    {
        var game = new SpiderSolitaireGame();

        Assert.IsFalse(game.IsGameWon);

        game.CompletedSequences = 7;
        Assert.IsFalse(game.IsGameWon);

        game.CompletedSequences = 8;
        Assert.IsTrue(game.IsGameWon);
    }

    [TestMethod]
    public void SpiderSolitaireGame_GetValidMoves_IncludesDealWhenPossible()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        var moves = game.GetValidMoves();

        Assert.IsTrue(moves.Any(m => m.Type == SpiderMoveType.DealFromStock));
    }

    [TestMethod]
    public void SpiderSolitaireGame_GetValidMoves_ExcludesDealWhenTableauEmpty()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        game.Tableaus[0].Clear();

        var moves = game.GetValidMoves();

        Assert.IsFalse(moves.Any(m => m.Type == SpiderMoveType.DealFromStock));
    }

    [TestMethod]
    public void SpiderSolitaireGame_SaveAndLoad_PreservesState()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.TwoSuits, 42);
        game.DealFromStock();

        var saveData = game.SaveGame();
        var loadedGame = SpiderSolitaireGame.LoadGame(saveData) as SpiderSolitaireGame;

        Assert.IsNotNull(loadedGame);
        Assert.AreEqual(game.Stock.Count, loadedGame.Stock.Count);
        Assert.AreEqual(game.CompletedSequences, loadedGame.CompletedSequences);
        Assert.AreEqual(game.Difficulty, loadedGame.Difficulty);
        Assert.AreEqual(game.MoveHistory.Count, loadedGame.MoveHistory.Count);
    }

    [TestMethod]
    public void SpiderSolitaireGame_MoveHistory_TracksAllMoves()
    {
        var game = new SpiderSolitaireGame();
        game.NewGame(SpiderDifficulty.FourSuits, 42);

        game.DealFromStock();

        Assert.AreEqual(1, game.MoveHistory.Count);
        Assert.AreEqual(SpiderMoveType.DealFromStock, game.MoveHistory[0].Type);
    }

    [TestMethod]
    public void SpiderSolitaireGame_SameSeed_ProducesSameGame()
    {
        var game1 = new SpiderSolitaireGame();
        var game2 = new SpiderSolitaireGame();

        game1.NewGame(SpiderDifficulty.FourSuits, 123);
        game2.NewGame(SpiderDifficulty.FourSuits, 123);

        // Compare all tableaus
        for (int t = 0; t < 10; t++)
        {
            Assert.AreEqual(game1.Tableaus[t].Count, game2.Tableaus[t].Count);
            for (int c = 0; c < game1.Tableaus[t].Count; c++)
            {
                Assert.AreEqual(game1.Tableaus[t].Cards[c].Suit, game2.Tableaus[t].Cards[c].Suit);
                Assert.AreEqual(game1.Tableaus[t].Cards[c].Rank, game2.Tableaus[t].Cards[c].Rank);
            }
        }
    }

    [TestMethod]
    public void SpiderSolitaireGame_MoveToEmptyTableau_Succeeds()
    {
        var game = new SpiderSolitaireGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Five, true));

        Assert.IsTrue(game.MoveCards(0, 0, 1));
        Assert.AreEqual(0, game.Tableaus[0].Count);
        Assert.AreEqual(1, game.Tableaus[1].Count);
    }
}
