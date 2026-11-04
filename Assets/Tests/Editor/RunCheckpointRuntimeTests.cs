using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public partial class RunCheckpointRuntimeTests
{
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(RunSession.Instance!=null)Object.DestroyImmediate(RunSession.Instance.gameObject);
        if(Application.isPlaying)yield return new ExitPlayMode();
        RunSaves.UseStore(null);RunAssetCatalog.SetRuntimeCatalog(null);RunDeckInventory.ResetForNewGame();RunRelicInventory.ResetForNewGame();
    }
    private static void Isolate()
    {RunSaves.UseStore(new RunSaveStore(Path.GetFullPath(".utmp/run-save/runtime-"+Guid.NewGuid().ToString("N")+".json")));}
    private static void StartNewAdventure(TitleMenuUI title)
    {
        title.StartGame();
        var selection = Ref<CharacterSelectionUI>(title, "characterSelection");
        Ref<Toggle>(selection, "soldierToggle").OnSubmit(null);
        title.ConfirmAdventure();
    }

    [UnityTest] public IEnumerator ReturnFromBattleRestartsItsEntryStateAcrossSessionDestruction()
    {yield return new EnterPlayMode();yield return VerifyBattleReturn();yield return new ExitPlayMode();}
    private static IEnumerator VerifyBattleReturn()
    {
        Isolate();yield return Load("TitleScene");TitleMenuUI title=Object.FindFirstObjectByType<TitleMenuUI>();
        Assert.That(Ref<Button>(title,"continueButton").gameObject.activeSelf,Is.False);
        StartNewAdventure(title);yield return WaitMap();Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        BattleManager battle=Object.FindFirstObjectByType<BattleManager>();int hp=battle.GetPlayer().GetCurrentHP();
        int savedGold = RunGoldInventory.Gold;
        string[] hand=battle.GetHand(CardDeckType.TypeDream).GetCardsSnapshot().Select(c=>c.GetCardData().name).ToArray();
        int count=battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
        battle.GetPlayer().TakeDamage(13);battle.GetDeckController().AddCardToDeck(battle.GetDeckController().GetDeck(CardDeckType.TypeDream)[0],CardDeckType.TypeDream);RunGoldInventory.Add(44);
        SettingsUI settings=Object.FindFirstObjectByType<SettingsUI>();settings.Toggle();Assert.That(settings.IsOpen,Is.True);
        settings.RequestQuit();settings.ConfirmQuit();settings.ConfirmQuit();yield return LoadDone("TitleScene");
        Assert.That(RunSession.Instance.Progress,Is.Null);title=Object.FindFirstObjectByType<TitleMenuUI>();
        Assert.That(Ref<Button>(title,"continueButton").gameObject.activeSelf,Is.True);
        Object.Destroy(RunSession.Instance.gameObject);yield return null;RunDeckInventory.ResetForNewGame();RunRelicInventory.ResetForNewGame();
        Ref<Button>(title,"continueButton").onClick.Invoke();yield return WaitBattle();battle=Object.FindFirstObjectByType<BattleManager>();
        Assert.That(battle.GetPlayer().GetCurrentHP(),Is.EqualTo(hp));
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(savedGold));
        Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count,Is.EqualTo(count));
        Assert.That(battle.GetHand(CardDeckType.TypeDream).GetCardsSnapshot().Select(c=>c.GetCardData().name),Is.EqualTo(hand));
        Assert.That(RunSession.Instance.Progress.Phase,Is.EqualTo(RunPhase.Battle));
    }
    [UnityTest] public IEnumerator PartialRewardsAndFixedOffersSurviveResumeWithoutDuplicateClaims()
    {yield return new EnterPlayMode();yield return VerifyRewards();yield return new ExitPlayMode();}
    private static IEnumerator VerifyRewards()
    {
        Isolate();yield return Load("TitleScene");StartNewAdventure(Object.FindFirstObjectByType<TitleMenuUI>());yield return WaitMap();
        Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        BattleManager battle=Object.FindFirstObjectByType<BattleManager>();battle.GetPlayer().TakeDamage(7);battle.GetTarget().TakeDamage(100000);
        BattleRewardSystem rewards=Object.FindFirstObjectByType<BattleRewardSystem>();Assert.That(rewards.ClaimGold(),Is.True);
        Assert.That(rewards.OpenCardReward(),Is.True);Assert.That(rewards.SelectDeck(CardDeckType.TypeDream),Is.True);
        string[] offers=rewards.Offers.Select(c=>c.name).ToArray();int gold=RunGoldInventory.Gold;
        SettingsUI settings=Object.FindFirstObjectByType<SettingsUI>();settings.Toggle();Assert.That(settings.IsOpen,Is.True,"Settings must open during reward selection.");
        settings.RequestQuit();settings.ConfirmQuit();Assert.That(rewards.ChooseCard(0),Is.False,"A departing scene must not grant another reward.");
        yield return LoadDone("TitleScene");Object.Destroy(RunSession.Instance.gameObject);yield return null;RunDeckInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();yield return LoadDone("BattleScene");
        battle=Object.FindFirstObjectByType<BattleManager>();rewards=Object.FindFirstObjectByType<BattleRewardSystem>();
        Assert.That(battle.Result,Is.EqualTo(BattleResult.Victory));Assert.That(battle.GetHand(CardDeckType.TypeDream).GetCardsSnapshot(),Is.Empty);
        Assert.That(battle.GetPlayer().GetCurrentHP(),Is.EqualTo(73));Assert.That(rewards.State,Is.EqualTo(BattleRewardState.CardChoice));
        Assert.That(rewards.Offers.Select(c=>c.name),Is.EqualTo(offers));Assert.That(RunGoldInventory.Gold,Is.EqualTo(gold));Assert.That(rewards.ClaimGold(),Is.False);
        Assert.That(rewards.ChooseCard(0),Is.True);int deckCount=battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count;
        settings=Object.FindFirstObjectByType<SettingsUI>();settings.Toggle();settings.RequestQuit();settings.ConfirmQuit();yield return LoadDone("TitleScene");
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();yield return LoadDone("BattleScene");
        rewards=Object.FindFirstObjectByType<BattleRewardSystem>();battle=Object.FindFirstObjectByType<BattleManager>();
        Assert.That(rewards.CardAvailable,Is.False);Assert.That(rewards.ChooseCard(0),Is.False);Assert.That(rewards.ClaimGold(),Is.False);
        Assert.That(battle.GetDeckController().GetDeck(CardDeckType.TypeDream).Count,Is.EqualTo(deckCount));
        Assert.That(rewards.SkipRemaining(),Is.True);yield return WaitMap();Assert.That(RunSession.Instance.Progress.NodeIndex,Is.EqualTo(1));
        Assert.That(RunSaves.TryLoad(out RunSaveData save,out _),Is.True);Assert.That(save.progress.phase,Is.EqualTo(RunPhase.Map));Assert.That(save.progress.node,Is.EqualTo(1));
    }
    [UnityTest] public IEnumerator FailedTitleReturnKeepsConfirmationOpenAndPreservesTheCheckpoint()
    {yield return new EnterPlayMode();yield return VerifyWriteFailure();yield return new ExitPlayMode();}
    private static IEnumerator VerifyWriteFailure()
    {
        Isolate();yield return Load("TitleScene");StartNewAdventure(Object.FindFirstObjectByType<TitleMenuUI>());yield return WaitMap();
        Object.FindFirstObjectByType<MapController>().SelectOption(0);yield return WaitBattle();
        var settings=Object.FindFirstObjectByType<SettingsUI>();settings.Toggle();settings.RequestQuit();
        string path=RunSaves.Store.Path;string before=File.ReadAllText(path);
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            settings.ConfirmQuit();Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("BattleScene"));
            Assert.That(settings.IsOpen,Is.True);Assert.That(Ref<TMP_Text>(settings,"navigationError").text,Is.Not.Empty);
        }
        Assert.That(File.ReadAllText(path),Is.EqualTo(before));settings.ConfirmQuit();yield return LoadDone("TitleScene");
    }
    private static T Ref<T>(Object owner,string name) where T:Object=>(T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
    private static IEnumerator Load(string name) {AsyncOperation load=SceneManager.LoadSceneAsync(name);yield return Until(()=>load.isDone);yield return null;yield return null;}
    private static IEnumerator LoadDone(string name) {yield return Until(()=>SceneManager.GetActiveScene().name==name&&RunSession.Instance!=null&&!RunSession.Instance.IsTransitioning);yield return null;yield return null;}
    private static IEnumerator WaitMap() {yield return LoadDone("MapScene");}
    private static IEnumerator WaitBattle() {yield return LoadDone("BattleScene");yield return Until(()=>Object.FindFirstObjectByType<BattleManager>()!=null&&Object.FindFirstObjectByType<BattleManager>().CanInspectCards);}
    private static IEnumerator Until(Func<bool> ready) {float end=Time.realtimeSinceStartup+20;while(!ready()){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}}
}
