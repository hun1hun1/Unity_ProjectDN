using System;

public enum EffectAmountMode { Fixed, PerSpentAmmo }

internal static class EffectAmount
{
    internal static int Resolve(int amount, EffectAmountMode mode, int spentAmmo)
    {
        if (mode != EffectAmountMode.PerSpentAmmo) return amount;
        return (int)Math.Min(int.MaxValue, (long)Math.Max(0, amount) * Math.Max(0, spentAmmo));
    }

    internal static string Describe(int amount, EffectAmountMode mode) =>
        mode == EffectAmountMode.PerSpentAmmo ? $"{amount} × 소모한 총알 수" : amount.ToString();
}
