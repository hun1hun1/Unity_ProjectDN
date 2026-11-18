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

    public event System.Action Changed;

    public int GetCurrentCost(CardDeckType deckType) => deckType == CardDeckType.TypeDream ? currentDreamCost :
        deckType == CardDeckType.TypeNightmare ? currentNightmareCost : 0;

    public bool CanPay(CardData cardData)
    {
        if (cardData == null || (cardData.deckType != CardDeckType.TypeDream && cardData.deckType != CardDeckType.TypeNightmare)) return false;
        int available = GetCurrentCost(cardData.deckType);
        if (cardData.costMode == CardCostMode.X) return available >= 0;
        if (cardData.costMode != CardCostMode.Fixed) return false;
        int required = cardData.deckType == CardDeckType.TypeDream ? cardData.cost.dreamCost : cardData.cost.nightmareCost;
        return required >= 0 && available >= required;
    }

    public bool TryPay(CardData cardData) => TryPay(cardData, out _);

    public bool TryPay(CardData cardData, out int paidCost)
    {
        paidCost = 0;
        if (!CanPay(cardData)) return false;
        paidCost = cardData.costMode == CardCostMode.X ? GetCurrentCost(cardData.deckType) :
            cardData.deckType == CardDeckType.TypeDream ? cardData.cost.dreamCost : cardData.cost.nightmareCost;
        if (cardData.deckType == CardDeckType.TypeDream) currentDreamCost -= paidCost;
        else currentNightmareCost -= paidCost;
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
        Changed?.Invoke();
    }

    public void ResetForTurn()
    {
        currentDreamCost = maxDreamCost;
        currentNightmareCost = 0;

        UpdateUI();
    }

    internal void FillDreamGaugeToHalf()
    {
        currentDreamGauge = maxDreamGauge / 2;
        UpdateGaugeUI();
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
