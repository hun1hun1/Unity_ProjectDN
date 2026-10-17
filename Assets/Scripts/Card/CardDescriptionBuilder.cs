using System.Text.RegularExpressions;

public static class CardDescriptionBuilder
{
    private static readonly Regex Tokens = new Regex(@"\{(damage|block|ammoCost|ammoCondition|openingCondition|spentAmmo|spentAmmoCondition|turnDamage|turnBlock|turnAmmoCost|turnAmmoCondition|turnOpeningCondition|turnSpentAmmo|turnSpentAmmoCondition):(\d+(?:\.\d+)*)\}");

    public static string BuildEffects(string template, CardEffect[] effects, int? spentAmmo = null)
    {
        var context = new CardPreviewContext(null, null, false, forTurnStart: true, capturedSpentAmmo: spentAmmo);
        context.PreviewEffects(effects);
        return Tokens.Replace(template ?? "", match =>
            context.TryGetValue(match.Groups[1].Value + ":" + match.Groups[2].Value, out string value) ? value : match.Value);
    }

    public static string Build(CardData card, BattleManager battle = null, bool applyStatusModifiers = true)
    {
        if (card == null) return "";
        string template = card.description ?? "";
        if (template.Contains("{x}"))
        {
            CostSystem costs = battle != null ? battle.GetPlayer()?.GetCostSystem() : null;
            string repeats = card.costMode != CardCostMode.X ? "1" :
                applyStatusModifiers && costs != null ? costs.GetCurrentCost(card.deckType).ToString() : "X";
            template = template.Replace("{x}", repeats);
        }
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
                "turnOpeningCondition" => "openingCondition",
                "turnSpentAmmo" => "spentAmmo",
                "turnSpentAmmoCondition" => "spentAmmoCondition",
                _ => kind
            };
            string key = token + ":" + match.Groups[2].Value;
            CardPreviewContext source = isTurn ? turnContext : context;
            return source != null && source.TryGetValue(key, out string value) ? value : match.Value;
        });
    }
}
