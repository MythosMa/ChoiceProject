using Godot;
using System;

public partial class Hitbox : Area2D
{
	[Export]
	private CollisionShape2D _shape;
	private RectangleShape2D _rect;
	private MoveData _move;


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Monitoring = true;
		_shape.Disabled = true;
		_rect = _shape.Shape as RectangleShape2D;

	}

	public void Configure(MoveData move)
	{
		_move = move;
		Position = move.hitboxOffset;
		_rect.Size = move.hitboxSize;
	}

	public void SetActive(bool active)
	{
		_shape.Disabled = !active;
	}

	public void OnAreaEntered(Area2D area)
	{
		if (area is Hurtbox hurtbox && _move != null)
		{
			bool hitLanded = hurtbox.Receiver.TakeDamage(_move.damage);
			if (hitLanded)
			{
				FeedbackSystem.Instance.RequestHitstop(_move.hitstopFrames);
				FeedbackSystem.Instance.RequestShake(_move.hitstopFrames, _move.shakeMagnitude);
			}
		}
	}
}
