using Godot;
using System;
using System.Diagnostics.CodeAnalysis;

public partial class HitstunHandler : Node, IInputReceiver
{
    [Export]
    public int hitstunFrames = 20;

    private int _frame;
    private int _duration;
    private Player _player;
    private InputStateMachine _inputStateMachine;
    private Sprite2D _sprite;

    public void Initialize(Player player, InputStateMachine inputStateMachine)
    {
        _player = player;
        _inputStateMachine = inputStateMachine;
        _sprite = player.GetNode<Sprite2D>("Sprite2D");
    }

    /// <summary>强制一段指定时长的硬直（破防用）。</summary>
    public void SetDuration(int frames)
    {
        _duration = Mathf.Max(hitstunFrames, frames);
    }

    public void Enter()
    {
        _frame = 0;
        if (_duration <= 0)
        {
            _duration = hitstunFrames;
        }
        _player.moveAxis = 0;
        _sprite.Modulate = new Color(0.7f, 0.2f, 1.0f);
    }
    public void Exit()
    {
        _sprite.Modulate = Colors.White;
    }
    public void Tick(double delta)
    {
        _frame++;
        if (_frame >= _duration)
        {
            _duration = 0;
            _inputStateMachine.TransitionTo(_inputStateMachine.movementHandler);
        }
    }
}
