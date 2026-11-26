using System.Collections;
using UnityEngine;

[CreateAssetMenu(menuName = "Card/Presentation/Steps/Play Sound")]
public sealed class PlaySoundPresentationStep : CardPresentationStep
{
    public AudioClip clip;
    [Range(0, 1)] public float volume = 1;
    public bool waitForCompletion;
    [Tooltip("Allow playback to finish while the battle scene remains open.")]
    public bool finishPlayingAfterPresentation;
    public override void Validate() { CheckFinite(volume); if (volume < 0 || volume > 1) throw new System.ArgumentOutOfRangeException(nameof(volume)); }
    public override IEnumerator Play(CardPresentationContext context)
    {
        if (clip == null) yield break;
        if (finishPlayingAfterPresentation) context.PlaySoundToEnd(clip, volume);
        else context.Sound(clip, volume);
        if (waitForCompletion) yield return context.Wait(clip.length);
    }
}
