using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class MapSessionTests
{
    private GameObject root;
    private RunSession session;
    private RunDefinition run;
    private FloorMapDefinition floor;
    private BattleEncounterDefinition encounter;
    private EnemyBehavior behavior;

    [SetUp] public void Setup()
    {
        root = new GameObject("Run session test"); session = root.AddComponent<RunSession>();
        behavior = ScriptableObject.CreateInstance<EnemyBehavior>();
        encounter = ScriptableObject.CreateInstance<BattleEncounterDefinition>(); encounter.behavior = behavior;
        floor = ScriptableObject.CreateInstance<FloorMapDefinition>(); floor.mode = FloorMapMode.NormalBattlesOnly; floor.nodeCount = 5;
        floor.normalBattles = floor.eliteBattles = floor.bossBattles = new[] { encounter };
        run = ScriptableObject.CreateInstance<RunDefinition>(); run.floors = new[] { floor };
    }
    [TearDown] public void Cleanup()
    {
        Object.DestroyImmediate(root); Object.DestroyImmediate(run); Object.DestroyImmediate(floor);
        Object.DestroyImmediate(encounter); Object.DestroyImmediate(behavior);
        RunDeckInventory.ResetForNewGame(); RunRelicInventory.ResetForNewGame();
    }

    [Test] public void CallbacksWithoutAnActiveRunAreIgnored()
    {
        Assert.That(session.RecordBattleResult(null, 0, 0, BattleResult.Victory, 80, 80), Is.False);
        Assert.That(session.CompleteBattle(null, 0, 0, 80, 80), Is.False);
        Assert.That(session.CompleteContent(null, 0, 0), Is.False);
    }

    [Test] public void PreviousBattleResultCannotResolveTheNextBattle()
    {
        session.StartNewRun(run, 5); RunProgress state = session.Progress;
        state.TrySelectOption(0); session.RecordBattleResult(state, 0, 0, BattleResult.Victory, 70, 80);
        session.CompleteBattle(state, 0, 0, 70, 80);
        state.TrySelectOption(0);
        Assert.That(session.RecordBattleResult(state, 0, 0, BattleResult.Defeat, 0, 80), Is.False,
            "A previous node's delayed result must not end the current battle.");
        Assert.That(state.Phase, Is.EqualTo(RunPhase.Battle));
        Assert.That(state.CurrentHP, Is.EqualTo(70));
    }

    [Test] public void PlannedRunWithoutRelicHandlerIsRejectedBeforeInventoryReset()
    {
        floor.mode = FloorMapMode.PlannedFloor;
        run.enabledContent = (MapContentType[])Enum.GetValues(typeof(MapContentType));
        RunGoldInventory.Add(77);
        Assert.Throws<InvalidOperationException>(() => session.StartNewRun(run, 5));
        Assert.That(RunGoldInventory.Gold, Is.EqualTo(77));
        Assert.That(session.Progress, Is.Null);
    }

    [Test] public void NewRunRejectsBattleAndRewardCallbacksFromOldRun()
    {
        session.StartNewRun(run, 4); RunProgress old = session.Progress; old.TrySelectOption(0);
        session.StartNewRun(run, 5); session.Progress.TrySelectOption(0);
        Assert.That(session.RecordBattleResult(old, 0, 0, BattleResult.Victory, 1, 80), Is.False);
        Assert.That(session.CompleteBattle(old, 0, 0, 1, 80), Is.False);
        Assert.That(session.Progress.CurrentHP, Is.EqualTo(80));
        Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Battle));
    }

    [Test] public void RegisteredRelicAllowsPlannedRunWhileUnsupportedOptionalShopsAreOmitted()
    {
        floor.mode = FloorMapMode.PlannedFloor; floor.nonBattleChance = 1;
        floor.nonBattleContents = new[] { new WeightedMapContent { type = MapContentType.Shop, weight = 1 } };
        run.enabledContent = (MapContentType[])Enum.GetValues(typeof(MapContentType));
        session.StartNewRun(run, 5, new[] { MapContentType.Relic });
        Assert.That(session.Progress.Phase, Is.EqualTo(RunPhase.Map));
        foreach (MapNode node in session.Progress.Nodes)
            foreach (MapChoice choice in node.Choices) Assert.That(choice.Type, Is.Not.EqualTo(MapContentType.Shop));
    }
}
