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

	[Export]
	public BlockHandler blockHandler;

	private IInputReceiver currentInputReceiver;



	public override void _Ready()
	{
		movementHandler.Initialize(player);
		combatHandler.Initialize(player, this);
		dodgeHandler.Initialize(player, this);
		hitstunHandler.Initialize(player, this);
		blockHandler.Initialize(player, this);

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
		if (currentInputReceiver == combatHandler && !combatHandler.CurrentMoveDodgeCancellable)
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

	/// <summary>破防硬直：进入指定帧数的硬直。</summary>
	public void ForceGuardBreak(int frames)
	{
		hitstunHandler.SetDuration(frames);
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

		// 格挡：按住进入、松开退出（只在移动态可进入）
		if (currentInputReceiver == movementHandler && (buttons & InputButtons.Block) != InputButtons.None)
		{
			TransitionTo(blockHandler);
			return;
		}

		currentInputReceiver?.Tick(delta);
	}

	public void TransitionTo(IInputReceiver next)
	{
		currentInputReceiver?.Exit();
		currentInputReceiver = next;
		currentInputReceiver.Enter();
		DebugLog.V($"Transitioned to {next.GetType().Name}");
	}

}
