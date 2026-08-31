using UnityEngine;

public class CostSystem : MonoBehaviour
{
    [Header("Maximum Cost")]
    [SerializeField] private int maxDreamCost = 3;
    [SerializeField] private int maxNightmareCost = 3;

    [Header("Current Cost")]
    [SerializeField] private int currentDreamCost;
    [SerializeField] private int currentNightmareCost;

    [Header("UI")]
    [SerializeField] private CostUI dreamCostUI;
    [SerializeField] private CostUI nightmareCostUI;

    private void Awake()
    {
        currentDreamCost = maxDreamCost;
        currentNightmareCost = 0;

        UpdateUI();
    }

    public bool CanPay(Cost cost)
    {
        return currentDreamCost >= cost.dreamCost &&
               currentNightmareCost >= cost.nightmareCost;
    }

    public bool TryPay(Cost cost)
    {
        if (!CanPay(cost))
            return false;

        currentDreamCost -= cost.dreamCost;
        currentNightmareCost -= cost.nightmareCost;

        UpdateUI();

        return true;
    }

    public void AddDreamCost(int amount)
    {
        currentDreamCost += amount;

        if (currentDreamCost > maxDreamCost)
            currentDreamCost = maxDreamCost;

        UpdateUI();
    }

    public void AddNightmareCost(int amount)
    {
        currentNightmareCost += amount;

        if (currentNightmareCost > maxNightmareCost)
            currentNightmareCost = maxNightmareCost;

        UpdateUI();
    }

    public void ModifyDreamCost(int amount)
    {
        currentDreamCost += amount;

        if (currentDreamCost < 0)
            currentDreamCost = 0;

        if (currentDreamCost > maxDreamCost)
            currentDreamCost = maxDreamCost;

        UpdateUI();
    }

    public void ModifyNightmareCost(int amount)
    {
        currentNightmareCost += amount;

        if (currentNightmareCost < 0)
            currentNightmareCost = 0;

        if (currentNightmareCost > maxNightmareCost)
            currentNightmareCost = maxNightmareCost;

        UpdateUI();
    }

    public int GetDreamCost()
    {
        return currentDreamCost;
    }

    public int GetNightmareCost()
    {
        return currentNightmareCost;
    }

    private void UpdateUI()
    {
        if (dreamCostUI != null)
        {
            dreamCostUI.SetCost(currentDreamCost, maxDreamCost);
        }

        if (nightmareCostUI != null)
        {
            nightmareCostUI.SetCost(currentNightmareCost,maxNightmareCost);
        }
    }
}
