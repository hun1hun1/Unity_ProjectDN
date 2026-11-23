using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

public partial class RunCheckpointRuntimeTests
{
    [UnityTest] public IEnumerator ContentPurchaseRestoresStockAndRollsBackAllChangesOnWriteFailure()
    {yield return new EnterPlayMode();yield return VerifyContent();yield return new ExitPlayMode();}
    [UnityTest] public IEnumerator PreviousContentHandlerCannotPurchaseInsideTheNextNode()
    {yield return new EnterPlayMode();yield return VerifyContent(true);yield return new ExitPlayMode();}
    private static IEnumerator VerifyContent(bool checkStale=false)
    {
        Isolate();yield return Load("TitleScene");
        var run=Object.Instantiate(AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/TemporaryRun.asset"));
        run.enabledContent=new[]{MapContentType.NormalBattle,MapContentType.Shop,MapContentType.Relic};
        var catalog=Object.Instantiate(RunAssetCatalog.Load());
        var state=new SerializedObject(catalog);var entries=state.FindProperty("entries");int slot=entries.arraySize++;var entry=entries.GetArrayElementAtIndex(slot);
        entry.FindPropertyRelative("id").stringValue="fixture-run";entry.FindPropertyRelative("asset").objectReferenceValue=run;state.ApplyModifiedPropertiesWithoutUndo();
        RunAssetCatalog.SetRuntimeCatalog(catalog);
        var session=RunSession.GetOrCreate();session.StartNewRun(run,44,new[]{MapContentType.Shop,MapContentType.Relic});yield return WaitMap();
        RunProgressData data=session.Progress.Capture(catalog);data.phase=RunPhase.Content;
        data.nodes[0].status=MapNodeStatus.Resolving;data.nodes[0].selected=0;data.nodes[0].choices=new[]{new RunChoiceData {type=MapContentType.Shop}};
        data.nodes[1].choices=new[]{new RunChoiceData {type=MapContentType.Shop}};
        var save=new RunSaveData {progress=data,inventory=new RunInventoryData {gold=100}};
        Assert.That(RunSaves.Store.TryWrite(save,out _),Is.True);Assert.That(session.ResumeSavedRun(),Is.True);yield return WaitMap();
        CheckpointShopFixture shop=BindShop();string offer=shop.Offer;
        CardDatabase db=AssetDatabase.FindAssets("t:CardDatabase").Select(g=>AssetDatabase.LoadAssetAtPath<CardDatabase>(AssetDatabase.GUIDToAssetPath(g)))
            .First(d=>d.GetAllCards().Count>0&&d.GetAllCards().All(c=>c!=null&&c.deckType==CardDeckType.TypeDream));
        var root=new GameObject("Fixture deck");root.SetActive(false);var deck=root.AddComponent<DeckController>();
        state=new SerializedObject(deck);state.FindProperty("dreamStartingDeckDatabase").objectReferenceValue=db;state.ApplyModifiedPropertiesWithoutUndo();
        CardData card=deck.GetDeck(CardDeckType.TypeDream)[0];int count=deck.GetDeck(CardDeckType.TypeDream).Count;
        var relic=AssetDatabase.LoadAssetAtPath<ConduitData>("Assets/CardData/Soldier/Examples/AmmoCapacityRelic.asset");
        Assert.That(session.SaveCheckpoint(contentState:shop.CaptureState()),Is.True);
        if(checkStale)
        {
            shop.Complete();Assert.That(session.SelectOption(0),Is.True);CheckpointShopFixture next=BindShop();
            Assert.That(shop.Purchase(deck,card,relic),Is.False,"A previous node's acquisition must not commit in the next shop.");
            Assert.That(RunGoldInventory.Gold,Is.EqualTo(100));Assert.That(next.Stock,Is.EqualTo(1));
            RunAssetCatalog.SetRuntimeCatalog(null);Object.Destroy(catalog);Object.Destroy(run);yield break;
        }
        using(var locked=new FileStream(RunSaves.Store.Path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            Assert.That(shop.Purchase(deck,card,relic),Is.False);Assert.That(shop.Stock,Is.EqualTo(1));Assert.That(RunGoldInventory.Gold,Is.EqualTo(100));
            Assert.That(deck.GetDeck(CardDeckType.TypeDream).Count,Is.EqualTo(count));Assert.That(RunRelicInventory.Contains(relic),Is.False);
        }
        Assert.That(shop.Purchase(deck,card,relic),Is.True);Assert.That(RunGoldInventory.Gold,Is.EqualTo(75));Assert.That(shop.Stock,Is.Zero);
        Assert.That(session.ReturnToTitle(),Is.True);yield return LoadDone("TitleScene");
        Object.Destroy(session.gameObject);yield return null;RunDeckInventory.ResetForNewGame();RunRelicInventory.ResetForNewGame();
        Object.FindFirstObjectByType<TitleMenuUI>().ContinueGame();yield return WaitMap();shop=BindShop();
        Assert.That(shop.Stock,Is.Zero);Assert.That(shop.Offer,Is.EqualTo(offer));Assert.That(RunGoldInventory.Gold,Is.EqualTo(75));Assert.That(RunRelicInventory.Contains(relic),Is.True);
        var inventory=RunSaveCodec.CaptureInventory(catalog);Assert.That(inventory.decks.Single().dream.Length,Is.EqualTo(count+1));
        Assert.That(shop.Purchase(deck,card,relic),Is.False);shop.Complete();Assert.That(RunSession.Instance.Progress.NodeIndex,Is.EqualTo(1));
        Assert.That(RunSaves.TryLoad(out save,out _),Is.True);Assert.That(save.progress.phase,Is.EqualTo(RunPhase.Map));
        RunAssetCatalog.SetRuntimeCatalog(null);Object.Destroy(catalog);Object.Destroy(run);
    }
    private static CheckpointShopFixture BindShop()
    {
        var map=Object.FindFirstObjectByType<MapController>();var shop=map.gameObject.AddComponent<CheckpointShopFixture>();
        Assert.That(map,Is.Not.Null,"Map must finish loading before the handler is attached.");
        Assert.That(shop,Is.Not.Null,"Fixture handler must be a valid runtime component.");
        var state=new SerializedObject(map);var handlers=state.FindProperty("contentHandlers");handlers.arraySize=1;handlers.GetArrayElementAtIndex(0).objectReferenceValue=shop;state.ApplyModifiedPropertiesWithoutUndo();
        var start=typeof(MapController).GetMethod("Start",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Assert.That(start,Is.Not.Null);start.Invoke(map,null);
        return shop;
    }
}
