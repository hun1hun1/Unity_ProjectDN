using System;

[Serializable]
public sealed class RunSaveData
{
    public int version=1;
    public RunProgressData progress;
    public RunInventoryData inventory;
    public bool hasRewards;
    public RunRewardData rewards;
    public string contentState="";
}

[Serializable]
public sealed class RunProgressData
{
    public string definition;
    public int seed, floor, node, completed, hp, maxHP;
    public RunPhase phase;
    public MapContentType[] enabled;
    public RunNodeData[] nodes;
}
[Serializable]
public sealed class RunNodeData
{
    public MapNodeRule rule;
    public MapNodeStatus status;
    public int selected=-1;
    public RunChoiceData[] choices;
}
[Serializable]
public sealed class RunChoiceData
{
    public MapContentType type;
    public string encounter, content;
}
[Serializable]
public sealed class RunInventoryData
{
    public int gold;
    public RunDeckData[] decks=Array.Empty<RunDeckData>();
    public RunRelicData[] relics=Array.Empty<RunRelicData>();
}
[Serializable]
public sealed class RunDeckData
{
    public string dreamDatabase, nightmareDatabase;
    public string[] dream, nightmare;
}
[Serializable]
public sealed class RunRelicData
{
    public string id;
    public int bonus;
}
[Serializable]
public sealed class RunRewardData
{
    public BattleRewardState state;
    public int gold;
    public bool goldAvailable, cardAvailable;
    public string[] offers=Array.Empty<string>();
}
