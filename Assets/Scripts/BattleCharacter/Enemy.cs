using UnityEngine;

public class Enemy : BattleCharacter
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Die()
    {
        Debug.Log($"{gameObject.name}이(가) 사망했습니다.");

        // 적 사망 처리
    }

    public void EnemyTurn()
    {
        // 적의 행동
    }
}