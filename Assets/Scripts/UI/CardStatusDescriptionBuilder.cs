using System.Collections.Generic;
using System.Text;

public static class CardStatusDescriptionBuilder
{
    internal static string FormatTitle(string title) => $"<color=#F3D58A>{title}</color>";

    // Card traits share the explanation panel without becoming character statuses.
    public static string BuildForCard(CardData card)
    {
        string statuses = Build(CardStatusCollector.Collect(card));
        if (card == null || !card.ExhaustsOnUse) return statuses;
        string exhaust = $"{FormatTitle("소멸")}\n사용 후 이번 전투 동안 제외되며, 다시 뽑히지 않습니다.";
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
            text.Append(FormatTitle(status.DisplayName)).Append("\n").Append(status.Description);
        }
        return text.ToString();
    }
}
