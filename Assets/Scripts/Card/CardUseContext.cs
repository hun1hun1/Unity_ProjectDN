// Immutable facts captured once after payment succeeds, shared by nested use-time effects.
public sealed class CardUseContext
{
    public CardData Card { get; }
    public bool IsOpening { get; }

    internal CardUseContext(CardData card, bool isFirstCard)
    {
        Card = card;
        IsOpening = isFirstCard && card != null && card.costMode == CardCostMode.Fixed;
    }
}
