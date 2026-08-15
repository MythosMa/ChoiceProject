using Godot;
using System;

public partial class CombatHandler : Node, IInputReceiver
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
		GD.Print("CombatHandler Tick");
	}
}
