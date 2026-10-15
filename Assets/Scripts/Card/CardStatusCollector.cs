using System.Collections.Generic;

// Reads effect metadata without executing effects or touching live battle state.
public sealed class CardStatusCollector
{
    private readonly List<StatusEffectData> statuses = new List<StatusEffectData>();
    private readonly HashSet<StatusEffectType> statusTypes = new HashSet<StatusEffectType>();
    private readonly HashSet<CardEffect> visited = new HashSet<CardEffect>();

    private CardStatusCollector() { }

    public static IReadOnlyList<StatusEffectData> Collect(CardData card)
    {
        var collector = new CardStatusCollector();
        if (card != null)
        {
            collector.CollectEffects(card.effects);
            if (card.type == CardType.Continuous) collector.CollectEffects(card.turnStartEffects);
        }
        return collector.statuses;
    }

    public void Add(StatusEffectData status)
    {
        if (status != null && statusTypes.Add(status.Type)) statuses.Add(status);
    }

    public void CollectEffects(CardEffect[] effects)
    {
        if (effects == null) return;
        foreach (CardEffect effect in effects)
            if (effect != null && visited.Add(effect)) effect.CollectReferencedStatuses(this);
    }
}
