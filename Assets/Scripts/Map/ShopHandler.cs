using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ShopHandler : MapContentHandler
{
    private static readonly Color SelectionGold = new Color(1f, 0.84f, 0.42f);

    [SerializeField] private GameObject panel;
    [SerializeField] private RectTransform display;
    [SerializeField] private GameObject wallet;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private UnityEngine.UI.Button leaveButton;
    [SerializeField] private CardRewardPoolCatalog rewardPools;
    [SerializeField] private CardDatabase dreamStartingDeckDatabase;
    [SerializeField] private CardDatabase nightmareStartingDeckDatabase;
    [SerializeField] private Sprite soldOutSprite;
    [SerializeField] private DeckCardUI[] dreamSlots;
    [SerializeField] private DeckCardUI[] nightmareSlots;
    [SerializeField] private TMP_Text[] dreamPrices;
    [SerializeField] private TMP_Text[] nightmarePrices;
    [SerializeField] private TMP_Text dreamStockStatus;
    [SerializeField] private TMP_Text nightmareStockStatus;
    private Func<bool> complete;
    private RunNodeVisit visit;
    private int priceSeed;
    private Offer[] dreamOffers;
    private Offer[] nightmareOffers;

    private sealed class Offer
    {
        public readonly CardData Card;
        public readonly int Price;
        public bool SoldOut;

        public Offer(CardData card, int price, bool soldOut)
        {
            Card = card;
            Price = price;
            SoldOut = soldOut;
        }
    }

    [Serializable]
    private sealed class StockState
    {
        public string[] dream;
        public string[] nightmare;
        public bool[] dreamSoldOut;
        public bool[] nightmareSoldOut;
    }

    public override MapContentType ContentType => MapContentType.Shop;

    private void Awake() => leaveButton.onClick.AddListener(Leave);

    private void OnDestroy()
    {
        if (leaveButton != null) leaveButton.onClick.RemoveListener(Leave);
    }

    public override void Begin(MapChoice choice, RunProgress progress, Func<bool> complete)
    {
        this.complete = complete;
        visit = CaptureVisit(progress);
        statusText.text = string.Empty;
        int nodeSeed = progress.NodeSeed;
        var random = new System.Random(nodeSeed);
        // Keep prices reproducible without consuming the stock sampler's random values.
        priceSeed = nodeSeed ^ 0x4F1BBCDC;
        SetOffers(ShopStockSampler.Generate(CardDeckType.TypeDream, rewardPools.Pools, random),
            ShopStockSampler.Generate(CardDeckType.TypeNightmare, rewardPools.Pools, random));
        panel.SetActive(true);
        wallet.SetActive(true);
        RefreshStock();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(leaveButton.gameObject);
    }

    internal override void PlayEntrance(UnityEngine.UI.Button[] choices, int selectedIndex)
    {
        if (display != null) StartEntrance(panel, choices, selectedIndex, SelectionGold, shelf: display);
    }

    public override string CaptureState()
    {
        var catalog = RunAssetCatalog.Load();
        return JsonUtility.ToJson(new StockState
        {
            dream = Array.ConvertAll(dreamOffers, offer => catalog.Key(offer.Card)),
            nightmare = Array.ConvertAll(nightmareOffers, offer => catalog.Key(offer.Card)),
            dreamSoldOut = Array.ConvertAll(dreamOffers, offer => offer.SoldOut),
            nightmareSoldOut = Array.ConvertAll(nightmareOffers, offer => offer.SoldOut)
        });
    }

    public override void RestoreState(string state)
    {
        if (string.IsNullOrEmpty(state)) return;
        try
        {
            var saved = JsonUtility.FromJson<StockState>(state);
            if (saved?.dream?.Length != dreamOffers.Length || saved.nightmare?.Length != nightmareOffers.Length) return;
            var catalog = RunAssetCatalog.Load();
            CardData[] dream = Array.ConvertAll(saved.dream, catalog.Resolve<CardData>);
            CardData[] nightmare = Array.ConvertAll(saved.nightmare, catalog.Resolve<CardData>);
            if (!IsValidShelf(dream, saved.dreamSoldOut, CardDeckType.TypeDream) ||
                !IsValidShelf(nightmare, saved.nightmareSoldOut, CardDeckType.TypeNightmare)) return;
            SetOffers(dream, nightmare, saved.dreamSoldOut, saved.nightmareSoldOut);
        }
        catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
        {
            // An invalid stock record leaves the node's seeded offers in place.
            return;
        }
        RefreshStock();
    }

    private static bool IsValidShelf(CardData[] cards, bool[] soldOut, CardDeckType type)
    {
        // Older checkpoints contain only the offer cards.
        if (soldOut is { Length: > 0 } && soldOut.Length != cards.Length) return false;
        for (int i = 0; i < cards.Length; i++)
        {
            CardData card = cards[i];
            if (card != null ? card.deckType != type : soldOut is { Length: > 0 } && soldOut[i]) return false;
        }
        return true;
    }

    private void SetOffers(CardData[] dream, CardData[] nightmare, bool[] dreamSoldOut = null, bool[] nightmareSoldOut = null)
    {
        var random = new System.Random(priceSeed);
        dreamOffers = BuildOffers(dream, dreamSoldOut, random);
        nightmareOffers = BuildOffers(nightmare, nightmareSoldOut, random);
    }

    private static Offer[] BuildOffers(CardData[] cards, bool[] soldOut, System.Random random)
    {
        var offers = new Offer[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            CardData card = cards[i];
            // A sold slot keeps its original offer and consumes the same price roll.
            int price = card != null ? ShopStockSampler.RollPrice(card.rarity, random) : 0;
            offers[i] = new Offer(card, price, soldOut is { Length: > 0 } && soldOut[i]);
        }
        return offers;
    }

    private void RefreshStock()
    {
        goldText.text = $"보유 골드 {RunGoldInventory.Gold}";
        RefreshShelf(dreamSlots, dreamOffers, dreamPrices, dreamStockStatus, CardDeckType.TypeDream);
        RefreshShelf(nightmareSlots, nightmareOffers, nightmarePrices, nightmareStockStatus, CardDeckType.TypeNightmare);
    }

    private void RefreshShelf(DeckCardUI[] slots, Offer[] offers, TMP_Text[] prices,
        TMP_Text stockStatus, CardDeckType type)
    {
        bool hasOffers = false;
        for (int i = 0; i < slots.Length; i++)
        {
            Offer offer = offers[i];
            DeckCardUI cardView = slots[i];
            bool available = offer.Card != null && !offer.SoldOut;
            cardView.gameObject.SetActive(available);
            prices[i].gameObject.SetActive(available);
            UpdateSoldOutMarker((RectTransform)cardView.transform, offer.SoldOut);

            int index = i;
            cardView.InspectionOverride = available ? () => OpenOffer(type, index) : null;
            if (offer.Card != null) hasOffers = true;
            if (!available) continue;
            cardView.SetCard(offer.Card, applyStatusModifiers: false);
            prices[i].text = offer.Price.ToString();
        }
        stockStatus.gameObject.SetActive(!hasOffers);
    }

    private void UpdateSoldOutMarker(RectTransform card, bool soldOut)
    {
        Transform marker = card.parent.Find("SoldOut");
        if (marker == null && soldOut)
        {
            var sign = new GameObject("SoldOut", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var image = sign.GetComponent<UnityEngine.UI.Image>();
            image.sprite = soldOutSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.SetParent(card.parent, false);
            rect.anchorMin = card.anchorMin;
            rect.anchorMax = card.anchorMax;
            rect.pivot = card.pivot;
            rect.sizeDelta = card.sizeDelta;
            rect.anchoredPosition = card.anchoredPosition;
            marker = rect;
        }
        if (marker != null) marker.gameObject.SetActive(soldOut);
    }

    private Offer GetOffer(CardDeckType type, int index) =>
        (type == CardDeckType.TypeDream ? dreamOffers : nightmareOffers)[index];

    private void OpenOffer(CardDeckType type, int index)
    {
        if (visit == null || IsEntering || !panel.activeInHierarchy || DeckOverviewUI.BlocksBattleInput) return;
        Offer offer = GetOffer(type, index);
        if (offer.Card == null || offer.SoldOut) return;
        RunNodeVisit expectedVisit = visit;
        CardInspectionUI.TryOpenShopOffer(offer.Card, offer.Price, goldText.font, leaveButton.targetGraphic as UnityEngine.UI.RawImage,
            () => Purchase(expectedVisit, type, index, offer.Card, offer.Price));
    }

    private string Purchase(RunNodeVisit expectedVisit, CardDeckType type, int index, CardData card, int price)
    {
        Offer offer = GetOffer(type, index);
        if (visit != expectedVisit || !panel.activeInHierarchy || offer.SoldOut || offer.Card != card ||
            offer.Price != price || price <= 0 || dreamStartingDeckDatabase == null || nightmareStartingDeckDatabase == null)
            return "카드를 구매할 수 없습니다.";
        if (RunGoldInventory.Gold < price) return "골드가 부족합니다.";

        bool committed = CommitAcquisition(visit, () =>
        {
            if (!RunGoldInventory.TrySpend(price) ||
                !RunDeckInventory.TryAddCard(dreamStartingDeckDatabase, nightmareStartingDeckDatabase, card))
                throw new InvalidOperationException("Could not apply the shop purchase.");
            offer.SoldOut = true;
        });
        if (!committed) return "저장에 실패했습니다. 다시 시도해 주세요.";

        RefreshStock();
        return null;
    }

    private void Leave()
    {
        if (complete == null || IsEntering || DeckOverviewUI.BlocksBattleInput || SettingsUI.BlocksBattleInput ||
            CardInspectionUI.BlocksGameplayInput) return;
        if (!complete())
        {
            statusText.text = "진행 저장에 실패했습니다. 다시 시도해 주세요.";
            return;
        }

        complete = null;
        visit = null;
        wallet.SetActive(false);
        panel.SetActive(false);
    }
}
