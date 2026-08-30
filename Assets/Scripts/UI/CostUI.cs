using TMPro;
using UnityEngine;

public class CostUI : MonoBehaviour
{
    [SerializeField] private TMP_Text costText;

    public void SetCost(int current, int max)
    {
        costText.text = $"{current} / {max}";
    }
}
