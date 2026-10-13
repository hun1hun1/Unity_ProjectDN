using System.Collections.Generic;
using System.Text;

public static class CardStatusDescriptionBuilder
{
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
