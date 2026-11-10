using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ShopHandler : MapContentHandler
{
    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject wallet;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private UnityEngine.UI.Button leaveButton;
    [SerializeField] private CardRewardPoolCatalog rewardPools;
    [SerializeField] private DeckCardUI[] dreamSlots;
    [SerializeField] private DeckCardUI[] nightmareSlots;
    [SerializeField] private TMP_Text[] dreamPrices;
    [SerializeField] private TMP_Text[] nightmarePrices;
    [SerializeField] private TMP_Text dreamStockStatus;
    [SerializeField] private TMP_Text nightmareStockStatus;
    private Func<bool> complete;
    private int priceSeed;
    private CardData[] dreamStock;
    private CardData[] nightmareStock;

    [Serializable]
    private sealed class StockState
    {
        public string[] dream;
        public string[] nightmare;
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
        goldText.text = $"보유 골드 {RunGoldInventory.Gold}";
        statusText.text = string.Empty;
        int nodeSeed = progress.NodeSeed;
        var random = new System.Random(nodeSeed);
        // Keep prices reproducible without consuming the stock sampler's random values.
        priceSeed = nodeSeed ^ 0x4F1BBCDC;
        dreamStock = ShopStockSampler.Generate(CardDeckType.TypeDream, rewardPools.Pools, random);
        nightmareStock = ShopStockSampler.Generate(CardDeckType.TypeNightmare, rewardPools.Pools, random);
        panel.SetActive(true);
        wallet.SetActive(true);
        RefreshStock();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(leaveButton.gameObject);
    }

    public override string CaptureState()
    {
        var catalog = RunAssetCatalog.Load();
        return JsonUtility.ToJson(new StockState
        {
            dream = Array.ConvertAll(dreamStock, card => catalog.Key(card)),
            nightmare = Array.ConvertAll(nightmareStock, card => catalog.Key(card))
        });
    }

    public override void RestoreState(string state)
    {
        if (string.IsNullOrEmpty(state)) return;
        try
        {
            var saved = JsonUtility.FromJson<StockState>(state);
            if (saved?.dream?.Length != dreamStock.Length || saved.nightmare?.Length != nightmareStock.Length) return;
            var catalog = RunAssetCatalog.Load();
            CardData[] dream = Array.ConvertAll(saved.dream, catalog.Resolve<CardData>);
            CardData[] nightmare = Array.ConvertAll(saved.nightmare, catalog.Resolve<CardData>);
            if (Array.Exists(dream, card => card != null && card.deckType != CardDeckType.TypeDream) ||
                Array.Exists(nightmare, card => card != null && card.deckType != CardDeckType.TypeNightmare)) return;
            dreamStock = dream;
            nightmareStock = nightmare;
        }
        catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
        {
            // An older or invalid stock record leaves the node's seeded offers in place.
            return;
        }
        RefreshStock();
    }

    private void RefreshStock()
    {
        var priceRandom = new System.Random(priceSeed);
        RefreshShelf(dreamSlots, dreamStock, dreamPrices, dreamStockStatus, priceRandom);
        RefreshShelf(nightmareSlots, nightmareStock, nightmarePrices, nightmareStockStatus, priceRandom);
    }

    private static void RefreshShelf(DeckCardUI[] slots, CardData[] stock, TMP_Text[] prices,
        TMP_Text stockStatus, System.Random priceRandom)
    {
        bool hasStock = false;
        for (int i = 0; i < slots.Length; i++)
        {
            CardData card = stock[i];
            slots[i].gameObject.SetActive(card != null);
            prices[i].gameObject.SetActive(card != null);
            if (card == null) continue;
            slots[i].SetCard(card, applyStatusModifiers: false);
            prices[i].text = ShopStockSampler.RollPrice(card.rarity, priceRandom).ToString();
            hasStock = true;
        }
        stockStatus.gameObject.SetActive(!hasStock);
    }

    private void Leave()
    {
        if (complete == null || DeckOverviewUI.BlocksBattleInput || SettingsUI.BlocksBattleInput ||
            CardInspectionUI.BlocksGameplayInput) return;
        if (!complete())
        {
            statusText.text = "진행 저장에 실패했습니다. 다시 시도해 주세요.";
            return;
        }

        complete = null;
        wallet.SetActive(false);
        panel.SetActive(false);
    }
}
