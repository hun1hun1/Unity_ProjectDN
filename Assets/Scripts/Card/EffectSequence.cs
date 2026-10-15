using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Run immediate effects before returning, and suspend only for a real asynchronous step.
public static class EffectSequence
{
    private sealed class Pending
    {
        public IEnumerator Flat;
        public bool Disposed;
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true; (Flat as IDisposable)?.Dispose();
        }
    }
    private static readonly Dictionary<MonoBehaviour, List<Pending>> pending = new Dictionary<MonoBehaviour, List<Pending>>();

    public static void Stop(MonoBehaviour owner)
    {
        if (!pending.TryGetValue(owner, out List<Pending> work)) return;
        pending.Remove(owner);
        foreach (Pending item in work.ToArray()) item.Dispose();
    }

    public static void Start(MonoBehaviour owner, IEnumerator sequence)
    {
        if (owner == null || sequence == null) return;
        IEnumerator flat = Flatten(sequence);
        try
        {
            if (!flat.MoveNext()) { (flat as IDisposable)?.Dispose(); return; }
            if (!Application.isPlaying || !owner.isActiveAndEnabled)
            {
                (flat as IDisposable)?.Dispose();
                return;
            }
            var item = new Pending { Flat = flat };
            if (!pending.TryGetValue(owner, out List<Pending> work)) pending.Add(owner, work = new List<Pending>());
            work.Add(item);
            owner.StartCoroutine(Resume(owner, item, flat.Current));
        }
        catch { (flat as IDisposable)?.Dispose(); throw; }
    }

    private static IEnumerator Resume(MonoBehaviour owner, Pending item, object first)
    {
        try
        {
            yield return first;
            while (!item.Disposed && item.Flat.MoveNext()) yield return item.Flat.Current;
        }
        finally
        {
            if (pending.TryGetValue(owner, out List<Pending> work))
            {
                work.Remove(item); if (work.Count == 0) pending.Remove(owner);
            }
            item.Dispose();
        }
    }

    private static IEnumerator Flatten(IEnumerator sequence)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(sequence);
        try
        {
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop(); (current as IDisposable)?.Dispose();
                    continue;
                }
                if (current.Current is IEnumerator nested) stack.Push(nested);
                else yield return current.Current;
            }
        }
        finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
    }
}
