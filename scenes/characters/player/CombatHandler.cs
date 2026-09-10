using Godot;
using System;
using System.Net.Sockets;

public partial class CombatHandler : Node, IInputReceiver
{
	private enum Phase { Listening, Playing }
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

	[Export] public int checkFrames = 30; // 每环节监听窗口
	[Export] public int chainCap = 3;     // 占位最大链长

	private Phase _phase;
	private MoveData _move;
	private int _frameInMove;
	private int _chainHits;
	private int _counter;
	private Hitbox _hitbox;

	// Showing 期预输入暂存
	private bool _pendingAttack;
	private Direction _pendingDirection = Direction.Neutral;

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
		_pendingAttack = false;
		_pendingDirection = Direction.Neutral;
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
		if (_phase == Phase.Playing)
		{
			TickPlaying(delta);
		}
		else
		{
			TickListening(delta);
		}
	}

	private void AdvanceChain()
	{
		_chainHits++;
		Direction dir = ResolveAttackDirection();
		StartMove(SelectMove(dir));
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
		_phase = Phase.Playing;
		_pendingAttack = false;
		_pendingDirection = Direction.Neutral;

		_hitbox.Configure(move);


		GD.Print($"连段第 {_chainHits} 击: {move.moveName}");
		ApplySegmentVisual();
		SyncHitbox();
	}

	private void TickPlaying(double delta)
	{
		CapturePending();

		_frameInMove++;
		if (_frameInMove >= _move.TotalFrames)
		{
			_hitbox.SetActive(false);
			if (_chainHits < chainCap)
			{
				_phase = Phase.Listening;
				_counter = checkFrames;
				SetVisual(Colors.Yellow);
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

	private void TickListening(double delta)
	{
		InputManager input = InputManager.Instance;
		InputButtons pressed = Tools.GetPressed(input.Previous, input.Current);
		if (_pendingAttack || (pressed & InputButtons.AttackMask) != InputButtons.None)
		{
			AdvanceChain();
			return;
		}

		if ((pressed & InputButtons.DirectionMask) != InputButtons.None)
		{
			Direction current = Tools.GetDirection(input.Current);
			if (current != Direction.Neutral)
			{
				_pendingDirection = current;
				GD.Print($"路线标记：{Tools.GetDirectionArrow(_pendingDirection)}");
			}
		}

		_counter--;
		if (_counter > 0)
		{
			return;
		}
		inputStateMachine.TransitionTo(inputStateMachine.movementHandler);
	}

	private void CapturePending()
	{
		InputManager input = InputManager.Instance;
		InputButtons pressed = Tools.GetPressed(input.Previous, input.Current);

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

	private Direction ResolveAttackDirection()
	{
		Direction now = Tools.GetDirection(InputManager.Instance.Current);
		return now != Direction.Neutral ? now : _pendingDirection;
	}

}
