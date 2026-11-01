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
        Isolate();yield return Load("TitleScene");Object.FindFirstObjectByType<TitleMenuUI>().StartGame();yield return WaitMap();
        Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
        var rewards=Object.FindFirstObjectByType<BattleRewardSystem>();System.Func<bool> stale=rewards.ClaimGold;
        Assert.That(RunSession.Instance.ReturnToTitle(),Is.True);yield return LoadDone("TitleScene");
        Assert.That(stale(),Is.False);Assert.That(RunGoldInventory.Gold,Is.Zero);
    }
    [UnityTest] public IEnumerator FailedRewardSaveDoesNotKeepAnUncommittedGoldClaim()
    {yield return new EnterPlayMode();yield return VerifyClaimFailure();yield return new ExitPlayMode();}
    private static IEnumerator VerifyClaimFailure()
    {
        Isolate();yield return Load("TitleScene");Object.FindFirstObjectByType<TitleMenuUI>().StartGame();yield return WaitMap();
        Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        Object.FindFirstObjectByType<BattleManager>().GetTarget().TakeDamage(100000);
        BattleRewardSystem rewards=Object.FindFirstObjectByType<BattleRewardSystem>();
        using(var locked=new FileStream(RunSaves.Store.Path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            rewards.ClaimGold();Assert.That(RunGoldInventory.Gold,Is.Zero);Assert.That(rewards.GoldAvailable,Is.True);
        }
        Assert.That(rewards.ClaimGold(),Is.True);Assert.That(RunGoldInventory.Gold,Is.EqualTo(rewards.GoldReward));
    }
    [UnityTest] public IEnumerator FailedNodeEntrySaveLeavesTheMapReadyToRetry()
    {yield return new EnterPlayMode();yield return VerifyEntryFailure();yield return new ExitPlayMode();}
    private static IEnumerator VerifyEntryFailure()
    {
        Isolate();yield return Load("TitleScene");Object.FindFirstObjectByType<TitleMenuUI>().StartGame();yield return WaitMap();
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
    [UnityTest] public IEnumerator FailedNodeCompletionSaveKeepsRewardsAvailableToRetry()
    {yield return new EnterPlayMode();yield return VerifyCompletionFailure();yield return new ExitPlayMode();}
    private static IEnumerator VerifyCompletionFailure()
    {
        Isolate();yield return Load("TitleScene");Object.FindFirstObjectByType<TitleMenuUI>().StartGame();yield return WaitMap();
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
