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

    public bool TryPay(CardData cardData)
    {
        switch (cardData.deckType)
        {
            case CardDeckType.TypeDream:

                if (currentDreamCost < cardData.cost.dreamCost)
                {
                    Debug.Log("Dream 코스트 부족");
                    return false;
                }

                currentDreamCost -= cardData.cost.dreamCost;

                UpdateUI();
                return true;


            case CardDeckType.TypeNightmare:

                if (currentNightmareCost < cardData.cost.nightmareCost)
                {
                    Debug.Log("Nightmare 코스트 부족");
                    return false;
                }       

                currentNightmareCost -= cardData.cost.nightmareCost;

                UpdateUI();
                return true;
        }

        return false;
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
