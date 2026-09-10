using Godot;
using System;

public partial class Hurtbox : Area2D
{
	public IDamageable Receiver { get; private set; }
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Monitoring = false;
		Monitorable = true;
		Receiver = GetParent() as IDamageable;
	}
}
