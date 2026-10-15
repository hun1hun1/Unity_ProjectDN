using UnityEngine;

public abstract class BattleCharacter : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] protected int maxHP = 80;
    [SerializeField] protected int currentHP;

    [Header("Block")]
    [SerializeField] protected int block;

    [Header("UI")]
    [SerializeField] private CharacterInfoUI characterInfoUI;

    public StatusEffectSystem StatusEffects { get; } = new StatusEffectSystem();
    public ContinuousCardSystem ContinuousCards { get; } = new ContinuousCardSystem();

    protected virtual void Awake()
    {
        currentHP = maxHP;
        if (characterInfoUI != null) characterInfoUI.BindStatusEffects(StatusEffects, ContinuousCards);

        UpdateUI();
    }

    public virtual void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public virtual void TakeDamage(int damage, BattleCharacter attacker)
    {
        damage = DamageCalculator.Calculate(damage, attacker, this);
        // 방어도가 먼저 피해를 흡수
        int remainingDamage = damage - block;

        if (remainingDamage < 0)
            remainingDamage = 0;

        block -= damage;

        if (block < 0)
            block = 0;

        currentHP -= remainingDamage;

        if (currentHP < 0)
            currentHP = 0;

        Debug.Log($"{gameObject.name} 피해 {damage} / HP {currentHP} / Block {block}");

        UpdateUI();

        if (currentHP <= 0)
        {
            Die();
        }
    }

    public virtual void AddBlock(int amount)
    {
        amount = BlockCalculator.Calculate(amount, this);
        block += amount;

        Debug.Log($"{gameObject.name} 방어도 +{amount} / 현재 방어도 {block}");

        UpdateUI();
    }

    public virtual void ResetBlock()
    {
        block = 0;

        Debug.Log($"{gameObject.name} 방어도 초기화 / 현재 방어도 {block}");

        UpdateUI();
    }

    public virtual void Heal(int amount)
    {
        currentHP += amount;
        if (currentHP > maxHP) currentHP = maxHP;

        Debug.Log($"{gameObject.name} 회복 +{amount} / 현재 HP {currentHP}");

        UpdateUI();
    }

    protected void UpdateUI()
    {
        if (characterInfoUI == null) return;

        characterInfoUI.SetHP(currentHP, maxHP);
        characterInfoUI.SetBlock(block);
    }

    protected virtual void Die()
    {
        Debug.Log($"{gameObject.name} 사망");
    }

    public int GetCurrentHP()
    {
        return currentHP;
    }

    public int GetMaxHP()
    {
        return maxHP;
    }

    public int GetBlock()
    {
        return block;
    }
}
