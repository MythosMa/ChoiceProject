using Godot;

public partial class CombatHandler : Node, IInputReceiver
{
	private enum Segment { Startup, Active, Recovery }

	/// <summary>轻攻击（Z / A）。</summary>
	[Export]
	public MoveData lightMove;

	/// <summary>重攻击（X / S）。2026-11-04 起由按键决定，不再由方向决定。</summary>
	[Export]
	public MoveData heavyMove;

	[Export] public int chainCap = 3;     // 占位最大链长

	private MoveData _move;
	private int _frameInMove;
	private int _chainHits;
	private Hitbox _hitbox;

	// Showing 期预输入暂存
	private int _moveStartInputId = -1;

	private Segment _prevSegment = Segment.Startup;

	public bool CurrentMoveDodgeCancellable => _move == null || _move.dodgeCancellable;

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
		InputManager input = InputManager.Instance;
		AttackType pressed = Tools.GetAttack(Tools.GetPressed(input.Previous, input.Current));
		Direction dir = Tools.GetDirection(input.Current);
		if (!StartMove(SelectMove(pressed), ResolveAttackFacing(dir)))
		{
			_move = null;
		}
	}

	public void Exit()
	{
		_hitbox.SetActive(false);
		_hitbox.HideThreat();
		SetVisual(Colors.White);
		_move = null;
		player.WhiffPunishable = false;
		player.externalVelocityX = 0f;
	}

	public void Tick(double delta)
	{
		TickPlaying(delta);
	}


	private bool StartMove(MoveData move, int facing)
	{
		if (move == null)
		{
			GD.Print("招式配置错误，无法出招");
			return false;
		}
		if (!player.TrySpendEnergy(move.energyCost))
		{
			GD.Print($"能量不足，无法出招, 需要 {move.energyCost} ，当前 {player.Energy}");
			return false;
		}

		_move = move;
		_frameInMove = 0;
		_prevSegment = Segment.Startup;
		player.WhiffPunishable = false;
		_hitbox.Configure(move, facing);


		DebugLog.V($"连段第 {_chainHits} 击: {move.displayName}");
		ApplySegmentVisual();
		SyncHitbox();

		_moveStartInputId = LastInputId();
		return true;
	}

	private int LastInputId()
	{
		var buffer = InputManager.Instance.Buffer;
		return buffer.Count > 0 ? buffer[^1].Id : -1;
	}

	private void TickPlaying(double delta)
	{
		if (_move == null)
		{
			inputStateMachine.TransitionTo(inputStateMachine.movementHandler);
			return;
		}
		if (_move != null && !_move.lockMovement)
		{
			// todo: 动作演出时不锁定移动的处理逻辑
		}

		_frameInMove++;
		if (_frameInMove >= _move.TotalFrames)
		{
			_hitbox.SetActive(false);
			player.WhiffPunishable = false;
			MoveData next = ResolveNext(out Direction nextDir);
			if (next != null && _chainHits < chainCap)
			{
				_chainHits++;
				StartMove(next, ResolveAttackFacing(nextDir));
			}
			else
			{
				inputStateMachine.TransitionTo(inputStateMachine.movementHandler);
			}
			return;
		}
		Segment seg = CurrentSegment();
		if (_prevSegment == Segment.Active && seg == Segment.Recovery && !_hitbox.hasConnected && !_move.dodgeCancellable)
		{
			player.WhiffPunishable = true;
			GD.Print($"[挥空] {_move.displayName}挥空，露出破绽");
		}
		_prevSegment = seg;

		SyncHitbox();
		ApplySegmentVisual();
		ApplySegmentShift();
	}

	/// <summary>
	/// 把 MoveData 的三段位移（像素）换算成本帧速度，交给 Player 统一积分。
	/// 位移按段帧数均摊，所以"总位移"对设计师是可读的；判定框是身体子节点，会跟着一起动——
	/// 等于判定期前冲会动态延长攻击距离（这是有意的，后续突进/升龙类招式直接复用）。
	/// </summary>
	private void ApplySegmentShift()
	{
		Segment seg = CurrentSegment();
		float shift;
		int frames;

		switch (seg)
		{
			case Segment.Startup:
				shift = _move.startupShift;
				frames = _move.startupFrames;
				break;
			case Segment.Active:
				shift = _move.activeShift;
				frames = _move.activeFrames;
				break;
			default:
				shift = _move.recoveryShift;
				frames = _move.recoveryFrames;
				break;
		}

		if (frames <= 0 || Mathf.IsZeroApprox(shift))
		{
			player.externalVelocityX = 0f;
			return;
		}

		player.externalVelocityX = shift / frames * 60f * player.facing;
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
		if (_move == null)
		{
			return;
		}

		Color accent = _move.accentColor;

		switch (CurrentSegment())
		{
			case Segment.Startup:
				// 身体用相位色（黄=起手），预演框用招式色（半透明）——两个通道各管一件事
				SetVisual(new Color(1.0f, 0.85f, 0.0f));
				_hitbox.ShowThreat(new Color(accent.R, accent.G, accent.B, 0.3f));
				break;
			case Segment.Active:
				// 身体仍然红（相位），但挥击块是实心招式色：轻拳细长冷色、重拳粗壮暖色
				SetVisual(new Color(1.0f, 0.1f, 0.1f));
				_hitbox.ShowSwing(accent);
				break;
			case Segment.Recovery:
				SetVisual(new Color(0.1f, 1.0f, 0.4f));
				_hitbox.HideThreat();   // 后摇不该再显示攻击范围
				break;
		}
	}

	private void SetVisual(Color color)
	{
		player.GetNode<Sprite2D>("Sprite2D").Modulate = color;
	}

	/// <summary>
	/// 选招：由**按下的攻击键**决定，不再由方向决定（2026-11-04）。
	/// Z / A = 轻攻击；X / S = 重攻击。同时按下轻重时重攻击优先。
	/// 方向只负责"朝哪打"（ResolveAttackFacing），不负责"用哪招"。
	/// </summary>
	private MoveData SelectMove(AttackType attack)
	{
		bool heavy = (attack & (AttackType.HP | AttackType.HK)) != 0;
		MoveData m = heavy ? heavyMove : lightMove;
		return m ?? lightMove;
	}

	private MoveData ResolveNext(out Direction dir)
	{
		dir = Direction.Neutral;
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
				dir = rec.Direction;
				return SelectMove(rec.Attack);
			}
		}
		return null;
	}

	/// <summary>
	/// 出招朝向：以"出招瞬间按下的水平方向"为准（绝对方向语义），
	/// 只有在没有水平方向时才沿用移动朝向。
	/// 修复：面向左时按 →+攻击，选到的是 heavy，但判定框按旧 facing 镜像到背后（挥空）。
	/// </summary>
	private int ResolveAttackFacing(Direction dir)
	{
		int facing = dir switch
		{
			Direction.Left or Direction.UpLeft or Direction.DownLeft => -1,
			Direction.Right or Direction.UpRight or Direction.DownRight => 1,
			_ => player.facing,
		};
		player.facing = facing;
		return facing;
	}

}
