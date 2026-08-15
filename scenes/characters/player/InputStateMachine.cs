using Godot;
using System;

public interface IInputReceiver
{
	void Enter();
	void Exit();

	void Tick(double delta);
}

public partial class InputStateMachine : Node
{
	[Export]
	public Player player;

	[Export]
	public MovementHandler movementHandler;

	[Export]
	public CombatHandler combatHandler;

	private IInputReceiver currentInputReceiver;



	public override void _Ready()
	{
		movementHandler.Initialize(player);
		combatHandler.Initialize(player);
		TransitionTo(movementHandler);
	}

	public void Tick(double delta)
	{
		if (player.GetViewport().GuiGetFocusOwner() != null)
		{
			return;
		}
		currentInputReceiver?.Tick(delta);
	}

	public void TransitionTo(IInputReceiver next)
	{
		currentInputReceiver?.Exit();
		currentInputReceiver = next;
		currentInputReceiver.Enter();
	}

}
