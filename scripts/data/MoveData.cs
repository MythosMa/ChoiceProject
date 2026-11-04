using Godot;
using System;

[GlobalClass]
public partial class MoveData : Resource
{
    [ExportGroup("标识")]
    /// <summary>稳定标识：AI 读招、招式匹配、存档等一切"认招"的地方都用它。
    /// 改它等于换一个招式身份；换资源文件而不改它，逻辑不受影响。</summary>
    [Export]
    public string moveId = "unnamed";

    /// <summary>显示名：只给人看（日志 / HUD）。随便改，不影响任何逻辑。</summary>
    [Export]
    public string displayName = "未命名";

    /// <summary>取标识的统一入口：没配就回落到 "unnamed"，避免 null 传播。</summary>
    public string Id => string.IsNullOrEmpty(moveId) ? "unnamed" : moveId;

    [ExportGroup("资源消耗")]
    [Export]
    public int energyCost = 20; // 优先级

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

    [ExportGroup("移动锁定")]
    [Export]
    public bool lockMovement = true; // 是否移动锁定
    [Export]
    public bool dodgeCancellable = true;


    [ExportGroup("可取消性")]
    [Export]
    public bool canBeCancelled = false; // 是否可取消
    [Export]
    public int cancelWindowStart = 0; // 取消窗口开始帧数
    [Export]
    public int cancelWindowEnd = 0; // 取消窗口结束帧数


    public int TotalFrames => startupFrames + activeFrames + recoveryFrames;
}
