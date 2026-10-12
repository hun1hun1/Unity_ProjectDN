using UnityEngine;

public abstract class EnemyAction : ScriptableObject
{
    [SerializeField]
    private Sprite actionIcon;

    [SerializeField]
    private string actionName;

    public Sprite GetActionIcon()
    {
        return actionIcon;
    }

    public string GetActionName()
    {
        return actionName;
    }

    public abstract string GetActionDescription();

    public virtual string GetActionDescription(BattleManager battleManager)
    {
        return GetActionDescription();
    }

    public abstract void Execute(BattleManager battleManager);
}
