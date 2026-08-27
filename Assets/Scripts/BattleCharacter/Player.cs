using UnityEngine;

public class Player : BattleCharacter
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Die()
    {
        Debug.Log("플레이어가 사망했습니다.");

        // 게임 오버 처리
    }
}
