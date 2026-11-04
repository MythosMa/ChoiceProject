using Godot;
using System;

public partial class Hitbox : Area2D
{
	[Export]
	public CollisionShape2D _shape;
	[Export]
	public PackedScene hitSparkScene;
	private RectangleShape2D _rect;
	private MoveData _move;
	private Polygon2D _threat;

	public bool hasConnected { get; private set; }


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Monitoring = true;
		_shape.Disabled = true;
		_rect = _shape.Shape as RectangleShape2D;

		_threat = new Polygon2D();
		_threat.Visible = false;
		_threat.ZIndex = 50;
		AddChild(_threat);

	}

	public void Configure(MoveData move, int facing = 1)
	{
		_move = move;
		hasConnected = false;
		Position = new Vector2(move.hitboxOffset.X * facing, move.hitboxOffset.Y);
		_rect.Size = move.hitboxSize;
		RebuildThreatPolygon();
	}

	private void RebuildThreatPolygon()
	{
		if (_threat == null || _rect == null)
		{
			return;
		}
		Vector2 s = _rect.Size;
		_threat.Position = _shape.Position;
		_threat.Polygon = new Vector2[] {
			new Vector2(-s.X / 2, -s.Y / 2),
			new Vector2(s.X / 2, -s.Y / 2),
			new Vector2(s.X / 2, s.Y / 2),
			new Vector2(-s.X / 2, s.Y / 2)
		};
	}

	public void ShowThreat(Color color)
	{
		if (_threat != null)
		{
			RebuildThreatPolygon();
			_threat.Modulate = color;
			_threat.Visible = true;
		}
	}

	public void HideThreat()
	{
		if (_threat != null)
		{
			_threat.Visible = false;
		}
	}

	public void SetActive(bool active)
	{
		_shape.SetDeferred(CollisionShape2D.PropertyName.Disabled, !active);
	}

	public void OnAreaEntered(Area2D area)
	{
		if (area is Hurtbox hurtbox && _move != null)
		{
			bool hitLanded = hurtbox.Receiver.TakeDamage(_move.damage, _move);
			if (hitLanded)
			{
				hasConnected = true;
				FeedbackSystem.Instance.RequestHitstop(_move.hitstopFrames);
				FeedbackSystem.Instance.RequestShake(_move.hitstopFrames, _move.shakeMagnitude);
				SpawnHitSpark(hurtbox.GlobalPosition);
			}
		}
	}

	private void SpawnHitSpark(Vector2 position)
	{
		if (hitSparkScene != null)
		{
			HitSpark hitSpark = hitSparkScene.Instantiate() as HitSpark;
			GetTree().CurrentScene.AddChild(hitSpark);
			hitSpark.GlobalPosition = position;
			hitSpark.Play();
		}
	}
}
