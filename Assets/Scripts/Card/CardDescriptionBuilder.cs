using System.Text.RegularExpressions;

public static class CardDescriptionBuilder
{
    private static readonly Regex Tokens = new Regex(@"\{(damage|block|ammoCost|ammoCondition|turnDamage|turnBlock|turnAmmoCost|turnAmmoCondition):(\d+(?:\.\d+)*)\}");

    public static string Build(CardData card, BattleManager battle = null, bool applyStatusModifiers = true)
    {
        if (card == null) return "";
        string template = card.description ?? "";
        if (!Tokens.IsMatch(template)) return template;
        var context = new CardPreviewContext(card, battle, applyStatusModifiers);
        context.PreviewEffects(card.effects);
        CardPreviewContext turnContext = null;
        if (card.type == CardType.Continuous)
        {
            turnContext = new CardPreviewContext(card, battle, applyStatusModifiers, forTurnStart: true);
            turnContext.PreviewEffects(card.turnStartEffects);
        }
        return Tokens.Replace(template, match =>
        {
            string kind = match.Groups[1].Value;
            bool isTurn = kind.StartsWith("turn", System.StringComparison.Ordinal);
            string token = kind switch
            {
                "turnDamage" => "damage",
                "turnBlock" => "block",
                "turnAmmoCost" => "ammoCost",
                "turnAmmoCondition" => "ammoCondition",
                _ => kind
            };
            string key = token + ":" + match.Groups[2].Value;
            CardPreviewContext source = isTurn ? turnContext : context;
            return source != null && source.TryGetValue(key, out string value) ? value : match.Value;
        });
    }
}
