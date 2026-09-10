using Godot;
using System;

[GlobalClass]
public partial class MoveData : Resource
{
    [Export]
    public string moveName = "default_data";

    [ExportGroup("帧数据")]
    [Export]
    public int startupFrames = 4;    // 前摇帧数
    [Export]
    public int activeFrames = 4;     // 活跃帧数
    [Export]
    public int recoveryFrames = 10;   // 后摇帧数

    [ExportGroup("命中效果")]
    [Export]
    public int damage = 10; // 命中伤害
    [Export]
    public int hitstopFrames = 6; // 命中僵直帧数
    [Export]
    public float shakeMagnitude = 8f; // 命中震屏强度
    [Export]
    public Vector2 knockback = Vector2.Zero; // 击退力度

    [ExportGroup("攻击判定")]
    [Export]
    public Vector2 hitboxOffset = Vector2.Zero; // Hitbox 偏移
    [Export]
    public Vector2 hitboxSize = Vector2.Zero; // Hitbox 大小

    [ExportGroup("可取消性")]
    [Export]
    public bool canBeCancelled = false; // 是否可取消
    [Export]
    public int cancelWindowStart = 0; // 取消窗口开始帧数
    [Export]
    public int cancelWindowEnd = 0; // 取消窗口结束帧数

    public int TotalFrames => startupFrames + activeFrames + recoveryFrames;
}
