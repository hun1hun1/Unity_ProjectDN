using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EnemyCompositeAction", menuName = "Enemy/Action/Composite")]
public class EnemyCompositeAction : EnemyAction
{
    [Tooltip("Executed in order as ONE enemy turn. Null entries are skipped; nested composites are allowed without cycles.")]
    [SerializeField] private EnemyAction[] actions = Array.Empty<EnemyAction>();

    internal override void Validate(HashSet<EnemyAction> visiting)
    {
        base.Validate(visiting);
        if (!visiting.Add(this)) throw new InvalidOperationException($"Composite action '{name}' contains a cycle.");
        try
        {
            bool hasAction = false;
            if (actions != null)
                foreach (EnemyAction action in actions)
                {
                    if (action == null) continue;
                    hasAction = true;
                    action.Validate(visiting);
                }
            if (!hasAction) throw new InvalidOperationException($"Composite action '{name}' needs at least one action.");
        }
        finally { visiting.Remove(this); }
    }
    public override EnemyActionKinds GetActionKinds()
    {
        Validate();
        EnemyActionKinds kinds = EnemyActionKinds.None;
        foreach (EnemyAction action in actions)
            if (action != null) kinds |= action.GetActionKinds();
        return kinds;
    }
    public override Sprite GetActionIcon()
    {
        // The combination takes precedence over the explicit fallback icon.
        Sprite selected = GetKindIcon(GetActionKinds());
        return selected != null ? selected : base.GetActionIcon();
    }
    public override string GetActionDescription() => GetActionDescription(null);
    public override string GetActionDescription(BattleManager battle) => Preview(new EnemyActionPreviewContext(battle));
    internal override string Preview(EnemyActionPreviewContext context)
    {
        Validate();
        var descriptions = new List<string>();
        foreach (EnemyAction action in actions)
        {
            if (!context.CanContinue) break;
            if (action != null) descriptions.Add(action.Preview(context));
        }
        return string.Join("\n", descriptions);
    }
    protected override IEnumerator ExecuteEffectsRoutine(BattleManager battle)
    {
        foreach (EnemyAction action in actions)
        {
            if (!battle.CanContinueEffects) yield break;
            if (action != null) yield return action.ExecuteRoutine(battle);
        }
    }
    public override void Execute(BattleManager battle)
    {
        // Validate the entire graph before changing any combat state.
        Validate();
        if (battle == null) return;
        foreach (EnemyAction action in actions)
        {
            if (!battle.CanContinueEffects) break;
            if (action != null) action.Execute(battle);
        }
    }
}
