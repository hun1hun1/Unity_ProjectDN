using UnityEngine;

public enum CardType
{
    Attack,
    Skill,
    Continuous
}

public enum CardRarity
{
    Common,
    Uncommon,
    Rare
}

[CreateAssetMenu(fileName = "CardData", menuName = "Card/CardData")]
public class CardData : ScriptableObject
{
    public string cardName;
    public Cost cost;
    public Sprite artwork;

    [TextArea]
    public string description;

    public CardDeckType deckType;
    public CardType type;
    public CardRarity rarity;
    public CardEffect[] effects;

    [Header("Card Traits")]
    [Tooltip("Excludes this card from the current battle after use. Permanent continuous cards always absorb instead.")]
    public bool exhaustOnUse;

    public bool ExhaustsOnUse => exhaustOnUse &&
        (type != CardType.Continuous || continuousDurationType == ContinuousDurationType.FixedTurns);

    [Header("Continuous Card")]
    [Tooltip("Optional effects executed at each following player-turn start, before expiry. Never executed on use.")]
    public CardEffect[] turnStartEffects;
    public ContinuousDurationType continuousDurationType;
    [Min(1)] public int continuousTurns = 1;
    [Tooltip("Status icon for this continuous card. Uses artwork when empty.")]
    public Sprite continuousIcon;

    [Header("Ammo Requirement")]
    [Min(0)] public int requiredAmmo;
    [Tooltip("Consume the required ammo when this card is used.")]
    public bool consumeRequiredAmmo;
}