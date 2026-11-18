using UnityEngine;

// All character Awakes finish first; this Start injects run state before BattleManager.Start.
[DefaultExecutionOrder(-100)]
public class RunBattleBridge : MonoBehaviour
{
    [SerializeField] private BattleManager battle;
    [SerializeField] private BattleRewardSystem rewards;
    [SerializeField] private GameOverUI gameOver;
    private RunSession session;
    private RunProgress progress;
    private int floor;
    private int node;
    private bool restoring;

    private void OnEnable()
    {
        if (battle != null) battle.BattleEnded += OnBattleEnded;
        if (rewards != null) { rewards.Completed += OnRewardsCompleted; rewards.Changed += OnRewardsChanged; }
    }
    private void Start()
    {
        session = RunSession.Instance;
        if (session == null || session.Progress == null || (session.Progress.Phase != RunPhase.Battle && session.Progress.Phase != RunPhase.Rewards)) return;
        progress = session.Progress; floor = progress.FloorIndex; node = progress.NodeIndex;
        BattleEncounterDefinition encounter = progress.CurrentNode.SelectedChoice.Encounter;
        encounter.Validate();
        rewards.BindRun(progress);
        battle.GetPlayer().RestoreHealth(progress.CurrentHP, progress.MaxHP);
        battle.ConfigureEncounter(encounter);
        if(progress.Phase == RunPhase.Rewards)
        {
            restoring=true;
            rewards.RestoreCheckpoint(session.SavedRewards,RunAssetCatalog.Load());
            battle.FinishBattle(BattleResult.Victory);
            restoring=false;
        }
        else
        {
            int gold = encounter.goldReward;

            if (floor == 0) // 1Ãþ
            {
                int baseGold = new System.Random(progress.NodeSeed).Next(20, 26);
                gold = baseGold + (node + 1);
            }

            rewards.ConfigureGoldReward(gold);
            // Deck Awakes have populated the persistent inventory, before starting hands are drawn.
            session.SaveBattleEntryCheckpoint(progress);
        }
    }
    private void OnDisable()
    {
        if (battle != null) battle.BattleEnded -= OnBattleEnded;
        if (rewards != null) { rewards.Completed -= OnRewardsCompleted; rewards.Changed -= OnRewardsChanged; }
    }
    private void OnBattleEnded(BattleResult result)
    {
        if (restoring) return;
        Player player = battle.GetPlayer();
        if (progress == null)
        {
            if (result == BattleResult.Defeat && gameOver != null) gameOver.Show(player, null);
            return;
        }
        if (session == null || !ReferenceEquals(session.Progress, progress)) return;

        bool presentDefeat = result == BattleResult.Defeat && gameOver != null;
        bool recorded = session.RecordBattleResult(progress, floor, node, result, player.GetCurrentHP(),
            player.GetMaxHP(), keepBattleSceneOnDefeat: presentDefeat);
        if (presentDefeat && progress.Phase == RunPhase.Defeated) gameOver.Show(player, progress);
        if (recorded && result == BattleResult.Victory)
        {
            int recoveredHP = progress.CurrentHP - player.GetCurrentHP();
            if (recoveredHP > 0) player.Heal(recoveredHP);
            rewards.PrepareVictory();
            OnRewardsChanged();
        }
    }
    private void OnRewardsChanged()
    {
        if(restoring || session==null || progress==null || !ReferenceEquals(session.Progress,progress) || progress.Phase!=RunPhase.Rewards ||
            rewards.State==BattleRewardState.Closed || rewards.State==BattleRewardState.Completed)return;
        if(!session.SaveRewards(progress,floor,node,rewards.CaptureCheckpoint(RunAssetCatalog.Load())) && session.SavedRewards!=null)
        {
            restoring=true;rewards.RestoreCheckpoint(session.SavedRewards,RunAssetCatalog.Load());restoring=false;
        }
    }
    private void OnRewardsCompleted()
    {
        if (session == null || progress == null || battle.Result != BattleResult.Victory) return;
        Player player = battle.GetPlayer();
        if(!session.CompleteBattle(progress, floor, node, player.GetCurrentHP(), player.GetMaxHP()) &&
            ReferenceEquals(session.Progress,progress) && progress.Phase==RunPhase.Rewards && session.SavedRewards!=null)
        {
            restoring=true;
            rewards.RestoreCheckpoint(session.SavedRewards,RunAssetCatalog.Load());
            restoring=false;
        }
    }
}
