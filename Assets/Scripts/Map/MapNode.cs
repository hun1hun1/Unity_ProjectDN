using System.Collections.Generic;
using UnityEngine;

public enum MapContentType { NormalBattle, EliteBattle, BossBattle, Shop, Relic, Event, Upgrade, Rest }
public enum MapNodeRule { Start, Regular, Midpoint, GuaranteedRelic, Boss }
public enum MapNodeStatus { Pending, Resolving, Completed }
public enum RunPhase { Map, Battle, Rewards, Content, Completed, Defeated, Cancelled }

public sealed class MapChoice
{
    public MapContentType Type { get; }
    public BattleEncounterDefinition Encounter { get; }
    public ScriptableObject ContentData { get; }
    public bool IsBattle => Type == MapContentType.NormalBattle || Type == MapContentType.EliteBattle || Type == MapContentType.BossBattle;

    public MapChoice(MapContentType type, BattleEncounterDefinition encounter = null, ScriptableObject contentData = null)
    { Type = type; Encounter = encounter; ContentData = contentData; }
}

public sealed class MapNode
{
    public MapNodeRule Rule { get; }
    public IReadOnlyList<MapChoice> Choices { get; }
    public MapNodeStatus Status { get; internal set; }
    public MapChoice SelectedChoice { get; internal set; }

    internal MapNode(MapNodeRule rule, List<MapChoice> choices)
    { Rule = rule; Choices = choices.AsReadOnly(); }
}
