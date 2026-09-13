using Godot;
using System;

public partial class DodgeHandler : Node, IInputReceiver
{
    [Export]
    public int dodgeFrames = 12;

    private int _frame;
    private Direction _dodgeDirection;

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
    }
    public void Exit() { }
    public void Tick(double delta)
    {
        _frame++;
        player.moveAxis = player.facing;
        if (_frame >= dodgeFrames)
        {
            inputStateMachine.TransitionTo(inputStateMachine.movementHandler);
        }
    }
}
