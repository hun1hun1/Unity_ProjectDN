using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyActionUI : MonoBehaviour
{
    [SerializeField]
    private Image actionIcon;

    [SerializeField]
    private TMP_Text actionText;

    public void SetAction(EnemyAction action)
    {
        if (action == null)
        {
            ClearAction();
            return;
        }

        actionIcon.sprite = action.GetActionIcon();
        actionText.text = action.GetActionDescription();
    }

    public void ClearAction()
    {
        actionIcon.sprite = null;
        actionText.text = "";
    }
}
