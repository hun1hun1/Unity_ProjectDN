using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [Header("Card Hand")]
    [SerializeField] private CardHandController cardHandController;

    [Header("Test Cards")]
    [SerializeField] private CardData[] testCards;

    [SerializeField] private Player player;
    [SerializeField] private Enemy target;

    private void Start()
    {
        DrawTestCards();
    }

    private void DrawTestCards()
    {
        while (!cardHandController.IsFull())
        {
            foreach (CardData cardData in testCards)
            {
                cardHandController.AddCard(cardData, this);
            }
        }
    }

    public Player GetPlayer()
    {
        return player;
    }

    public Enemy GetTarget()
    {
        return target;
    }

    public void UseCard(CardData cardData)
    {
        Debug.Log($"카드 사용: {cardData.cardName}");

        foreach (CardEffect effect in cardData.effects)
        {
            effect.Execute(this);
        }
    }
}
