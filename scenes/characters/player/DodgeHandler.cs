using Godot;
using System;

public partial class DodgeHandler : Node, IInputReceiver
{
    [Export]
    public int dodgeFrames = 12;
    /// <summary>冲刺速度（px/s）。12 帧 × 500 = 100px，够穿过 32px 的敌人身体还有余量。</summary>
    [Export]
    public float dodgeSpeed = 500f;
    [Export]
    public int cooldownFrames = 60;

    private int _frame;
    /// <summary>这次闪避的方向：取**按下瞬间的输入方向**，不取上次朝向。</summary>
    private int _dir;
    private uint _savedMask;
    private uint _savedLayer;

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

        // 方向以"按下闪避时正在按的方向"为准。
        // 用 player.facing 会在"先转身后闪避"的同一帧里取到旧朝向（实测会反向冲）。
        InputButtons current = InputManager.Instance.Current;
        if ((current & InputButtons.Left) != InputButtons.None)
        {
            _dir = -1;
        }
        else if ((current & InputButtons.Right) != InputButtons.None)
        {
            _dir = 1;
        }
        else
        {
            _dir = player.facing;
        }
        player.facing = _dir;
        player.invincible = true;
        player.dodgeCooldown = cooldownFrames;

        // 闪避穿人必须是**双向**的：
        //   1) 玩家不检测敌人 —— CollisionMask 去掉敌人身体层
        //   2) 玩家也不在敌人检测的层上 —— CollisionLayer 清零
        // 只做 1) 的话，敌人的 mask 仍然会撞到玩家，反过来把玩家顶住（实测会被顶回 5px）。
        _savedMask = player.CollisionMask;
        _savedLayer = player.CollisionLayer;
        player.CollisionMask = _savedMask & ~Tools.EnemyBodyLayer;
        player.CollisionLayer = 0;

        player.GetNode<Sprite2D>("Sprite2D").Modulate = new Color(0.2f, 0.5f, 1.0f);
    }
    public void Exit()
    {
        player.CollisionMask = _savedMask;
        player.CollisionLayer = _savedLayer;
        player.invincible = false;
        player.GetNode<Sprite2D>("Sprite2D").Modulate = Colors.White;
    }
    public void Tick(double delta)
    {
        _frame++;
        // 冲刺是瞬时速度，绕过移动加减速
        player.externalVelocityX = _dir * dodgeSpeed;
        if (_frame >= dodgeFrames)
        {
            inputStateMachine.TransitionTo(inputStateMachine.movementHandler);
        }
    }
}
