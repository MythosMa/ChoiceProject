using System;

/// <summary>
/// 攻击动作类型（位掩码）：多个攻击键可同时按下（如 LP+HP 组合键），
/// 因此每个动作占一个二进制位，支持并存表达。
/// 属于数据结构，供战斗系统与操作记录使用。
/// </summary>
[Flags]
public enum AttackType
{
    None = 0,
    LP = 1 << 0,
    HP = 1 << 1,
    LK = 1 << 2,
    HK = 1 << 3
}
