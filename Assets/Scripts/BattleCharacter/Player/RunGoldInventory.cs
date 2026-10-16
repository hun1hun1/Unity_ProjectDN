using UnityEngine;

public static class RunGoldInventory
{
    public static int Gold { get; private set; }
    public static void Add(int amount)
    {
        if (amount > 0) Gold = (int)System.Math.Min(int.MaxValue, (long)Gold + amount);
    }
    public static void ResetForNewGame() => Gold = 0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitializeRuntime() => ResetForNewGame();
}
