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

    public BattleResult Result { get; private set; }
    public event System.Action<BattleResult> BattleEnded;
    private BattleState currentState;
    private bool isResolvingEffects;
    private int activeCardUses;
    private int cardsPlayedThisTurn;
    public CardUseContext CurrentCardUse { get; private set; }
    public int CurrentSpentAmmo { get; private set; }
    public bool OpeningAvailable => currentState == BattleState.PlayerTurn && CanContinueEffects && cardsPlayedThisTurn == 0;
    public event System.Action OpeningChanged;

    private void ResetOpeningForTurn()
    {
        cardsPlayedThisTurn = 0;
        OpeningChanged?.Invoke();
    }
    private CardUI resolvingCard;
    private Player boundPlayer;
    private Enemy boundEnemy;
    private readonly MulliganSystem mulligans = new MulliganSystem();
    public MulliganSystem Mulligans => mulligans;
    public DelayedEffectSystem DelayedEffects { get; } = new DelayedEffectSystem();
    public CardData CurrentEffectCard { get; private set; }
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

    public IEnumerator ExecuteEffectsRoutine(CardEffect[] effects, ActiveContinuousCard source = null, CardData sourceCard = null)
    {
        if (effects == null) yield break;
        BindBattleCharacters();
        CardData previousCard = CurrentEffectCard;
        CurrentEffectCard = sourceCard ?? source?.Card ?? previousCard;
        try
        {
            foreach (CardEffect effect in effects)
            {
                if (!CanContinueEffects || (source != null && !source.IsActive)) yield break;
                if (effect == null) continue;
                IEnumerator step = source == null ? effect.ExecuteRoutine(this) : effect.ActivateContinuousRoutine(this, source);
                if (step != null) yield return step;
            }
        }
        finally { CurrentEffectCard = previousCard; }
    }

    public IEnumerator ExecuteWithSpentAmmoRoutine(CardEffect[] effects, int spentAmmo,
        ActiveContinuousCard source = null, CardData sourceCard = null)
    {
        int previous = CurrentSpentAmmo;
        CurrentSpentAmmo = System.Math.Max(0, spentAmmo);
        try { yield return ExecuteEffectsRoutine(effects, source, sourceCard); }
        finally { CurrentSpentAmmo = CanContinueEffects ? previous : 0; }
    }

    public IEnumerator ExecuteCardEffectsRoutine(CardData card, int repeatCount, ActiveContinuousCard source = null)
    {
        if (card == null) yield break;
        for (int i = 0; i < repeatCount; i++)
        {
            if (!CanContinueEffects || source != null && !source.IsActive) yield break;
            yield return ExecuteEffectsRoutine(card.effects, source, card);
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

    internal void BindBattleCharacters()
    {
        if (boundPlayer != player)
        {
            if (boundPlayer != null) boundPlayer.Died -= OnCharacterDied;
            boundPlayer = player;
            if (boundPlayer != null) boundPlayer.Died += OnCharacterDied;
        }
        // Character Awake may have rebound its HUD after our OnEnable. Restore the HUD independently of death subscriptions.
        if (player != null) player.BindDelayedEffects(DelayedEffects);
        if (boundEnemy != target)
        {
            if (boundEnemy != null) boundEnemy.Died -= OnCharacterDied;
            boundEnemy = target;
            if (boundEnemy != null) boundEnemy.Died += OnCharacterDied;
        }
    }

    private void OnEnable() { BindBattleCharacters(); }

    private void OnCharacterDied(BattleCharacter character)
    {
        FinishBattle(character == player || player != null && player.GetCurrentHP() <= 0
            ? BattleResult.Defeat : BattleResult.Victory);
    }

    public void EndBattle() => FinishBattle(BattleResult.Cancelled);

    public void FinishBattle(BattleResult result)
    {
        if (currentState == BattleState.BattleEnd) return;
        CurrentSpentAmmo = 0;
        Result = result;
        ChangeState(BattleState.BattleEnd);
        DelayedEffects.Clear();
        mulligans.Cancel();
        if (player != null) player.ContinuousCards.ResetForBattle();
        if (target != null) target.ContinuousCards.ResetForBattle();
        isResolvingEffects = false;
        CardInspectionUI.CloseForBattleEnd();
        BattleEnded?.Invoke(Result);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        EffectSequence.Stop(this);
        mulligans.Cancel();
        DelayedEffects.Clear();
        if (player != null) player.ContinuousCards.ResetForBattle();
        if (target != null) target.ContinuousCards.ResetForBattle();
        if (boundPlayer != null) boundPlayer.Died -= OnCharacterDied;
        if (boundEnemy != null) boundEnemy.Died -= OnCharacterDied;
        boundPlayer = null; boundEnemy = null;
        CurrentSpentAmmo = 0;
        resolvingCard = null; CurrentEffectCard = null; CurrentCardUse = null; activeCardUses = 0; isResolvingEffects = false;
        ResetOpeningForTurn();
    }

    private void Start()
    {
        CurrentCardUse = null;
        ResetOpeningForTurn();
        DelayedEffects.Clear();
        BindBattleCharacters();
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
        bool isXCost = cardData.costMode == CardCostMode.X;
        int paidCost;
        CardUseContext cardUse;
        isResolvingEffects = true;
        try
        {
            // Cost notifications must not rewrite the resolving card's pre-payment preview.
            cardUI.PauseDescriptionUpdates();
            if (!costSystem.TryPay(cardData, out paidCost))
                return false;
            if (cardData.consumeRequiredAmmo && cardData.requiredAmmo > 0 &&
                !player.GetAmmoSystem().TrySpend(cardData.requiredAmmo))
            {
                // Restore the card cost if a resource changed after preflight.
                if (cardData.deckType == CardDeckType.TypeDream)
                    costSystem.AddDreamCost(paidCost);
                else
                    costSystem.AddNightmareCost(paidCost);
                return false;
            }

            cardUse = new CardUseContext(cardData, cardsPlayedThisTurn == 0);
            cardUI.MarkAsUsed();
            cardsPlayedThisTurn++;
            OpeningChanged?.Invoke();
            Debug.Log($"Card used: {cardData.cardName}");

        }
        finally
        {
            if (cardUI != null && !cardUI.IsUsed) cardUI.ResumeDescriptionUpdates();
            isResolvingEffects = false;
        }

        activeCardUses++;
        EffectSequence.Start(this, UseCardRoutine(cardUI, cardData, isXCost ? paidCost : 1, cardUse));
        return true;
    }

    private IEnumerator UseCardRoutine(CardUI cardUI, CardData cardData, int repeatCount, CardUseContext cardUse)
    {
        bool settled = false;
        bool exhausts = repeatCount == 0 ? cardData.exhaustOnUse : cardData.ExhaustsOnUse;
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
            CardUseContext previousUse = CurrentCardUse;
            CurrentCardUse = cardUse;
            bool previous = isResolvingEffects;
            isResolvingEffects = true;
            try
            {
                if (cardData.type == CardType.Continuous && repeatCount > 0)
                    yield return player.ContinuousCards.ActivateRoutine(cardData, this, deckController, entry => continuous = entry, repeatCount);
                else
                    yield return ExecuteCardEffectsRoutine(cardData, repeatCount);
            }
            finally
            {
                if (resolvingCard == cardUI) resolvingCard = null;
                CurrentCardUse = previousUse;
                isResolvingEffects = previous;
            }

            // Effect execution also supports callers without a hand/presentation binding.
            if (handController == null) { settled = true; yield break; }
            if (!CanContinueEffects) yield break;
            yield return cardUI.UsedCardAnimation(cardUsingPoint, handController.GetDiscardPilePoint(),
                continuous != null ? player.transform : null, continuous != null ? continuous.ExhaustsOnUse : exhausts);

            if (continuous == null)
            {
                if (exhausts) deckController.ExhaustCard(cardData, cardData.deckType);
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
                if (deckController != null && continuous == null)
                {
                    if (exhausts) deckController.ExhaustCard(cardData, cardData.deckType);
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
        if (!CanContinueEffects) yield break;

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

        if (!CanContinueEffects) yield break;
        StartEnemyTurn();
        if (!CanContinueEffects) yield break;

        // =========================================
        // 3. 플레이어 턴 시작
        // =========================================

        StartPlayerTurn();
    }

    private void StartPlayerTurn() => EffectSequence.Start(this, StartPlayerTurnRoutine());

    private IEnumerator StartPlayerTurnRoutine()
    {
        if (!CanContinueEffects) yield break;
        bool previousResolving = isResolvingEffects;
        isResolvingEffects = true;
        try
        {
            ResetOpeningForTurn();
            DelayedEffects.AdvanceTurn();
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

            yield return DelayedEffects.ProcessDueEffectsRoutine(this);
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
        OpeningChanged?.Invoke();

        Debug.Log($"Battle State : {currentState}");
    }

    private void StartEnemyTurn()
    {
        if (!CanContinueEffects) return;
        ChangeState(BattleState.EnemyTurn);

        Debug.Log("적 턴 시작");

        target.ResetBlock();

        target.EnemyTurn(this);

        if (CanContinueEffects) EndEnemyTurn();
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
