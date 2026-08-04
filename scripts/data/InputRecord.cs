using System;

/// <summary>
/// 单条操作记录：记录一次输入操作的编号、持续帧数、方向与动作。
/// 使用只读结构体（值类型），适合放入输入缓冲队列做搓招匹配。
/// </summary>
public readonly struct InputRecord
{
    /// <summary>操作编号（自增，用于排序/去重）。</summary>
    public int Id { get; }

    /// <summary>当前操作持续帧数。</summary>
    public int DurationFrames { get; }

    /// <summary>输入方向。</summary>
    public Direction Direction { get; }

    /// <summary>输入动作。</summary>
    public AttackType Attack { get; }

    public InputRecord(int id, int durationFrames, Direction direction, AttackType attack)
    {
        Id = id;
        DurationFrames = durationFrames;
        Direction = direction;
        Attack = attack;
    }

    public override string ToString()
    {
        string arrow = Tools.GetDirectionArrow(Direction);
        string attackStr = Tools.GetAttackString(Attack);
        string attack = attackStr == string.Empty ? "-" : attackStr;
        return $"#{Id} {arrow}{attack} ({DurationFrames}f)";
    }
}
