using System;
using System.Collections;
using UnityEngine;

public enum PresentationTarget { Player, Enemy }

public abstract class CardPresentationStep : ScriptableObject
{
    public PresentationTarget target;
    public abstract IEnumerator Play(CardPresentationContext context);
    public virtual void Validate() { }
    protected static void CheckDuration(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Presentation duration must be finite and nonnegative.");
    }
    protected static void CheckFinite(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
