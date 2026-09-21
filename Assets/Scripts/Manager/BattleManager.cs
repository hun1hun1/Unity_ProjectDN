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

    private void Start()
    {
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

    public void UseCard(CardUI cardUI)
    {
        if (currentState != BattleState.PlayerTurn)
            return;

        if (cardUI == null)
        {
            Debug.Log("cardUI is null");
            return;
        }

        CardData cardData = cardUI.GetCardData();

        if (cardData == null)
        {
            Debug.Log("cardData is null");
            return;
        }

        Player player = GetPlayer();

        if (player == null)
        {
            Debug.Log("player is null");
            return;
        }

        CostSystem costSystem = player.GetCostSystem();

        if (costSystem == null)
        {
            Debug.Log("costSystem is null");
            return;
        }

        // 코스트 지불 실패
        if (!costSystem.TryPay(cardData))
        {
            Debug.Log("코스트가 부족합니다.");
            return;
        }

        Debug.Log($"카드 사용: {cardData.cardName}");

        foreach (CardEffect effect in cardData.effects)
        {
            if (effect != null)
            {
                effect.Execute(this);
            }
        }

        //deckController.DiscardCard(cardData, cardData.deckType);

        //CardHandController handController = cardUI.GetHandController();

        //if (handController != null)
        //{
        //    handController.RemoveCard(cardUI);

        //    RefillHand(cardData.deckType);
        //}

        StartCoroutine(UseCardRoutine(cardUI, cardData));
    }

    private IEnumerator UseCardRoutine(
    CardUI cardUI,
    CardData cardData)
    {
        if (cardUI == null)
            yield break;

        CardHandController handController =
            cardUI.GetHandController();

        if (handController == null)
            yield break;

        // 손패 목록에서 제거하되 UI 오브젝트는 유지
        handController.RemoveCard(cardUI);

        // LayoutGroup의 영향을 받지 않도록 이동 레이어로 옮김
        cardUI.transform.SetParent(
            handController.GetCardAnimationLayer(),
            true
        );

        // CardUsingArea 이동 → 축소 → 버림 더미 이동
        yield return StartCoroutine(
            cardUI.UsedCardAnimation(
                cardUsingPoint,
                handController.GetDiscardPilePoint()
            )
        );

        // 애니메이션 완료 후 실제 버림 더미에 추가
        deckController.DiscardCard(
            cardData,
            cardData.deckType
        );

        // UI 오브젝트 제거
        Destroy(cardUI.gameObject);

        // 손패 보충
        RefillHand(cardData.deckType);
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

    private void ProcessPlayerStatusEffects()
    {
        Debug.Log("플레이어 상태이상 처리");
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
    //    ProcessPlayerStatusEffects();

    //    // 3. 적 행동
    //    StartEnemyTurn();

    //    StartPlayerTurn();
    //}

    public void EndPlayerTurn()
    {
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
        // 2. 플레이어 상태이상 처리
        // =========================================

        ProcessPlayerStatusEffects();

        // =========================================
        // 3. 적 턴
        // =========================================

        StartEnemyTurn();

        // =========================================
        // 4. 플레이어 턴 시작
        // =========================================

        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
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
