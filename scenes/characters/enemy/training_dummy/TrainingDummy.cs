using Godot;
using System;

public partial class TrainingDummy : Node2D, IDamageable
{
	[Export]
	public int maxHP = 100;
	[Export]
	public int maxFlashFrames = 10;
	[Export]
	public Sprite2D sprite;

	private int _hp;
	private int _flashFrames;
	private readonly Color _baseColor = Colors.White;
	private readonly Color _hitColor = new Color(1, 0.2f, 0.2f);
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_hp = maxHP;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_flashFrames > 0)
		{
			_flashFrames--;
			if (_flashFrames == 0)
			{
				sprite.Modulate = _baseColor;
			}
		}
	}

	public void TakeDamage(int damageAmount)
	{
		_hp = Mathf.Max(0, _hp - damageAmount);
		GD.Print($"被攻击： -{damageAmount}, HP: {_hp} / {maxHP}");

		sprite.Modulate = _hitColor;
		_flashFrames = maxFlashFrames;
		if (_hp <= 0)
		{
			GD.Print("被击杀了");
		}
	}
}
