using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CardDatabase",
    menuName = "Card/Card Database"
)]
public class CardDatabase : ScriptableObject
{
    [SerializeField]
    private List<CardData> cards = new List<CardData>();

    public IReadOnlyList<CardData> GetAllCards()
    {
        return cards;
    }
}
