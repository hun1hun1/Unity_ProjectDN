using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RunProgress
{
    public RunDefinition Definition { get; }
    public int Seed { get; }
    public int FloorIndex { get; private set; }
    public int NodeIndex { get; private set; }
    public int CompletedNodes { get; private set; }
    public int CurrentHP { get; private set; }
    public int MaxHP { get; private set; }
    public RunPhase Phase { get; private set; } = RunPhase.Map;
    public IReadOnlyList<MapNode> Nodes { get; private set; }
    public MapNode CurrentNode => NodeIndex < Nodes.Count ? Nodes[NodeIndex] : null;
    public bool IsFinished => Phase == RunPhase.Completed || Phase == RunPhase.Defeated || Phase == RunPhase.Cancelled;
    private readonly MapContentType[] enabledContent;

    public RunProgress(RunDefinition definition, int seed, IReadOnlyCollection<MapContentType> supportedContent = null)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (definition.startingMaxHP < 1 || definition.floors == null || definition.floors.Length == 0 ||
            string.IsNullOrWhiteSpace(definition.mapScene) || string.IsNullOrWhiteSpace(definition.battleScene))
            throw new InvalidOperationException("A run needs positive HP, scenes and at least one floor.");
        if (definition.enabledContent == null) throw new InvalidOperationException("Enabled content must be configured.");
        var enabled = new List<MapContentType>();
        foreach (MapContentType type in definition.enabledContent)
        {
            if (supportedContent == null) { enabled.Add(type); continue; }
            foreach (MapContentType available in supportedContent)
                if (available == type) { enabled.Add(type); break; }
        }
        enabledContent = enabled.ToArray();
        foreach (FloorMapDefinition floor in definition.floors) MapGenerator.Validate(floor, enabledContent);
        Definition = definition; Seed = seed; MaxHP = CurrentHP = definition.startingMaxHP;
        Nodes = GenerateFloor(0);
    }

    private IReadOnlyList<MapNode> GenerateFloor(int floor) =>
        MapGenerator.Generate(Definition.floors[floor], enabledContent, unchecked(Seed + floor * 7919));

    public bool TrySelectOption(int index)
    {
        if (Phase != RunPhase.Map || CurrentNode == null || CurrentNode.Status != MapNodeStatus.Pending ||
            index < 0 || index >= CurrentNode.Choices.Count) return false;
        CurrentNode.SelectedChoice = CurrentNode.Choices[index];
        CurrentNode.Status = MapNodeStatus.Resolving;
        Phase = CurrentNode.SelectedChoice.IsBattle ? RunPhase.Battle : RunPhase.Content;
        return true;
    }

    public void CaptureHealth(int currentHP, int maxHP)
    {
        if (maxHP < 1) throw new ArgumentOutOfRangeException(nameof(maxHP));
        MaxHP = maxHP; CurrentHP = Mathf.Clamp(currentHP, 0, maxHP);
    }

    public bool RecordBattleResult(BattleResult result)
    {
        if (Phase != RunPhase.Battle || result == BattleResult.None || !Enum.IsDefined(typeof(BattleResult), result)) return false;
        Phase = result == BattleResult.Victory ? RunPhase.Rewards : result == BattleResult.Defeat ? RunPhase.Defeated : RunPhase.Cancelled;
        return true;
    }

    public bool CompleteNode(int expectedFloor, int expectedNode)
    {
        if (expectedFloor != FloorIndex || expectedNode != NodeIndex || (Phase != RunPhase.Rewards && Phase != RunPhase.Content)) return false;
        IReadOnlyList<MapNode> nextFloor = null;
        if (NodeIndex == Nodes.Count - 1 && FloorIndex + 1 < Definition.floors.Length) nextFloor = GenerateFloor(FloorIndex + 1);
        CurrentNode.Status = MapNodeStatus.Completed;
        CompletedNodes++; NodeIndex++;
        if (NodeIndex == Nodes.Count)
        {
            if (nextFloor == null) { Phase = RunPhase.Completed; return true; }
            FloorIndex++; NodeIndex = 0; Nodes = nextFloor;
        }
        Phase = RunPhase.Map;
        return true;
    }
}
