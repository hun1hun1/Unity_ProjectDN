using UnityEngine;

public enum CardCostMode { Fixed, X }

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
    [Tooltip("Fixed uses the configured cost. X spends the matching remaining pool and repeats use-time effects that many times.")]
    public CardCostMode costMode;
    public string CostLabel => costMode == CardCostMode.X ? "X" :
        (deckType == CardDeckType.TypeDream ? cost.dreamCost : cost.nightmareCost).ToString();
    public Sprite artwork;

    [TextArea]
    public string description;

    public CardDeckType deckType;
    public CardType type;
    public string TypeLabel => type switch
    {
        CardType.Attack => "\uACF5\uACA9",
        CardType.Skill => "\uC2A4\uD0AC",
        CardType.Continuous => "\uC9C0\uC18D",
        _ => type.ToString()
    };
    public CardRarity rarity;
    public CardEffect[] effects;

    [Header("Use Presentation")]
    public CardPresentation presentation;

    [Header("Card Traits")]
    [Tooltip("Excludes this card from the current battle after use. Permanent continuous cards absorb unless a use-time delayed effect is scheduled.")]
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