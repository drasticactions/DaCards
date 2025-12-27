using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaCards;

public enum FreeCellMoveType
{
    TableauToFreeCell,
    TableauToFoundation,
    TableauToTableau,
    FreeCellToFoundation,
    FreeCellToTableau
}

public class FreeCellMove
{
    public FreeCellMoveType Type { get; set; }
    public int SourceIndex { get; set; }
    public int DestinationIndex { get; set; }
    public int CardCount { get; set; }

    public FreeCellMove()
    {
    }

    public FreeCellMove(FreeCellMoveType type, int sourceIndex = -1, int destinationIndex = -1, int cardCount = 1)
    {
        Type = type;
        SourceIndex = sourceIndex;
        DestinationIndex = destinationIndex;
        CardCount = cardCount;
    }
}

public class FreeCellTableauPile : CardPile
{
    public bool CanAcceptCard(Card card)
    {
        if (IsEmpty)
        {
            // Any card can go on an empty tableau
            return true;
        }

        var topCard = TopCard!;
        // Must be opposite color and one rank lower
        return card.IsOppositeColor(topCard) && (int)card.Rank == (int)topCard.Rank - 1;
    }

    public bool CanPickupSequence(int cardIndex, int maxCards)
    {
        if (cardIndex < 0 || cardIndex >= Count)
            return false;

        int cardsToMove = Count - cardIndex;
        if (cardsToMove > maxCards)
            return false;

        // All cards from this index must form a valid alternating-color descending sequence
        for (int i = cardIndex; i < Count - 1; i++)
        {
            var current = Cards[i];
            var next = Cards[i + 1];

            if (!next.IsOppositeColor(current))
                return false;
            if ((int)current.Rank != (int)next.Rank + 1)
                return false;
        }

        return true;
    }

    public int GetValidSequenceLength()
    {
        if (IsEmpty)
            return 0;

        int length = 1;
        for (int i = Count - 2; i >= 0; i--)
        {
            var current = Cards[i];
            var next = Cards[i + 1];

            if (!next.IsOppositeColor(current))
                break;
            if ((int)current.Rank != (int)next.Rank + 1)
                break;

            length++;
        }

        return length;
    }
}

public class FreeCellGame : ICardGame
{
    public const int TableauCount = 8;
    public const int FreeCellCount = 4;
    public const int FoundationCount = 4;

    public List<FreeCellTableauPile> Tableaus { get; set; } = new();
    public List<Card?> FreeCells { get; set; } = new();
    public List<FoundationPile> Foundations { get; set; } = new();
    public List<FreeCellMove> MoveHistory { get; set; } = new();

    [JsonIgnore]
    public bool IsGameWon => Foundations.All(f => f.IsComplete);

    [JsonIgnore]
    public int EmptyFreeCellCount => FreeCells.Count(c => c == null);

    [JsonIgnore]
    public int EmptyTableauCount => Tableaus.Count(t => t.IsEmpty);

    public FreeCellGame()
    {
        for (int i = 0; i < TableauCount; i++)
        {
            Tableaus.Add(new FreeCellTableauPile());
        }

        for (int i = 0; i < FreeCellCount; i++)
        {
            FreeCells.Add(null);
        }

        for (int i = 0; i < FoundationCount; i++)
        {
            Foundations.Add(new FoundationPile());
        }
    }

    public void NewGame(int? seed = null)
    {
        var deck = seed.HasValue ? new Deck(seed.Value) : new Deck();
        deck.Shuffle();

        // Clear all piles
        MoveHistory.Clear();

        for (int i = 0; i < FreeCellCount; i++)
        {
            FreeCells[i] = null;
        }

        foreach (var foundation in Foundations)
        {
            foundation.Clear();
            foundation.Suit = null;
        }

        foreach (var tableau in Tableaus)
        {
            tableau.Clear();
        }

        // Deal all 52 cards face up to tableaus
        // First 4 columns get 7 cards, last 4 get 6 cards
        int cardIndex = 0;
        for (int col = 0; col < TableauCount; col++)
        {
            int cardCount = col < 4 ? 7 : 6;
            for (int row = 0; row < cardCount; row++)
            {
                var card = deck.Draw();
                if (card != null)
                {
                    card.IsFaceUp = true; // All cards face up in FreeCell
                    Tableaus[col].AddCard(card);
                }
                cardIndex++;
            }
        }
    }

    /// <summary>
    /// Calculates the maximum number of cards that can be moved as a "supermove"
    /// Formula: (1 + empty free cells) * 2^(empty tableaus)
    /// </summary>
    public int GetMaxMovableCards(int excludeTableauIndex = -1)
    {
        int emptyFreeCells = EmptyFreeCellCount;
        int emptyTableaus = Tableaus.Where((t, i) => t.IsEmpty && i != excludeTableauIndex).Count();

        // (1 + empty free cells) * 2^(empty tableaus)
        return (1 + emptyFreeCells) * (1 << emptyTableaus);
    }

    public bool MoveTableauToFreeCell(int tableauIndex, int freeCellIndex)
    {
        if (tableauIndex < 0 || tableauIndex >= TableauCount)
            return false;
        if (freeCellIndex < 0 || freeCellIndex >= FreeCellCount)
            return false;

        var tableau = Tableaus[tableauIndex];
        if (tableau.IsEmpty)
            return false;

        if (FreeCells[freeCellIndex] != null)
            return false;

        var card = tableau.RemoveTopCard();
        FreeCells[freeCellIndex] = card;

        MoveHistory.Add(new FreeCellMove(FreeCellMoveType.TableauToFreeCell, tableauIndex, freeCellIndex));
        return true;
    }

    public bool MoveTableauToFoundation(int tableauIndex, int foundationIndex)
    {
        if (tableauIndex < 0 || tableauIndex >= TableauCount)
            return false;
        if (foundationIndex < 0 || foundationIndex >= FoundationCount)
            return false;

        var tableau = Tableaus[tableauIndex];
        if (tableau.IsEmpty)
            return false;

        var card = tableau.TopCard!;
        var foundation = Foundations[foundationIndex];

        if (!foundation.CanAcceptCard(card))
            return false;

        tableau.RemoveTopCard();
        foundation.AddCard(card);

        MoveHistory.Add(new FreeCellMove(FreeCellMoveType.TableauToFoundation, tableauIndex, foundationIndex));
        return true;
    }

    public bool MoveTableauToTableau(int sourceIndex, int cardIndex, int destinationIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= TableauCount)
            return false;
        if (destinationIndex < 0 || destinationIndex >= TableauCount)
            return false;
        if (sourceIndex == destinationIndex)
            return false;

        var source = Tableaus[sourceIndex];
        var destination = Tableaus[destinationIndex];

        if (cardIndex < 0 || cardIndex >= source.Count)
            return false;

        int cardsToMove = source.Count - cardIndex;
        int maxMovable = GetMaxMovableCards(destination.IsEmpty ? destinationIndex : -1);

        if (!source.CanPickupSequence(cardIndex, maxMovable))
            return false;

        var cardToMove = source.Cards[cardIndex];
        if (!destination.CanAcceptCard(cardToMove))
            return false;

        var cards = source.RemoveCardsFrom(cardIndex);
        destination.AddCards(cards);

        MoveHistory.Add(new FreeCellMove(FreeCellMoveType.TableauToTableau, sourceIndex, destinationIndex, cardsToMove));
        return true;
    }

    public bool MoveFreeCellToFoundation(int freeCellIndex, int foundationIndex)
    {
        if (freeCellIndex < 0 || freeCellIndex >= FreeCellCount)
            return false;
        if (foundationIndex < 0 || foundationIndex >= FoundationCount)
            return false;

        var card = FreeCells[freeCellIndex];
        if (card == null)
            return false;

        var foundation = Foundations[foundationIndex];
        if (!foundation.CanAcceptCard(card))
            return false;

        FreeCells[freeCellIndex] = null;
        foundation.AddCard(card);

        MoveHistory.Add(new FreeCellMove(FreeCellMoveType.FreeCellToFoundation, freeCellIndex, foundationIndex));
        return true;
    }

    public bool MoveFreeCellToTableau(int freeCellIndex, int tableauIndex)
    {
        if (freeCellIndex < 0 || freeCellIndex >= FreeCellCount)
            return false;
        if (tableauIndex < 0 || tableauIndex >= TableauCount)
            return false;

        var card = FreeCells[freeCellIndex];
        if (card == null)
            return false;

        var tableau = Tableaus[tableauIndex];
        if (!tableau.CanAcceptCard(card))
            return false;

        FreeCells[freeCellIndex] = null;
        tableau.AddCard(card);

        MoveHistory.Add(new FreeCellMove(FreeCellMoveType.FreeCellToTableau, freeCellIndex, tableauIndex));
        return true;
    }

    public bool AutoMoveToFoundation()
    {
        bool moved = false;

        // Try to move from tableaus
        for (int t = 0; t < TableauCount; t++)
        {
            var tableau = Tableaus[t];
            if (tableau.IsEmpty)
                continue;

            var card = tableau.TopCard!;
            for (int f = 0; f < FoundationCount; f++)
            {
                if (Foundations[f].CanAcceptCard(card))
                {
                    if (MoveTableauToFoundation(t, f))
                    {
                        moved = true;
                        break;
                    }
                }
            }
        }

        // Try to move from free cells
        for (int fc = 0; fc < FreeCellCount; fc++)
        {
            var card = FreeCells[fc];
            if (card == null)
                continue;

            for (int f = 0; f < FoundationCount; f++)
            {
                if (Foundations[f].CanAcceptCard(card))
                {
                    if (MoveFreeCellToFoundation(fc, f))
                    {
                        moved = true;
                        break;
                    }
                }
            }
        }

        return moved;
    }

    public List<FreeCellMove> GetValidMoves()
    {
        var moves = new List<FreeCellMove>();

        // Tableau to free cell
        for (int t = 0; t < TableauCount; t++)
        {
            if (Tableaus[t].IsEmpty)
                continue;

            for (int fc = 0; fc < FreeCellCount; fc++)
            {
                if (FreeCells[fc] == null)
                {
                    moves.Add(new FreeCellMove(FreeCellMoveType.TableauToFreeCell, t, fc));
                }
            }
        }

        // Tableau to foundation
        for (int t = 0; t < TableauCount; t++)
        {
            var tableau = Tableaus[t];
            if (tableau.IsEmpty)
                continue;

            var card = tableau.TopCard!;
            for (int f = 0; f < FoundationCount; f++)
            {
                if (Foundations[f].CanAcceptCard(card))
                {
                    moves.Add(new FreeCellMove(FreeCellMoveType.TableauToFoundation, t, f));
                }
            }
        }

        // Tableau to tableau (including multi-card moves)
        for (int s = 0; s < TableauCount; s++)
        {
            var source = Tableaus[s];
            if (source.IsEmpty)
                continue;

            for (int d = 0; d < TableauCount; d++)
            {
                if (s == d)
                    continue;

                var destination = Tableaus[d];
                int maxMovable = GetMaxMovableCards(destination.IsEmpty ? d : -1);
                int seqLength = source.GetValidSequenceLength();
                int movableCards = Math.Min(seqLength, maxMovable);

                // Try each valid starting position
                for (int startIdx = source.Count - movableCards; startIdx < source.Count; startIdx++)
                {
                    if (startIdx < 0)
                        continue;

                    var card = source.Cards[startIdx];
                    if (destination.CanAcceptCard(card) && source.CanPickupSequence(startIdx, maxMovable))
                    {
                        moves.Add(new FreeCellMove(FreeCellMoveType.TableauToTableau, s, d, source.Count - startIdx));
                    }
                }
            }
        }

        // Free cell to foundation
        for (int fc = 0; fc < FreeCellCount; fc++)
        {
            var card = FreeCells[fc];
            if (card == null)
                continue;

            for (int f = 0; f < FoundationCount; f++)
            {
                if (Foundations[f].CanAcceptCard(card))
                {
                    moves.Add(new FreeCellMove(FreeCellMoveType.FreeCellToFoundation, fc, f));
                }
            }
        }

        // Free cell to tableau
        for (int fc = 0; fc < FreeCellCount; fc++)
        {
            var card = FreeCells[fc];
            if (card == null)
                continue;

            for (int t = 0; t < TableauCount; t++)
            {
                if (Tableaus[t].CanAcceptCard(card))
                {
                    moves.Add(new FreeCellMove(FreeCellMoveType.FreeCellToTableau, fc, t));
                }
            }
        }

        return moves;
    }

    public string SaveGame()
    {
        return JsonSerializer.Serialize(this, SourceGenerationContext.Default.FreeCellGame);
    }

    public bool ValidateGame()
    {
        var allCards = new HashSet<(Suit, Rank)>();

        // Collect from tableaus
        foreach (var tableau in Tableaus)
        {
            foreach (var card in tableau.Cards)
            {
                if (!allCards.Add((card.Suit, card.Rank)))
                    return false; // Duplicate
            }
        }

        // Collect from free cells
        foreach (var card in FreeCells)
        {
            if (card != null)
            {
                if (!allCards.Add((card.Suit, card.Rank)))
                    return false;
            }
        }

        // Collect from foundations
        foreach (var foundation in Foundations)
        {
            foreach (var card in foundation.Cards)
            {
                if (!allCards.Add((card.Suit, card.Rank)))
                    return false;
            }
        }

        // Should have exactly 52 unique cards
        if (allCards.Count != 52)
            return false;

        // Validate foundation sequences
        foreach (var foundation in Foundations)
        {
            for (int i = 0; i < foundation.Count; i++)
            {
                if ((int)foundation.Cards[i].Rank != i + 1)
                    return false;

                if (i > 0 && foundation.Cards[i].Suit != foundation.Cards[0].Suit)
                    return false;
            }
        }

        // All tableau cards should be face up in FreeCell
        foreach (var tableau in Tableaus)
        {
            foreach (var card in tableau.Cards)
            {
                if (!card.IsFaceUp)
                    return false;
            }
        }

        return true;
    }

    public static ICardGame? LoadGame(string saveData)
    {
        return JsonSerializer.Deserialize<FreeCellGame>(saveData, SourceGenerationContext.Default.FreeCellGame);
    }
}
