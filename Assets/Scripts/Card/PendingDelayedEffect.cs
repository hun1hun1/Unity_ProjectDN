using System;
using UnityEngine;

// One record per execution, including multiple uses of the same shared asset.
public sealed class PendingDelayedEffect
{
    private readonly DelayedEffectSystem owner;
    internal CardEffect[] EffectSnapshot { get; }
    internal DelayEffect Definition { get; }
    public long Id { get; }
    public int SpentAmmo { get; }
    public CardData Card { get; }
    public long DueTurn { get; }
    public long RemainingTurns => Math.Max(0, DueTurn - owner.CurrentTurn);
    public Sprite Icon { get; }
    public string Description { get; }
    public ActiveContinuousCard ContinuousSource { get; }

    internal PendingDelayedEffect(DelayedEffectSystem owner, long id, DelayEffect effect,
        CardData card, ActiveContinuousCard source, int spentAmmo)
    {
        SpentAmmo = spentAmmo;
        this.owner = owner; Id = id; Definition = effect; Card = card; ContinuousSource = source;
        DueTurn = owner.CurrentTurn + effect.DelayTurns;
        EffectSnapshot = (CardEffect[])effect.Effects.Clone();
        Icon = effect.Icon != null ? effect.Icon : card != null ? card.artwork : null;
        Description = effect.Description;
    }
}
