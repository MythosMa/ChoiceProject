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
		combatHandler.Initialize(player, this);
		TransitionTo(movementHandler);
	}

	public void Tick(double delta)
	{
		if (player.GetViewport().GuiGetFocusOwner() != null)
		{
			return;
		}

		InputManager input = InputManager.Instance;
		InputButtons buttons = Tools.GetPressed(input.Previous, input.Current);

		if ((buttons & InputButtons.Dodge) != InputButtons.None && currentInputReceiver != movementHandler)
		{
			TransitionTo(movementHandler);
			return;
		}

		if (currentInputReceiver == movementHandler && (buttons & InputButtons.AttackMask) != InputButtons.None)
		{
			TransitionTo(combatHandler);
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
