using Godot;

public partial class Hud : CanvasLayer
{
    [Export]
    public ProgressBar hpBar;

    [Export]
    public ProgressBar energyBar;

    [Export]
    public ProgressBar dodgeBar;

    private Player _player;
    private DodgeHandler _dodge;

    public override void _Ready()
    {
        TryBind();
    }

    public override void _Process(double delta)
    {
        if (_player == null)
        {
            TryBind();
            if (_player == null)
            {
                return;
            }
        }

        hpBar.MaxValue = _player.maxHP;
        hpBar.Value = _player.HP;

        energyBar.MaxValue = _player.maxEnergy;
        energyBar.Value = _player.Energy;

        // 闪避：空 = 冷却中，满 = 可用
        if (dodgeBar != null && _dodge != null)
        {
            int max = Mathf.Max(1, _dodge.cooldownFrames);
            dodgeBar.MaxValue = max;
            dodgeBar.Value = Mathf.Clamp(max - _player.dodgeCooldown, 0, max);
        }
    }

    private void TryBind()
    {
        _player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (_player == null)
        {
            return;
        }
        InputStateMachine machine = _player.GetNodeOrNull<InputStateMachine>("InputStateMachine");
        _dodge = machine?.dodgeHandler;
    }
}
