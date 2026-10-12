using TMPro;
using UnityEngine;

public class StatusIconUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private TMP_Text turnsText;
    public StatusEffectType StatusType { get; private set; }

    public void SetStatus(ActiveStatusEffect effect)
    {
        StatusType = effect.Type;
        gameObject.name = effect.Definition.DisplayName;
        if (icon != null && effect.Definition.Icon != null) icon.sprite = effect.Definition.Icon;
        if (turnsText != null) turnsText.text = effect.RemainingTurns.ToString();
    }
}
