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
    public bool Loop { get; private set; } = true;
    public bool IsComplete => !Loop && clip != null && elapsed >= (double)clip.frameDuration * clip.frames.Length;
    public struct PlaybackState
    {
        internal UISpriteAnimation Clip;
        internal double Elapsed;
        internal bool Loop;
        internal Sprite Sprite;
    }
    private void OnEnable() { ResetPlayback(); }
    private void Update() { Advance(Time.deltaTime); }
    public void SetAnimation(UISpriteAnimation animation) => Play(animation, true);
    public void Play(UISpriteAnimation animation, bool loop)
    {
        if (animation != null) animation.Validate();
        clip = animation; Loop = loop; ResetPlayback();
    }
    public PlaybackState CaptureState()
    {
        if (targetImage == null) targetImage = GetComponent<UnityEngine.UI.Image>();
        return new PlaybackState { Clip = clip, Elapsed = elapsed, Loop = Loop, Sprite = targetImage.sprite };
    }
    public void RestoreState(PlaybackState state)
    {
        clip = state.Clip; elapsed = state.Elapsed; Loop = state.Loop;
        CurrentFrame = clip == null || clip.frames.Length == 0 ? 0 :
            Math.Min(clip.frames.Length - 1, (int)Math.Floor(elapsed / clip.frameDuration + .000001));
        if (targetImage == null) targetImage = GetComponent<UnityEngine.UI.Image>();
        targetImage.sprite = state.Sprite;
    }
    private void ResetPlayback()
    {
        elapsed = 0; CurrentFrame = 0;
        if (targetImage == null) targetImage = GetComponent<UnityEngine.UI.Image>();
        if (clip != null && clip.frames != null && clip.frames.Length > 0) targetImage.sprite = clip.frames[0];
    }
    public void Advance(float deltaTime)
    {
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (clip == null || clip.frames == null || clip.frames.Length == 0 || deltaTime == 0) return;
        double cycle = (double)clip.frameDuration * clip.frames.Length;
        if (cycle <= 0 || double.IsNaN(cycle) || double.IsInfinity(cycle)) return;
        elapsed = Loop ? (elapsed + deltaTime) % cycle : Math.Min(cycle, elapsed + deltaTime);
        int frame = Math.Min(clip.frames.Length - 1, (int)Math.Floor(elapsed / clip.frameDuration + .000001));
        if (frame == CurrentFrame) return;
        CurrentFrame = frame;
        if (targetImage == null) targetImage = GetComponent<UnityEngine.UI.Image>();
        targetImage.sprite = clip.frames[frame];
    }
}
