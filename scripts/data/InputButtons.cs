using System;

/// <summary>
/// 输入按键位掩码：每个按键占一个二进制位，单帧输入状态压缩为一个整数。
/// 用法：
/// - 状态变化检测：prev != current
/// - 刚按下的键：current &amp; ~prev
/// - 刚松开的键：prev &amp; ~current
/// </summary>
[Flags]
public enum InputButtons
{
    None = 0,

    // 方向键
    Up = 1 << 0,
    Down = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,

    // 攻击键
    LP = 1 << 4,
    HP = 1 << 5,
    LK = 1 << 6,
    HK = 1 << 7,

    // 系统键（预留：跳跃/格挡）
    Jump = 1 << 8,
    Block = 1 << 9,

    /// <summary>所有方向键的掩码。</summary>
    DirectionMask = Up | Down | Left | Right,

    /// <summary>所有攻击键的掩码。</summary>
    AttackMask = LP | HP | LK | HK
}
