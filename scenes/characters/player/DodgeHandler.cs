using Godot;
using System;

public partial class DodgeHandler : Node, IInputReceiver
{
    [Export]
    public int dodgeFrames = 12;
    [Export]
    public int cooldownFrames = 60;

    private int _frame;
    private uint _savedMask;

    public Player player { get; set; }
    public InputStateMachine inputStateMachine { get; set; }

    public void Initialize(Player player, InputStateMachine inputStateMachine)
    {
        this.player = player;
        this.inputStateMachine = inputStateMachine;
    }

    public void Enter()
    {
        _frame = 0;
        player.invincible = true;
        player.dodgeCooldown = cooldownFrames;

        // 闪避穿人：临时去掉"敌人身体"层的检测（地面/墙壁仍在世界层，照常碰撞）。
        // 只改 CollisionMask，不动 CollisionShape，所以不需要 deferred，也不会触发物理 flush 限制。
        _savedMask = player.CollisionMask;
        player.CollisionMask = _savedMask & ~Tools.EnemyBodyLayer;

        player.GetNode<Sprite2D>("Sprite2D").Modulate = new Color(0.2f, 0.5f, 1.0f);
    }
    public void Exit()
    {
        player.CollisionMask = _savedMask;
        player.invincible = false;
        player.GetNode<Sprite2D>("Sprite2D").Modulate = Colors.White;
    }
    public void Tick(double delta)
    {
        _frame++;
        // 冲刺是瞬时速度，绕过移动加减速
        player.externalVelocityX = player.facing * Player.Speed;
        if (_frame >= dodgeFrames)
        {
            inputStateMachine.TransitionTo(inputStateMachine.movementHandler);
        }
    }
}
