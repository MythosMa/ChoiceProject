using Godot;
using System;
using System.Diagnostics.CodeAnalysis;

public partial class HitstunHandler : Node, IInputReceiver
{
    [Export]
    public int hitstunFrames = 20;

    private int _frame;
    private Player _player;
    private InputStateMachine _inputStateMachine;
    private Sprite2D _sprite;

    public void Initialize(Player player, InputStateMachine inputStateMachine)
    {
        _player = player;
        _inputStateMachine = inputStateMachine;
        _sprite = player.GetNode<Sprite2D>("Sprite2D");
    }

    public void Enter()
    {
        _frame = 0;
        _player.moveAxis = 0;
        _sprite.Modulate = new Color(1.0f, 0.2f, 0.2f);
    }
    public void Exit()
    {
        _sprite.Modulate = Colors.White;
    }
    public void Tick(double delta)
    {
        _frame++;
        if (_frame >= hitstunFrames)
        {
            _inputStateMachine.TransitionTo(_inputStateMachine.movementHandler);
        }
    }
}
