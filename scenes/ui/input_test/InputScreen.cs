using Godot;
using System;

public partial class InputScreen : CanvasLayer
{
	[Export]
	public InputInfo[] inputInfos;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		for (int i = 0; i < inputInfos.Length; i++)
		{
			if (i < InputManager.Instance.Buffer.Count)
			{
				inputInfos[i].setInputRecord(InputManager.Instance.Buffer[^(i + 1)]);
			}
			else
			{
				break;
			}
		}
	}
}
