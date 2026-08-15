using Godot;
using System;

public partial class InputInfo : PanelContainer
{
	[Export]
	public Label frameLabel;

	[Export]
	public Label directionLabel;

	[Export]
	public Label actionLabel;

	public override void _Ready()
	{
		frameLabel.Visible = false;
		directionLabel.Visible = false;
		actionLabel.Visible = false;
	}

	public void SetInputRecord(InputRecord record)
	{
		frameLabel.Text = (record.DurationFrames > 99 ? 99 : record.DurationFrames).ToString();
		directionLabel.Text = Tools.GetDirectionArrow(record.Direction);
		actionLabel.Text = Tools.GetAttackString(record.Attack);

		frameLabel.Visible = true;
		directionLabel.Visible = true;
		actionLabel.Visible = true;
	}

}