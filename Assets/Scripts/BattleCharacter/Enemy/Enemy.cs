using UnityEngine;

public class Enemy : BattleCharacter
{
    [SerializeField]
    private EnemyBehavior behavior;

    [SerializeField]
    private EnemyActionUI actionUI;

    private int currentActionIndex;

    protected override void Awake()
    {
        base.Awake();

        currentActionIndex = 0;

        UpdateNextActionUI(behavior.GetAction(currentActionIndex));
    }

    protected override void Die()
    {
        Debug.Log($"{gameObject.name}이(가) 사망했습니다.");

        // 적 사망 처리
    }

    public void EnemyTurn(BattleManager battleManager)
    {
        if (behavior == null) return;

        EnemyAction action = behavior.GetAction(currentActionIndex);

        if (action != null)
        {
            action.Execute(battleManager);
        }

        currentActionIndex++;

        if (currentActionIndex >= behavior.GetActionCount())
        {
            currentActionIndex = 0;
        }

        UpdateNextActionUI(behavior.GetAction(currentActionIndex));
    }

    private void UpdateNextActionUI(EnemyAction action)
    {
        if (action == null)
        {
            actionUI.ClearAction();
            return;
        }

        actionUI.SetAction(action);
    }
}