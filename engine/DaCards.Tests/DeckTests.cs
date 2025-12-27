namespace DaCards.Tests;

[TestClass]
public sealed class DeckTests
{
    [TestMethod]
    public void Deck_Initialize_Has52Cards()
    {
        var deck = new Deck();

        Assert.AreEqual(52, deck.Count);
    }

    [TestMethod]
    public void Deck_Initialize_HasAllUniqueCards()
    {
        var deck = new Deck();
        var uniqueCards = new HashSet<(Suit, Rank)>();

        foreach (var card in deck.Cards)
        {
            Assert.IsTrue(uniqueCards.Add((card.Suit, card.Rank)), "Duplicate card found");
        }

        Assert.AreEqual(52, uniqueCards.Count);
    }

    [TestMethod]
    public void Deck_Draw_ReturnsCard()
    {
        var deck = new Deck();

        var card = deck.Draw();

        Assert.IsNotNull(card);
        Assert.AreEqual(51, deck.Count);
    }

    [TestMethod]
    public void Deck_Draw_ReturnsNullWhenEmpty()
    {
        var deck = new Deck();

        // Draw all cards
        for (int i = 0; i < 52; i++)
        {
            deck.Draw();
        }

        Assert.IsNull(deck.Draw());
    }

    [TestMethod]
    public void Deck_DrawMultiple_ReturnsRequestedCards()
    {
        var deck = new Deck();

        var cards = deck.DrawMultiple(5);

        Assert.AreEqual(5, cards.Count);
        Assert.AreEqual(47, deck.Count);
    }

    [TestMethod]
    public void Deck_Shuffle_WithSameSeed_ProducesSameOrder()
    {
        var deck1 = new Deck(42);
        var deck2 = new Deck(42);

        deck1.Shuffle();
        deck2.Shuffle();

        for (int i = 0; i < 52; i++)
        {
            var card1 = deck1.Draw();
            var card2 = deck2.Draw();
            Assert.AreEqual(card1!.Suit, card2!.Suit);
            Assert.AreEqual(card1.Rank, card2.Rank);
        }
    }

    [TestMethod]
    public void Deck_Shuffle_ChangesOrder()
    {
        var deck1 = new Deck(1);
        var deck2 = new Deck(2);

        deck1.Shuffle();
        deck2.Shuffle();

        var differentFound = false;
        for (int i = 0; i < 52; i++)
        {
            var card1 = deck1.Draw();
            var card2 = deck2.Draw();
            if (card1!.Suit != card2!.Suit || card1.Rank != card2.Rank)
            {
                differentFound = true;
                break;
            }
        }

        Assert.IsTrue(differentFound, "Different seeds should produce different orders");
    }
}
