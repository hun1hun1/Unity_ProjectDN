using System;

[Flags]
public enum EnemyActionKinds
{
    None = 0,
    Attack = 1,
    Block = 2,
    Debuff = 4
}
