namespace DaCards.Tests;

[TestClass]
public sealed class CardTests
{
    [TestMethod]
    public void Card_Constructor_SetsProperties()
    {
        var card = new Card(Suit.Hearts, Rank.Ace, true);

        Assert.AreEqual(Suit.Hearts, card.Suit);
        Assert.AreEqual(Rank.Ace, card.Rank);
        Assert.IsTrue(card.IsFaceUp);
    }

    [TestMethod]
    public void Card_IsRed_ReturnsTrueForHeartsAndDiamonds()
    {
        var hearts = new Card(Suit.Hearts, Rank.King);
        var diamonds = new Card(Suit.Diamonds, Rank.Queen);

        Assert.IsTrue(hearts.IsRed);
        Assert.IsTrue(diamonds.IsRed);
    }

    [TestMethod]
    public void Card_IsBlack_ReturnsTrueForClubsAndSpades()
    {
        var clubs = new Card(Suit.Clubs, Rank.Jack);
        var spades = new Card(Suit.Spades, Rank.Ten);

        Assert.IsTrue(clubs.IsBlack);
        Assert.IsTrue(spades.IsBlack);
    }

    [TestMethod]
    public void Card_IsOppositeColor_ReturnsCorrectly()
    {
        var redCard = new Card(Suit.Hearts, Rank.Five);
        var blackCard = new Card(Suit.Spades, Rank.Six);
        var anotherRed = new Card(Suit.Diamonds, Rank.Four);

        Assert.IsTrue(redCard.IsOppositeColor(blackCard));
        Assert.IsTrue(blackCard.IsOppositeColor(redCard));
        Assert.IsFalse(redCard.IsOppositeColor(anotherRed));
    }

    [TestMethod]
    public void Card_Equals_ComparesCorrectly()
    {
        var card1 = new Card(Suit.Hearts, Rank.Ace);
        var card2 = new Card(Suit.Hearts, Rank.Ace);
        var card3 = new Card(Suit.Hearts, Rank.King);

        Assert.IsTrue(card1.Equals(card2));
        Assert.IsFalse(card1.Equals(card3));
    }

    [TestMethod]
    public void Card_ToString_FormatsCorrectly()
    {
        Assert.AreEqual("A♥", new Card(Suit.Hearts, Rank.Ace).ToString());
        Assert.AreEqual("K♠", new Card(Suit.Spades, Rank.King).ToString());
        Assert.AreEqual("Q♦", new Card(Suit.Diamonds, Rank.Queen).ToString());
        Assert.AreEqual("J♣", new Card(Suit.Clubs, Rank.Jack).ToString());
        Assert.AreEqual("10♥", new Card(Suit.Hearts, Rank.Ten).ToString());
    }
}
