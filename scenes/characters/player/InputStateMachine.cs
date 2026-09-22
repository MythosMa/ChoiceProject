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

	[Export]
	public DodgeHandler dodgeHandler;

	[Export]
	public HitstunHandler hitstunHandler;

	private IInputReceiver currentInputReceiver;



	public override void _Ready()
	{
		movementHandler.Initialize(player);
		combatHandler.Initialize(player, this);
		dodgeHandler.Initialize(player, this);
		hitstunHandler.Initialize(player, this);

		TransitionTo(movementHandler);
	}

	public bool TryDodgeInterrupt()
	{
		if (player.GetViewport().GuiGetFocusOwner() != null)
		{
			return false;
		}
		InputManager input = InputManager.Instance;
		InputButtons buttons = Tools.GetPressed(input.Previous, input.Current);
		if ((buttons & InputButtons.Dodge) == InputButtons.None)
		{
			return false;
		}
		if (currentInputReceiver == dodgeHandler || currentInputReceiver == hitstunHandler)
		{
			return false;
		}
		if (player.dodgeCooldown > 0)
		{
			return false;
		}
		TransitionTo(dodgeHandler);
		return true;
	}

	public void ForceHitstun()
	{
		TransitionTo(hitstunHandler);
	}


	public void Tick(double delta)
	{
		if (player.GetViewport().GuiGetFocusOwner() != null)
		{
			return;
		}

		InputManager input = InputManager.Instance;
		InputButtons buttons = Tools.GetPressed(input.Previous, input.Current);

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
		GD.Print($"Transitioned to {next.GetType().Name}");
	}

}
