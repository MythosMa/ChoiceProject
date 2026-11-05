using Godot;

/// <summary>
/// 格挡（按下即进入，松开即退出）。
/// 设计取向（M2，方案 1）：**闪避考时机，格挡考判断与资源**。
/// - 减伤 chipRatio（仍吃削血）
/// - 按住持续耗气，气空立即自动破防
/// - 不能移动、不能出招、不回复气
/// </summary>
public partial class BlockHandler : Node, IInputReceiver
{
    /// <summary>格挡时仍承受的伤害比例（0.2 = 吃 20%）。</summary>
    [Export]
    public float chipRatio = 0.2f;

    /// <summary>格挡时每秒消耗的气。</summary>
    [Export]
    public float energyPerSecond = 18f;

    /// <summary>气被打空后进入的破防硬直帧数。</summary>
    [Export]
    public int autoBreakStunFrames = 40;

    private Player _player;
    private InputStateMachine _inputStateMachine;
    private Sprite2D _sprite;
    private float _drainAccum;

    public void Initialize(Player player, InputStateMachine inputStateMachine)
    {
        _player = player;
        _inputStateMachine = inputStateMachine;
        _sprite = player.GetNode<Sprite2D>("Sprite2D");
    }

    public void Enter()
    {
        _drainAccum = 0f;
        _player.blocking = true;
        _player.moveAxis = 0;
        _sprite.Modulate = new Color(0.55f, 0.75f, 1.0f);
    }

    public void Exit()
    {
        _player.blocking = false;
        _sprite.Modulate = Colors.White;
    }

    public void Tick(double delta)
    {
        _player.moveAxis = 0;

        // 松开格挡键 → 回到移动
        InputManager input = InputManager.Instance;
        if ((input.Current & InputButtons.Block) == InputButtons.None)
        {
            _inputStateMachine.TransitionTo(_inputStateMachine.movementHandler);
            return;
        }

        // 持续耗气；气空 → 自动破防
        _drainAccum += (float)delta * energyPerSecond;
        int cost = (int)_drainAccum;
        if (cost > 0)
        {
            _drainAccum -= cost;
            if (_player.TrySpendEnergy(cost))
            {
                return;
            }

            _player.blocking = false;
            GD.Print("[破防] 气被打空，格挡崩溃！");
            _inputStateMachine.ForceGuardBreak(autoBreakStunFrames);
        }
    }
}
