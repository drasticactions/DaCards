namespace DaCards;

public enum Suit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

public enum Rank
{
    Ace = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13
}

public class Card
{
    public Suit Suit { get; set; }
    public Rank Rank { get; set; }
    public bool IsFaceUp { get; set; }

    public Card()
    {
    }

    public Card(Suit suit, Rank rank, bool isFaceUp = false)
    {
        Suit = suit;
        Rank = rank;
        IsFaceUp = isFaceUp;
    }

    public bool IsRed => Suit == Suit.Hearts || Suit == Suit.Diamonds;
    public bool IsBlack => Suit == Suit.Clubs || Suit == Suit.Spades;

    public bool IsOppositeColor(Card other) => IsRed != other.IsRed;

    public override string ToString()
    {
        var suitSymbol = Suit switch
        {
            Suit.Hearts => "♥",
            Suit.Diamonds => "♦",
            Suit.Clubs => "♣",
            Suit.Spades => "♠",
            _ => "?"
        };

        var rankStr = Rank switch
        {
            Rank.Ace => "A",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            _ => ((int)Rank).ToString()
        };

        return $"{rankStr}{suitSymbol}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is Card other)
        {
            return Suit == other.Suit && Rank == other.Rank;
        }
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(Suit, Rank);
}
