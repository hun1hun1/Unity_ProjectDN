using UnityEngine;

public static class RunGoldInventory
{
    public static int Gold { get; private set; }
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

    public static void ResetForNewGame() => Gold = 0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeRuntime() => ResetForNewGame();
}
