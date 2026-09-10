using Godot;
using System;

public partial class MovementHandler : Node, IInputReceiver
{
	public Player player { get; set; }

	public void Initialize(Player node)
	{
		player = node;
	}

	public void Enter()
	{
	}

	public void Exit()
	{
	}

	public void Tick(double delta)
	{
		InputManager input = InputManager.Instance;

		float axis = 0;
		if (input.Current.HasFlag(InputButtons.Left))
		{
			axis -= 1;
		}
		if (input.Current.HasFlag(InputButtons.Right))
		{
			axis += 1;
		}
		player.moveAxis = axis;

		bool jumpJustPressed = Tools.GetPressed(input.Previous, input.Current).HasFlag(InputButtons.Jump);
		if (jumpJustPressed && player.IsOnFloor())
		{
			player.RequestJump();
		}
	}
}
