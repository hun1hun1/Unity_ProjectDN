using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public class RunSaveTests
{
    private string folder;
    private RunAssetCatalog catalog;
    [SetUp] public void Setup()
    {
        folder=Path.GetFullPath(".utmp/run-save/unit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        catalog=RunAssetCatalog.Load();RunDeckInventory.ResetForNewGame();RunRelicInventory.ResetForNewGame();
    }
    [TearDown] public void Cleanup() {RunDeckInventory.ResetForNewGame();RunRelicInventory.ResetForNewGame();RunSaves.UseStore(null);}
    private RunSaveData Sample()
    {
        var run=AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/TemporaryRun.asset");
        return RunSaveCodec.Capture(new RunProgress(run,173),catalog);
    }
    [Test] public void JsonRoundtripRestoresProgressSelectionsAndIndependentData()
    {
        RunSaveData saved=Sample();var progress=RunProgress.Restore(saved.progress,catalog);
        Assert.That(progress.TrySelectOption(0),Is.True);saved=RunSaveCodec.Capture(progress,catalog);
        var store=new RunSaveStore(Path.Combine(folder,"checkpoint.json"));
        Assert.That(store.TryWrite(saved,out string error),Is.True,error);
        Assert.That(store.TryRead(out RunSaveData loaded,out error),Is.True,error);
        RunSaveCodec.Validate(loaded,catalog);RunProgress restored=RunProgress.Restore(loaded.progress,catalog);
        Assert.That(restored.Phase,Is.EqualTo(RunPhase.Battle));Assert.That(restored.CurrentNode.SelectedChoice.Encounter,Is.SameAs(progress.CurrentNode.SelectedChoice.Encounter));
        Assert.That(restored.NodeSeed,Is.EqualTo(progress.NodeSeed));
        loaded.progress.hp=1;Assert.That(saved.progress.hp,Is.EqualTo(80));
    }
    [Test] public void InventoryPreservesDuplicateCardsRelicsCapturedBonusAndGold()
    {
        CardDatabase database=AssetDatabase.FindAssets("t:CardDatabase").Select(g=>AssetDatabase.LoadAssetAtPath<CardDatabase>(AssetDatabase.GUIDToAssetPath(g)))
            .First(db=>db.GetAllCards().Any(c=>c!=null&&c.deckType==CardDeckType.TypeDream));
        CardData card=database.GetAllCards().First(c=>c!=null&&c.deckType==CardDeckType.TypeDream);
        var data=new RunInventoryData {gold=234,decks=new[]{new RunDeckData {dreamDatabase=catalog.Key(database),nightmareDatabase="",dream=new[]{catalog.Key(card),catalog.Key(card)},nightmare=Array.Empty<string>()}}};
        var relic=AssetDatabase.LoadAssetAtPath<AmmoCapacityRelic>("Assets/CardData/Soldier/Examples/AmmoCapacityRelic.asset");
        data.relics=new[]{new RunRelicData {id=catalog.Key(relic),bonus=relic.MaxAmmoBonus+3}};
        RunSaveCodec.RestoreInventory(data,catalog);
        RunInventoryData copy=RunSaveCodec.CaptureInventory(catalog);
        Assert.That(copy.gold,Is.EqualTo(234));Assert.That(copy.decks.Single().dream,Is.EqualTo(data.decks[0].dream));
        Assert.That(RunRelicInventory.Contains(relic),Is.True);Assert.That(RunRelicInventory.MaxAmmoBonus,Is.EqualTo(data.relics[0].bonus));
    }
    [Test] public void MissingAssetAndInvalidPhaseAreRejectedBeforeInventoryReplacement()
    {
        RunGoldInventory.Add(91);RunSaveData data=Sample();data.progress.definition="missing";
        Assert.Throws<InvalidOperationException>(()=>RunSaveCodec.Validate(data,catalog));Assert.That(RunGoldInventory.Gold,Is.EqualTo(91));
        data=Sample();data.progress.phase=RunPhase.Rewards;
        Assert.Throws<InvalidOperationException>(()=>RunSaveCodec.Validate(data,catalog));
    }
    [Test] public void CorruptAndFutureFilesDoNotOfferContinueOrModifyResources()
    {
        string path=Path.Combine(folder,"checkpoint.json");RunSaves.UseStore(new RunSaveStore(path));
        File.WriteAllText(path,"broken JSON");Assert.That(RunSaves.HasContinue,Is.False);
        var saved=Sample();saved.version=999;File.WriteAllText(path,JsonUtility.ToJson(saved));Assert.That(RunSaves.HasContinue,Is.False);
        Assert.That(RunGoldInventory.Gold,Is.Zero);
    }
    [Test] public void FailedAtomicReplacementLeavesThePreviousCheckpointUntouched()
    {
        string path=Path.Combine(folder,"checkpoint.json");var store=new RunSaveStore(path);RunSaveData first=Sample();
        Assert.That(store.TryWrite(first,out _),Is.True);string before=File.ReadAllText(path);
        var next=Sample();next.inventory.gold=999;
        using(var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
            Assert.That(store.TryWrite(next,out string error),Is.False,error);
        Assert.That(File.ReadAllText(path),Is.EqualTo(before));Assert.That(File.Exists(path+".tmp"),Is.False);
        Assert.That(store.TryWrite(next,out _),Is.True);Assert.That(store.TryRead(out RunSaveData read,out _),Is.True);Assert.That(read.inventory.gold,Is.EqualTo(999));
    }
    [Test] public void EndingALockedRunCannotExposeItsOldCheckpointAfterRestart()
    {
        var store=new RunSaveStore(Path.Combine(folder,"checkpoint.json"));Assert.That(store.TryWrite(Sample(),out _),Is.True);
        using(var locked=new FileStream(store.Path,FileMode.Open,FileAccess.Read,FileShare.None))
            Assert.That(store.TryDelete(out string error),Is.True,error);
        var reopened=new RunSaveStore(store.Path);Assert.That(reopened.TryRead(out _,out _),Is.False);
        Assert.That(reopened.TryWrite(Sample(),out _),Is.True);Assert.That(reopened.TryRead(out _,out _),Is.True);
    }
}
