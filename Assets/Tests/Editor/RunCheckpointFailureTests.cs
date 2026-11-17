using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public partial class RunCheckpointRuntimeTests
{
    [UnityTest] public IEnumerator DestroyedRewardCallbackCannotGrantGoldAfterTitleTransitionCompletes()
    {yield return new EnterPlayMode();yield return VerifyStaleReward();yield return new ExitPlayMode();}
    private static IEnumerator VerifyStaleReward()
    {
        Isolate();yield return Load("TitleScene");yield return StartNewAdventure(Object.FindFirstObjectByType<TitleMenuUI>());yield return WaitMap();
        Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
        var rewards=Object.FindFirstObjectByType<BattleRewardSystem>();System.Func<bool> stale=rewards.ClaimGold;
        int goldBefore = RunGoldInventory.Gold;
        Assert.That(RunSession.Instance.ReturnToTitle(),Is.True);yield return LoadDone("TitleScene");
        Assert.That(stale(),Is.False);
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(goldBefore));
    }
    [UnityTest] public IEnumerator FailedRewardSaveDoesNotKeepAnUncommittedGoldClaim()
    {yield return new EnterPlayMode();yield return VerifyClaimFailure();yield return new ExitPlayMode();}
    private static IEnumerator VerifyClaimFailure()
    {
        Isolate();yield return Load("TitleScene");yield return StartNewAdventure(Object.FindFirstObjectByType<TitleMenuUI>());yield return WaitMap();
        RunSession session = RunSession.Instance;
        Assert.That(session.SelectOption(0), Is.True, session.SaveError);
        yield return WaitBattle();
        Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
        BattleRewardSystem rewards=Object.FindFirstObjectByType<BattleRewardSystem>();
        int goldBefore = RunGoldInventory.Gold;
        using(var locked=new FileStream(RunSaves.Store.Path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            rewards.ClaimGold();
            Assert.That(RunGoldInventory.Gold, Is.EqualTo(goldBefore));
            Assert.That(rewards.GoldAvailable,Is.True);
        }
        bool claimed = rewards.ClaimGold();
        Assert.That(claimed, Is.True, $"Save error: {session.SaveError}. Phase: {session.Progress.Phase}. Reward: {rewards.State}. Transition: {session.IsTransitioning}.");
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(goldBefore + rewards.GoldReward));
    }
    [UnityTest] public IEnumerator FailedNodeEntrySaveLeavesTheMapReadyToRetry()
    {yield return new EnterPlayMode();yield return VerifyEntryFailure();yield return new ExitPlayMode();}
    private static IEnumerator VerifyEntryFailure()
    {
        Isolate();yield return Load("TitleScene");yield return StartNewAdventure(Object.FindFirstObjectByType<TitleMenuUI>());yield return WaitMap();
        RunSession session=RunSession.Instance;RunProgress original=session.Progress;
        using(var locked=new FileStream(RunSaves.Store.Path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            Assert.That(session.SelectOption(0),Is.False);
            Assert.That(session.Progress,Is.SameAs(original));
            Assert.That(original.Phase,Is.EqualTo(RunPhase.Map));
            Assert.That(original.CurrentNode.Status,Is.EqualTo(MapNodeStatus.Pending));Assert.That(original.CurrentNode.SelectedChoice,Is.Null);
        }
        Assert.That(session.SelectOption(0),Is.True);yield return WaitBattle();
    }
    [UnityTest] public IEnumerator EliteCompletionCheckpointGrantsItsConduitExactlyOnce()
    {
        yield return new EnterPlayMode();
        Isolate();
        yield return Load("TitleScene");
        yield return StartNewAdventure(Object.FindFirstObjectByType<TitleMenuUI>());
        yield return WaitMap();

        RunSession session = RunSession.Instance;
        RunProgress state = session.Progress;
        for (int node = 0; node < 3; node++)
        {
            Assert.That(state.TrySelectOption(0), Is.True);
            Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
            Assert.That(state.CompleteNode(0, node), Is.True);
        }

        Assert.That(session.SelectOption(0), Is.True);
        yield return WaitBattle();
        Assert.That(state.CurrentNode.SelectedChoice.Type, Is.EqualTo(MapContentType.EliteBattle));
        AmmoCapacityRelic reward = state.CurrentNode.SelectedChoice.Encounter.victoryConduit;
        Assert.That(reward, Is.Not.Null);
        Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
        BattleRewardSystem rewards = Object.FindFirstObjectByType<BattleRewardSystem>();
        using (var locked = new FileStream(RunSaves.Store.Path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            rewards.SkipRemaining();
            Assert.That(state.Phase, Is.EqualTo(RunPhase.Rewards));
            Assert.That(RunRelicInventory.Contains(reward), Is.False);
        }
        Assert.That(rewards.SkipRemaining(), Is.True);
        yield return WaitMap();
        Assert.That(RunRelicInventory.Contains(reward), Is.True);
        Assert.That(RunRelicInventory.Count, Is.EqualTo(2));
        Assert.That(RunSaves.TryLoad(out RunSaveData saved, out _), Is.True);
        Assert.That(saved.inventory.relics.Length, Is.EqualTo(2));
        RunAssetCatalog catalog = RunAssetCatalog.Load();
        var casing = UnityEditor.AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/MapData/SoldierStartingCasing.asset");
        Assert.That(System.Array.ConvertAll(saved.inventory.relics, entry => entry.id),
            Is.EquivalentTo(new[] { catalog.Key(casing), catalog.Key(reward) }));
        yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator FailedNodeCompletionSaveKeepsRewardsAvailableToRetry()
    {yield return new EnterPlayMode();yield return VerifyCompletionFailure();yield return new ExitPlayMode();}
    private static IEnumerator VerifyCompletionFailure()
    {
        Isolate();yield return Load("TitleScene");yield return StartNewAdventure(Object.FindFirstObjectByType<TitleMenuUI>());yield return WaitMap();
        Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
        RunSession session=RunSession.Instance;BattleRewardSystem rewards=Object.FindFirstObjectByType<BattleRewardSystem>();
        using(var locked=new FileStream(RunSaves.Store.Path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            rewards.SkipRemaining();Assert.That(session.Progress.Phase,Is.EqualTo(RunPhase.Rewards));
            Assert.That(session.Progress.NodeIndex,Is.Zero);Assert.That(rewards.State,Is.EqualTo(BattleRewardState.Summary));
        }
        Assert.That(rewards.SkipRemaining(),Is.True);yield return WaitMap();Assert.That(session.Progress.NodeIndex,Is.EqualTo(1));
    }
}
