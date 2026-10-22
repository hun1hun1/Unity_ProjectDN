using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Card/Presentation/Steps/Wait")]
public sealed class WaitPresentationStep : CardPresentationStep
{
    [Min(0)] public float duration = .04f;
    public override void Validate() => CheckDuration(duration);
    public override IEnumerator Play(CardPresentationContext context) => context.Wait(duration);
}
