using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterInfoUI : MonoBehaviour
{
    [SerializeField]
    private Image hpFill;

    [SerializeField]
    private TMP_Text hpText;

    public void SetHP(int currentHP, int maxHP)
    {
        if (maxHP <= 0) return;

        hpFill.fillAmount = (float)currentHP / maxHP;
        hpText.text = $"{currentHP} / {maxHP}";
    }
}