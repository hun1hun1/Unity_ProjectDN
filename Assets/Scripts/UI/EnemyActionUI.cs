using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyActionUI : MonoBehaviour
{
    [SerializeField]
    private Image actionIcon;

    [SerializeField]
    private TMP_Text actionText;

    public void ValidateBindings()
    {
        if (actionIcon == null || actionText == null)
            throw new System.InvalidOperationException("Enemy intent HUD needs icon and text references.");
    }

    public void SetAction(EnemyAction action)
    {
        SetAction(action, null);
    }

    public void SetAction(EnemyAction action, BattleManager battleManager)
    {
        if (action == null)
        {
            ClearAction();
            return;
        }

        if (actionIcon != null)
        {
            actionIcon.sprite = action.GetActionIcon();
            actionIcon.enabled = actionIcon.sprite != null;
        }
        if (actionText != null) actionText.text = action.GetActionDescription(battleManager);
    }

    public void ClearAction()
    {
        if (actionIcon != null) { actionIcon.sprite = null; actionIcon.enabled = false; }
        if (actionText != null) actionText.text = "";
    }
}
