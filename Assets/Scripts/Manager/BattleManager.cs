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
    private CardUI resolvingCard;
    private readonly MulliganSystem mulligans = new MulliganSystem();
    public MulliganSystem Mulligans => mulligans;
    public bool BlocksCardInteraction => isResolvingEffects;
    public bool CanContinueEffects => isActiveAndEnabled && currentState != BattleState.BattleEnd;
    public bool HasOtherCardUses => activeCardUses > (resolvingCard != null ? 1 : 0);
    public bool HandsAreMoving => HandIsMoving(dreamCardHand) || HandIsMoving(nightmareCardHand);
    public CardHandController GetHand(CardDeckType type) => GetHandController(type);
    public DeckController GetDeckController() => deckController;
    public void PresentResolvingCard()
    {
        if (resolvingCard != null) resolvingCard.PresentWhileResolving(cardUsingPoint);
    }

    private static bool HandIsMoving(CardHandController hand)
    {
        if (hand == null) return false;
        foreach (CardUI card in hand.GetCardsSnapshot())
            if (card != null && card.isActiveAndEnabled && !card.IsUsed && card.IsMoving) return true;
        return false;
    }

    public IEnumerator ExecuteEffectsRoutine(CardEffect[] effects, ActiveContinuousCard source = null)
    {
        if (effects == null) yield break;
        foreach (CardEffect effect in effects)
        {
            if (!CanContinueEffects || (source != null && !source.IsActive)) yield break;
            if (effect == null) continue;
            IEnumerator step = source == null ? effect.ExecuteRoutine(this) : effect.ActivateContinuousRoutine(this, source);
            if (step != null) yield return step;
        }
    }

    public void RunEffectRoutine(IEnumerator sequence) => EffectSequence.Start(this, LockedEffects(sequence));

    private IEnumerator LockedEffects(IEnumerator sequence)
    {
        bool previous = isResolvingEffects;
        isResolvingEffects = true;
        try { if (sequence != null) yield return sequence; }
        finally { isResolvingEffects = previous; }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        EffectSequence.Stop(this);
        mulligans.Cancel();
        resolvingCard = null; activeCardUses = 0; isResolvingEffects = false;
    }

    private void Start()
    {
        if (player != null)
        {
            player.ContinuousCards.ResetForBattle();
            player.StatusEffects.Clear();
        }
        if (target != null)
        {
            target.ContinuousCards.ResetForBattle();
            target.StatusEffects.Clear();
            target.BindBattle(this);
        }
        if (player != null)
        {
            AmmoSystem ammo = player.GetAmmoSystem();
            if (ammo != null) ammo.ResetForBattle();
        }
        if (deckController != null) deckController.ResetForBattle();
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

    public bool CanInspectCards => currentState == BattleState.PlayerTurn && !isResolvingEffects &&
        activeCardUses == 0 && !CardUI.HasBusyCards(this);

    public bool CanUseCard(CardData cardData)
    {
        if (CardInspectionUI.BlocksGameplayInput || currentState != BattleState.PlayerTurn || isResolvingEffects ||
            cardData == null || cardData.requiredAmmo < 0 || player == null)
            return false;

        if (cardData.type == CardType.Continuous &&
            (deckController == null || !ContinuousCardSystem.CanActivate(cardData)))
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

            // Preserve the preview from before resource payment and effect execution.
            cardUI.PauseDescriptionUpdates();
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

        }
        finally
        {
            if (cardUI != null && !cardUI.IsUsed) cardUI.ResumeDescriptionUpdates();
            isResolvingEffects = false;
        }

        activeCardUses++;
        EffectSequence.Start(this, UseCardRoutine(cardUI, cardData));
        return true;
    }

    private IEnumerator UseCardRoutine(CardUI cardUI, CardData cardData)
    {
        bool settled = false;
        ActiveContinuousCard continuous = null;
        try
        {
            if (cardUI == null) yield break;
            CardHandController handController = cardUI.GetHandController();
            if (handController != null)
            {
                handController.RemoveCard(cardUI);
                cardUI.transform.SetParent(handController.GetCardAnimationLayer(), true);
            }

            resolvingCard = cardUI;
            bool previous = isResolvingEffects;
            isResolvingEffects = true;
            try
            {
                if (cardData.type == CardType.Continuous)
                    yield return player.ContinuousCards.ActivateRoutine(cardData, this, deckController, entry => continuous = entry);
                else
                    yield return ExecuteEffectsRoutine(cardData.effects);
            }
            finally
            {
                if (resolvingCard == cardUI) resolvingCard = null;
                isResolvingEffects = previous;
            }

            // Effect execution also supports callers without a hand/presentation binding.
            if (handController == null) { settled = true; yield break; }
            if (!CanContinueEffects) yield break;
            yield return cardUI.UsedCardAnimation(cardUsingPoint, handController.GetDiscardPilePoint(),
                cardData.type == CardType.Continuous ? player.transform : null, cardData.ExhaustsOnUse);

            if (cardData.type != CardType.Continuous)
            {
                if (cardData.ExhaustsOnUse) deckController.ExhaustCard(cardData, cardData.deckType);
                else deckController.DiscardCard(cardData, cardData.deckType);
            }
            settled = true;
            Destroy(cardUI.gameObject);
            if (currentState == BattleState.PlayerTurn) RefillHand(cardData.deckType);
        }
        finally
        {
            if (!settled && cardUI != null && cardUI.IsUsed)
            {
                // A suspended use still needs a destination when its owner is disabled.
                if (deckController != null && (continuous == null || !continuous.IsActive))
                {
                    if (cardData.ExhaustsOnUse) deckController.ExhaustCard(cardData, cardData.deckType);
                    else deckController.DiscardCard(cardData, cardData.deckType);
                }
                Destroy(cardUI.gameObject);
            }
            activeCardUses = Mathf.Max(0, activeCardUses - 1);
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
        if (player != null) player.ContinuousCards.ReduceRemainingTurns();
        if (target != null) target.ContinuousCards.ReduceRemainingTurns();
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
        if (CardInspectionUI.BlocksGameplayInput || isResolvingEffects) return;
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

    private void StartPlayerTurn() => EffectSequence.Start(this, StartPlayerTurnRoutine());

    private IEnumerator StartPlayerTurnRoutine()
    {
        bool previousResolving = isResolvingEffects;
        isResolvingEffects = true;
        try
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

            yield return player.ContinuousCards.ProcessTurnStartEffectsRoutine(this);
            if (target != null) yield return target.ContinuousCards.ProcessTurnStartEffectsRoutine(this);
            if (!CanContinueEffects) yield break;

            DrawCards(CardDeckType.TypeDream, dreamCardHand);
            DrawCards(CardDeckType.TypeNightmare, nightmareCardHand);
        }
        finally
        {
            isResolvingEffects = previousResolving;
        }
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
