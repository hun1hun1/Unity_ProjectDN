using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Card/Presentation/Steps/Projectile")]
public sealed class ProjectilePresentationStep : CardPresentationStep
{
    public GameObject prefab;
    public PresentationTarget destination = PresentationTarget.Enemy;
    public Vector2 startOffset;
    public Vector2 endOffset;
    [Min(0)] public float duration = .15f;
    public override void Validate()
    {
        CheckDuration(duration); CheckFinite(startOffset.x); CheckFinite(startOffset.y);
        CheckFinite(endOffset.x); CheckFinite(endOffset.y);
    }
    public override IEnumerator Play(CardPresentationContext context)
    {
        RectTransform from = context.Visual(target), to = context.Visual(destination);
        if (prefab == null || from == null || to == null) yield break;
        GameObject item = context.Spawn(prefab, from.parent);
        Vector3 start = from.TransformPoint(startOffset), end = to.TransformPoint(endOffset);
        float elapsed = 0;
        try
        {
            while (context.CanContinue && item != null && elapsed < duration)
            {
                item.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                yield return null; elapsed += Time.deltaTime;
            }
            if (item != null) item.transform.position = end;
        }
        finally { context.Release(item); }
    }
}
