namespace DaCards;

public abstract class CardPile
{
    public List<Card> Cards { get; set; } = new();

    public int Count => Cards.Count;
    public bool IsEmpty => Cards.Count == 0;
    public Card? TopCard => Cards.Count > 0 ? Cards[^1] : null;

    public virtual void AddCard(Card card)
    {
        Cards.Add(card);
    }

    public virtual void AddCards(IEnumerable<Card> cards)
    {
        Cards.AddRange(cards);
    }

    public virtual Card? RemoveTopCard()
    {
        if (Cards.Count == 0)
            return null;

        var card = Cards[^1];
        Cards.RemoveAt(Cards.Count - 1);
        return card;
    }

    public virtual List<Card> RemoveCardsFrom(int index)
    {
        if (index < 0 || index >= Cards.Count)
            return new List<Card>();

        var removed = Cards.Skip(index).ToList();
        Cards.RemoveRange(index, Cards.Count - index);
        return removed;
    }

    public void Clear()
    {
        Cards.Clear();
    }
}

public class StockPile : CardPile
{
    public Card? DrawCard()
    {
        if (IsEmpty)
            return null;

        var card = Cards[^1];
        Cards.RemoveAt(Cards.Count - 1);
        card.IsFaceUp = true;
        return card;
    }

    public void Reset(List<Card> wasteCards)
    {
        // Reverse the waste cards and add them face down
        wasteCards.Reverse();
        foreach (var card in wasteCards)
        {
            card.IsFaceUp = false;
            Cards.Add(card);
        }
    }
}

public class WastePile : CardPile
{
    public void AddCardFromStock(Card card)
    {
        card.IsFaceUp = true;
        AddCard(card);
    }

    public List<Card> TakeAllCards()
    {
        var cards = Cards.ToList();
        Cards.Clear();
        return cards;
    }
}

public class FoundationPile : CardPile
{
    public Suit? Suit { get; set; }

    public bool CanAcceptCard(Card card)
    {
        if (IsEmpty)
        {
            // Foundation must start with Ace
            return card.Rank == Rank.Ace;
        }

        // Same suit and next rank
        return card.Suit == Suit && (int)card.Rank == (int)TopCard!.Rank + 1;
    }

    public override void AddCard(Card card)
    {
        if (IsEmpty)
        {
            Suit = card.Suit;
        }
        base.AddCard(card);
    }

    public bool IsComplete => Count == 13;
}

public class TableauPile : CardPile
{
    public bool CanAcceptCard(Card card)
    {
        if (IsEmpty)
        {
            // Empty tableau can only accept King
            return card.Rank == Rank.King;
        }

        var topCard = TopCard!;
        // Must be opposite color and one rank lower
        return topCard.IsFaceUp &&
               card.IsOppositeColor(topCard) &&
               (int)card.Rank == (int)topCard.Rank - 1;
    }

    public bool CanAcceptCards(List<Card> cards)
    {
        if (cards.Count == 0)
            return false;

        return CanAcceptCard(cards[0]);
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
        for (int i = 0; i < Cards.Count; i++)
        {
            if (Cards[i].IsFaceUp)
                return i;
        }
        return -1;
    }

    public List<Card> GetFaceUpCards()
    {
        return Cards.Where(c => c.IsFaceUp).ToList();
    }
}
