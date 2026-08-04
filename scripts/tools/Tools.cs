using Godot;
using System;
using System.Collections.Generic;

public static partial class Tools
{
	private static readonly Dictionary<Direction, string> DirectionArrows = new()
	{
		{ Direction.Neutral, "N" },
		{ Direction.Up, "↑" },
		{ Direction.Down, "↓" },
		{ Direction.Left, "←" },
		{ Direction.Right, "→" },
		{ Direction.UpLeft, "↖" },
		{ Direction.UpRight, "↗" },
		{ Direction.DownLeft, "↙" },
		{ Direction.DownRight, "↘" }
	};

	public static string GetDirectionArrow(Direction direction)
	{
		return DirectionArrows.TryGetValue(direction, out var arrow) ? arrow : "";
	}

	public static string GetAttackString(AttackType attack)
	{
		if (attack == AttackType.None)
		{
			return string.Empty;
		}

		var parts = new List<string>(4);
		if (attack.HasFlag(AttackType.LP)) parts.Add("LP");
		if (attack.HasFlag(AttackType.HP)) parts.Add("HP");
		if (attack.HasFlag(AttackType.LK)) parts.Add("LK");
		if (attack.HasFlag(AttackType.HK)) parts.Add("HK");
		return string.Join("|", parts);
	}

	public static Direction GetDirection(bool up, bool down, bool left, bool right)
	{
		Direction dir = Direction.Neutral;

		int vertical = 0;
		if (up && !down)
		{
			vertical = -1;
		}
		else if (!up && down)
		{
			vertical = 1;
		}
		int horizontal = 0;
		if (left && !right)
		{
			horizontal = -1;
		}
		else if (!left && right)
		{
			horizontal = 1;
		}

		dir = (vertical, horizontal) switch
		{
			(0, 0) => Direction.Neutral,
			(-1, 0) => Direction.Up,
			(1, 0) => Direction.Down,
			(0, -1) => Direction.Left,
			(0, 1) => Direction.Right,
			(-1, -1) => Direction.UpLeft,
			(-1, 1) => Direction.UpRight,
			(1, -1) => Direction.DownLeft,
			(1, 1) => Direction.DownRight,
			_ => Direction.Neutral
		};
		return dir;
	}

	/// <summary>从按键位掩码中提取方向。</summary>
	public static Direction GetDirection(InputButtons buttons)
	{
		bool up = buttons.HasFlag(InputButtons.Up);
		bool down = buttons.HasFlag(InputButtons.Down);
		bool left = buttons.HasFlag(InputButtons.Left);
		bool right = buttons.HasFlag(InputButtons.Right);
		return GetDirection(up, down, left, right);
	}

	/// <summary>从按键位掩码中提取攻击动作（多个攻击键同时按下时全部保留）。
	/// 依赖布局约定：InputButtons 攻击键占 bit 4~7，AttackType 占 bit 0~3，故右移 4 位即得。</summary>
	public static AttackType GetAttack(InputButtons buttons)
	{
		return (AttackType)(((int)buttons & (int)InputButtons.AttackMask) >> 4);
	}

	/// <summary>输入状态是否发生变化。</summary>
	public static bool InputChanged(InputButtons previous, InputButtons current)
	{
		return previous != current;
	}

	/// <summary>本帧刚按下的键（上一帧未按且本帧按下）。</summary>
	public static InputButtons GetPressed(InputButtons previous, InputButtons current)
	{
		return current & ~previous;
	}

	/// <summary>本帧刚松开的键（上一帧按下且本帧未按）。</summary>
	public static InputButtons GetReleased(InputButtons previous, InputButtons current)
	{
		return previous & ~current;
	}
}
