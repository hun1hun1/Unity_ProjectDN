using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle UI/Sprite Animation")]
public class UISpriteAnimation : ScriptableObject
{
    [HideInInspector] public int setupVersion;
    public Sprite[] frames = Array.Empty<Sprite>();
    [Min(0.01f)] public float frameDuration = 0.1f;
    public Vector2 groundPivot = new Vector2(0.5f, 0.0625f);
    public Rect contentBounds = new Rect(0, 0, 1, 1);

    public void Validate()
    {
        if (float.IsNaN(frameDuration) || float.IsInfinity(frameDuration) || frameDuration <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameDuration));
        if (frames == null || frames.Length == 0) throw new InvalidOperationException("An animation needs frames.");
        foreach (Sprite frame in frames)
            if (frame == null || frame.rect.size != frames[0].rect.size)
                throw new InvalidOperationException("Animation frames must exist and have identical cell sizes.");
        if (!Finite(groundPivot.x) || !Finite(groundPivot.y) || groundPivot.x < 0 || groundPivot.x > 1 || groundPivot.y < 0 || groundPivot.y > 1)
            throw new InvalidOperationException("Ground pivot must be normalized.");
        if (!Finite(contentBounds.x) || !Finite(contentBounds.y) || !Finite(contentBounds.width) || !Finite(contentBounds.height) ||
            contentBounds.width <= 0 || contentBounds.height <= 0 || contentBounds.xMin < 0 || contentBounds.yMin < 0 || contentBounds.xMax > 1.0001f || contentBounds.yMax > 1.0001f)
            throw new InvalidOperationException("Content bounds must be a normalized nonempty rectangle.");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
