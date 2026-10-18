using UnityEngine;

public class Enemy : BattleCharacter
{
    [SerializeField]
    private EnemyBehavior behavior;

    [SerializeField]
    private EnemyActionUI actionUI;

    private int currentActionIndex;
    private BattleManager battleContext;
    private Player subscribedPlayer;

    public void ConfigureEncounter(BattleEncounterDefinition encounter)
    {
        if (encounter == null) throw new System.ArgumentNullException(nameof(encounter));
        encounter.Validate();
        behavior = encounter.behavior;
        currentActionIndex = 0;
        RestoreHealth(encounter.enemyMaxHP, encounter.enemyMaxHP);
        RefreshNextActionUI();
    }

    public void BindBattle(BattleManager battleManager)
    {
        UnsubscribeStatuses();
        battleContext = battleManager;
        if (isActiveAndEnabled) SubscribeStatuses();
        RefreshNextActionUI();
    }

    private void OnEnable()
    {
        SubscribeStatuses();
        RefreshNextActionUI();
    }

    private void OnDisable() { UnsubscribeStatuses(); }

    private void SubscribeStatuses()
    {
        UnsubscribeStatuses();
        StatusEffects.Changed += RefreshNextActionUI;
        subscribedPlayer = battleContext != null ? battleContext.GetPlayer() : null;
        if (subscribedPlayer != null) subscribedPlayer.StatusEffects.Changed += RefreshNextActionUI;
    }

    private void UnsubscribeStatuses()
    {
        StatusEffects.Changed -= RefreshNextActionUI;
        if (subscribedPlayer != null) subscribedPlayer.StatusEffects.Changed -= RefreshNextActionUI;
        subscribedPlayer = null;
    }

    private void RefreshNextActionUI()
    {
        UpdateNextActionUI(behavior != null ? behavior.GetAction(currentActionIndex) : null);
    }

    protected override void Awake()
    {
        base.Awake();

        currentActionIndex = 0;

        RefreshNextActionUI();
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

        RefreshNextActionUI();
    }

    private void UpdateNextActionUI(EnemyAction action)
    {
        if (actionUI == null) return;
        if (action == null)
        {
            actionUI.ClearAction();
            return;
        }

        actionUI.SetAction(action, battleContext);
    }
}