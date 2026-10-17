using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class AmmoSystem : MonoBehaviour
{
    [Header("Ammo")]
    [Min(0)] [SerializeField] private int maxAmmo = 10;
    [Min(0)] [SerializeField] private int startingAmmo;
    [SerializeField] private int currentAmmo;
    private bool pendingInspectorNotification;
    private int battleMaxAmmoBonus;
    private int runMaxAmmoBonus;
    private readonly Dictionary<object, int> continuousBonuses = new Dictionary<object, int>();
    private long continuousMaxAmmoBonus;
    private uint gameVersion;

    public int BaseMaxAmmo => maxAmmo;
    public int BattleMaxAmmoBonus => battleMaxAmmoBonus;
    public int RunMaxAmmoBonus => runMaxAmmoBonus;
    public int MaxAmmo => (int)Math.Min(int.MaxValue,
        (long)Mathf.Max(0, maxAmmo) + runMaxAmmoBonus + battleMaxAmmoBonus + continuousMaxAmmoBonus);
    public int CurrentAmmo => currentAmmo;
    public event Action<int, int> AmmoChanged;

    private void Awake()
    {
        ResetForBattle();
    }

    private void OnEnable()
    {
        RunRelicInventory.Changed += RefreshRunCapacity;
        RefreshRunCapacity();
    }

    private void OnDisable()
    {
        RunRelicInventory.Changed -= RefreshRunCapacity;
    }

    private void OnValidate()
    {
        NormalizeSettings();
        currentAmmo = Mathf.Clamp(currentAmmo, 0, MaxAmmo);
        pendingInspectorNotification = true;
    }

    private void LateUpdate()
    {
        // OnValidate may run outside the main thread; notify UI on the next frame.
        if (!pendingInspectorNotification) return;
        pendingInspectorNotification = false;
        AmmoChanged?.Invoke(currentAmmo, MaxAmmo);
    }

    public void ResetForBattle()
    {
        int previousCurrent = currentAmmo;
        int previousMax = MaxAmmo;
        gameVersion = RunRelicInventory.GameVersion;
        runMaxAmmoBonus = RunRelicInventory.MaxAmmoBonus;
        battleMaxAmmoBonus = 0;
        continuousBonuses.Clear();
        continuousMaxAmmoBonus = 0;
        NormalizeSettings();
        currentAmmo = startingAmmo;
        NotifyIfChanged(previousCurrent, previousMax);
    }

    public int IncreaseMaxForBattle(int amount)
    {
        if (amount <= 0) return 0;
        int previousMax = MaxAmmo;
        battleMaxAmmoBonus = (int)Math.Min(int.MaxValue, (long)battleMaxAmmoBonus + amount);
        NotifyIfChanged(currentAmmo, previousMax);
        return MaxAmmo - previousMax;
    }

    public Action AddContinuousMaxForBattle(int amount)
    {
        if (amount <= 0) return null;
        int previousMax = MaxAmmo;
        object token = new object();
        continuousBonuses.Add(token, amount);
        continuousMaxAmmoBonus += amount;
        NotifyIfChanged(currentAmmo, previousMax);
        return () =>
        {
            if (!continuousBonuses.Remove(token)) return;
            int oldMax = MaxAmmo;
            int oldCurrent = currentAmmo;
            continuousMaxAmmoBonus -= amount;
            currentAmmo = Mathf.Clamp(currentAmmo, 0, MaxAmmo);
            NotifyIfChanged(oldCurrent, oldMax);
        };
    }

    private void RefreshRunCapacity()
    {
        // A disabled character also detects a new game when enabled again.
        if (gameVersion != RunRelicInventory.GameVersion)
        {
            ResetForBattle();
            return;
        }
        int previousCurrent = currentAmmo;
        int previousMax = MaxAmmo;
        runMaxAmmoBonus = RunRelicInventory.MaxAmmoBonus;
        currentAmmo = Mathf.Clamp(currentAmmo, 0, MaxAmmo);
        NotifyIfChanged(previousCurrent, previousMax);
    }

    public bool HasAmmo(int amount)
    {
        return amount >= 0 && currentAmmo >= amount;
    }

    public int Load(int amount)
    {
        if (amount <= 0) return 0;
        int loaded = Mathf.Min(amount, MaxAmmo - currentAmmo);
        SetCurrentAmmo(currentAmmo + loaded);
        return loaded;
    }

    public bool TrySpend(int amount)
    {
        if (!HasAmmo(amount)) return false;
        SetCurrentAmmo(currentAmmo - amount);
        return true;
    }

    public int SpendAll()
    {
        int spent = currentAmmo;
        SetCurrentAmmo(0);
        return spent;
    }

    private void NormalizeSettings()
    {
        maxAmmo = Mathf.Max(0, maxAmmo);
        startingAmmo = Mathf.Clamp(startingAmmo, 0, MaxAmmo);
    }

    private void SetCurrentAmmo(int amount)
    {
        int previousCurrent = currentAmmo;
        currentAmmo = amount;
        NotifyIfChanged(previousCurrent, MaxAmmo);
    }

    private void NotifyIfChanged(int previousCurrent, int previousMax)
    {
        if (currentAmmo == previousCurrent && MaxAmmo == previousMax) return;
        pendingInspectorNotification = false;
        AmmoChanged?.Invoke(currentAmmo, MaxAmmo);
    }
}
