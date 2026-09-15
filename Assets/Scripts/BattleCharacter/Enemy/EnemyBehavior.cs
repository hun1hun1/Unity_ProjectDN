using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyBehavior",
    menuName = "Enemy/Behavior")]
public class EnemyBehavior : ScriptableObject
{
    [SerializeField]
    private EnemyAction[] actions;

    private int currentActionIndex = 0;

    public EnemyAction GetNextAction()
    {
        if (actions == null || actions.Length == 0)
            return null;

        EnemyAction action = actions[currentActionIndex];

        currentActionIndex++;

        if (currentActionIndex >= actions.Length)
        {
            currentActionIndex = 0;
        }

        return action;
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
