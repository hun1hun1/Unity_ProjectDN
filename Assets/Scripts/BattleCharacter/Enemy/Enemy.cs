using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : BattleCharacter
{
    [SerializeField]
    private EnemyBehavior behavior;

    [SerializeField]
    private EnemyActionUI actionUI;

    private bool isActing;
    private int currentActionIndex;
    private EnemyBehavior preparedBehavior;
    private EnemyAction nextAction;
    private EnemyAction lastAction;
    private int consecutiveCount;
    private readonly List<EnemyAction> actionCandidates = new List<EnemyAction>();
    public EnemyAction NextAction => nextAction;
    private BattleManager battleContext;
    private Player subscribedPlayer;

    public void ConfigureEncounter(BattleEncounterDefinition encounter)
    {
        if (encounter == null) throw new System.ArgumentNullException(nameof(encounter));
        encounter.Validate();
        behavior = encounter.behavior;
        ResetActionPattern();
        RestoreHealth(encounter.enemyMaxHP, encounter.enemyMaxHP);
        RefreshNextActionUI();
    }

    public void BindBattle(BattleManager battleManager)
    {
        UnsubscribeStatuses();
        bool changedBattle = battleContext != null && battleContext != battleManager;
        battleContext = battleManager;
        if (changedBattle) ResetActionPattern();
        if (isActiveAndEnabled) SubscribeStatuses();
        RefreshNextActionUI();
    }

    public void BindActionUI(EnemyActionUI ui)
    {
        actionUI = ui;
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
        VitalsChanged += RefreshNextActionUI;
        subscribedPlayer = battleContext != null ? battleContext.GetPlayer() : null;
        if (subscribedPlayer != null)
        {
            subscribedPlayer.StatusEffects.Changed += RefreshNextActionUI;
            subscribedPlayer.VitalsChanged += RefreshNextActionUI;
        }
    }

    private void UnsubscribeStatuses()
    {
        StatusEffects.Changed -= RefreshNextActionUI;
        VitalsChanged -= RefreshNextActionUI;
        if (subscribedPlayer != null)
        {
            subscribedPlayer.StatusEffects.Changed -= RefreshNextActionUI;
            subscribedPlayer.VitalsChanged -= RefreshNextActionUI;
        }
        subscribedPlayer = null;
    }

    private void RefreshNextActionUI()
    {
        PrepareNextAction();
        UpdateNextActionUI(nextAction);
    }

    protected override void Awake()
    {
        base.Awake();

        ResetActionPattern();

        RefreshNextActionUI();
    }

    protected override void Die()
    {
        Debug.Log($"{gameObject.name}이(가) 사망했습니다.");

        // 적 사망 처리
    }

    public void EnemyTurn(BattleManager battleManager) =>
        EffectSequence.Start(battleManager, EnemyTurnRoutine(battleManager));

    public IEnumerator EnemyTurnRoutine(BattleManager battleManager)
    {
        if (this == null || isActing || !isActiveAndEnabled || battleManager == null || !battleManager.CanContinueEffects) yield break;
        PrepareNextAction();
        if (behavior == null) yield break;
        isActing = true;
        try
        {
            EnemyAction action = nextAction;
            if (action != null) yield return action.ExecuteRoutine(battleManager);
            if (this == null || !battleManager.CanContinueEffects || !isActiveAndEnabled) yield break;
            CompleteAction(action, battleManager);
        }
        finally { isActing = false; }
    }

    private void CompleteAction(EnemyAction action, BattleManager battleManager)
    {
        if (action != null)
        {
            consecutiveCount = action == lastAction ? Mathf.Min(2, consecutiveCount + 1) : 1;
            lastAction = action;
        }

        if (behavior.SelectionMode == EnemyActionSelectionMode.Sequential)
        {
            currentActionIndex++;
            if (currentActionIndex >= behavior.GetActionCount()) currentActionIndex = 0;
        }
        nextAction = null;
        if (battleManager != null && !battleManager.CanContinueEffects)
        {
            UpdateNextActionUI(null);
            return;
        }
        SelectNextAction();
        RefreshNextActionUI();
    }

    private void ResetActionPattern()
    {
        if (behavior != null) behavior.Validate();
        preparedBehavior = behavior;
        currentActionIndex = 0;
        lastAction = null;
        consecutiveCount = 0;
        nextAction = null;
        SelectNextAction();
    }

    private void PrepareNextAction()
    {
        if (preparedBehavior != behavior) ResetActionPattern();
        // Sequential intent also respects callers that explicitly reset the sequence index.
        else if (behavior != null && behavior.SelectionMode == EnemyActionSelectionMode.Sequential)
            nextAction = behavior.GetAction(currentActionIndex);
    }

    private void SelectNextAction()
    {
        nextAction = null;
        if (behavior == null) return;
        if (behavior.SelectionMode == EnemyActionSelectionMode.Sequential)
        {
            nextAction = behavior.GetAction(currentActionIndex);
            return;
        }

        actionCandidates.Clear();
        for (int i = 0; i < behavior.GetActionCount(); i++)
        {
            EnemyAction candidate = behavior.GetAction(i);
            if (candidate == null || consecutiveCount >= 2 && candidate == lastAction || actionCandidates.Contains(candidate))
                continue;
            actionCandidates.Add(candidate);
        }
        if (actionCandidates.Count > 0)
            nextAction = actionCandidates[Random.Range(0, actionCandidates.Count)];
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