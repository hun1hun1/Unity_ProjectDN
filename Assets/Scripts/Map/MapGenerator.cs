using System;
using System.Collections.Generic;
using UnityEngine;

public static class MapGenerator
{
    public static IReadOnlyList<MapNode> Generate(FloorMapDefinition definition, IReadOnlyCollection<MapContentType> enabled, int seed)
    {
        Validate(definition, enabled);
        var random = new System.Random(seed);
        var nodes = new List<MapNode>(definition.nodeCount);
        bool planned = definition.mode == FloorMapMode.PlannedFloor;
        bool hasFixedShop = !planned && definition.fixedShopNodeNumber > 0 && Contains(enabled, MapContentType.Shop);
        bool hasFixedRest = !planned && definition.fixedRestNodeNumber > 0 && Contains(enabled, MapContentType.Rest);
        bool hasFixedRelic = !planned && definition.fixedRelicNodeNumber > 0 && Contains(enabled, MapContentType.Relic);
        int midpoint = -1;
        if (planned)
        {
            int min = Math.Max(2, (int)Math.Ceiling(definition.nodeCount * definition.midpointMinRatio));
            int max = Math.Min(definition.nodeCount - 2, (int)Math.Floor(definition.nodeCount * definition.midpointMaxRatio));
            midpoint = min <= max ? random.Next(min, max + 1) - 1 :
                Mathf.Clamp(Mathf.RoundToInt(definition.nodeCount * (definition.midpointMinRatio + definition.midpointMaxRatio) * .5f), 2, definition.nodeCount - 2) - 1;
        }
        var possibleBattles = new Dictionary<MapBattleLimit, int>();
        for (int i = 0; i < definition.nodeCount; i++)
        {
            var choices = new List<MapChoice>();
            MapNodeRule rule;
            bool hasUnknown = Contains(enabled, MapContentType.Unknown) && Array.IndexOf(definition.unknown?.nodeNumbers ?? Array.Empty<int>(), i + 1) >= 0;
            if (planned && i == definition.nodeCount - 1)
            { rule = MapNodeRule.Boss; choices.Add(Battle(MapContentType.BossBattle, definition.bossBattles, random)); }
            else if (planned && i == midpoint + 1)
            { rule = MapNodeRule.GuaranteedRelic; choices.Add(new MapChoice(MapContentType.Relic, contentData: definition.guaranteedRelicData)); }
            else
            {
                rule = i == 0 ? MapNodeRule.Start : planned && i == midpoint ? MapNodeRule.Midpoint : MapNodeRule.Regular;
                choices.Add(Battle(MapContentType.NormalBattle, definition.normalBattles, random));
                if (hasFixedShop && i + 1 == definition.fixedShopNodeNumber)
                    choices.Add(new MapChoice(MapContentType.Shop));
                if (hasFixedRest && i + 1 == definition.fixedRestNodeNumber)
                    choices.Add(new MapChoice(MapContentType.Rest));
                if (hasFixedRelic && i + 1 == definition.fixedRelicNodeNumber)
                    choices.Add(new MapChoice(MapContentType.Relic, contentData: definition.guaranteedRelicData));
                if (rule == MapNodeRule.Midpoint)
                    choices.Add(Battle(MapContentType.EliteBattle, definition.eliteBattles, random));
                else if (planned && rule == MapNodeRule.Regular)
                {
                    if (!hasUnknown && random.NextDouble() < definition.nonBattleChance)
                    {
                        WeightedMapContent selected = PickContent(definition.nonBattleContents, enabled, random);
                        if (selected != null) choices.Add(new MapChoice(selected.type, contentData: selected.contentData));
                    }
                    if (Contains(enabled, MapContentType.EliteBattle) && random.NextDouble() < definition.eliteChance)
                        choices.Add(Battle(MapContentType.EliteBattle, definition.eliteBattles, random));
                }
            }
            if (hasUnknown && (rule == MapNodeRule.Start || rule == MapNodeRule.Regular))
                choices.Add(new MapChoice(MapContentType.Unknown));
            if (choices.Count > 3)
                throw new InvalidOperationException($"Node {i + 1} has more than three choices.");
            MapBattleLimit limit = definition.GetBattleLimit(i + 1);
            if (limit != null && (rule == MapNodeRule.Start || rule == MapNodeRule.Regular))
            {
                possibleBattles.TryGetValue(limit, out int previousBattles);
                if (previousBattles >= limit.maxBattles &&
                    !choices.Exists(choice => choice.Type == MapContentType.Unknown
                        ? UnknownNodeResolver.HasOutcome(definition.unknown, false, type => type != MapContentType.Relic && Contains(enabled, type))
                        : !choice.IsBattle))
                    throw new InvalidOperationException($"Node {i + 1} needs a nonbattle choice when its battle limit is reached.");
                possibleBattles[limit] = previousBattles + 1;
            }
            nodes.Add(new MapNode(rule, choices));
        }
        return nodes.AsReadOnly();
    }

    public static void Validate(FloorMapDefinition definition, IReadOnlyCollection<MapContentType> enabled)
    {
        if (definition == null || enabled == null) throw new ArgumentNullException("Floor definition and enabled content are required.");
        if (!Enum.IsDefined(typeof(FloorMapMode), definition.mode) || definition.nodeCount < 1)
            throw new InvalidOperationException("A floor needs a valid mode and at least one node.");
        Require(enabled, MapContentType.NormalBattle); ValidatePool(definition.normalBattles);
        ValidateUnknownAndLimits(definition, enabled);
        if (definition.mode == FloorMapMode.NormalBattlesOnly)
        {
            if (definition.fixedShopNodeNumber < 0 || definition.fixedShopNodeNumber > definition.nodeCount)
                throw new InvalidOperationException("The fixed shop must be disabled (0) or use an existing node number.");
            if (definition.fixedRestNodeNumber < 0 || definition.fixedRestNodeNumber > definition.nodeCount)
                throw new InvalidOperationException("The fixed rest choice must be disabled (0) or use an existing node number.");
            if (definition.fixedRelicNodeNumber < 0 || definition.fixedRelicNodeNumber > definition.nodeCount)
                throw new InvalidOperationException("The fixed Conduit choice must be disabled (0) or use an existing node number.");
            if (definition.fixedRelicNodeNumber > 0 && Contains(enabled, MapContentType.Relic) &&
                (!(definition.guaranteedRelicData is AmmoCapacityRelic relic) || relic.MaxAmmoBonus <= 0))
                throw new InvalidOperationException("A fixed Conduit choice needs a valid Conduit asset.");
            return;
        }
        if (definition.nodeCount < 4) throw new InvalidOperationException("Planned floors need at least four nodes.");
        if (!Probability(definition.nonBattleChance) || !Probability(definition.eliteChance) ||
            !Probability(definition.midpointMinRatio) || !Probability(definition.midpointMaxRatio) || definition.midpointMinRatio > definition.midpointMaxRatio)
            throw new InvalidOperationException("Map probabilities and ordered midpoint ratios must be between zero and one.");
        Require(enabled, MapContentType.EliteBattle); Require(enabled, MapContentType.BossBattle); Require(enabled, MapContentType.Relic);
        ValidatePool(definition.eliteBattles); ValidatePool(definition.bossBattles);
        if (definition.nonBattleContents == null) return;
        foreach (WeightedMapContent entry in definition.nonBattleContents)
            if (entry == null || entry.weight < 0 || entry.type < MapContentType.Shop || entry.type > MapContentType.Rest)
                throw new InvalidOperationException("Nonbattle weights need a nonbattle type and nonnegative weight.");
    }

    private static void ValidateUnknownAndLimits(FloorMapDefinition floor, IReadOnlyCollection<MapContentType> enabled)
    {
        var occupied = new HashSet<int>();
        foreach (MapBattleLimit limit in floor.battleLimits ?? Array.Empty<MapBattleLimit>())
        {
            if (limit == null || limit.firstNode < 1 || limit.lastNode < limit.firstNode ||
                limit.lastNode > floor.nodeCount || limit.maxBattles < 0)
                throw new InvalidOperationException("A battle limit needs a valid node range and a nonnegative limit.");
            for (int i = limit.firstNode; i <= limit.lastNode; i++)
                if (!occupied.Add(i)) throw new InvalidOperationException("Battle limit ranges must not overlap.");
        }

        UnknownNodeSettings settings = floor.unknown;
        if (!Contains(enabled, MapContentType.Unknown) || settings?.nodeNumbers == null || settings.nodeNumbers.Length == 0) return;
        occupied.Clear();
        foreach (int node in settings.nodeNumbers)
            if (node < 1 || node > floor.nodeCount || !occupied.Add(node))
                throw new InvalidOperationException("Unknown nodes must use distinct existing node numbers.");

        if (settings.battleWeight < 0 || settings.eventWeight < 0 || settings.shopWeight < 0 ||
            settings.restWeight < 0 || settings.relicWeight < 0)
            throw new InvalidOperationException("Unknown outcome weights must be nonnegative.");
        foreach (MapEventDefinition mapEvent in settings.events ?? Array.Empty<MapEventDefinition>())
            if (mapEvent == null || string.IsNullOrWhiteSpace(mapEvent.title) || mapEvent.goldReward < 0)
                throw new InvalidOperationException("The floor event pool contains an invalid event.");
        foreach (AmmoCapacityRelic relic in settings.relics ?? Array.Empty<AmmoCapacityRelic>())
            if (relic == null || relic.MaxAmmoBonus <= 0)
                throw new InvalidOperationException("The unknown relic pool contains an invalid relic.");
        foreach (int node in settings.nodeNumbers)
            if (floor.GetBattleLimit(node) != null && settings.battleWeight > 0 &&
                (!Contains(enabled, MapContentType.Event) || (settings.events == null || settings.events.Length == 0)))
                throw new InvalidOperationException("A capped unknown node needs an event pool to receive the blocked battle weight.");
    }

    private static bool Probability(float value) => !float.IsNaN(value) && value >= 0 && value <= 1;
    private static void Require(IReadOnlyCollection<MapContentType> enabled, MapContentType type)
    { if (!Contains(enabled, type)) throw new InvalidOperationException($"Required map content is disabled: {type}"); }
    internal static bool Contains(IReadOnlyCollection<MapContentType> enabled, MapContentType type)
    { foreach (MapContentType item in enabled) if (item == type) return true; return false; }
    private static void ValidatePool(BattleEncounterDefinition[] pool)
    {
        if (pool == null || pool.Length == 0) throw new InvalidOperationException("A required battle pool is empty.");
        foreach (BattleEncounterDefinition encounter in pool)
        { if (encounter == null) throw new InvalidOperationException("A battle pool contains an empty reference."); encounter.Validate(); }
    }
    private static MapChoice Battle(MapContentType type, BattleEncounterDefinition[] pool, System.Random random) =>
        new MapChoice(type, pool[random.Next(pool.Length)]);
    private static WeightedMapContent PickContent(WeightedMapContent[] entries, IReadOnlyCollection<MapContentType> enabled, System.Random random)
    {
        double total = 0;
        if (entries == null) return null;
        foreach (WeightedMapContent entry in entries) if (Contains(enabled, entry.type)) total += entry.weight;
        if (total <= 0) return null;
        double roll = random.NextDouble() * total;
        foreach (WeightedMapContent entry in entries)
        {
            if (!Contains(enabled, entry.type) || entry.weight == 0) continue;
            roll -= entry.weight;
            if (roll < 0) return entry;
        }
        return null;
    }
}
