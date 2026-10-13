using System;

// Shared subscription lifecycle for hand cards and pile-list cards.
internal sealed class CardDescriptionBinding
{
    private readonly Action<string> display;
    private CardData card;
    private BattleManager battle;
    private bool applyStatusModifiers;
    private StatusEffectSystem playerStatuses;
    private StatusEffectSystem enemyStatuses;
    private AmmoSystem ammo;

    internal CardDescriptionBinding(Action<string> display) { this.display = display; }

    internal void Bind(CardData data, BattleManager context, bool enabled, bool applyStatusModifiers = true)
    {
        Disable();
        card = data;
        battle = context;
        this.applyStatusModifiers = applyStatusModifiers;
        if (enabled) Enable();
        else Refresh();
    }

    internal void Enable()
    {
        Disable();
        Player player = battle != null ? battle.GetPlayer() : null;
        Enemy enemy = battle != null ? battle.GetTarget() : null;
        playerStatuses = player != null ? player.StatusEffects : null;
        enemyStatuses = enemy != null ? enemy.StatusEffects : null;
        ammo = player != null ? player.GetAmmoSystem() : null;
        if (playerStatuses != null) playerStatuses.Changed += Refresh;
        if (enemyStatuses != null) enemyStatuses.Changed += Refresh;
        if (ammo != null) ammo.AmmoChanged += OnAmmoChanged;
        Refresh();
    }

    internal void Disable()
    {
        if (playerStatuses != null) playerStatuses.Changed -= Refresh;
        if (enemyStatuses != null) enemyStatuses.Changed -= Refresh;
        if (ammo != null) ammo.AmmoChanged -= OnAmmoChanged;
        playerStatuses = null;
        enemyStatuses = null;
        ammo = null;
    }

    private void OnAmmoChanged(int current, int maximum) { Refresh(); }
    private void Refresh() { display(CardDescriptionBuilder.Build(card, battle, applyStatusModifiers)); }
}
