using System.Text.Json;
using System.Text.Json.Serialization;

namespace DaCards;

public enum MoveType
{
    DrawFromStock,
    ResetStock,
    WasteToFoundation,
    WasteToTableau,
    TableauToFoundation,
    TableauToTableau
}

public class Move
{
    public MoveType Type { get; set; }
    public int SourceIndex { get; set; }
    public int DestinationIndex { get; set; }
    public int CardCount { get; set; }

    public Move()
    {
    }

    public Move(MoveType type, int sourceIndex = -1, int destinationIndex = -1, int cardCount = 1)
    {
        Type = type;
        SourceIndex = sourceIndex;
        DestinationIndex = destinationIndex;
        CardCount = cardCount;
    }
}

public class SolitaireGame : ICardGame
{
    public StockPile Stock { get; set; } = new();
    public WastePile Waste { get; set; } = new();
    public List<FoundationPile> Foundations { get; set; } = new();
    public List<TableauPile> Tableaus { get; set; } = new();
    public List<Move> MoveHistory { get; set; } = new();
    public int DrawCount { get; set; } = 1; // 1 or 3 card draw

    [JsonIgnore]
    public bool IsGameWon => Foundations.All(f => f.IsComplete);

    public SolitaireGame()
    {
        // Initialize empty foundations
        for (int i = 0; i < 4; i++)
        {
            Foundations.Add(new FoundationPile());
        }

        // Initialize empty tableaus
        for (int i = 0; i < 7; i++)
        {
            Tableaus.Add(new TableauPile());
        }
    }

    public void NewGame(int? seed = null)
    {
        var deck = seed.HasValue ? new Deck(seed.Value) : new Deck();
        deck.Shuffle();

        // Clear all piles
        Stock.Clear();
        Waste.Clear();
        MoveHistory.Clear();

        foreach (var foundation in Foundations)
        {
            foundation.Clear();
            foundation.Suit = null;
        }

        foreach (var tableau in Tableaus)
        {
            tableau.Clear();
        }

        // Deal to tableaus: column i gets i+1 cards
        for (int col = 0; col < 7; col++)
        {
            for (int row = 0; row <= col; row++)
            {
                var card = deck.Draw();
                if (card != null)
                {
                    // Only the top card (last dealt) is face up
                    card.IsFaceUp = (row == col);
                    Tableaus[col].AddCard(card);
                }
            }
        }

        // Remaining cards go to stock
        while (deck.Count > 0)
        {
            var card = deck.Draw();
            if (card != null)
            {
                card.IsFaceUp = false;
                Stock.AddCard(card);
            }
        }
    }

    public bool DrawFromStock()
    {
        if (Stock.IsEmpty)
        {
            return false;
        }

        for (int i = 0; i < DrawCount && !Stock.IsEmpty; i++)
        {
            var card = Stock.DrawCard();
            if (card != null)
            {
                Waste.AddCardFromStock(card);
            }
        }

        MoveHistory.Add(new Move(MoveType.DrawFromStock));
        return true;
    }

    public bool ResetStock()
    {
        if (!Stock.IsEmpty || Waste.IsEmpty)
        {
            return false;
        }

        var wasteCards = Waste.TakeAllCards();
        Stock.Reset(wasteCards);

        MoveHistory.Add(new Move(MoveType.ResetStock));
        return true;
    }

    public bool MoveWasteToFoundation(int foundationIndex)
    {
        if (foundationIndex < 0 || foundationIndex >= 4)
            return false;

        if (Waste.IsEmpty)
            return false;

        var card = Waste.TopCard!;
        var foundation = Foundations[foundationIndex];

        if (!foundation.CanAcceptCard(card))
            return false;

        Waste.RemoveTopCard();
        foundation.AddCard(card);

        MoveHistory.Add(new Move(MoveType.WasteToFoundation, -1, foundationIndex));
        return true;
    }

    public bool MoveWasteToTableau(int tableauIndex)
    {
        if (tableauIndex < 0 || tableauIndex >= 7)
            return false;

        if (Waste.IsEmpty)
            return false;

        var card = Waste.TopCard!;
        var tableau = Tableaus[tableauIndex];

        if (!tableau.CanAcceptCard(card))
            return false;

        Waste.RemoveTopCard();
        tableau.AddCard(card);

        MoveHistory.Add(new Move(MoveType.WasteToTableau, -1, tableauIndex));
        return true;
    }

    public bool MoveTableauToFoundation(int tableauIndex, int foundationIndex)
    {
        if (tableauIndex < 0 || tableauIndex >= 7)
            return false;
        if (foundationIndex < 0 || foundationIndex >= 4)
            return false;

        var tableau = Tableaus[tableauIndex];
        if (tableau.IsEmpty)
            return false;

        var card = tableau.TopCard!;
        if (!card.IsFaceUp)
            return false;

        var foundation = Foundations[foundationIndex];
        if (!foundation.CanAcceptCard(card))
            return false;

        tableau.RemoveTopCard();
        foundation.AddCard(card);

        // Flip the new top card if needed
        tableau.FlipTopCard();

        MoveHistory.Add(new Move(MoveType.TableauToFoundation, tableauIndex, foundationIndex));
        return true;
    }

    public bool MoveTableauToTableau(int sourceIndex, int cardIndex, int destinationIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= 7)
            return false;
        if (destinationIndex < 0 || destinationIndex >= 7)
            return false;
        if (sourceIndex == destinationIndex)
            return false;

        var source = Tableaus[sourceIndex];
        var destination = Tableaus[destinationIndex];

        if (cardIndex < 0 || cardIndex >= source.Count)
            return false;

        var card = source.Cards[cardIndex];
        if (!card.IsFaceUp)
            return false;

        if (!destination.CanAcceptCard(card))
            return false;

        var cardsToMove = source.RemoveCardsFrom(cardIndex);
        destination.AddCards(cardsToMove);

        // Flip the new top card if needed
        source.FlipTopCard();

        MoveHistory.Add(new Move(MoveType.TableauToTableau, sourceIndex, destinationIndex, cardsToMove.Count));
        return true;
    }

    public bool AutoMoveToFoundation(int tableauIndex)
    {
        if (tableauIndex < 0 || tableauIndex >= 7)
            return false;

        var tableau = Tableaus[tableauIndex];
        if (tableau.IsEmpty || !tableau.TopCard!.IsFaceUp)
            return false;

        var card = tableau.TopCard;

        for (int i = 0; i < 4; i++)
        {
            if (Foundations[i].CanAcceptCard(card))
            {
                return MoveTableauToFoundation(tableauIndex, i);
            }
        }

        return false;
    }

    public bool AutoMoveWasteToFoundation()
    {
        if (Waste.IsEmpty)
            return false;

        var card = Waste.TopCard!;

        for (int i = 0; i < 4; i++)
        {
            if (Foundations[i].CanAcceptCard(card))
            {
                return MoveWasteToFoundation(i);
            }
        }

        return false;
    }

    public List<Move> GetValidMoves()
    {
        var moves = new List<Move>();

        // Draw from stock
        if (!Stock.IsEmpty)
        {
            moves.Add(new Move(MoveType.DrawFromStock));
        }

        // Reset stock
        if (Stock.IsEmpty && !Waste.IsEmpty)
        {
            moves.Add(new Move(MoveType.ResetStock));
        }

        // Waste to foundation
        if (!Waste.IsEmpty)
        {
            var wasteCard = Waste.TopCard!;
            for (int i = 0; i < 4; i++)
            {
                if (Foundations[i].CanAcceptCard(wasteCard))
                {
                    moves.Add(new Move(MoveType.WasteToFoundation, -1, i));
                }
            }

            // Waste to tableau
            for (int i = 0; i < 7; i++)
            {
                if (Tableaus[i].CanAcceptCard(wasteCard))
                {
                    moves.Add(new Move(MoveType.WasteToTableau, -1, i));
                }
            }
        }

        // Tableau to foundation
        for (int t = 0; t < 7; t++)
        {
            var tableau = Tableaus[t];
            if (!tableau.IsEmpty && tableau.TopCard!.IsFaceUp)
            {
                for (int f = 0; f < 4; f++)
                {
                    if (Foundations[f].CanAcceptCard(tableau.TopCard))
                    {
                        moves.Add(new Move(MoveType.TableauToFoundation, t, f));
                    }
                }
            }
        }

        // Tableau to tableau
        for (int s = 0; s < 7; s++)
        {
            var source = Tableaus[s];
            var firstFaceUp = source.GetFirstFaceUpIndex();

            if (firstFaceUp < 0)
                continue;

            for (int cardIdx = firstFaceUp; cardIdx < source.Count; cardIdx++)
            {
                var card = source.Cards[cardIdx];
                for (int d = 0; d < 7; d++)
                {
                    if (s != d && Tableaus[d].CanAcceptCard(card))
                    {
                        moves.Add(new Move(MoveType.TableauToTableau, s, d, source.Count - cardIdx));
                    }
                }
            }
        }

        return moves;
    }

    public string SaveGame()
    {
        return JsonSerializer.Serialize(this, SourceGenerationContext.Default.SolitaireGame);
    }

    public bool ValidateGame()
    {
        // Check that all cards are accounted for (52 total)
        var allCards = new HashSet<(Suit, Rank)>();

        // Collect from all locations
        foreach (var card in Stock.Cards)
        {
            if (!allCards.Add((card.Suit, card.Rank)))
                return false; // Duplicate
        }

        foreach (var card in Waste.Cards)
        {
            if (!allCards.Add((card.Suit, card.Rank)))
                return false;
        }

        foreach (var foundation in Foundations)
        {
            foreach (var card in foundation.Cards)
            {
                if (!allCards.Add((card.Suit, card.Rank)))
                    return false;
            }
        }

        foreach (var tableau in Tableaus)
        {
            foreach (var card in tableau.Cards)
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

        // Validate tableau sequences
        foreach (var tableau in Tableaus)
        {
            var faceUpStarted = false;
            for (int i = 0; i < tableau.Count; i++)
            {
                var card = tableau.Cards[i];
                if (card.IsFaceUp)
                {
                    faceUpStarted = true;
                    if (i > 0 && tableau.Cards[i - 1].IsFaceUp)
                    {
                        var prevCard = tableau.Cards[i - 1];
                        // Must be opposite color and descending
                        if (!card.IsOppositeColor(prevCard))
                            return false;
                        if ((int)card.Rank != (int)prevCard.Rank - 1)
                            return false;
                    }
                }
                else if (faceUpStarted)
                {
                    // Face-down card after face-up is invalid
                    return false;
                }
            }
        }

        return true;
    }

    public static ICardGame? LoadGame(string saveData)
    {
        return JsonSerializer.Deserialize<SolitaireGame>(saveData, SourceGenerationContext.Default.SolitaireGame);
    }
}
