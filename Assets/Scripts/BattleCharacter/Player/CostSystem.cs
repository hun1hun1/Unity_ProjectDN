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

    [Header("Gauge")]
    [SerializeField] private int maxDreamGauge = 10;
    [SerializeField] private int maxNightmareGauge = 20;

    [SerializeField] private int currentDreamGauge;
    [SerializeField] private int currentNightmareGauge;

    [Header("Cost Gauge")]
    [SerializeField] private CostGaugeUI dreamGaugeUI;
    [SerializeField] private CostGaugeUI nightmareGaugeUI;

    private CostGaugeState pendingState = CostGaugeState.None;

    private void Awake()
    {
        ResetForTurn();

        currentDreamGauge = 0;
        currentNightmareGauge = 0;

        pendingState = CostGaugeState.None;

        UpdateGaugeUI();
    }

    public bool CanPay(CardData cardData)
    {
        if (cardData == null) return false;

        switch (cardData.deckType)
        {
            case CardDeckType.TypeDream:
                return cardData.cost.dreamCost >= 0 &&
                    currentDreamCost >= cardData.cost.dreamCost;
            case CardDeckType.TypeNightmare:
                return cardData.cost.nightmareCost >= 0 &&
                    currentNightmareCost >= cardData.cost.nightmareCost;
            default:
                return false;
        }
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

    public void ResetForTurn()
    {
        currentDreamCost = maxDreamCost;
        currentNightmareCost = 0;

        UpdateUI();
    }

    public void AddRemainingCostToGauge()
    {
        currentDreamGauge += currentDreamCost;
        currentNightmareGauge += currentNightmareCost;

        currentDreamGauge =
            Mathf.Min(currentDreamGauge, maxDreamGauge);

        currentNightmareGauge =
            Mathf.Min(currentNightmareGauge, maxNightmareGauge);

        UpdateGaugeUI();

        CheckGaugeState();
    }

    private void CheckGaugeState()
    {
        bool dreamFull =
            currentDreamGauge >= maxDreamGauge;

        bool nightmareFull =
            currentNightmareGauge >= maxNightmareGauge;

        if (dreamFull && nightmareFull)
        {
            pendingState = CostGaugeState.Twilight;
        }
        else if (dreamFull)
        {
            pendingState = CostGaugeState.Dream;
        }
        else if (nightmareFull)
        {
            pendingState = CostGaugeState.Nightmare;
        }
    }

    public CostGaugeState ConsumePendingState()
    {
        CostGaugeState state = pendingState;

        if (state == CostGaugeState.None)
            return CostGaugeState.None;

        pendingState = CostGaugeState.None;

        currentDreamGauge = 0;
        currentNightmareGauge = 0;

        UpdateGaugeUI();

        return state;
    }

    private void UpdateGaugeUI()
    {
        if (dreamGaugeUI != null)
        {
            dreamGaugeUI.SetGauge(
                currentDreamGauge,
                maxDreamGauge
            );
        }

        if (nightmareGaugeUI != null)
        {
            nightmareGaugeUI.SetGauge(
                currentNightmareGauge,
                maxNightmareGauge
            );
        }
    }
}
