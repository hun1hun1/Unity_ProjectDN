using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Card/Presentation/Sequence")]
public sealed class CardPresentation : ScriptableObject
{
    public CardPresentationStep[] beforeImpact = Array.Empty<CardPresentationStep>();
    public CardPresentationStep[] afterImpact = Array.Empty<CardPresentationStep>();
    public void Validate()
    {
        ValidateSteps(beforeImpact); ValidateSteps(afterImpact);
    }
    private static void ValidateSteps(CardPresentationStep[] steps)
    {
        if (steps == null) return;
        foreach (CardPresentationStep step in steps) if (step != null) step.Validate();
    }
    public IEnumerator PlayBefore(CardPresentationContext context) => Play(beforeImpact, context);
    public IEnumerator PlayAfter(CardPresentationContext context) => Play(afterImpact, context);
    private static IEnumerator Play(CardPresentationStep[] steps, CardPresentationContext context)
    {
        if (steps == null) yield break;
        foreach (CardPresentationStep step in steps)
        {
            if (!context.CanContinue) yield break;
            if (step != null) yield return step.Play(context);
        }
    }
}
