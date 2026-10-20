using System;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Image))]
public class UISpriteAnimator : MonoBehaviour
{
    [SerializeField] private UISpriteAnimation clip;
    private UnityEngine.UI.Image targetImage;
    private double elapsed;
    public int CurrentFrame { get; private set; }
    public UISpriteAnimation Animation => clip;

    private void OnEnable() { ResetPlayback(); }
    private void Update() { Advance(Time.deltaTime); }

    public void SetAnimation(UISpriteAnimation animation)
    {
        if (animation != null) animation.Validate();
        clip = animation;
        ResetPlayback();
    }

    private void ResetPlayback()
    {
        elapsed = 0;
        CurrentFrame = 0;
        if (targetImage == null) targetImage = GetComponent<UnityEngine.UI.Image>();
        if (clip != null && clip.frames != null && clip.frames.Length > 0)
            targetImage.sprite = clip.frames[0];
    }

    public void Advance(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (clip == null || clip.frames == null || clip.frames.Length == 0 || deltaTime == 0) return;
        double cycle = (double)clip.frameDuration * clip.frames.Length;
        if (cycle <= 0 || double.IsNaN(cycle) || double.IsInfinity(cycle)) return;
        elapsed = (elapsed + deltaTime) % cycle;
        int frame = Math.Min(clip.frames.Length - 1, (int)Math.Floor(elapsed / clip.frameDuration + 0.000001));
        if (frame == CurrentFrame) return;
        CurrentFrame = frame;
        if (targetImage == null) targetImage = GetComponent<UnityEngine.UI.Image>();
        targetImage.sprite = clip.frames[frame];
    }
}
