using Godot;
using System;

public partial class Hud : CanvasLayer
{
    [Export]
    public ProgressBar hpBar;

    [Export]
    public ProgressBar energyBar;

    private Player _player;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("Player") as Player;
    }

    public override void _Process(double delta)
    {
        if (_player == null)
        {
            _player = GetTree().GetFirstNodeInGroup("Player") as Player;
            if (_player == null)
            {
                return;
            }
        }
        hpBar.MaxValue = _player.maxHP;
        hpBar.Value = _player.HP;
        energyBar.MaxValue = _player.maxEnergy;
        energyBar.Value = _player.Energy;
    }
}
