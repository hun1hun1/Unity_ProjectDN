using System.Collections.Generic;
using System.Text;

public static class DelayedEffectDescriptionBuilder
{
    public static string Build(DelayEffect effect)
    {
        if (effect == null) return "";
        return Build(effect.Description, effect.Effects);
    }

    public static string Build(string description, CardEffect[] effects, int? spentAmmo = null)
    {
        return !string.IsNullOrWhiteSpace(description)
            ? CardDescriptionBuilder.BuildEffects(description, effects, spentAmmo)
            : Summarize(effects, new HashSet<CardEffect>());
    }

    public static string Summarize(CardEffect[] effects, HashSet<CardEffect> visiting)
    {
        var text = new StringBuilder();
        if (effects == null) return "";
        foreach (CardEffect effect in effects)
        {
            if (effect == null || !visiting.Add(effect)) continue;
            try
            {
                string summary = effect.GetEffectSummary(visiting);
                if (string.IsNullOrEmpty(summary)) continue;
                if (text.Length > 0) text.Append('\n');
                text.Append(summary);
            }
            finally { visiting.Remove(effect); }
        }
        return text.ToString();
    }
}
