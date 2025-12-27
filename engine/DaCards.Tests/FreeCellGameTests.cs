namespace DaCards.Tests;

[TestClass]
public sealed class FreeCellGameTests
{
    [TestMethod]
    public void FreeCellGame_Constructor_InitializesCorrectly()
    {
        var game = new FreeCellGame();

        Assert.AreEqual(8, game.Tableaus.Count);
        Assert.AreEqual(4, game.FreeCells.Count);
        Assert.AreEqual(4, game.Foundations.Count);
    }

    [TestMethod]
    public void FreeCellGame_NewGame_DealsCardsCorrectly()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        // First 4 tableaus get 7 cards each
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(7, game.Tableaus[i].Count, $"Tableau {i} should have 7 cards");
        }

        // Last 4 tableaus get 6 cards each
        for (int i = 4; i < 8; i++)
        {
            Assert.AreEqual(6, game.Tableaus[i].Count, $"Tableau {i} should have 6 cards");
        }

        // Total: 4*7 + 4*6 = 28 + 24 = 52 cards
        int totalCards = game.Tableaus.Sum(t => t.Count);
        Assert.AreEqual(52, totalCards);
    }

    [TestMethod]
    public void FreeCellGame_NewGame_AllCardsFaceUp()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        foreach (var tableau in game.Tableaus)
        {
            foreach (var card in tableau.Cards)
            {
                Assert.IsTrue(card.IsFaceUp, "All cards should be face up in FreeCell");
            }
        }
    }

    [TestMethod]
    public void FreeCellGame_NewGame_FreeCellsEmpty()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        foreach (var cell in game.FreeCells)
        {
            Assert.IsNull(cell);
        }

        Assert.AreEqual(4, game.EmptyFreeCellCount);
    }

    [TestMethod]
    public void FreeCellGame_NewGame_ValidatesSuccessfully()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        Assert.IsTrue(game.ValidateGame());
    }

    [TestMethod]
    public void FreeCellGame_MoveTableauToFreeCell_Succeeds()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        var cardBefore = game.Tableaus[0].TopCard;
        int countBefore = game.Tableaus[0].Count;

        Assert.IsTrue(game.MoveTableauToFreeCell(0, 0));

        Assert.AreEqual(countBefore - 1, game.Tableaus[0].Count);
        Assert.AreEqual(cardBefore, game.FreeCells[0]);
        Assert.AreEqual(3, game.EmptyFreeCellCount);
    }

    [TestMethod]
    public void FreeCellGame_MoveTableauToFreeCell_FailsWhenFreeCellOccupied()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        game.MoveTableauToFreeCell(0, 0);

        Assert.IsFalse(game.MoveTableauToFreeCell(1, 0));
    }

    [TestMethod]
    public void FreeCellGame_MoveTableauToFreeCell_FailsWhenTableauEmpty()
    {
        var game = new FreeCellGame();
        game.Tableaus[0].Clear();

        Assert.IsFalse(game.MoveTableauToFreeCell(0, 0));
    }

    [TestMethod]
    public void FreeCellGame_MoveFreeCellToTableau_Succeeds()
    {
        var game = new FreeCellGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        var redSix = new Card(Suit.Hearts, Rank.Six, true);
        var blackFive = new Card(Suit.Spades, Rank.Five, true);

        game.Tableaus[0].AddCard(redSix);
        game.FreeCells[0] = blackFive;

        Assert.IsTrue(game.MoveFreeCellToTableau(0, 0));
        Assert.IsNull(game.FreeCells[0]);
        Assert.AreEqual(2, game.Tableaus[0].Count);
    }

    [TestMethod]
    public void FreeCellGame_MoveFreeCellToTableau_FailsWrongColor()
    {
        var game = new FreeCellGame();

        game.Tableaus[0].Clear();

        var redSix = new Card(Suit.Hearts, Rank.Six, true);
        var redFive = new Card(Suit.Diamonds, Rank.Five, true);

        game.Tableaus[0].AddCard(redSix);
        game.FreeCells[0] = redFive;

        Assert.IsFalse(game.MoveFreeCellToTableau(0, 0));
    }

    [TestMethod]
    public void FreeCellGame_MoveFreeCellToTableau_AcceptsAnyCardOnEmpty()
    {
        var game = new FreeCellGame();
        game.Tableaus[0].Clear();

        var card = new Card(Suit.Hearts, Rank.Five, true);
        game.FreeCells[0] = card;

        Assert.IsTrue(game.MoveFreeCellToTableau(0, 0));
        Assert.AreEqual(1, game.Tableaus[0].Count);
    }

    [TestMethod]
    public void FreeCellGame_MoveTableauToFoundation_MovesAce()
    {
        var game = new FreeCellGame();
        game.Tableaus[0].Clear();

        var ace = new Card(Suit.Hearts, Rank.Ace, true);
        game.Tableaus[0].AddCard(ace);

        Assert.IsTrue(game.MoveTableauToFoundation(0, 0));
        Assert.AreEqual(0, game.Tableaus[0].Count);
        Assert.AreEqual(1, game.Foundations[0].Count);
    }

    [TestMethod]
    public void FreeCellGame_MoveFreeCellToFoundation_MovesAce()
    {
        var game = new FreeCellGame();

        var ace = new Card(Suit.Hearts, Rank.Ace, true);
        game.FreeCells[0] = ace;

        Assert.IsTrue(game.MoveFreeCellToFoundation(0, 0));
        Assert.IsNull(game.FreeCells[0]);
        Assert.AreEqual(1, game.Foundations[0].Count);
    }

    [TestMethod]
    public void FreeCellTableauPile_CanAcceptCard_AcceptsOppositeColorDescending()
    {
        var tableau = new FreeCellTableauPile();
        tableau.AddCard(new Card(Suit.Hearts, Rank.Seven, true));

        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Spades, Rank.Six, true)));
        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Clubs, Rank.Six, true)));
        Assert.IsFalse(tableau.CanAcceptCard(new Card(Suit.Diamonds, Rank.Six, true)));
        Assert.IsFalse(tableau.CanAcceptCard(new Card(Suit.Spades, Rank.Five, true)));
    }

    [TestMethod]
    public void FreeCellTableauPile_CanAcceptCard_AcceptsAnyOnEmpty()
    {
        var tableau = new FreeCellTableauPile();

        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Hearts, Rank.Five, true)));
        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Spades, Rank.King, true)));
        Assert.IsTrue(tableau.CanAcceptCard(new Card(Suit.Clubs, Rank.Ace, true)));
    }

    [TestMethod]
    public void FreeCellGame_GetMaxMovableCards_CalculatesCorrectly()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        // All 4 free cells empty, no empty tableaus
        // Formula: (1 + 4) * 2^0 = 5
        Assert.AreEqual(5, game.GetMaxMovableCards());

        // Fill one free cell
        game.MoveTableauToFreeCell(0, 0);
        // (1 + 3) * 2^0 = 4
        Assert.AreEqual(4, game.GetMaxMovableCards());

        // Fill another free cell
        game.MoveTableauToFreeCell(1, 1);
        // (1 + 2) * 2^0 = 3
        Assert.AreEqual(3, game.GetMaxMovableCards());
    }

    [TestMethod]
    public void FreeCellGame_GetMaxMovableCards_IncludesEmptyTableaus()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        // All tableaus have cards, 4 free cells empty
        // (1 + 4) * 2^0 = 5
        Assert.AreEqual(5, game.GetMaxMovableCards());

        // Clear a tableau to make it empty
        game.Tableaus[0].Clear();

        // 4 free cells empty, 1 empty tableau
        // (1 + 4) * 2^1 = 10
        Assert.AreEqual(10, game.GetMaxMovableCards());

        // Clear another tableau
        game.Tableaus[1].Clear();
        // (1 + 4) * 2^2 = 20
        Assert.AreEqual(20, game.GetMaxMovableCards());
    }

    [TestMethod]
    public void FreeCellGame_MoveTableauToTableau_MovesSingleCard()
    {
        var game = new FreeCellGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        game.Tableaus[0].AddCard(new Card(Suit.Hearts, Rank.Seven, true));
        game.Tableaus[1].AddCard(new Card(Suit.Spades, Rank.Six, true));

        Assert.IsTrue(game.MoveTableauToTableau(1, 0, 0));
        Assert.AreEqual(0, game.Tableaus[1].Count);
        Assert.AreEqual(2, game.Tableaus[0].Count);
    }

    [TestMethod]
    public void FreeCellGame_MoveTableauToTableau_MovesMultipleCards()
    {
        var game = new FreeCellGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        // Create valid sequence on tableau 1
        game.Tableaus[1].AddCard(new Card(Suit.Hearts, Rank.Seven, true));
        game.Tableaus[1].AddCard(new Card(Suit.Spades, Rank.Six, true));
        game.Tableaus[1].AddCard(new Card(Suit.Hearts, Rank.Five, true));

        // Target on tableau 0
        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Eight, true));

        // With 4 empty free cells, max movable is 5
        Assert.IsTrue(game.MoveTableauToTableau(1, 0, 0)); // Move all 3 cards
        Assert.AreEqual(0, game.Tableaus[1].Count);
        Assert.AreEqual(4, game.Tableaus[0].Count);
    }

    [TestMethod]
    public void FreeCellGame_MoveTableauToTableau_RespectsMaxMovable()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        // Fill all free cells
        for (int i = 0; i < 4; i++)
        {
            game.FreeCells[i] = new Card(Suit.Hearts, Rank.Ace, true);
        }

        // Set up specific tableaus for the test
        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        // Create sequence of 3 cards (valid alternating sequence)
        game.Tableaus[1].AddCard(new Card(Suit.Hearts, Rank.Seven, true));
        game.Tableaus[1].AddCard(new Card(Suit.Spades, Rank.Six, true));
        game.Tableaus[1].AddCard(new Card(Suit.Hearts, Rank.Five, true));

        // Target on tableau 0 - Spades 8 can accept Hearts 7
        game.Tableaus[0].AddCard(new Card(Suit.Spades, Rank.Eight, true));

        // With 0 empty free cells and 0 empty tableaus (other 6 tableaus have cards), max movable is 1
        Assert.AreEqual(1, game.GetMaxMovableCards());

        // Cannot move 3 cards starting from Hearts 7 (would need Hearts 7 -> Spades 8, valid, but max is 1)
        Assert.IsFalse(game.MoveTableauToTableau(1, 0, 0));

        // Cannot move 2 cards starting from Spades 6 (Spades 6 can't go on Spades 8)
        Assert.IsFalse(game.MoveTableauToTableau(1, 1, 0));

        // Add a second destination that Hearts 5 can go on
        game.Tableaus[2].Clear();
        game.Tableaus[2].AddCard(new Card(Suit.Spades, Rank.Six, true));

        // Can move just the top card (Hearts 5 onto Spades 6)
        Assert.IsTrue(game.MoveTableauToTableau(1, 2, 2));
    }

    [TestMethod]
    public void FreeCellTableauPile_GetValidSequenceLength_CalculatesCorrectly()
    {
        var tableau = new FreeCellTableauPile();

        // Valid alternating sequence
        tableau.AddCard(new Card(Suit.Hearts, Rank.Seven, true));
        tableau.AddCard(new Card(Suit.Spades, Rank.Six, true));
        tableau.AddCard(new Card(Suit.Hearts, Rank.Five, true));

        Assert.AreEqual(3, tableau.GetValidSequenceLength());
    }

    [TestMethod]
    public void FreeCellTableauPile_GetValidSequenceLength_StopsAtBreak()
    {
        var tableau = new FreeCellTableauPile();

        tableau.AddCard(new Card(Suit.Hearts, Rank.King, true));
        tableau.AddCard(new Card(Suit.Hearts, Rank.Seven, true)); // Break here (same color or wrong rank)
        tableau.AddCard(new Card(Suit.Spades, Rank.Six, true));
        tableau.AddCard(new Card(Suit.Hearts, Rank.Five, true));

        Assert.AreEqual(3, tableau.GetValidSequenceLength());
    }

    [TestMethod]
    public void FreeCellGame_AutoMoveToFoundation_MovesAvailableCards()
    {
        var game = new FreeCellGame();

        game.Tableaus[0].Clear();
        game.Tableaus[0].AddCard(new Card(Suit.Hearts, Rank.Ace, true));

        game.FreeCells[0] = new Card(Suit.Spades, Rank.Ace, true);

        Assert.IsTrue(game.AutoMoveToFoundation());
        Assert.IsTrue(game.Foundations.Sum(f => f.Count) >= 1);
    }

    [TestMethod]
    public void FreeCellGame_IsGameWon_ReturnsTrueWhenComplete()
    {
        var game = new FreeCellGame();

        Assert.IsFalse(game.IsGameWon);

        // Fill all foundations
        for (int f = 0; f < 4; f++)
        {
            var suit = (Suit)f;
            for (int r = 1; r <= 13; r++)
            {
                game.Foundations[f].AddCard(new Card(suit, (Rank)r, true));
            }
        }

        Assert.IsTrue(game.IsGameWon);
    }

    [TestMethod]
    public void FreeCellGame_GetValidMoves_ReturnsAllMoveTypes()
    {
        var game = new FreeCellGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        game.Tableaus[0].AddCard(new Card(Suit.Hearts, Rank.Ace, true));
        game.Tableaus[1].AddCard(new Card(Suit.Spades, Rank.Two, true));
        game.FreeCells[0] = new Card(Suit.Diamonds, Rank.Ace, true);

        var moves = game.GetValidMoves();

        // Should have moves to free cells, foundations, and tableaus
        Assert.IsTrue(moves.Any(m => m.Type == FreeCellMoveType.TableauToFreeCell));
        Assert.IsTrue(moves.Any(m => m.Type == FreeCellMoveType.TableauToFoundation));
        Assert.IsTrue(moves.Any(m => m.Type == FreeCellMoveType.FreeCellToFoundation));
    }

    [TestMethod]
    public void FreeCellGame_SaveAndLoad_PreservesState()
    {
        var game = new FreeCellGame();
        game.NewGame(42);
        game.MoveTableauToFreeCell(0, 0);
        game.MoveTableauToFreeCell(1, 1);

        var saveData = game.SaveGame();
        var loadedGame = FreeCellGame.LoadGame(saveData) as FreeCellGame;

        Assert.IsNotNull(loadedGame);
        Assert.AreEqual(game.EmptyFreeCellCount, loadedGame.EmptyFreeCellCount);
        Assert.AreEqual(game.MoveHistory.Count, loadedGame.MoveHistory.Count);

        for (int i = 0; i < 8; i++)
        {
            Assert.AreEqual(game.Tableaus[i].Count, loadedGame.Tableaus[i].Count);
        }
    }

    [TestMethod]
    public void FreeCellGame_MoveHistory_TracksAllMoves()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        game.MoveTableauToFreeCell(0, 0);

        Assert.AreEqual(1, game.MoveHistory.Count);
        Assert.AreEqual(FreeCellMoveType.TableauToFreeCell, game.MoveHistory[0].Type);
    }

    [TestMethod]
    public void FreeCellGame_SameSeed_ProducesSameGame()
    {
        var game1 = new FreeCellGame();
        var game2 = new FreeCellGame();

        game1.NewGame(123);
        game2.NewGame(123);

        for (int t = 0; t < 8; t++)
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
    public void FreeCellGame_MoveToEmptyTableau_AcceptsAnyCard()
    {
        var game = new FreeCellGame();

        game.Tableaus[0].Clear();
        game.Tableaus[1].Clear();

        game.Tableaus[1].AddCard(new Card(Suit.Hearts, Rank.Five, true));

        Assert.IsTrue(game.MoveTableauToTableau(1, 0, 0));
        Assert.AreEqual(1, game.Tableaus[0].Count);
        Assert.AreEqual(Rank.Five, game.Tableaus[0].TopCard!.Rank);
    }

    [TestMethod]
    public void FreeCellGame_ValidateGame_DetectsDuplicateCards()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        // Add a duplicate card
        game.FreeCells[0] = new Card(game.Tableaus[0].TopCard!.Suit, game.Tableaus[0].TopCard!.Rank, true);

        Assert.IsFalse(game.ValidateGame());
    }

    [TestMethod]
    public void FreeCellGame_EmptyTableauCount_CalculatesCorrectly()
    {
        var game = new FreeCellGame();
        game.NewGame(42);

        Assert.AreEqual(0, game.EmptyTableauCount);

        game.Tableaus[0].Clear();
        Assert.AreEqual(1, game.EmptyTableauCount);

        game.Tableaus[1].Clear();
        Assert.AreEqual(2, game.EmptyTableauCount);
    }

    [TestMethod]
    public void FreeCellTableauPile_CanPickupSequence_ValidatesCorrectly()
    {
        var tableau = new FreeCellTableauPile();

        tableau.AddCard(new Card(Suit.Hearts, Rank.Seven, true));
        tableau.AddCard(new Card(Suit.Spades, Rank.Six, true));
        tableau.AddCard(new Card(Suit.Hearts, Rank.Five, true));

        // Can pickup any valid sequence within limits
        Assert.IsTrue(tableau.CanPickupSequence(0, 5)); // All 3 cards
        Assert.IsTrue(tableau.CanPickupSequence(1, 5)); // Bottom 2 cards
        Assert.IsTrue(tableau.CanPickupSequence(2, 5)); // Just top card

        // Cannot exceed max
        Assert.IsFalse(tableau.CanPickupSequence(0, 2)); // Can't move 3 with max 2
    }

    [TestMethod]
    public void FreeCellTableauPile_CanPickupSequence_RejectsBrokenSequence()
    {
        var tableau = new FreeCellTableauPile();

        tableau.AddCard(new Card(Suit.Hearts, Rank.Seven, true));
        tableau.AddCard(new Card(Suit.Hearts, Rank.Six, true)); // Same color - breaks sequence from 7
        tableau.AddCard(new Card(Suit.Hearts, Rank.Five, true)); // Same color - breaks sequence from 6

        Assert.IsFalse(tableau.CanPickupSequence(0, 10)); // Broken at index 0-1 (same color)
        Assert.IsFalse(tableau.CanPickupSequence(1, 10)); // Broken at index 1-2 (same color)
        Assert.IsTrue(tableau.CanPickupSequence(2, 10));  // Just top card is valid
    }
}
