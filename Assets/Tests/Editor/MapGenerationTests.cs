using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class MapGenerationTests
{
    private readonly List<Object> created = new List<Object>();
    private FloorMapDefinition floor;
    private RunDefinition run;
    private readonly MapContentType[] all = (MapContentType[])Enum.GetValues(typeof(MapContentType));

    [SetUp] public void Setup()
    {
        BattleEncounterDefinition encounter = Asset<BattleEncounterDefinition>();
        encounter.behavior = Asset<EnemyBehavior>();
        floor = Asset<FloorMapDefinition>();
        floor.normalBattles = floor.eliteBattles = floor.bossBattles = new[] { encounter };
        run = Asset<RunDefinition>(); run.floors = new[] { floor }; run.enabledContent = all;
    }
    [TearDown] public void Cleanup() { foreach (Object item in created) Object.DestroyImmediate(item); created.Clear(); }
    private T Asset<T>() where T : ScriptableObject { T item = ScriptableObject.CreateInstance<T>(); created.Add(item); return item; }

    [TestCase(4)] [TestCase(5)] [TestCase(12)] [TestCase(21)]
    public void PlannedFloorsAlwaysReserveStartMidpointRelicAndBoss(int count)
    {
        floor.nodeCount = count;
        for (int seed = 0; seed < 100; seed++)
        {
            var nodes = MapGenerator.Generate(floor, all, seed);
            Assert.That(nodes.Count, Is.EqualTo(count));
            Assert.That(nodes[0].Choices.Select(c => c.Type), Is.EqualTo(new[] { MapContentType.NormalBattle }));
            Assert.That(nodes[count - 1].Choices.Select(c => c.Type), Is.EqualTo(new[] { MapContentType.BossBattle }));
            int middle = nodes.ToList().FindIndex(n => n.Rule == MapNodeRule.Midpoint);
            Assert.That(nodes.Count(n => n.Rule == MapNodeRule.Midpoint), Is.EqualTo(1));
            Assert.That(middle, Is.InRange(1, count - 3));
            Assert.That(nodes[middle].Choices.Select(c => c.Type), Is.EqualTo(new[] { MapContentType.NormalBattle, MapContentType.EliteBattle }));
            Assert.That(nodes[middle + 1].Rule, Is.EqualTo(MapNodeRule.GuaranteedRelic));
            Assert.That(nodes[middle + 1].Choices.Select(c => c.Type), Is.EqualTo(new[] { MapContentType.Relic }));
        }
    }

    [Test] public void ZeroExtraChancesLeaveRegularNodesWithOnlyNormalBattle()
    {
        floor.nonBattleChance = floor.eliteChance = 0;
        foreach (MapNode node in MapGenerator.Generate(floor, all, 12).Where(n => n.Rule == MapNodeRule.Regular))
            Assert.That(node.Choices.Select(c => c.Type), Is.EqualTo(new[] { MapContentType.NormalBattle }));
    }

    [Test] public void FullChancesAddOneWeightedContentAndIndependentElite()
    {
        floor.nonBattleChance = floor.eliteChance = 1;
        floor.nonBattleContents = new[]
        {
            new WeightedMapContent { type = MapContentType.Shop, weight = 0 },
            new WeightedMapContent { type = MapContentType.Event, weight = 7 }
        };
        foreach (MapNode node in MapGenerator.Generate(floor, all, 7).Where(n => n.Rule == MapNodeRule.Regular))
            Assert.That(node.Choices.Select(c => c.Type), Is.EqualTo(new[] { MapContentType.NormalBattle, MapContentType.Event, MapContentType.EliteBattle }));
    }

    [Test] public void DisabledAndZeroWeightedContentsNeverAppear()
    {
        floor.nonBattleChance = 1;
        floor.nonBattleContents = new[] { new WeightedMapContent { type = MapContentType.Shop, weight = 100 } };
        var enabled = all.Where(t => t != MapContentType.Shop).ToArray();
        Assert.That(MapGenerator.Generate(floor, enabled, 13).SelectMany(n => n.Choices).Any(c => c.Type == MapContentType.Shop), Is.False);
        floor.nonBattleContents[0].weight = 0;
        Assert.That(MapGenerator.Generate(floor, all, 13).SelectMany(n => n.Choices).Any(c => c.Type == MapContentType.Shop), Is.False);
    }

    [Test] public void MapSeedIsUnaffectedByUnityRandomConsumption()
    {
        var first = MapGenerator.Generate(floor, all, 2718).Select(n => n.Rule + ":" + string.Join(",", n.Choices.Select(c => c.Type))).ToArray();
        UnityEngine.Random.State previous = UnityEngine.Random.state;
        try { for (int i = 0; i < 100; i++) UnityEngine.Random.Range(0, 100); }
        finally { UnityEngine.Random.state = previous; }
        var second = MapGenerator.Generate(floor, all, 2718).Select(n => n.Rule + ":" + string.Join(",", n.Choices.Select(c => c.Type))).ToArray();
        Assert.That(second, Is.EqualTo(first));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(3)]
    public void PlannedFloorsRejectInsufficientNodeCounts(int count)
    { floor.nodeCount = count; Assert.Throws<InvalidOperationException>(() => MapGenerator.Generate(floor, all, 1)); }

    [Test] public void PlannedFloorRejectsDisabledRequiredRelicInsteadOfSilentlyReplacingIt()
    { Assert.Throws<InvalidOperationException>(() => MapGenerator.Generate(floor, all.Where(t => t != MapContentType.Relic).ToArray(), 1)); }

    [Test] public void MissingRequiredEncounterIsConfigurationError()
    { floor.normalBattles = Array.Empty<BattleEncounterDefinition>(); Assert.Throws<InvalidOperationException>(() => MapGenerator.Generate(floor, all, 1)); }

    [TestCase(-.1f)] [TestCase(1.1f)] [TestCase(float.NaN)]
    public void InvalidChancesAreRejected(float chance)
    { floor.nonBattleChance = chance; Assert.Throws<InvalidOperationException>(() => MapGenerator.Generate(floor, all, 1)); }

    [Test] public void TemporaryRunCompletesOnlyAfterFiveVictoriesAndTheirRewards()
    {
        floor.mode = FloorMapMode.NormalBattlesOnly; floor.nodeCount = 5;
        run.enabledContent = new[] { MapContentType.NormalBattle };
        var state = new RunProgress(run, 42);
        Assert.That(state.Nodes.SelectMany(n => n.Choices).All(c => c.Type == MapContentType.NormalBattle), Is.True);
        for (int i = 0; i < 5; i++)
        {
            Assert.That(state.NodeIndex, Is.EqualTo(i));
            Assert.That(state.CompleteNode(0, i), Is.False);
            Assert.That(state.TrySelectOption(-1), Is.False);
            Assert.That(state.TrySelectOption(1), Is.False);
            Assert.That(state.TrySelectOption(0), Is.True);
            Assert.That(state.TrySelectOption(0), Is.False);
            Assert.That(state.CompleteNode(0, i), Is.False, "Entering battle must not complete its node.");
            Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
            Assert.That(state.CompletedNodes, Is.EqualTo(i), "Victory must wait for rewards.");
            Assert.That(state.CompleteNode(0, i), Is.True);
            Assert.That(state.CompleteNode(0, i), Is.False, "A duplicate completion cannot advance another node.");
        }
        Assert.That(state.Phase, Is.EqualTo(RunPhase.Completed));
        Assert.That(state.CompletedNodes, Is.EqualTo(5));
        Assert.That(state.CurrentNode, Is.Null);
        Assert.That(state.TrySelectOption(0), Is.False);
    }

    [TestCase(BattleResult.Defeat, RunPhase.Defeated)] [TestCase(BattleResult.Cancelled, RunPhase.Cancelled)]
    public void FailedBattleEndsRunWithoutAdvancing(BattleResult result, RunPhase phase)
    {
        var state = new RunProgress(run, 3); state.TrySelectOption(0);
        Assert.That(state.RecordBattleResult(result), Is.True);
        Assert.That(state.Phase, Is.EqualTo(phase));
        Assert.That(state.CompleteNode(0, 0), Is.False);
        Assert.That(state.CompletedNodes, Is.Zero);
        Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.False);
    }

    [TestCase(0)] [TestCase(1)]
    public void BothMidpointBattleChoicesLeadToGuaranteedRelic(int option)
    {
        floor.nodeCount = 4;
        var state = new RunProgress(run, 1);
        state.TrySelectOption(0); state.RecordBattleResult(BattleResult.Victory); state.CompleteNode(0, 0);
        state.TrySelectOption(option); state.RecordBattleResult(BattleResult.Victory); state.CompleteNode(0, 1);
        Assert.That(state.CurrentNode.Rule, Is.EqualTo(MapNodeRule.GuaranteedRelic));
        state.TrySelectOption(0);
        Assert.That(state.Phase, Is.EqualTo(RunPhase.Content));
        Assert.That(state.CompleteNode(0, 1), Is.False, "Stale battle callback must not complete the relic.");
        Assert.That(state.CompleteNode(0, 2), Is.True);
        Assert.That(state.CurrentNode.Rule, Is.EqualTo(MapNodeRule.Boss));
    }

    [Test] public void UnknownBattlesCountTowardBothSegmentsAndMidpointDoesNot()
    {
        floor.nodeCount = 13;
        floor.midpointMinRatio = floor.midpointMaxRatio = 6f / 13;
        floor.nonBattleChance = floor.eliteChance = 0;
        floor.battleLimits = new[]
        {
            new MapBattleLimit { firstNode = 1, lastNode = 5, maxBattles = 4 },
            new MapBattleLimit { firstNode = 8, lastNode = 12, maxBattles = 4 }
        };
        floor.unknown.nodeNumbers = new[] { 2, 5, 8, 12 };
        floor.unknown.battleWeight = 100;
        floor.unknown.eventWeight = floor.unknown.shopWeight = floor.unknown.restWeight = floor.unknown.relicWeight = 0;
        var floorEvent = Asset<MapEventDefinition>();
        floorEvent.title = "Floor event";
        floor.unknown.events = new[] { floorEvent };
        var state = new RunProgress(run, 83);

        FinishChoice(0, MapContentType.NormalBattle);
        FinishChoice(1, MapContentType.NormalBattle);
        FinishChoice(0, MapContentType.NormalBattle);
        FinishChoice(0, MapContentType.NormalBattle);
        Assert.That(state.TrySelectOption(0), Is.False);
        FinishChoice(1, MapContentType.Event);
        FinishChoice(1, MapContentType.EliteBattle);
        FinishChoice(0, MapContentType.Relic);
        FinishChoice(1, MapContentType.NormalBattle);
        FinishChoice(0, MapContentType.NormalBattle);
        FinishChoice(0, MapContentType.NormalBattle);
        FinishChoice(0, MapContentType.NormalBattle);
        Assert.That(state.TrySelectOption(0), Is.False);
        FinishChoice(1, MapContentType.Event);
        FinishChoice(0, MapContentType.BossBattle);
        Assert.That(state.Phase, Is.EqualTo(RunPhase.Completed));

        void FinishChoice(int index, MapContentType expected)
        {
            Assert.That(state.TrySelectOption(index), Is.True, $"Node {state.NodeIndex + 1}");
            MapChoice result = state.CurrentNode.SelectedChoice;
            Assert.That(result.Type, Is.EqualTo(expected));
            if (expected == MapContentType.Event) Assert.That(result.ContentData, Is.SameAs(floorEvent));
            if (result.IsBattle) Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
            Assert.That(state.CompleteNode(0, state.NodeIndex), Is.True);
        }
    }

    [Test] public void CombatCapTransfersOnlyTheBattleRollsToTheFloorEventPool()
    {
        floor.mode = FloorMapMode.NormalBattlesOnly;
        floor.nodeCount = 2;
        floor.unknown.nodeNumbers = new[] { 2 };
        var first = Asset<MapEventDefinition>();
        var second = Asset<MapEventDefinition>();
        first.title = "First";
        second.title = "Second";
        floor.unknown.events = new[] { first, second };
        floor.unknown.relics = new[] { Asset<AmmoCapacityRelic>() };
        var limit = new MapBattleLimit { firstNode = 1, lastNode = 2 };
        floor.battleLimits = new[] { limit };
        var seen = new HashSet<MapContentType>();
        var seenEvents = new HashSet<ScriptableObject>();

        for (int seed = 0; seed < 320; seed++)
        {
            MapChoice uncapped = Resolve(seed, 2);
            MapChoice capped = Resolve(seed, 1);
            seen.Add(uncapped.Type);
            if (uncapped.Type == MapContentType.NormalBattle)
            {
                Assert.That(capped.Type, Is.EqualTo(MapContentType.Event));
                seenEvents.Add(capped.ContentData);
            }
            else
            {
                Assert.That(capped.Type, Is.EqualTo(uncapped.Type), $"Seed {seed}");
                Assert.That(capped.ContentData, Is.SameAs(uncapped.ContentData));
            }
        }

        Assert.That(seen, Is.EquivalentTo(new[] { MapContentType.NormalBattle, MapContentType.Event, MapContentType.Shop,
            MapContentType.Rest, MapContentType.Relic }));
        Assert.That(seenEvents, Is.EquivalentTo(new[] { first, second }));

        MapChoice Resolve(int seed, int maximum)
        {
            limit.maxBattles = maximum;
            var state = new RunProgress(run, seed);
            Assert.That(state.TrySelectOption(0), Is.True);
            Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
            Assert.That(state.CompleteNode(0, 0), Is.True);
            Assert.That(state.TrySelectOption(1), Is.True);
            Assert.That(state.CurrentNode.Choices[state.CurrentNode.SelectedIndex].Type, Is.EqualTo(MapContentType.Unknown));
            return state.CurrentNode.SelectedChoice;
        }
    }

    [Test] public void CompletingFloorPreservesHealthAndStartsNextFloor()
    {
        floor.mode = FloorMapMode.NormalBattlesOnly; floor.nodeCount = 1;
        run.floors = new[] { floor, floor };
        var state = new RunProgress(run, 17); state.CaptureHealth(31, 90);
        state.TrySelectOption(0); state.RecordBattleResult(BattleResult.Victory); state.CompleteNode(0, 0);
        Assert.That(state.FloorIndex, Is.EqualTo(1)); Assert.That(state.NodeIndex, Is.Zero);
        Assert.That(state.Phase, Is.EqualTo(RunPhase.Map));
        Assert.That(state.CurrentHP, Is.EqualTo(31)); Assert.That(state.MaxHP, Is.EqualTo(90));
        Assert.That(state.CurrentNode.Status, Is.EqualTo(MapNodeStatus.Pending));
    }
}
