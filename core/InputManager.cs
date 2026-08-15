using Godot;
using System.Collections.Generic;

/// <summary>
/// 输入管理器：每物理帧采集按键位掩码快照，与上一帧比对。
/// - 输入变化时生成新的 InputRecord
/// - 输入未变化时累加最后一条记录的持续帧数
/// 后续将扩展为输入仲裁状态机（Idle/QuickAttack/Combo/Charge）。
/// </summary>
public partial class InputManager : Node
{
	/// <summary>全局单例访问（配合 Autoload 使用），任意脚本可通过 InputManager.Instance 访问。</summary>
	public static InputManager Instance { get; private set; }

	public override void _EnterTree()
	{
		if (Instance != null && Instance != this)
		{
			GD.PushWarning("InputManager: 检测到重复实例，请确保只配置了一个 Autoload。");
		}
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	// 输入动作映射：动作名在 Godot 项目设置的 Input Map 中配置，改键位无需改代码
	private static readonly (string action, InputButtons button)[] ActionBindings =
	{
		("up", InputButtons.Up),
		("down", InputButtons.Down),
		("left", InputButtons.Left),
		("right", InputButtons.Right),
		("light_punch", InputButtons.LP),
		("heavy_punch", InputButtons.HP),
		("light_kick", InputButtons.LK),
		("heavy_kick", InputButtons.HK),
		("jump", InputButtons.Jump),
		("dodge", InputButtons.Dodge),
		("block", InputButtons.Block),
	};

	/// <summary>输入缓冲容量（帧）。60fps下约保存1秒历史。</summary>
	private const int BufferCapacity = 60;

	/// <summary>本帧按键快照。</summary>
	public InputButtons Current { get; private set; } = InputButtons.None;

	/// <summary>上一帧按键快照。</summary>
	public InputButtons Previous { get; private set; } = InputButtons.None;

	/// <summary>本帧派生方向。</summary>
	public Direction CurrentDirection => Tools.GetDirection(Current);

	/// <summary>输入操作记录缓冲（从旧到新）。</summary>
	public IReadOnlyList<InputRecord> Buffer => _buffer;

	private readonly List<InputRecord> _buffer = new();
	private int _nextId;

	public override void _Ready()
	{
		// 初始状态（无按键）也写入一条中立记录，避免缓冲一开始为空；
		// 之后每帧未操作时会持续累加它的持续帧数
		AppendRecord(new InputRecord(_nextId++, 0, Direction.Neutral, AttackType.None));
	}

	public override void _PhysicsProcess(double delta)
	{
		Previous = Current;
		Current = SampleInput();

		if (Tools.InputChanged(Previous, Current))
		{
			AppendRecord(new InputRecord(_nextId++, 0,
				Tools.GetDirection(Current), Tools.GetAttack(Current)));
		}
		else if (_buffer.Count > 0)
		{
			// 输入未变化：累加最后一条记录的持续帧数
			InputRecord last = _buffer[^1];
			_buffer[^1] = new InputRecord(last.Id, last.DurationFrames + 1,
				last.Direction, last.Attack);
		}
	}

	/// <summary>采集本帧所有输入动作，压缩为一个位掩码。</summary>
	private static InputButtons SampleInput()
	{
		InputButtons buttons = InputButtons.None;
		foreach (var (action, button) in ActionBindings)
		{
			if (Input.IsActionPressed(action))
			{
				buttons |= button;
			}
		}
		return buttons;
	}

	private void AppendRecord(InputRecord record)
	{
		_buffer.Add(record);
		if (_buffer.Count > BufferCapacity)
		{
			_buffer.RemoveAt(0);
		}
	}
}
