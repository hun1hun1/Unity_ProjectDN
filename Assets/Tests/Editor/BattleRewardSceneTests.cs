using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class BattleRewardSceneTests
{
    [TearDown] public void Cleanup()
    {
        RunDeckInventory.ResetForNewGame();
        typeof(MulliganSelectionUI).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,null);
        typeof(CardInspectionUI).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,null);
    }
    [UnityTest] public IEnumerator EnemyDeathOpensWiredRewardsAndCardClickAddsToRunDeck()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return null; yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        BattleRewardSystem flow = Object.FindFirstObjectByType<BattleRewardSystem>();
        CardRewardUI ui = Object.FindFirstObjectByType<CardRewardUI>();
        Assert.That(ui.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        battle.GetTarget().TakeDamage(100000);
        yield return null; yield return null;
        Assert.That(battle.Result, Is.EqualTo(BattleResult.Victory));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Summary));
        Assert.That(ui.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        Assert.That(ui.transform.Find("SummaryPanel").gameObject.activeInHierarchy, Is.True);
        Assert.That(ui.transform.Find("SummaryPanel").GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
        Click(ui.transform.Find("SummaryPanel/GoldButton"));
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(20));
        battle.FinishBattle(BattleResult.Victory);
        Assert.That(flow.ClaimGold(), Is.False);
        Click(ui.transform.Find("SummaryPanel/CardButton"));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.DeckChoice));
        Click(ui.transform.Find("DeckSelectPanel/DreamButton"));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.CardChoice));
        Assert.That(ui.transform.Find("SummaryPanel").gameObject.activeInHierarchy, Is.False);
        Assert.That(ui.transform.Find("DeckSelectPanel").gameObject.activeInHierarchy, Is.False);
        int before = battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
        Assert.That(flow.Offers.Count, Is.EqualTo(3));
        Click(ui.transform.Find("CardRewardPanel/Options/RewardCard1"));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Summary));
        Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count, Is.EqualTo(before + 1));
        Click(ui.transform.Find("SummaryPanel/SkipRemainingButton"));
        Assert.That(flow.State, Is.EqualTo(BattleRewardState.Completed));
        Assert.That(ui.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        yield return new ExitPlayMode();
    }
    [UnityTest] public IEnumerator PlayerDeathDoesNotOpenRewards()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene"); yield return null; yield return null;
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        battle.GetPlayer().TakeDamage(100000); yield return null;
        Assert.That(battle.Result, Is.EqualTo(BattleResult.Defeat));
        Assert.That(Object.FindFirstObjectByType<BattleRewardSystem>().State, Is.EqualTo(BattleRewardState.Closed));
        Assert.That(Object.FindFirstObjectByType<CardRewardUI>().GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        yield return new ExitPlayMode();
    }
    private static void Click(Transform button) { Assert.That(button, Is.Not.Null); button.GetComponent<Button>().onClick.Invoke(); }
}
