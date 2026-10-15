using System.Collections;
using UnityEngine;

public abstract class CardEffect : ScriptableObject
{
    [SerializeField] protected CardTarget target;
    public abstract void Execute(BattleManager battleManager);

    // Null means that the effect already completed synchronously.
    public virtual IEnumerator ExecuteRoutine(BattleManager battleManager)
    {
        Execute(battleManager);
        return null;
    }

    public virtual IEnumerator ActivateContinuousRoutine(BattleManager battleManager, ActiveContinuousCard source)
    {
        source.AddRelease(ActivateContinuous(battleManager, source));
        return null;
    }

    // Immediate effects run once. Sustained effects return a per-use release callback.
    public virtual System.Action ActivateContinuous(BattleManager battleManager, ActiveContinuousCard source)
    {
        Execute(battleManager);
        return null;
    }
    public virtual void CollectReferencedStatuses(CardStatusCollector collector) { }
    public virtual void Preview(CardPreviewContext context, string path) { }
}
