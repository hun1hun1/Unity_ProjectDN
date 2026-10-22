using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Card/Presentation/Steps/Play Sprite")]
public sealed class PlaySpritePresentationStep : CardPresentationStep
{
    public UISpriteAnimation animation;
    public bool waitForCompletion = true;
    public override void Validate() { if (animation != null) animation.Validate(); }
    public override IEnumerator Play(CardPresentationContext context)
    {
        RectTransform visual = context.Visual(target);
        UISpriteAnimator animator = visual != null ? visual.GetComponent<UISpriteAnimator>() : null;
        if (animator == null || !animator.isActiveAndEnabled || animation == null) yield break;
        context.Play(animator, animation);
        while (context.CanContinue && animator != null && animator.isActiveAndEnabled &&
            animator.Animation == animation && waitForCompletion && !animator.IsComplete) yield return null;
    }
}
