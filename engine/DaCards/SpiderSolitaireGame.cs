using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaCards;

public enum SpiderDifficulty
{
    OneSuit = 1,    // All Spades - Easy
    TwoSuits = 2,   // Spades and Hearts - Medium
    FourSuits = 4   // All suits - Hard
}

public enum SpiderMoveType
{
    DealFromStock,
    TableauToTableau,
    CompleteSequence
}

public class SpiderMove
{
    public SpiderMoveType Type { get; set; }
    public int SourceIndex { get; set; }
    public int DestinationIndex { get; set; }
    public int CardCount { get; set; }

    public SpiderMove()
    {
    }

    public SpiderMove(SpiderMoveType type, int sourceIndex = -1, int destinationIndex = -1, int cardCount = 1)
    {
        Type = type;
        SourceIndex = sourceIndex;
        DestinationIndex = destinationIndex;
        CardCount = cardCount;
    }
}

public class SpiderTableauPile : CardPile
{
    public bool CanPickupFrom(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= Count)
            return false;

        // Card must be face up
        if (!Cards[cardIndex].IsFaceUp)
            return false;

        // All cards from this index must form a valid descending same-suit sequence
        for (int i = cardIndex; i < Count - 1; i++)
        {
            var current = Cards[i];
            var next = Cards[i + 1];

            if (current.Suit != next.Suit)
                return false;
            if ((int)current.Rank != (int)next.Rank + 1)
                return false;
        }

        return true;
    }

    public bool CanAcceptCard(Card card)
    {
        if (IsEmpty)
        {
            // Any card can go on an empty tableau in Spider
            return true;
        }

        var topCard = TopCard!;
        // Must be one rank lower (any suit allowed for placement)
        return topCard.IsFaceUp && (int)card.Rank == (int)topCard.Rank - 1;
    }

    public void FlipTopCard()
    {
        if (!IsEmpty && !TopCard!.IsFaceUp)
        {
            TopCard.IsFaceUp = true;
        }
    }

    public int GetFirstFaceUpIndex()
    {
        for (int i = 0; i < Count; i++)
        {
            if (Cards[i].IsFaceUp)
                return i;
        }
        return -1;
    }

    public bool HasCompleteSequence()
    {
        if (Count < 13)
            return false;

        // Check if the last 13 cards form a complete K-A sequence of the same suit
        int startIndex = Count - 13;
        var suit = Cards[startIndex].Suit;

        if (Cards[startIndex].Rank != Rank.King)
            return false;

        for (int i = 0; i < 13; i++)
        {
            var card = Cards[startIndex + i];
            if (!card.IsFaceUp)
                return false;
            if (card.Suit != suit)
                return false;
            if ((int)card.Rank != 13 - i)
                return false;
        }

        return true;
    }

    public List<Card> RemoveCompleteSequence()
    {
        if (!HasCompleteSequence())
            return new List<Card>();

        var removed = RemoveCardsFrom(Count - 13);
        FlipTopCard();
        return removed;
    }
}

public class SpiderSolitaireGame : ICardGame
{
    public const int TableauCount = 10;
    public const int CompletedSequencesNeeded = 8;

    public List<SpiderTableauPile> Tableaus { get; set; } = new();
    public List<Card> Stock { get; set; } = new();
    public int CompletedSequences { get; set; }
    public SpiderDifficulty Difficulty { get; set; } = SpiderDifficulty.FourSuits;
    public List<SpiderMove> MoveHistory { get; set; } = new();

    [JsonIgnore]
    public bool IsGameWon => CompletedSequences >= CompletedSequencesNeeded;

    [JsonIgnore]
    public int StockDealsRemaining => Stock.Count / TableauCount;

    public SpiderSolitaireGame()
    {
        for (int i = 0; i < TableauCount; i++)
        {
            Tableaus.Add(new SpiderTableauPile());
        }
    }

    public void NewGame(SpiderDifficulty difficulty = SpiderDifficulty.FourSuits, int? seed = null)
    {
        Difficulty = difficulty;
        var cards = CreateDeck(difficulty, seed);
        Shuffle(cards, seed);

        // Clear all piles
        Stock.Clear();
        CompletedSequences = 0;
        MoveHistory.Clear();

        foreach (var tableau in Tableaus)
        {
            tableau.Clear();
        }

        // Deal to tableaus:
        // First 4 columns get 6 cards (5 face-down + 1 face-up)
        // Last 6 columns get 5 cards (4 face-down + 1 face-up)
        int cardIndex = 0;
        for (int col = 0; col < TableauCount; col++)
        {
            int cardCount = col < 4 ? 6 : 5;
            for (int row = 0; row < cardCount; row++)
            {
                var card = cards[cardIndex++];
                card.IsFaceUp = (row == cardCount - 1); // Only top card is face up
                Tableaus[col].AddCard(card);
            }
        }

        // Remaining 50 cards go to stock
        while (cardIndex < cards.Count)
        {
            var card = cards[cardIndex++];
            card.IsFaceUp = false;
            Stock.Add(card);
        }
    }

    private List<Card> CreateDeck(SpiderDifficulty difficulty, int? seed)
    {
        var cards = new List<Card>();
        var suits = difficulty switch
        {
            SpiderDifficulty.OneSuit => new[] { Suit.Spades },
            SpiderDifficulty.TwoSuits => new[] { Suit.Spades, Suit.Hearts },
            _ => new[] { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds }
        };

        // 104 cards total (8 complete sets)
        int setsPerSuit = 8 / suits.Length;
        for (int set = 0; set < setsPerSuit; set++)
        {
            foreach (var suit in suits)
            {
                foreach (Rank rank in Enum.GetValues<Rank>())
                {
                    cards.Add(new Card(suit, rank));
                }
            }
        }

        return cards;
    }

    private void Shuffle(List<Card> cards, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    public bool DealFromStock()
    {
        if (Stock.Count < TableauCount)
            return false;

        // Cannot deal if any tableau is empty
        for (int i = 0; i < TableauCount; i++)
        {
            if (Tableaus[i].IsEmpty)
                return false;
        }

        // Deal one card to each tableau
        for (int i = 0; i < TableauCount; i++)
        {
            var card = Stock[^1];
            Stock.RemoveAt(Stock.Count - 1);
            card.IsFaceUp = true;
            Tableaus[i].AddCard(card);
        }

        MoveHistory.Add(new SpiderMove(SpiderMoveType.DealFromStock));

        // Check for completed sequences after deal
        CheckAndRemoveCompletedSequences();

        return true;
    }

    public bool MoveCards(int sourceIndex, int cardIndex, int destinationIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= TableauCount)
            return false;
        if (destinationIndex < 0 || destinationIndex >= TableauCount)
            return false;
        if (sourceIndex == destinationIndex)
            return false;

        var source = Tableaus[sourceIndex];
        var destination = Tableaus[destinationIndex];

        if (!source.CanPickupFrom(cardIndex))
            return false;

        var cardToMove = source.Cards[cardIndex];
        if (!destination.CanAcceptCard(cardToMove))
            return false;

        var cardsToMove = source.RemoveCardsFrom(cardIndex);
        destination.AddCards(cardsToMove);

        source.FlipTopCard();

        MoveHistory.Add(new SpiderMove(SpiderMoveType.TableauToTableau, sourceIndex, destinationIndex, cardsToMove.Count));

        // Check for completed sequences
        CheckAndRemoveCompletedSequences();

        return true;
    }

    private void CheckAndRemoveCompletedSequences()
    {
        for (int i = 0; i < TableauCount; i++)
        {
            while (Tableaus[i].HasCompleteSequence())
            {
                Tableaus[i].RemoveCompleteSequence();
                CompletedSequences++;
                MoveHistory.Add(new SpiderMove(SpiderMoveType.CompleteSequence, i));
            }
        }
    }

    public List<SpiderMove> GetValidMoves()
    {
        var moves = new List<SpiderMove>();

        // Deal from stock (if all tableaus have cards)
        if (Stock.Count >= TableauCount && Tableaus.All(t => !t.IsEmpty))
        {
            moves.Add(new SpiderMove(SpiderMoveType.DealFromStock));
        }

        // Tableau to tableau moves
        for (int s = 0; s < TableauCount; s++)
        {
            var source = Tableaus[s];
            var firstFaceUp = source.GetFirstFaceUpIndex();

            if (firstFaceUp < 0)
                continue;

            // Find all valid pickup positions (must be same-suit descending sequence)
            for (int cardIdx = firstFaceUp; cardIdx < source.Count; cardIdx++)
            {
                if (!source.CanPickupFrom(cardIdx))
                    continue;

                var card = source.Cards[cardIdx];

                for (int d = 0; d < TableauCount; d++)
                {
                    if (s == d)
                        continue;

                    if (Tableaus[d].CanAcceptCard(card))
                    {
                        moves.Add(new SpiderMove(SpiderMoveType.TableauToTableau, s, d, source.Count - cardIdx));
                    }
                }
            }
        }

        return moves;
    }

    public string SaveGame()
    {
        return JsonSerializer.Serialize(this, SourceGenerationContext.Default.SpiderSolitaireGame);
    }

    public bool ValidateGame()
    {
        var allCards = new List<(Suit, Rank)>();

        // Collect from all locations
        foreach (var card in Stock)
        {
            allCards.Add((card.Suit, card.Rank));
        }

        foreach (var tableau in Tableaus)
        {
            foreach (var card in tableau.Cards)
            {
                allCards.Add((card.Suit, card.Rank));
            }
        }

        // Count cards + completed sequences (13 cards each)
        int totalCards = allCards.Count + (CompletedSequences * 13);

        if (totalCards != 104)
            return false;

        // Validate card counts based on difficulty
        var expectedCounts = new Dictionary<(Suit, Rank), int>();
        var suits = Difficulty switch
        {
            SpiderDifficulty.OneSuit => new[] { Suit.Spades },
            SpiderDifficulty.TwoSuits => new[] { Suit.Spades, Suit.Hearts },
            _ => new[] { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds }
        };

        int copiesPerSuit = 8 / suits.Length;
        foreach (var suit in suits)
        {
            foreach (Rank rank in Enum.GetValues<Rank>())
            {
                expectedCounts[(suit, rank)] = copiesPerSuit;
            }
        }

        // Count actual cards (excluding completed sequences which we can't fully verify)
        var actualCounts = new Dictionary<(Suit, Rank), int>();
        foreach (var card in allCards)
        {
            if (!actualCounts.ContainsKey(card))
                actualCounts[card] = 0;
            actualCounts[card]++;
        }

        // Verify no unexpected cards and counts don't exceed expected
        foreach (var kvp in actualCounts)
        {
            if (!expectedCounts.ContainsKey(kvp.Key))
                return false;
            if (kvp.Value > expectedCounts[kvp.Key])
                return false;
        }

        // Validate tableau face-up/face-down order
        foreach (var tableau in Tableaus)
        {
            bool faceUpStarted = false;
            for (int i = 0; i < tableau.Count; i++)
            {
                var card = tableau.Cards[i];
                if (card.IsFaceUp)
                {
                    faceUpStarted = true;
                }
                else if (faceUpStarted)
                {
                    return false; // Face-down after face-up is invalid
                }
            }
        }

        return true;
    }

    public static ICardGame? LoadGame(string saveData)
    {
        return JsonSerializer.Deserialize<SpiderSolitaireGame>(saveData, SourceGenerationContext.Default.SpiderSolitaireGame);
    }
}
