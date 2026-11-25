using System.Collections;
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

    [Header("Presentation")]
    public CardPresentation presentation;

    public virtual EnemyActionKinds GetActionKinds() => EnemyActionKinds.None;
    protected Sprite GetKindIcon(EnemyActionKinds kinds) => iconSet != null ? iconSet.GetIcon(kinds) : null;
    public void Validate() => Validate(new HashSet<EnemyAction>());
    internal virtual void Validate(HashSet<EnemyAction> visiting) { presentation?.Validate(); }
    internal virtual string Preview(EnemyActionPreviewContext context)
    {
        string description = GetActionDescription(context.Battle);
        context.AddIntent($"{description} 행동을 합니다");
        return description;
    }

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

    // Actual turns use this bridge. Execute remains the immediate rules-only API.
    public IEnumerator ExecuteRoutine(BattleManager battle)
    {
        if (battle == null || !battle.CanContinueEffects) yield break;
        Enemy actor = battle.GetTarget();
        if (actor == null || !actor.isActiveAndEnabled || actor.GetCurrentHP() <= 0) yield break;
        Validate();
        battle.BindBattleCharacters();
        if (presentation == null) { yield return ExecuteEffectsRoutine(battle); yield break; }
        using (var context = new CardPresentationContext(battle, PresentationTarget.Enemy))
        {
            if (!context.CanContinue) yield break;
            if (context.Visual(PresentationTarget.Enemy) == null)
            {
                yield return ExecuteEffectsRoutine(battle);
                yield break;
            }
            yield return presentation.PlayBefore(context);
            if (!context.CanContinue) yield break;
            yield return ExecuteEffectsRoutine(battle);
            if (context.CanContinue) yield return presentation.PlayAfter(context);
        }
    }

    protected virtual IEnumerator ExecuteEffectsRoutine(BattleManager battle)
    {
        Execute(battle);
        yield break;
    }

    public abstract void Execute(BattleManager battleManager);
}
