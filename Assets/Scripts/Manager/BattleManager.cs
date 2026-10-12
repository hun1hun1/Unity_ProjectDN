using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [SerializeField] private DeckController deckController;

    [Header("Card Hand")]
    [SerializeField] private CardHandController dreamCardHand;
    [SerializeField] private CardHandController nightmareCardHand;

    [SerializeField] private Player player;
    [SerializeField] private Enemy target;

    [Header("Card Using Area")]
    [SerializeField] private RectTransform cardUsingPoint;

    private BattleState currentState;
    private bool isResolvingEffects;
    private int activeCardUses;

    private void Start()
    {
        if (player != null) player.StatusEffects.Clear();
        if (target != null)
        {
            target.StatusEffects.Clear();
            target.BindBattle(this);
        }
        if (player != null)
        {
            AmmoSystem ammo = player.GetAmmoSystem();
            if (ammo != null) ammo.ResetForBattle();
        }
        DrawStartingHands();
    }

    private void DrawStartingHands()
    {
        DrawCards(CardDeckType.TypeDream, dreamCardHand);

        DrawCards(CardDeckType.TypeNightmare, nightmareCardHand);
    }

    public bool DrawCard(CardDeckType deckType, CardHandController handController)
    {
        if (handController.IsFull())
            return false;

        CardData cardData = deckController.DrawCard(deckType);

        if (cardData == null)
            return false;

        handController.AddCard(cardData, this);

        return true;
    }

    public void DrawCards(CardDeckType deckType, CardHandController handController)
    {
        while (!handController.IsFull())
        {
            if(!DrawCard(deckType, handController)) break;
        }
    }

    private CardHandController GetHandController(CardDeckType deckType)
    {
        switch (deckType)
        {
            case CardDeckType.TypeDream:
                return dreamCardHand;

            case CardDeckType.TypeNightmare:
                return nightmareCardHand;
        }

        return null;
    }

    public void RefillHand(CardDeckType deckType)
    {
        CardHandController handController = GetHandController(deckType);

        if (handController == null) return;

        DrawCards(deckType, handController);
    }

    public Player GetPlayer()
    {
        return player;
    }

    public Enemy GetTarget()
    {
        return target;
    }

    public bool CanUseCard(CardData cardData)
    {
        if (currentState != BattleState.PlayerTurn || isResolvingEffects ||
            cardData == null || cardData.requiredAmmo < 0 || player == null)
            return false;

        CostSystem costSystem = player.GetCostSystem();
        if (costSystem == null || !costSystem.CanPay(cardData))
            return false;

        if (cardData.requiredAmmo == 0)
            return true;

        AmmoSystem ammoSystem = player.GetAmmoSystem();
        return ammoSystem != null && ammoSystem.HasAmmo(cardData.requiredAmmo);
    }

    public bool UseCard(CardUI cardUI)
    {
        if (cardUI == null || cardUI.IsUsed)
            return false;

        CardData cardData = cardUI.GetCardData();
        if (!CanUseCard(cardData))
            return false;

        CostSystem costSystem = player.GetCostSystem();
        isResolvingEffects = true;
        try
        {
            if (!costSystem.TryPay(cardData))
                return false;

            if (cardData.consumeRequiredAmmo && cardData.requiredAmmo > 0 &&
                !player.GetAmmoSystem().TrySpend(cardData.requiredAmmo))
            {
                // Restore the card cost if a resource changed after preflight.
                if (cardData.deckType == CardDeckType.TypeDream)
                    costSystem.AddDreamCost(cardData.cost.dreamCost);
                else
                    costSystem.AddNightmareCost(cardData.cost.nightmareCost);
                return false;
            }

            cardUI.MarkAsUsed();
            Debug.Log($"Card used: {cardData.cardName}");

            if (cardData.effects != null)
            {
                foreach (CardEffect effect in cardData.effects)
                    if (effect != null) effect.Execute(this);
            }
        }
        finally
        {
            isResolvingEffects = false;
        }

        activeCardUses++;
        StartCoroutine(UseCardRoutine(cardUI, cardData));
        return true;
    }

    private IEnumerator UseCardRoutine(CardUI cardUI, CardData cardData)
    {
        try
        {
            if (cardUI == null) yield break;

            CardHandController handController = cardUI.GetHandController();
            if (handController == null) yield break;

            handController.RemoveCard(cardUI);
            cardUI.transform.SetParent(handController.GetCardAnimationLayer(), true);

            yield return StartCoroutine(cardUI.UsedCardAnimation(
                cardUsingPoint, handController.GetDiscardPilePoint()));

            deckController.DiscardCard(cardData, cardData.deckType);
            Destroy(cardUI.gameObject);

            if (currentState == BattleState.PlayerTurn)
                RefillHand(cardData.deckType);
        }
        finally
        {
            activeCardUses--;
        }
    }

    //private void DiscardPlayerHands()
    //{
    //    DiscardHand(CardDeckType.TypeDream, dreamCardHand);

    //    DiscardHand(CardDeckType.TypeNightmare, nightmareCardHand);
    //}

    private void DiscardHand(CardDeckType deckType, CardHandController handController)
    {
        if (handController == null)
            return;

        List<CardData> cards = handController.RemoveAllCards();

        foreach (CardData cardData in cards)
        {
            deckController.DiscardCard(cardData, deckType);
        }
    }

    private void ProcessTurnStartStatusEffects()
    {
        if (player != null) player.StatusEffects.ReduceRemainingTurns();
        if (target != null) target.StatusEffects.ReduceRemainingTurns();
    }

    private IEnumerator DiscardPlayerHands()
    {
        Coroutine dreamDiscard = StartCoroutine(
            dreamCardHand.DiscardAllCards(
                cardData =>
                {
                    deckController.DiscardCard(
                        cardData,
                        cardData.deckType
                    );
                }
            )
        );

        Coroutine nightmareDiscard = StartCoroutine(
            nightmareCardHand.DiscardAllCards(
                cardData =>
                {
                    deckController.DiscardCard(
                        cardData,
                        cardData.deckType
                    );
                }
            )
        );

        // 두 손패의 버림 처리가 모두 끝날 때까지 대기
        yield return dreamDiscard;
        yield return nightmareDiscard;
    }

    //public void EndPlayerTurn()
    //{
    //    if (currentState != BattleState.PlayerTurn)
    //    {
    //        Debug.Log("플레이어 턴이 아닙니다.");
    //        return;
    //    }

    //    ChangeState(BattleState.PlayerTurnEnd);

    //    Debug.Log("플레이어 턴 종료");

    //    CostSystem costSystem = player.GetCostSystem();

    //    if (costSystem != null)
    //    {
    //        costSystem.AddRemainingCostToGauge();
    //    }

    //    // 1. 현재 손패 전부 버리기
    //    DiscardPlayerHands();

    //    // 2. 플레이어 상태이상 처리

    //    // 3. 적 행동
    //    StartEnemyTurn();

    //    StartPlayerTurn();
    //}

    public void EndPlayerTurn()
    {
        if (isResolvingEffects) return;
        StartCoroutine(EndPlayerTurnRoutine());
    }

    private IEnumerator EndPlayerTurnRoutine()
    {
        if (currentState != BattleState.PlayerTurn)
        {
            Debug.Log("플레이어 턴이 아닙니다.");
            yield break;
        }

        ChangeState(BattleState.PlayerTurnEnd);

        while (activeCardUses > 0)
            yield return null;

        Debug.Log("플레이어 턴 종료");

        // 남은 코스트 처리
        CostSystem costSystem =
            player.GetCostSystem();

        if (costSystem != null)
        {
            costSystem.AddRemainingCostToGauge();
        }

        // =========================================
        // 1. 현재 손패 버리기
        // =========================================

        yield return StartCoroutine(
            DiscardPlayerHands()
        );

        // =========================================
        // 2. 적 턴
        // =========================================

        StartEnemyTurn();

        // =========================================
        // 3. 플레이어 턴 시작
        // =========================================

        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        ProcessTurnStartStatusEffects();

        ChangeState(BattleState.PlayerTurn);

        Debug.Log("플레이어 턴 시작");

        CostSystem costSystem = player.GetCostSystem();

        if (costSystem != null)
        {
            CostGaugeState state =
            costSystem.ConsumePendingState();

            ApplyCostGaugeState(state);

            costSystem.ResetForTurn();
        }

        player.ResetBlock();

        DrawCards(CardDeckType.TypeDream, dreamCardHand);
        DrawCards(CardDeckType.TypeNightmare, nightmareCardHand);
    }

    private void ChangeState(BattleState newState)
    {
        currentState = newState;

        Debug.Log($"Battle State : {currentState}");
    }

    private void StartEnemyTurn()
    {
        ChangeState(BattleState.EnemyTurn);

        Debug.Log("적 턴 시작");

        target.ResetBlock();

        target.EnemyTurn(this);

        EndEnemyTurn();
    }

    private void EndEnemyTurn()
    {
        ChangeState(BattleState.EnemyTurnEnd);

        Debug.Log("적 턴 종료");
    }

    private void ApplyCostGaugeState(CostGaugeState state)
    {
        switch (state)
        {
            case CostGaugeState.Dream:
                Debug.Log("Dream 상태 발동");
                // Dream 상태 효과
                break;

            case CostGaugeState.Nightmare:
                Debug.Log("Nightmare 상태 발동");
                // Nightmare 상태 효과
                break;

            case CostGaugeState.Twilight:
                Debug.Log("황혼 상태 발동");
                // 황혼 상태 효과
                break;
        }
    }
}
