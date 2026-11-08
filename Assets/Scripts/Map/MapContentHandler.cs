using System;
using UnityEngine;

public abstract class MapContentHandler : MonoBehaviour
{
    public abstract MapContentType ContentType { get; }
    public abstract void Begin(MapChoice choice, RunProgress progress, Func<bool> complete);
    // Begin constructs the UI, then RestoreState reapplies fixed offers and committed receipts.
    public virtual string CaptureState() => "";
    public virtual void RestoreState(string state) { }
    protected RunNodeVisit CaptureVisit(RunProgress progress) => new RunNodeVisit(this,progress);
    protected bool CommitAcquisition(RunNodeVisit visit,Action acquisition)
    {
        if(this==null || !isActiveAndEnabled || visit==null || visit.Handler!=this)return false;
        RunSession session=RunSession.Instance;
        return session!=null && session.CommitContent(visit,acquisition);
    }
}

// Capture once at entry and retain this immutable visit in each UI callback.
public sealed class RunNodeVisit
{
    internal RunProgress Progress { get; }
    internal int Floor { get; }
    internal int Node { get; }
    internal MapContentHandler Handler { get; }
    internal RunNodeVisit(MapContentHandler handler,RunProgress progress)
    {
        Progress=progress ?? throw new ArgumentNullException(nameof(progress));
        Handler=handler;Floor=progress.FloorIndex;Node=progress.NodeIndex;
    }
}
