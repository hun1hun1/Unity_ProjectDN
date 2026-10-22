using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Card/Presentation/Steps/Spawn VFX")]
public sealed class SpawnVfxPresentationStep : CardPresentationStep
{
    public GameObject prefab;
    public Vector2 offset;
    [Min(0)] public float duration;
    public override void Validate() { CheckDuration(duration); CheckFinite(offset.x); CheckFinite(offset.y); }
    public override IEnumerator Play(CardPresentationContext context)
    {
        RectTransform visual = context.Visual(target);
        if (visual == null || prefab == null) yield break;
        GameObject item = context.Spawn(prefab, visual);
        item.transform.localPosition = new Vector3(offset.x, offset.y, 0);
        if (duration > 0) { yield return context.Wait(duration); context.Release(item); }
    }
}
