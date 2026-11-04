using Godot;
using System;

public partial class DodgeHandler : Node, IInputReceiver
{
    [Export]
    public int dodgeFrames = 12;
    [Export]
    public int cooldownFrames = 60;

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
        player.invincible = true;
        player.dodgeCooldown = cooldownFrames;
        player.GetNode<Sprite2D>("Sprite2D").Modulate = new Color(0.2f, 0.5f, 1.0f);
    }
    public void Exit()
    {
        player.invincible = false;
        player.GetNode<Sprite2D>("Sprite2D").Modulate = Colors.White;
    }
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
