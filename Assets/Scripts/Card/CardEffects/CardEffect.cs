using UnityEngine;

public abstract class CardEffect : ScriptableObject
{
    [SerializeField] protected CardTarget target;
    public abstract void Execute(BattleManager battleManager);
    public virtual void CollectReferencedStatuses(CardStatusCollector collector) { }
    public virtual void Preview(CardPreviewContext context, string path) { }
}
