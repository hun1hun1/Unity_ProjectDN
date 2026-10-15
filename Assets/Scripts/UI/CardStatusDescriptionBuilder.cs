using System.Collections.Generic;
using System.Text;

public static class CardStatusDescriptionBuilder
{
    // Card traits share the explanation panel without becoming character statuses.
    public static string BuildForCard(CardData card)
    {
        string statuses = Build(CardStatusCollector.Collect(card));
        if (card == null || !card.ExhaustsOnUse) return statuses;
        const string exhaust = "<b>소멸</b>\n사용 후 이번 전투 동안 제외되며, 다시 뽑히지 않습니다.";
        return string.IsNullOrEmpty(statuses) ? exhaust : exhaust + "\n\n" + statuses;
    }

    public static string Build(IReadOnlyList<StatusEffectData> statuses)
    {
        var text = new StringBuilder();
        if (statuses == null) return "";
        foreach (StatusEffectData status in statuses)
        {
            if (status == null) continue;
            if (text.Length > 0) text.Append("\n\n");
            text.Append("<b>").Append(status.DisplayName).Append("</b>\n").Append(status.Description);
        }
        return text.ToString();
    }
}
