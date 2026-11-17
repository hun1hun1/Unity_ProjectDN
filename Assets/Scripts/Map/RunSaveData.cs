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
    public RunEncounterOrderData encounterOrder;
}

[Serializable]
public sealed class RunEncounterOrderData
{
    public string[] early, late, elite;
}
[Serializable]
public sealed class RunNodeData
{
    public MapNodeRule rule;
    public MapNodeStatus status;
    public int selected=-1;
    public RunChoiceData[] choices;
    public bool hasResolvedChoice;
    public RunChoiceData resolvedChoice;
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
    public int cardRemovals;
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
    public int battleStartAmmo;
    public string battleStartBuff;
    public int battleStartBuffPower;
}
[Serializable]
public sealed class RunRewardData
{
    public BattleRewardState state;
    public int gold;
    public bool goldAvailable, cardAvailable;
    public string[] offers=Array.Empty<string>();
}
