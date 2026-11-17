using System;
using System.Collections.Generic;

internal static class UnknownNodeResolver
{
    internal static bool IsOutcome(MapContentType type) =>
        type == MapContentType.NormalBattle || type == MapContentType.Event || type == MapContentType.Shop ||
        type == MapContentType.Rest || type == MapContentType.Relic;

    internal static bool HasOutcome(UnknownNodeSettings settings, bool canBattle, Func<MapContentType, bool> available,
        Func<AmmoCapacityRelic, bool> canOfferRelic = null) =>
        GetOutcomes(settings, canBattle, available, AvailableRelics(settings, canOfferRelic)).Count != 0;

    internal static MapChoice Resolve(FloorMapDefinition floor, bool canBattle, Func<MapContentType, bool> available, Random random,
        Func<AmmoCapacityRelic, bool> canOfferRelic = null)
    {
        UnknownNodeSettings settings = floor.unknown;
        var relics = AvailableRelics(settings, canOfferRelic);
        var outcomes = GetOutcomes(settings, canBattle, available, relics);
        long total = 0;
        foreach (var outcome in outcomes) total += outcome.weight;
        if (total == 0) return null;

        double roll = random.NextDouble() * total;
        foreach (var outcome in outcomes)
        {
            roll -= outcome.weight;
            if (roll >= 0) continue;
            switch (outcome.type)
            {
                case MapContentType.NormalBattle:
                    return floor.mode == FloorMapMode.FirstFloor
                        ? new MapChoice(outcome.type)
                        : new MapChoice(outcome.type, floor.normalBattles[random.Next(floor.normalBattles.Length)]);
                case MapContentType.Event:
                    return new MapChoice(outcome.type, contentData: settings.events[random.Next(settings.events.Length)]);
                case MapContentType.Relic:
                    return new MapChoice(outcome.type, contentData: relics[random.Next(relics.Count)]);
                default:
                    return new MapChoice(outcome.type);
            }
        }
        return null;
    }

    private static List<(MapContentType type, long weight)> GetOutcomes(UnknownNodeSettings settings, bool canBattle,
        Func<MapContentType, bool> available, List<AmmoCapacityRelic> relics)
    {
        var outcomes = new List<(MapContentType type, long weight)>(5);
        if (settings == null) return outcomes;
        long transferred = canBattle ? 0 : settings.battleWeight;
        bool hasEvent = available(MapContentType.Event) && settings.events?.Length > 0;
        // Preserve the other outcomes' weights when the combat cap transfers its share.
        if (transferred > 0 && !hasEvent) return outcomes;
        if (canBattle) Add(MapContentType.NormalBattle, settings.battleWeight);
        if (hasEvent) Add(MapContentType.Event, settings.eventWeight + transferred);
        Add(MapContentType.Shop, settings.shopWeight);
        Add(MapContentType.Rest, settings.restWeight);
        if (settings.relicWeight > 0 && available(MapContentType.Relic) && relics.Count > 0)
            outcomes.Add((MapContentType.Relic, settings.relicWeight));
        return outcomes;

        void Add(MapContentType type, long weight)
        {
            if (weight > 0 && available(type)) outcomes.Add((type, weight));
        }
    }

    private static List<AmmoCapacityRelic> AvailableRelics(UnknownNodeSettings settings, Func<AmmoCapacityRelic, bool> canOfferRelic)
    {
        var relics = new List<AmmoCapacityRelic>();
        if (settings?.relics == null) return relics;
        foreach (AmmoCapacityRelic relic in settings.relics)
            if (relic != null && relic.HasEffect && !RunRelicInventory.Contains(relic) && (canOfferRelic?.Invoke(relic) ?? true))
                relics.Add(relic);
        return relics;
    }
}
