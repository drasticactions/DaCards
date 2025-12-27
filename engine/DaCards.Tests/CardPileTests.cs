namespace DaCards.Tests;

[TestClass]
public sealed class CardPileTests
{
    [TestMethod]
    public void FoundationPile_CanAcceptCard_AcceptsAceWhenEmpty()
    {
        var foundation = new FoundationPile();
        var ace = new Card(Suit.Hearts, Rank.Ace);
        var king = new Card(Suit.Hearts, Rank.King);

        Assert.IsTrue(foundation.CanAcceptCard(ace));
        Assert.IsFalse(foundation.CanAcceptCard(king));
    }

    [TestMethod]
    public void FoundationPile_CanAcceptCard_AcceptsNextRankSameSuit()
    {
        var foundation = new FoundationPile();
        foundation.AddCard(new Card(Suit.Hearts, Rank.Ace));

        var two = new Card(Suit.Hearts, Rank.Two);
        var three = new Card(Suit.Hearts, Rank.Three);
        var twoSpades = new Card(Suit.Spades, Rank.Two);

        Assert.IsTrue(foundation.CanAcceptCard(two));
        Assert.IsFalse(foundation.CanAcceptCard(three));
        Assert.IsFalse(foundation.CanAcceptCard(twoSpades));
    }

    [TestMethod]
    public void FoundationPile_IsComplete_ReturnsTrueWhenFull()
    {
        var foundation = new FoundationPile();

        for (int i = 1; i <= 13; i++)
        {
            foundation.AddCard(new Card(Suit.Hearts, (Rank)i));
        }

        Assert.IsTrue(foundation.IsComplete);
    }

    [TestMethod]
    public void TableauPile_CanAcceptCard_AcceptsKingWhenEmpty()
    {
        var tableau = new TableauPile();
        var king = new Card(Suit.Hearts, Rank.King, true);
        var queen = new Card(Suit.Hearts, Rank.Queen, true);

        Assert.IsTrue(tableau.CanAcceptCard(king));
        Assert.IsFalse(tableau.CanAcceptCard(queen));
    }

    [TestMethod]
    public void TableauPile_CanAcceptCard_AcceptsOppositeColorDescending()
    {
        var tableau = new TableauPile();
        var redKing = new Card(Suit.Hearts, Rank.King, true);
        tableau.AddCard(redKing);

        var blackQueen = new Card(Suit.Spades, Rank.Queen, true);
        var redQueen = new Card(Suit.Diamonds, Rank.Queen, true);
        var blackJack = new Card(Suit.Clubs, Rank.Jack, true);

        Assert.IsTrue(tableau.CanAcceptCard(blackQueen));
        Assert.IsFalse(tableau.CanAcceptCard(redQueen));
        Assert.IsFalse(tableau.CanAcceptCard(blackJack));
    }

    [TestMethod]
    public void TableauPile_FlipTopCard_FlipsWhenFaceDown()
    {
        var tableau = new TableauPile();
        var card = new Card(Suit.Hearts, Rank.King, false);
        tableau.AddCard(card);

        Assert.IsFalse(tableau.TopCard!.IsFaceUp);
        tableau.FlipTopCard();
        Assert.IsTrue(tableau.TopCard!.IsFaceUp);
    }

    [TestMethod]
    public void TableauPile_RemoveCardsFrom_RemovesCorrectCards()
    {
        var tableau = new TableauPile();
        tableau.AddCard(new Card(Suit.Hearts, Rank.King, true));
        tableau.AddCard(new Card(Suit.Spades, Rank.Queen, true));
        tableau.AddCard(new Card(Suit.Hearts, Rank.Jack, true));

        var removed = tableau.RemoveCardsFrom(1);

        Assert.AreEqual(2, removed.Count);
        Assert.AreEqual(1, tableau.Count);
        Assert.AreEqual(Rank.Queen, removed[0].Rank);
        Assert.AreEqual(Rank.Jack, removed[1].Rank);
    }

    [TestMethod]
    public void StockPile_DrawCard_FlipsCard()
    {
        var stock = new StockPile();
        var card = new Card(Suit.Hearts, Rank.Ace, false);
        stock.AddCard(card);

        var drawn = stock.DrawCard();

        Assert.IsNotNull(drawn);
        Assert.IsTrue(drawn.IsFaceUp);
    }

    [TestMethod]
    public void StockPile_Reset_ReversesAndFlipsCards()
    {
        var stock = new StockPile();
        var wasteCards = new List<Card>
        {
            new Card(Suit.Hearts, Rank.Ace, true),
            new Card(Suit.Hearts, Rank.Two, true),
            new Card(Suit.Hearts, Rank.Three, true)
        };

        stock.Reset(wasteCards);

        Assert.AreEqual(3, stock.Count);
        Assert.IsFalse(stock.TopCard!.IsFaceUp);
    }

    [TestMethod]
    public void WastePile_AddCardFromStock_SetsCardFaceUp()
    {
        var waste = new WastePile();
        var card = new Card(Suit.Hearts, Rank.Ace, false);

        waste.AddCardFromStock(card);

        Assert.IsTrue(waste.TopCard!.IsFaceUp);
    }
}
