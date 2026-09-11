using Godot;
using System;
using System.Net.Sockets;

public partial class CombatHandler : Node, IInputReceiver
{
	private enum Segment { Startup, Active, Recovery }

	[Export]
	public MoveData openerMove;

	[Export]
	public MoveData moveUp;

	[Export]
	public MoveData moveDown;

	[Export]
	public MoveData moveLeft;

	[Export]
	public MoveData moveRight;

	[Export] public int chainCap = 3;     // 占位最大链长

	private MoveData _move;
	private int _frameInMove;
	private int _chainHits;
	private Hitbox _hitbox;

	// Showing 期预输入暂存
	private int _moveStartInputId = -1;

	public Player player { get; set; }
	public InputStateMachine inputStateMachine { get; set; }

	public void Initialize(Player node, InputStateMachine machine)
	{
		player = node;
		inputStateMachine = machine;
		_hitbox = player.GetNode<Hitbox>("Hitbox");
	}

	public void Enter()
	{
		_chainHits = 1;
		Direction dir = Tools.GetDirection(InputManager.Instance.Current);
		StartMove(SelectMove(dir));

		// InputManager input = InputManager.Instance;
		// InputButtons pressed = Tools.GetPressed(input.Previous, input.Current);
		// if ((pressed & InputButtons.DirectionMask) != InputButtons.None)
		// {
		// 	Direction dir = Tools.GetDirection(input.Current);
		// 	if (dir != Direction.Neutral)
		// 	{
		// 		GD.Print($"首击路线：{Tools.GetDirectionArrow(dir)}");
		// 	}
		// }
	}

	public void Exit()
	{
		_hitbox.SetActive(false);
		SetVisual(Colors.White);
	}

	public void Tick(double delta)
	{
		TickPlaying(delta);
	}


	private void StartMove(MoveData move)
	{
		if (move == null)
		{
			GD.Print("招式配置错误，无法出招");
			return;
		}
		_move = move;
		_frameInMove = 0;
		_hitbox.Configure(move);


		GD.Print($"连段第 {_chainHits} 击: {move.moveName}");
		ApplySegmentVisual();
		SyncHitbox();

		_moveStartInputId = LastInputId();
	}

	private int LastInputId()
	{
		var buffer = InputManager.Instance.Buffer;
		return buffer.Count > 0 ? buffer[^1].Id : -1;
	}

	private void TickPlaying(double delta)
	{
		if (_move != null && !_move.lockMovement)
		{
			// todo: 动作演出时不锁定移动的处理逻辑
		}

		_frameInMove++;
		if (_frameInMove >= _move.TotalFrames)
		{
			_hitbox.SetActive(false);
			MoveData next = ResolveNext();
			if (next != null && _chainHits < chainCap)
			{
				_chainHits++;
				StartMove(next);
			}
			else
			{
				inputStateMachine.TransitionTo(inputStateMachine.movementHandler);
			}
			return;
		}
		SyncHitbox();
		ApplySegmentVisual();
	}

	private void SyncHitbox()
	{
		_hitbox.SetActive(CurrentSegment() == Segment.Active);
	}

	private Segment CurrentSegment()
	{
		int frame = _frameInMove;
		if (frame < _move.startupFrames)
		{
			return Segment.Startup;
		}
		if (frame < _move.startupFrames + _move.activeFrames)
		{
			return Segment.Active;
		}

		return Segment.Recovery;
	}

	private void ApplySegmentVisual()
	{
		switch (CurrentSegment())
		{
			case Segment.Startup:
				SetVisual(new Color(0.4f, 0.0f, 0.0f));
				break;
			case Segment.Active:
				SetVisual(new Color(1.0f, 0.5f, 0.0f));
				break;
			case Segment.Recovery:
				SetVisual(new Color(0.5f, 0.5f, 0.5f));
				break;
		}
	}

	private void SetVisual(Color color)
	{
		player.GetNode<Sprite2D>("Sprite2D").Modulate = color;
	}

	private MoveData SelectMove(Direction dir)
	{
		MoveData m = dir switch
		{
			Direction.Up => moveUp,
			Direction.Down => moveDown,
			Direction.Left => moveLeft,
			Direction.Right => moveRight,
			_ => openerMove,
		};

		return m ?? openerMove;
	}

	private MoveData ResolveNext()
	{
		var buffer = InputManager.Instance.Buffer;
		for (int i = buffer.Count - 1; i >= 0; i--)
		{
			var rec = buffer[i];
			if (rec.Id <= _moveStartInputId)
			{
				break;
			}
			if (rec.Attack == AttackType.None)
			{
				continue;
			}
			var prev = i > 0 ? buffer[i - 1].Attack : AttackType.None;
			if ((prev & rec.Attack) != rec.Attack)
			{
				return SelectMove(rec.Direction);
			}
		}
		return null;
	}

}
