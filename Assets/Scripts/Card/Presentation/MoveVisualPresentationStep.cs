using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Card/Presentation/Steps/Move Visual")]
public sealed class MoveVisualPresentationStep : CardPresentationStep
{
    public Vector2 offset;
    public bool relativeToSize = true;
    public float rotation;
    [Min(0)] public float duration = .1f;
    public AnimationCurve easing = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public override void Validate()
    {
        CheckDuration(duration); CheckFinite(offset.x); CheckFinite(offset.y); CheckFinite(rotation);
    }
    public override IEnumerator Play(CardPresentationContext context)
    {
        BattleCharacterPresentation view = context.View(target);
        if (view == null || view.Visual == null) yield break;
        Vector2 start = context.Offset(view), end = relativeToSize ? Vector2.Scale(offset, view.Visual.rect.size) : offset;
        float fromRotation = context.Rotation(view), elapsed = 0;
        while (context.CanContinue && elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easing != null ? easing.Evaluate(t) : t;
            context.Move(view, Vector2.LerpUnclamped(start, end, eased), Mathf.LerpUnclamped(fromRotation, rotation, eased));
            yield return null; elapsed += Time.deltaTime;
        }
        if (context.CanContinue) context.Move(view, end, rotation);
    }
}
