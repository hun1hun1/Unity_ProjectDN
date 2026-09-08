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

        deckController.DiscardCard(cardData, cardData.deckType);

        CardHandController handController = cardUI.GetHandController();

        if (handController != null)
        {
            handController.RemoveCard(cardUI);

            RefillHand(cardData.deckType);
        }
    }

    private void DiscardPlayerHands()
    {
        DiscardHand(CardDeckType.TypeDream, dreamCardHand);

        DiscardHand(CardDeckType.TypeNightmare, nightmareCardHand);
    }

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

    public void EndPlayerTurn()
    {
        Debug.Log("플레이어 턴 종료");

        // 1. 현재 손패 전부 버리기
        DiscardPlayerHands();

        // 2. 플레이어 상태이상 처리
        ProcessPlayerStatusEffects();

        // 3. 적 행동
        // 아직 구현하지 않음
    }
}
