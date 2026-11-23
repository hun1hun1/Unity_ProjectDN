using System;
using UnityEngine;

public enum FloorMapMode { PlannedFloor, NormalBattlesOnly, FirstFloor }
public enum FirstFloorNodeLayout { Regular, Start, EliteChoice, Midpoint, Conduit, RestChoice, Boss }

[Serializable]
public sealed class WeightedMapContent
{
    public MapContentType type;
    [Min(0)] public int weight = 1;
    public ScriptableObject contentData;
}

[Serializable]
public sealed class MapBattleLimit
{
    [Min(1)] public int firstNode = 1;
    [Min(1)] public int lastNode = 5;
    [Min(0)] public int maxBattles = 4;
}

[Serializable]
public sealed class UnknownNodeSettings
{
    public int[] nodeNumbers = Array.Empty<int>();
    [Min(0)] public int battleWeight = 40;
    [Min(0)] public int eventWeight = 40;
    [Min(0)] public int shopWeight = 9;
    [Min(0)] public int restWeight = 9;
    [Min(0)] public int relicWeight = 2;
    public MapEventDefinition[] events = Array.Empty<MapEventDefinition>();
    public ConduitData[] relics = Array.Empty<ConduitData>();
}

[CreateAssetMenu(fileName = "FloorMap", menuName = "Map/Floor Map")]
public class FloorMapDefinition : ScriptableObject
{
    public FloorMapMode mode;
    [Min(1)] public int nodeCount = 12;
    [Min(0), Tooltip("Fixed shop choice in Normal Battles Only mode. 1 is the first node; 0 disables it.")]
    public int fixedShopNodeNumber;
    [Min(0), Tooltip("Fixed rest choice in Normal Battles Only mode. 1 is the first node; 0 disables it.")]
    public int fixedRestNodeNumber;
    [Min(0), Tooltip("Fixed Conduit choice in Normal Battles Only mode. Uses Guaranteed Relic Data. 0 disables it.")]
    public int fixedRelicNodeNumber;
    [Range(0, 1)] public float midpointMinRatio = .4f;
    [Range(0, 1)] public float midpointMaxRatio = .6f;
    [Range(0, 1)] public float nonBattleChance = .35f;
    [Range(0, 1)] public float eliteChance = .1f;
    public WeightedMapContent[] nonBattleContents =
    {
        new WeightedMapContent { type = MapContentType.Shop },
        new WeightedMapContent { type = MapContentType.Relic },
        new WeightedMapContent { type = MapContentType.Event },
        new WeightedMapContent { type = MapContentType.Upgrade }
    };
    public FirstFloorNodeLayout[] firstFloorLayout = Array.Empty<FirstFloorNodeLayout>();
    [Range(0, 1)] public float secondNonBattleChance = .3f;
    public BattleEncounterDefinition[] lateNormalBattles = Array.Empty<BattleEncounterDefinition>();
    public ConduitRewardDatabase commonConduitRewards;
    public ConduitRewardDatabase uncommonConduitRewards;
    public MapBattleLimit[] battleLimits = Array.Empty<MapBattleLimit>();
    public UnknownNodeSettings unknown = new UnknownNodeSettings();
    public ScriptableObject guaranteedRelicData;
    public BattleEncounterDefinition[] normalBattles = Array.Empty<BattleEncounterDefinition>();
    public BattleEncounterDefinition[] eliteBattles = Array.Empty<BattleEncounterDefinition>();
    public BattleEncounterDefinition[] bossBattles = Array.Empty<BattleEncounterDefinition>();

    internal MapBattleLimit GetBattleLimit(int nodeNumber)
    {
        if (battleLimits != null)
            foreach (MapBattleLimit limit in battleLimits)
                if (nodeNumber >= limit.firstNode && nodeNumber <= limit.lastNode) return limit;
        return null;
    }
}
