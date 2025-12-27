namespace DaCards;

public class Deck
{
    private readonly List<Card> _cards = new();
    private readonly Random _random;

    public Deck() : this(new Random())
    {
    }

    public Deck(Random random)
    {
        _random = random;
        Initialize();
    }

    public Deck(int seed) : this(new Random(seed))
    {
    }

    private void Initialize()
    {
        _cards.Clear();
        foreach (Suit suit in Enum.GetValues<Suit>())
        {
            foreach (Rank rank in Enum.GetValues<Rank>())
            {
                _cards.Add(new Card(suit, rank));
            }
        }
    }

    public void Shuffle()
    {
        // Fisher-Yates shuffle
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public Card? Draw()
    {
        if (_cards.Count == 0)
            return null;

        var card = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }

    public List<Card> DrawMultiple(int count)
    {
        var drawn = new List<Card>();
        for (int i = 0; i < count && _cards.Count > 0; i++)
        {
            var card = Draw();
            if (card != null)
                drawn.Add(card);
        }
        return drawn;
    }

    public int Count => _cards.Count;

    public IReadOnlyList<Card> Cards => _cards.AsReadOnly();
}
