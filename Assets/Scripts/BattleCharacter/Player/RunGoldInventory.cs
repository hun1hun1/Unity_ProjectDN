using UnityEngine;

public static class RunGoldInventory
{
    public static int Gold { get; private set; }
    internal static int CardRemovals { get; private set; }
    internal static long CardRemovalPrice => 100L + 50L * CardRemovals;
    public static void Add(int amount)
    {
        if (amount > 0) Gold = (int)System.Math.Min(int.MaxValue, (long)Gold + amount);
    }
    public static bool TrySpend(int amount)
    {
        if (amount < 0 || amount > Gold) return false;
        Gold -= amount;
        return true;
    }

    internal static bool TryPayForCardRemoval(long price)
    {
        if (price != CardRemovalPrice || price > Gold || !TrySpend((int)price)) return false;
        CardRemovals++;
        return true;
    }

    internal static void Restore(int gold, int cardRemovals)
    {
        Gold = gold;
        CardRemovals = cardRemovals;
    }

    public static void ResetForNewGame() => Restore(0, 0);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeRuntime() => ResetForNewGame();
}
