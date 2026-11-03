using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Run", menuName = "Map/Run")]
public class RunDefinition : ScriptableObject
{
    public string mapScene = "MapScene";
    public string battleScene = "BattleScene";
    [Min(1)] public int startingMaxHP = 80;
    [Min(0)] public int startingGold;
    public FloorMapDefinition[] floors = Array.Empty<FloorMapDefinition>();
    [Tooltip("Only these content types may be generated. Nonbattle choices also need a MapContentHandler in MapScene.")]
    public MapContentType[] enabledContent = { MapContentType.NormalBattle };
}
