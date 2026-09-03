using Godot;
using System;

public partial class CombatHandler : Node, IInputReceiver
{
	private enum Phase { Listening, Showing }

	[Export] public int checkFrames = 30; // 每环节监听窗口
	[Export] public int showFrames = 12;  // 每击展示帧数（占位，将来=招式帧数据）
	[Export] public int chainCap = 3;     // 占位最大链长

	private Phase _phase;
	private int _chainHits;
	private int _counter;

	// Showing 期预输入暂存
	private bool _pendingAttack;
	private Direction _pendingDirection = Direction.Neutral;

	public Player player { get; set; }
	public InputStateMachine inputStateMachine { get; set; }

	public void Initialize(Player node, InputStateMachine machine)
	{
		player = node;
		inputStateMachine = machine;
	}

	public void Enter()
	{
		// 开窗按下 = 第 1 击，立刻出招
		_chainHits = 1;
		_phase = Phase.Showing;
		_counter = showFrames;
		_pendingAttack = false;
		_pendingDirection = Direction.Neutral;

		// 同帧方向边缘 = 首击路线（快速招式语义；按住不放的方向不算）
		InputManager input = InputManager.Instance;
		InputButtons pressed = Tools.GetPressed(input.Previous, input.Current);
		if ((pressed & InputButtons.DirectionMask) != InputButtons.None)
		{
			Direction dir = Tools.GetDirection(input.Current);
			if (dir != Direction.Neutral)
			{
				GD.Print($"首击路线：{Tools.GetDirectionArrow(dir)}");
			}
		}

		GD.Print($"连段第 {_chainHits} 击");
		SetVisual(Colors.Red);
	}

	public void Exit()
	{
		SetVisual(Colors.White);
	}

	public void Tick(double delta)
	{
		InputManager input = InputManager.Instance;
		InputButtons pressed = Tools.GetPressed(input.Previous, input.Current);

		if (_phase == Phase.Showing)
		{
			// 展示期：边缘输入暂存，方向必须存"按下那一刻的值"
			if ((pressed & InputButtons.AttackMask) != InputButtons.None)
			{
				_pendingAttack = true;
			}
			if ((pressed & InputButtons.DirectionMask) != InputButtons.None)
			{
				Direction dir = Tools.GetDirection(input.Current);
				if (dir != Direction.Neutral)
				{
					_pendingDirection = dir;
				}
			}
		}
		else // Listening
		{
			// 攻击：链条推进（pending 与当帧边缘合并，输入优先于超时）
			if (_pendingAttack || (pressed & InputButtons.AttackMask) != InputButtons.None)
			{
				_pendingAttack = false;
				AdvanceChain();
				return; // 出招帧不参与倒计时
			}

			// 方向：路线标记，不改相位、不重置倒计时
			Direction tag = _pendingDirection;
			if ((pressed & InputButtons.DirectionMask) != InputButtons.None)
			{
				Direction live = Tools.GetDirection(input.Current);
				if (live != Direction.Neutral)
				{
					tag = live;
				}
			}
			_pendingDirection = Direction.Neutral;
			if (tag != Direction.Neutral)
			{
				GD.Print($"路线标记：{Tools.GetDirectionArrow(tag)}");
			}
		}

		// 共用倒计时与相位流转
		_counter--;
		if (_counter > 0)
		{
			return;
		}

		if (_phase == Phase.Showing && _chainHits < chainCap)
		{
			// 展示结束、链未满 → 重开择窗口
			_phase = Phase.Listening;
			_counter = checkFrames;
			SetVisual(Colors.Yellow);
		}
		else
		{
			// 监听超时 或 链已满 → 回移动
			inputStateMachine.TransitionTo(inputStateMachine.movementHandler);
		}
	}

	private void AdvanceChain()
	{
		_chainHits++;
		_phase = Phase.Showing;
		_counter = showFrames;
		GD.Print($"连段第 {_chainHits} 击");
		SetVisual(Colors.Red);
	}

	private void SetVisual(Color color)
	{
		player.GetNode<Sprite2D>("Sprite2D").Modulate = color;
	}
}