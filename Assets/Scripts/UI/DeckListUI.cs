using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeckListUI : MonoBehaviour
{
    [SerializeField]
    private Transform content;

    [SerializeField]
    private DeckCardUI cardPrefab;

    [SerializeField, Min(0.1f)]
    private float cardScale = 1f;

    [SerializeField] private bool sortByName;
    [SerializeField, Min(0)] private int maxColumns;

    [SerializeField]
    private GameObject panel;

    private readonly List<DeckCardUI> displayedCards = new List<DeckCardUI>();
    private CardHandController sourceHand;
    private GridLayoutGroup grid;
    private Vector2 originalSpacing;
    private Vector2 appliedCardSize = new Vector2(-1f, -1f);

    private void LateUpdate()
    {
        if (panel.activeInHierarchy) RefreshCardSizes();
    }

    public void ShowCards(IReadOnlyList<CardData> cards, CardHandController hand = null)
    {
        if (CardInspectionUI.BlocksGameplayInput) return;
        Clear();

        sourceHand = hand;
        panel.SetActive(true);

        // 원본 DrawPile은 건드리지 않고 복사
        List<CardData> shuffledCards = new List<CardData>(cards);

        if (sortByName)
            shuffledCards.Sort((left, right) => System.StringComparer.Ordinal.Compare(left.cardName, right.cardName));
        else
        {
            // Preserve the pile viewer's random presentation.
            for (int i = shuffledCards.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                CardData temp = shuffledCards[i];
                shuffledCards[i] = shuffledCards[randomIndex];
                shuffledCards[randomIndex] = temp;
            }
        }

        foreach (CardData cardData in shuffledCards)
        {
            DeckCardUI cardUI = Instantiate(cardPrefab, content);

            cardUI.SetCard(cardData, sourceHand != null ? sourceHand.BattleContext : null);
            displayedCards.Add(cardUI);
        }

        Canvas.ForceUpdateCanvases();
        RefreshCardSizes(true);
    }

    private void RefreshCardSizes(bool force = false)
    {
        if (grid == null)
        {
            grid = content.GetComponent<GridLayoutGroup>();
            if (grid == null) return;
            originalSpacing = grid.spacing;
        }

        Vector2 prefabSize = ((RectTransform)cardPrefab.transform).rect.size;
        float scale = Mathf.Max(0.1f, cardScale);
        Vector2 size = prefabSize * scale;
        bool columnsChanged = false;
        if (maxColumns > 0)
        {
            float availableWidth = ((RectTransform)content).rect.width - grid.padding.horizontal;
            int columns = Mathf.Clamp(Mathf.FloorToInt((availableWidth + originalSpacing.x * scale) /
                Mathf.Max(1f, size.x + originalSpacing.x * scale)), 1, maxColumns);
            columnsChanged = grid.constraintCount != columns;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }
        if (!force && size == appliedCardSize && !columnsChanged) return;

        appliedCardSize = size;
        float ratio = prefabSize.y > 0f ? size.y / prefabSize.y : 1f;
        grid.cellSize = size;
        grid.spacing = originalSpacing * ratio;
        foreach (DeckCardUI card in displayedCards)
            if (card != null) card.ResizeForList(ratio);

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
    }

    public void Close()
    {
        if (CardInspectionUI.BlocksGameplayInput) return;
        HideAndClear();
    }

    internal void HideAndClear()
    {
        panel.SetActive(false);
        Clear();
        sourceHand = null;
    }

    private void Clear()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            // Destroy is deferred; hide old entries before rebuilding the grid.
            child.SetActive(false);
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
        displayedCards.Clear();
    }
}