using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CostGaugeUI : MonoBehaviour
{
    [SerializeField] private Image gaugeFill;

    [SerializeField] private TMP_Text gaugeText;

    public void SetGauge(int amount, int maxAmount)
    {
        if (maxAmount <= 0) return;

        amount = Mathf.Clamp(amount, 0, maxAmount);

        gaugeFill.fillAmount = (float)amount / maxAmount;

        gaugeText.text = $"{amount} / {maxAmount}";
    }
}
