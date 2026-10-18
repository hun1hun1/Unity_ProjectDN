using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleEncounter", menuName = "Map/Battle Encounter")]
public class BattleEncounterDefinition : ScriptableObject
{
    public string displayName;
    public EnemyBehavior behavior;
    [Min(1)] public int enemyMaxHP = 50;
    [Min(0)] public int goldReward = 20;

    public void Validate()
    {
        if (behavior == null || enemyMaxHP < 1 || goldReward < 0)
            throw new InvalidOperationException($"Encounter '{name}' needs behavior, positive HP and nonnegative gold.");
    }
}
