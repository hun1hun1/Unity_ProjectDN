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

    [Test] public void FirstFloorOffersFixedMilestonesAndRespectsBothCaps()
    {
        var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        Assert.That(definition, Is.Not.Null);
        var state = new RunProgress(definition, 42);
        Assert.That(state.Nodes.Count, Is.EqualTo(13));
        Assert.That(Types(), Is.EqualTo(new[] { MapContentType.NormalBattle }));
        for (int i = 0; i < 3; i++) Finish(MapContentType.NormalBattle);
        Assert.That(Types(), Has.Member(MapContentType.EliteBattle));
        Assert.That(Types().Length, Is.EqualTo(3));
        Finish(MapContentType.EliteBattle);
        Assert.That(Types().Length, Is.EqualTo(2));
        Assert.That(Types().Distinct().Count(), Is.EqualTo(2));
        Assert.That(state.CurrentNode.Choices.All(c => !c.IsBattle), Is.True);
        Finish(Types().First(t => t != MapContentType.Unknown));
        Assert.That(Types(), Is.EquivalentTo(new[] { MapContentType.EliteBattle, MapContentType.NormalBattle }));
        Finish(MapContentType.NormalBattle);
        Assert.That(Types(), Is.EqualTo(new[] { MapContentType.Relic }));
        Finish(MapContentType.Relic);
        for (int i = 0; i < 4; i++) Finish(MapContentType.NormalBattle);
        Assert.That(Types().Length, Is.EqualTo(2));
        Assert.That(Types(), Has.Member(MapContentType.Rest));
        Assert.That(Types(), Has.No.Member(MapContentType.NormalBattle));
        Finish(MapContentType.Rest);
        Assert.That(Types(), Is.EqualTo(new[] { MapContentType.BossBattle }));
        Assert.That(state.CurrentNode.Choices[0].Encounter.enemyPrefab.name, Is.EqualTo("LockedGatekeeper"));
        Finish(MapContentType.BossBattle);
        Assert.That(state.Phase, Is.EqualTo(RunPhase.Completed));

        MapContentType[] Types() => state.CurrentNode.Choices.Select(c => c.Type).ToArray();

        void Finish(MapContentType type)
        {
            Assert.That(state.TrySelectOption(Array.IndexOf(Types(), type)), Is.True);
            if (state.Phase == RunPhase.Battle)
                Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
            Assert.That(state.CompleteNode(0, state.NodeIndex), Is.True);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FirstFloorNodeTwelveUnknownFollowsTheCompletedBattleCount(bool capped)
    {
        var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        // This seed offers a mystery at node twelve and resolves it to battle when permitted.
        var state = Enumerable.Range(0, 1000).Select(seed => new RunProgress(definition, seed))
            .First(runState => runState.Nodes[11].Choices.Any(choice => choice.Type == MapContentType.Unknown) &&
                runState.Nodes[7].Choices.Any(choice => choice.Type == MapContentType.Shop || choice.Type == MapContentType.Rest) &&
                new System.Random(unchecked((runState.Seed ^ 73856093 ^ (12 * 19349663)) ^ 0x4F1BBCDC))
                    .NextDouble() < .4);
        var catalog = RunAssetCatalog.Load();
        for (int node = 0; node < 11; node++)
        {
            int index = 0;
            if (node == 7 && !capped)
                index = state.CurrentNode.Choices.ToList().FindIndex(choice => choice.Type != MapContentType.Unknown && !choice.IsBattle);
            if (node == 4)
                index = state.CurrentNode.Choices.ToList().FindIndex(choice => choice.Type != MapContentType.Unknown);
            if (node == 6)
                Assert.That(((ConduitData)state.CurrentNode.Choices[index].ContentData).Rarity, Is.EqualTo(ConduitRarity.Uncommon));
            Assert.That(state.TrySelectOption(index), Is.True);
            if (state.Phase == RunPhase.Battle)
                Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
            Assert.That(state.CompleteNode(0, node), Is.True);
        }

        int unknown = state.CurrentNode.Choices.ToList().FindIndex(choice => choice.Type == MapContentType.Unknown);
        Assert.That(state.TrySelectOption(unknown), Is.True);
        Assert.That(state.CurrentNode.SelectedChoice.Type, Is.EqualTo(capped ? MapContentType.Event : MapContentType.NormalBattle));
        RunProgress restored = RunProgress.Restore(state.Capture(catalog), catalog);
        Assert.That(restored.CurrentNode.SelectedChoice.Type, Is.EqualTo(state.CurrentNode.SelectedChoice.Type));
        Assert.That(restored.CurrentNode.SelectedChoice.Encounter, Is.SameAs(state.CurrentNode.SelectedChoice.Encounter));
    }

    [Test] public void FirstFloorEncounterPoolsContainTheAssignedEnemies()
    {
        var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        FloorMapDefinition firstFloor = definition.floors[0];

        Assert.That(firstFloor.normalBattles.Select(encounter => encounter.enemyPrefab.name),
            Is.EquivalentTo(new[] { "Slime", "Lizard", "FoldedPaperSoldier", "LanternMoth" }));
        Assert.That(firstFloor.lateNormalBattles.Select(encounter => encounter.enemyPrefab.name),
            Is.EquivalentTo(new[] { "SewingSpider", "Scarecrow", "HauntedBroom", "InkCrow", "SheetGhost" }));
        Assert.That(firstFloor.eliteBattles.Select(encounter => encounter.enemyPrefab.name),
            Is.EquivalentTo(new[] { "TinWoodcutter", "BrokenBell", "InvertedHourglass" }));
    }

    [Test]
    public void FirstFloorOffersWeaknessEngraverAsACommonConduit()
    {
        var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        var conduit = definition.floors[0].commonConduitRewards.conduits
            .SingleOrDefault(candidate => candidate != null && candidate.name == "WeaknessEngraver");
        Assert.That(conduit, Is.Not.Null, "The common reward pool must include Weakness Engraver.");
        Assert.That(conduit.Rarity, Is.EqualTo(ConduitRarity.Common));
        Assert.That(conduit.WeaknessExposureBonusTurns, Is.EqualTo(1));
        Assert.That(conduit.MaxAmmoBonus, Is.Zero);
        Assert.That(conduit.Artwork, Is.Not.Null);
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(conduit.Artwork),
            Is.EqualTo("Assets/Images/UI/Conduits/weakness-engraver.png"));
        RunAssetCatalog catalog = RunAssetCatalog.Load();
        Assert.That(catalog.Resolve<ConduitData>(catalog.Key(conduit)), Is.SameAs(conduit));
    }

    [Test]
    public void EliteHunterMedalChangesOnlyEliteOddsAndSurvivesRestore()
    {
        var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        var medal = definition.floors[0].commonConduitRewards.conduits
            .SingleOrDefault(candidate => candidate != null && candidate.name == "EliteHunterMedal");
        Assert.That(medal, Is.Not.Null, "The common reward pool must include Elite Hunter Medal.");
        Assert.That(medal.Rarity, Is.EqualTo(ConduitRarity.Common));
        Assert.That(UnityEditor.AssetDatabase.GetAssetPath(medal.Artwork),
            Is.EqualTo("Assets/Images/UI/Conduits/elite-hunter-medal.png"));

        // This seed rolls between the two cutoffs at both the unknown and the first elite node.
        var state = new RunProgress(definition, 142);
        var draw = typeof(RunProgress).GetMethod("GetEliteVictoryConduit",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        ConduitData DrawElite() => (ConduitData)draw.Invoke(state, null);
        void Select(MapContentType type)
        {
            int index = state.CurrentNode.Choices.ToList().FindIndex(choice => choice.Type == type);
            Assert.That(state.TrySelectOption(index), Is.True);
            if (state.CurrentNode.SelectedChoice.IsBattle)
                Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
        }
        void Finish(MapContentType type)
        {
            Select(type);
            Assert.That(state.CompleteNode(0, state.NodeIndex), Is.True);
        }

        RunRelicInventory.ResetForNewGame();
        try
        {
            Assert.That(medal.Acquire(), Is.True);
            Finish(MapContentType.NormalBattle);
            Select(MapContentType.Unknown);
            Assert.That(state.CurrentNode.SelectedChoice.Type, Is.EqualTo(MapContentType.Relic));
            Assert.That(((ConduitData)state.CurrentNode.SelectedChoice.ContentData).Rarity, Is.EqualTo(ConduitRarity.Common));
            Assert.That(state.CompleteNode(0, state.NodeIndex), Is.True);
            Finish(MapContentType.NormalBattle);
            Select(MapContentType.EliteBattle);

            RunRelicInventory.ResetForNewGame();
            Assert.That(DrawElite().Rarity, Is.EqualTo(ConduitRarity.Common));
            Assert.That(medal.Acquire(), Is.True);
            ConduitData award = DrawElite();
            Assert.That(award.Rarity, Is.EqualTo(ConduitRarity.Uncommon));

            RunAssetCatalog catalog = RunAssetCatalog.Load();
            var saved = JsonUtility.FromJson<RunInventoryData>(JsonUtility.ToJson(RunSaveCodec.CaptureInventory(catalog)));
            state = RunProgress.Restore(state.Capture(catalog), catalog);
            RunRelicInventory.ResetForNewGame();
            RunSaveCodec.RestoreInventory(saved, catalog);
            Assert.That(RunRelicInventory.Contains(medal), Is.True);
            Assert.That(DrawElite(), Is.SameAs(award));

            Assert.That(state.CompleteNode(0, state.NodeIndex), Is.True);
            Finish(MapContentType.NormalBattle);
            Finish(MapContentType.NormalBattle);
            Assert.That(state.CurrentNode.Rule, Is.EqualTo(MapNodeRule.GuaranteedRelic));
            Assert.That(((ConduitData)state.CurrentNode.Choices.Single().ContentData).Rarity,
                Is.EqualTo(ConduitRarity.Common));
        }
        finally { RunRelicInventory.ResetForNewGame(); }
    }

    [TestCase(24, ConduitRarity.Common)]
    [TestCase(27, ConduitRarity.Uncommon)]
    public void FirstFloorEliteRewardsUseTheRolledGradeAndReserveCandidatesForNodeSeven(int seed, ConduitRarity expected)
    {
        var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        var state = new RunProgress(definition, seed);
        for (int node = 0; node < 3; node++)
        {
            Assert.That(state.TrySelectOption(0), Is.True);
            Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
            Assert.That(state.CompleteNode(0, node), Is.True);
        }
        Assert.That(state.TrySelectOption(0), Is.True);
        Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);

        var draw = typeof(RunProgress).GetMethod("GetEliteVictoryConduit",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        RunAssetCatalog catalog = RunAssetCatalog.Load();
        RunRelicInventory.ResetForNewGame();
        try
        {
            var drawn = new HashSet<string>();
            ConduitRarity fallback = expected == ConduitRarity.Common ? ConduitRarity.Uncommon : ConduitRarity.Common;
            RunConduitOrderData order = state.Capture(catalog).conduitOrder;
            int preferredCount = (expected == ConduitRarity.Common ? order.common.Length : order.uncommon.Length) - 1;
            int randomCandidateCount = order.common.Length + order.uncommon.Length - 2;
            for (int i = 0; i < randomCandidateCount; i++)
            {
                var conduit = (ConduitData)draw.Invoke(state, null);
                Assert.That(conduit, Is.Not.Null);
                Assert.That(new[] { "FoldingCover", "ThornThicket", "AutomaticLoader",
                    "BarrierGenerator", "AntiRustOil", "DreamBookmark", "NightmareBookmark", "WeaknessEngraver",
                    "MerchantGuildToken", "EliteHunterMedal", "HalfMoonChalice", "MidasHand",
                    "EmergencyBandage", "TravelersCanteen" }, Does.Contain(conduit.name));
                Assert.That(conduit.Rarity, Is.EqualTo(i < preferredCount ? expected : fallback));
                Assert.That(drawn.Add(conduit.name), Is.True);
                Assert.That(conduit.Acquire(), Is.True);
                RunInventoryData saved = RunSaveCodec.CaptureInventory(catalog);
                state = RunProgress.Restore(state.Capture(catalog), catalog);
                RunRelicInventory.ResetForNewGame();
                RunSaveCodec.RestoreInventory(saved, catalog);
            }
            Assert.That(draw.Invoke(state, null), Is.Null, "Both grades must retain their final candidate for node seven.");
        }
        finally { RunRelicInventory.ResetForNewGame(); }
    }

    [TestCase(142, ConduitRarity.Common, false)]
    [TestCase(31, ConduitRarity.Uncommon, true)]
    public void FirstFloorUnknownEliteAndNodeSevenShareTheSavedOrder(int seed, ConduitRarity unknownRarity, bool midpointElite)
    {
        var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        RunAssetCatalog catalog = RunAssetCatalog.Load();
        RunRelicInventory.ResetForNewGame();
        try
        {
            var state = new RunProgress(definition, seed);
            RunConduitOrderData order = state.Capture(catalog).conduitOrder;
            ConduitData Common(int index) => catalog.Resolve<ConduitData>(order.common[index]);
            ConduitData Uncommon(int index) => catalog.Resolve<ConduitData>(order.uncommon[index]);

            var draw = typeof(RunProgress).GetMethod("GetEliteVictoryConduit",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var completeWithAward = typeof(RunProgress).GetMethod("CompleteNode",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            void Select(MapContentType type)
            {
                int index = state.CurrentNode.Choices.ToList().FindIndex(choice => choice.Type == type);
                Assert.That(state.TrySelectOption(index), Is.True, $"Node {state.NodeIndex + 1}: {type}");
                if (state.CurrentNode.SelectedChoice.IsBattle)
                    Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
            }
            void Finish(MapContentType type)
            {
                Select(type);
                Assert.That(state.CompleteNode(0, state.NodeIndex), Is.True);
            }
            ConduitData DrawElite() => (ConduitData)draw.Invoke(state, null);

            Finish(MapContentType.NormalBattle);
            Select(MapContentType.Unknown);
            ConduitData unknown = unknownRarity == ConduitRarity.Common ? Common(0) : Uncommon(0);
            Assert.That(state.CurrentNode.SelectedChoice.ContentData, Is.SameAs(unknown));
            state = RunProgress.Restore(state.Capture(catalog), catalog);
            Assert.That(state.CurrentNode.SelectedChoice.ContentData, Is.SameAs(unknown));
            Assert.That(state.CompleteNode(0, 1), Is.True); // Pass without acquiring the revealed item.
            Assert.That(RunRelicInventory.Contains(unknown), Is.False);

            Finish(MapContentType.NormalBattle);
            Select(MapContentType.EliteBattle);
            ConduitData firstElite = DrawElite();
            Assert.That(firstElite, Is.SameAs(unknownRarity == ConduitRarity.Common ? Common(1) : Uncommon(1)));
            Assert.That(firstElite.Acquire(), Is.True);
            Assert.That(state.CompleteNode(0, 3), Is.True);
            Finish(MapContentType.NormalBattle);

            if (midpointElite)
            {
                Select(MapContentType.EliteBattle);
                ConduitData midpointAward = DrawElite();
                Assert.That(midpointAward, Is.SameAs(Uncommon(2)));
                Assert.That(completeWithAward.Invoke(state, new object[] { 0, 5, midpointAward }), Is.True);
                Assert.That(midpointAward.Acquire(), Is.True);
            }
            else Finish(MapContentType.NormalBattle);

            Assert.That(state.CurrentNode.Rule, Is.EqualTo(MapNodeRule.GuaranteedRelic));
            ConduitData guarantee = midpointElite ? Uncommon(3) : Common(2);
            Assert.That(state.CurrentNode.Choices.Single().ContentData, Is.SameAs(guarantee));
            state = RunProgress.Restore(state.Capture(catalog), catalog);
            Assert.That(state.CurrentNode.Choices.Single().ContentData, Is.SameAs(guarantee));

            // A checkpoint without a saved order still retains the offer on node seven.
            var checkpoint = state.Capture(catalog);
            checkpoint.conduitOrder = null;
            ConduitData savedOffer = midpointElite ? Uncommon(4) : Common(3);
            checkpoint.nodes[6].choices[0].content = catalog.Key(savedOffer);
            state = RunProgress.Restore(checkpoint, catalog);
            Assert.That(state.CurrentNode.Choices.Single().ContentData, Is.SameAs(savedOffer));
            Assert.That(state.Capture(catalog).conduitOrder.common, Is.EqualTo(order.common));
        }
        finally { RunRelicInventory.ResetForNewGame(); }
    }

    [Test] public void FirstFloorNormalEncounterOrderSurvivesRestoreWithoutRepeatsInEitherSegment()
    {
        var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/MapData/FirstFloorRun.asset");
        var state = new RunProgress(definition, 41);
        var catalog = RunAssetCatalog.Load();
        var early = new List<BattleEncounterDefinition>();
        var late = new List<BattleEncounterDefinition>();

        for (int node = 0; node <= 10; node++)
        {
            int index = state.CurrentNode.Choices.ToList().FindIndex(choice => node == 4 || node == 6
                ? !choice.IsBattle && choice.Type != MapContentType.Unknown
                : choice.Type == MapContentType.NormalBattle);
            Assert.That(state.TrySelectOption(index), Is.True);
            if (state.Phase == RunPhase.Battle)
            {
                BattleEncounterDefinition chosen = state.CurrentNode.SelectedChoice.Encounter;
                state = RunProgress.Restore(state.Capture(catalog), catalog);
                Assert.That(state.CurrentNode.SelectedChoice.Encounter, Is.SameAs(chosen));
                (node < 5 ? early : late).Add(chosen);
                Assert.That(state.RecordBattleResult(BattleResult.Victory), Is.True);
            }
            Assert.That(state.CompleteNode(0, node), Is.True);
        }

        Assert.That(early, Is.EquivalentTo(definition.floors[0].normalBattles));
        Assert.That(late, Is.EquivalentTo(definition.floors[0].lateNormalBattles));
    }

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
        floor.unknown.relics = new[] { Asset<ConduitData>() };
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
