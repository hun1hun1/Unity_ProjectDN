using System.Collections.Generic;
using UnityEngine;

public abstract class EnemyAction : ScriptableObject
{
    [SerializeField]
    private Sprite actionIcon;

    [SerializeField]
    private string actionName;

    [SerializeField]
    [Tooltip("Shared icons by action kinds. Composite combinations take priority; Action Icon is the fallback.")]
    private EnemyActionIconSet iconSet;

    public virtual EnemyActionKinds GetActionKinds() => EnemyActionKinds.None;
    protected Sprite GetKindIcon(EnemyActionKinds kinds) => iconSet != null ? iconSet.GetIcon(kinds) : null;
    public void Validate() => Validate(new HashSet<EnemyAction>());
    internal virtual void Validate(HashSet<EnemyAction> visiting) { }
    internal virtual string Preview(EnemyActionPreviewContext context) => GetActionDescription(context.Battle);

    public virtual Sprite GetActionIcon()
    {
        return actionIcon != null ? actionIcon : GetKindIcon(GetActionKinds());
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
