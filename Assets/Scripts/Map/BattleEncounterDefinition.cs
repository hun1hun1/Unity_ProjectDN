using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BattleEncounter", menuName = "Map/Battle Encounter")]
public class BattleEncounterDefinition : ScriptableObject
{
    public string displayName;
    public EnemyBehavior behavior;
    [Tooltip("Canvas enemy prefab containing its own artwork, Animator and character HUD. Optional for legacy encounters.")]
    public Enemy enemyPrefab;
    [Min(1)] public int enemyMaxHP = 50;
    [Min(0)] public int goldReward = 20;
    public ConduitData victoryConduit;

    public void Validate()
    {
        if (behavior == null || enemyMaxHP < 1 || goldReward < 0)
            throw new InvalidOperationException($"Encounter '{name}' needs behavior, positive HP and nonnegative gold.");
        behavior.Validate();
        if (enemyPrefab != null && !(enemyPrefab.transform is RectTransform))
            throw new InvalidOperationException($"Encounter '{name}' needs a Canvas enemy prefab with a RectTransform root.");
    }
}
