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
    public int NodeSeed => unchecked(Seed ^ (FloorIndex+1)*73856093 ^ (NodeIndex+1)*19349663);
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

    public RunProgressData Capture(RunAssetCatalog catalog)
    {
        var saved=new RunNodeData[Nodes.Count];
        for(int i=0;i<Nodes.Count;i++)
        {
            MapNode node=Nodes[i];var choices=new RunChoiceData[node.Choices.Count];
            for(int j=0;j<choices.Length;j++)
            {
                choices[j] = SaveChoice(node.Choices[j], catalog);
            }
            saved[i] = new RunNodeData
            {
                rule = node.Rule, status = node.Status, selected = node.SelectedIndex, choices = choices,
                hasResolvedChoice = node.ResolvedChoice != null,
                resolvedChoice = node.ResolvedChoice == null ? null : SaveChoice(node.ResolvedChoice, catalog)
            };
        }
        return new RunProgressData {definition=catalog.Key(Definition),seed=Seed,floor=FloorIndex,node=NodeIndex,completed=CompletedNodes,
            hp=CurrentHP,maxHP=MaxHP,phase=Phase,enabled=(MapContentType[])enabledContent.Clone(),nodes=saved};
    }
    private static RunChoiceData SaveChoice(MapChoice choice, RunAssetCatalog catalog) =>
        new RunChoiceData { type = choice.Type, encounter = catalog.Key(choice.Encounter), content = catalog.Key(choice.ContentData) };

    private static MapChoice RestoreChoice(RunChoiceData saved, MapContentType[] enabled, RunAssetCatalog catalog)
    {
        if (saved == null || !Enum.IsDefined(typeof(MapContentType), saved.type) || Array.IndexOf(enabled, saved.type) < 0)
            throw new InvalidOperationException("Invalid map choice.");
        var choice = new MapChoice(saved.type, catalog.Resolve<BattleEncounterDefinition>(saved.encounter),
            catalog.Resolve<ScriptableObject>(saved.content));
        if (choice.IsBattle)
        {
            if (choice.Encounter == null) throw new InvalidOperationException("Missing encounter.");
            choice.Encounter.Validate();
        }
        return choice;
    }

    public static RunProgress Restore(RunProgressData data,RunAssetCatalog catalog)
    {
        if(data==null || data.enabled==null || data.nodes==null)throw new InvalidOperationException("Missing run progress.");
        RunDefinition definition=catalog.Resolve<RunDefinition>(data.definition);
        if(definition==null || data.floor<0 || data.floor>=definition.floors.Length || data.node<0 ||
            data.node>=data.nodes.Length || data.nodes.Length!=definition.floors[data.floor].nodeCount ||
            data.maxHP<1 || data.hp<1 || data.hp>data.maxHP || data.phase<RunPhase.Map || data.phase>RunPhase.Content)
            throw new InvalidOperationException("Invalid run progress.");
        int completed=data.node;
        for(int i=0;i<data.floor;i++)completed+=definition.floors[i].nodeCount;
        if(data.completed!=completed)throw new InvalidOperationException("Invalid completed node count.");
        var result=new RunProgress(definition,data.seed,data.enabled);
        var nodes=new List<MapNode>();
        for(int i=0;i<data.nodes.Length;i++)
        {
            RunNodeData saved=data.nodes[i];
            if(saved==null || saved.choices==null || saved.choices.Length<1 || saved.choices.Length>3 ||
                !Enum.IsDefined(typeof(MapNodeRule),saved.rule) || !Enum.IsDefined(typeof(MapNodeStatus),saved.status) ||
                saved.selected< -1 || saved.selected>=saved.choices.Length)
                throw new InvalidOperationException("Invalid map node.");
            bool resolving=i==data.node && data.phase!=RunPhase.Map;
            MapNodeStatus expected=i<data.node?MapNodeStatus.Completed:resolving?MapNodeStatus.Resolving:MapNodeStatus.Pending;
            if(saved.status!=expected || (expected==MapNodeStatus.Pending)!=(saved.selected==-1))throw new InvalidOperationException("Invalid node selection.");
            var choices=new List<MapChoice>();
            foreach (RunChoiceData choice in saved.choices) choices.Add(RestoreChoice(choice, data.enabled, catalog));
            bool selectedUnknown = saved.selected >= 0 && choices[saved.selected].Type == MapContentType.Unknown;
            if (selectedUnknown != saved.hasResolvedChoice) throw new InvalidOperationException("Invalid unknown outcome.");
            var node = new MapNode(saved.rule, choices) { Status = saved.status, SelectedIndex = saved.selected };
            if (selectedUnknown)
            {
                MapChoice outcome = RestoreChoice(saved.resolvedChoice, data.enabled, catalog);
                if (!UnknownNodeResolver.IsOutcome(outcome.Type) || (outcome.IsBattle != (outcome.Encounter != null)) ||
                    (outcome.Type == MapContentType.Event && !(outcome.ContentData is MapEventDefinition)) ||
                    (outcome.Type == MapContentType.Relic && !(outcome.ContentData is AmmoCapacityRelic)))
                    throw new InvalidOperationException("Invalid unknown outcome.");
                node.ResolvedChoice = outcome;
            }
            if(resolving && node.SelectedChoice.IsBattle!=(data.phase!=RunPhase.Content))throw new InvalidOperationException("Invalid node phase.");
            nodes.Add(node);
        }
        result.FloorIndex=data.floor;result.NodeIndex=data.node;result.CompletedNodes=data.completed;
        result.CurrentHP=data.hp;result.MaxHP=data.maxHP;result.Phase=data.phase;result.Nodes=nodes.AsReadOnly();
        return result;
    }
    internal void RestoreCheckpointInPlace(RunProgressData data,RunAssetCatalog catalog)
    {
        RunProgress restored=Restore(data,catalog);
        if(restored.Definition!=Definition || restored.Seed!=Seed)throw new InvalidOperationException("Checkpoint belongs to a different run.");
        FloorIndex=restored.FloorIndex;NodeIndex=restored.NodeIndex;CompletedNodes=restored.CompletedNodes;
        CurrentHP=restored.CurrentHP;MaxHP=restored.MaxHP;Phase=restored.Phase;Nodes=restored.Nodes;
    }
    internal void UndoNodeEntry()
    {
        CurrentNode.SelectedIndex = -1;
        CurrentNode.ResolvedChoice = null;
        CurrentNode.Status = MapNodeStatus.Pending;
        Phase = RunPhase.Map;
    }

    private bool CanEnterBattle()
    {
        if (CurrentNode.Rule != MapNodeRule.Start && CurrentNode.Rule != MapNodeRule.Regular) return true;
        MapBattleLimit limit = Definition.floors[FloorIndex].GetBattleLimit(NodeIndex + 1);
        if (limit == null) return true;

        int completedBattles = 0;
        for (int i = limit.firstNode - 1; i < NodeIndex; i++)
        {
            MapNode node = Nodes[i];
            if (node.Status == MapNodeStatus.Completed && (node.Rule == MapNodeRule.Start || node.Rule == MapNodeRule.Regular) &&
                node.SelectedChoice.IsBattle) completedBattles++;
        }
        return completedBattles < limit.maxBattles;
    }

    internal bool IsBattleOptionBlocked(MapChoice choice) => choice.IsBattle && !CanEnterBattle();

    internal bool CanChooseOption(int index, Func<MapContentType, bool> canDispatch)
    {
        if (Phase != RunPhase.Map || CurrentNode == null || CurrentNode.Status != MapNodeStatus.Pending ||
            index < 0 || index >= CurrentNode.Choices.Count) return false;
        MapChoice choice = CurrentNode.Choices[index];
        if (choice.Type == MapContentType.Unknown)
            return UnknownNodeResolver.HasOutcome(Definition.floors[FloorIndex].unknown, CanEnterBattle(), IsAvailable);
        return !IsBattleOptionBlocked(choice) && IsAvailable(choice.Type);

        bool IsAvailable(MapContentType type) => MapGenerator.Contains(enabledContent, type) && (canDispatch?.Invoke(type) ?? true);
    }

    public bool TrySelectOption(int index) => TrySelectOption(index, null);

    internal bool TrySelectOption(int index, Func<MapContentType, bool> canDispatch)
    {
        if (!CanChooseOption(index, canDispatch)) return false;
        MapChoice choice = CurrentNode.Choices[index];
        if (choice.Type == MapContentType.Unknown)
        {
            var random = new System.Random(unchecked(NodeSeed ^ 0x4F1BBCDC));
            MapChoice outcome = UnknownNodeResolver.Resolve(Definition.floors[FloorIndex], CanEnterBattle(),
                type => MapGenerator.Contains(enabledContent, type) && (canDispatch?.Invoke(type) ?? true), random);
            if (outcome == null) return false;
            CurrentNode.ResolvedChoice = outcome;
        }
        CurrentNode.SelectedIndex = index;
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
