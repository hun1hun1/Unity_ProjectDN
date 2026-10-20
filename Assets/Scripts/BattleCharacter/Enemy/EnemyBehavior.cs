using System;
using UnityEngine;

public enum EnemyActionSelectionMode
{
    Sequential = 0,
    RandomNoThreeConsecutive = 1
}

[CreateAssetMenu(
    fileName = "EnemyBehavior",
    menuName = "Enemy/Behavior")]
public class EnemyBehavior : ScriptableObject
{
    [SerializeField]
    private EnemyAction[] actions;

    [SerializeField]
    [Tooltip("Sequential repeats the list in order. Random mode prevents three consecutive uses of the same action asset.")]
    private EnemyActionSelectionMode selectionMode;

    public EnemyActionSelectionMode SelectionMode => selectionMode;

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(EnemyActionSelectionMode), selectionMode))
            throw new InvalidOperationException($"Behavior '{name}' has an unsupported selection mode.");
        if (actions != null)
            foreach (EnemyAction action in actions)
                if (action != null) action.Validate();
        if (selectionMode == EnemyActionSelectionMode.Sequential) return;

        EnemyAction first = null;
        if (actions != null)
            foreach (EnemyAction action in actions)
            {
                if (action == null) continue;
                if (first == null) first = action;
                else if (action != first) return;
            }
        throw new InvalidOperationException($"Behavior '{name}' needs at least two distinct non-null actions in random mode.");
    }

    public EnemyAction GetAction(int index)
    {
        if (actions == null || actions.Length == 0)
            return null;

        if (index < 0 || index >= actions.Length)
            return null;

        return actions[index];
    }

    public int GetActionCount()
    {
        return actions != null ? actions.Length : 0;
    }
}
