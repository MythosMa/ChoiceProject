using Godot;
using System;

public partial class Player : CharacterBody2D
{

	[Export]
	public InputStateMachine inputStateMachine;

	public const float Speed = 300.0f;
	public const float JumpVelocity = -400.0f;

	public float moveAxis;
	private bool jumpRequested = false;
	public void RequestJump() => jumpRequested = true;

	public override void _PhysicsProcess(double delta)
	{
		if (FeedbackSystem.Instance.ConsumeHitstop())
		{
			return;
		}
		moveAxis = 0;
		jumpRequested = false;

		inputStateMachine.Tick(delta);

		Vector2 velocity = Velocity;
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		if (jumpRequested && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}

		velocity.X = moveAxis != 0 ? moveAxis * Speed : Mathf.MoveToward(Velocity.X, 0, Speed);

		Velocity = velocity;
		MoveAndSlide();
	}
}
