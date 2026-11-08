using System;
using UnityEngine;

public enum FloorMapMode { PlannedFloor, NormalBattlesOnly }

[Serializable]
public sealed class WeightedMapContent
{
    public MapContentType type;
    [Min(0)] public int weight = 1;
    public ScriptableObject contentData;
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
    public ScriptableObject guaranteedRelicData;
    public BattleEncounterDefinition[] normalBattles = Array.Empty<BattleEncounterDefinition>();
    public BattleEncounterDefinition[] eliteBattles = Array.Empty<BattleEncounterDefinition>();
    public BattleEncounterDefinition[] bossBattles = Array.Empty<BattleEncounterDefinition>();
}
