using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterInfoUI : MonoBehaviour
{
    [Header("HP")]
    [SerializeField]
    private Image hpFill;

    [SerializeField]
    private TMP_Text hpText;

    [Header("Block")]
    [SerializeField]
    private TMP_Text blockText;

    [Header("Status")]
    [SerializeField] private StatusEffectsUI statusUI;

    public void BindStatusEffects(StatusEffectSystem status, ContinuousCardSystem continuous = null)
    {
        if (statusUI != null) statusUI.Bind(status, continuous);
    }

    public void SetHP(int currentHP, int maxHP)
    {
        if (maxHP <= 0) return;

        hpFill.fillAmount = (float)currentHP / maxHP;
        hpText.text = $"{currentHP} / {maxHP}";
    }

    public void SetBlock(int block)
    {
        blockText.text = block.ToString();
    }
}
