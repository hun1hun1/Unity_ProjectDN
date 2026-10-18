using UnityEngine;

// All character Awakes finish first; this Start injects run state before BattleManager.Start.
[DefaultExecutionOrder(-100)]
public class RunBattleBridge : MonoBehaviour
{
    [SerializeField] private BattleManager battle;
    [SerializeField] private BattleRewardSystem rewards;
    private RunSession session;
    private RunProgress progress;
    private int floor;
    private int node;

    private void OnEnable()
    {
        if (battle != null) battle.BattleEnded += OnBattleEnded;
        if (rewards != null) rewards.Completed += OnRewardsCompleted;
    }
    private void Start()
    {
        session = RunSession.Instance;
        if (session == null || session.Progress == null || session.Progress.Phase != RunPhase.Battle) return;
        progress = session.Progress; floor = progress.FloorIndex; node = progress.NodeIndex;
        BattleEncounterDefinition encounter = progress.CurrentNode.SelectedChoice.Encounter;
        encounter.Validate();
        battle.GetPlayer().RestoreHealth(progress.CurrentHP, progress.MaxHP);
        battle.GetTarget().ConfigureEncounter(encounter);
        rewards.ConfigureGoldReward(encounter.goldReward);
    }
    private void OnDisable()
    {
        if (battle != null) battle.BattleEnded -= OnBattleEnded;
        if (rewards != null) rewards.Completed -= OnRewardsCompleted;
    }
    private void OnBattleEnded(BattleResult result)
    {
        if (session == null || progress == null) return;
        Player player = battle.GetPlayer();
        session.RecordBattleResult(progress, floor, node, result, player.GetCurrentHP(), player.GetMaxHP());
    }
    private void OnRewardsCompleted()
    {
        if (session == null || progress == null || battle.Result != BattleResult.Victory) return;
        Player player = battle.GetPlayer();
        session.CompleteBattle(progress, floor, node, player.GetCurrentHP(), player.GetMaxHP());
    }
}
