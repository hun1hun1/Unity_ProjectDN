using System.Text.RegularExpressions;

public static class CardDescriptionBuilder
{
    private static readonly Regex Tokens = new Regex(@"\{(damage|ammoCost|ammoCondition):(\d+(?:\.\d+)*)\}");

    public static string Build(CardData card, BattleManager battle = null, bool applyStatusModifiers = true)
    {
        if (card == null) return "";
        string template = card.description ?? "";
        if (!Tokens.IsMatch(template)) return template;
        var context = new CardPreviewContext(card, battle, applyStatusModifiers);
        context.PreviewEffects(card.effects);
        return Tokens.Replace(template, match =>
        {
            string key = match.Groups[1].Value + ":" + match.Groups[2].Value;
            return context.TryGetValue(key, out string value) ? value : match.Value;
        });
    }
}
