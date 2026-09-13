using Godot;
using System;

public partial class Player : CharacterBody2D, IDamageable
{

	[Export]
	public InputStateMachine inputStateMachine;

	[Export]
	public int maxHP = 100;
	private int _hp;
	public const float Speed = 300.0f;
	public const float JumpVelocity = -400.0f;

	public int facing = 1;
	public float moveAxis;
	private bool jumpRequested = false;
	public void RequestJump() => jumpRequested = true;

	public override void _Ready()
	{
		_hp = maxHP;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (inputStateMachine.TryDodgeInterrupt())
		{
			FeedbackSystem.Instance.CancelHitstop();
		}
		else if (FeedbackSystem.Instance.ConsumeHitstop())
		{
			return;
		}
		moveAxis = 0;
		jumpRequested = false;
		inputStateMachine.Tick(delta);

		if (moveAxis != 0)
		{
			facing = moveAxis > 0 ? 1 : -1;
		}

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

	public void TakeDamage(int damageAmount)
	{
		_hp = Mathf.Max(0, _hp - damageAmount);
		GD.Print($"被攻击： -{damageAmount}, HP: {_hp} / {maxHP}");
		if (_hp <= 0)
		{
			GD.Print("Game Over!");
		}
	}
}
