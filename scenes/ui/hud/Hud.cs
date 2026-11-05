using Godot;

public partial class Hud : CanvasLayer
{
    [Export]
    public ProgressBar hpBar;

    [Export]
    public ProgressBar energyBar;

    [Export]
    public ProgressBar dodgeBar;

    [Export]
    public ProgressBar enemyBar;

    [Export]
    public Label enemyLabel;

    [Export]
    public ProgressBar enemyPressureBar;

    private Player _player;
    private DodgeHandler _dodge;
    private Enemy01 _enemy;

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

        if (_enemy == null || !IsInstanceValid(_enemy))
        {
            _enemy = GetTree().GetFirstNodeInGroup("Enemy") as Enemy01;
        }
        if (enemyBar != null && _enemy != null)
        {
            int maxHp = Mathf.Max(1, _enemy.MaxHP);
            enemyBar.MaxValue = maxHp;
            enemyBar.Value = Mathf.Clamp(_enemy.HP, 0, maxHp);
            if (enemyLabel != null)
            {
                enemyLabel.Text = $"{_enemy.displayName}   {_enemy.HP} / {maxHp}";
            }
            if (enemyPressureBar != null)
            {
                enemyPressureBar.MaxValue = Mathf.Max(1, _enemy.PressureMax);
                enemyPressureBar.Value = _enemy.Pressure;
            }
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
