using UnityEngine;

public abstract class CardEffect : ScriptableObject
{
    [SerializeField] protected CardTarget target;
    public abstract void Execute(BattleManager battleManager);
}
