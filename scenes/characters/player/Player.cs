using Godot;
using System;

public partial class Player : CharacterBody2D, IDamageable
{

	[Export]
	public InputStateMachine inputStateMachine;

	[Export]
	public int maxHP = 100;
	private int _hp;
	public int HP => _hp;
	[Export]
	public int maxEnergy = 100;
	private int _energy;
	public int Energy => _energy;
	public float energyRegenPerSecond = 12f;
	private float _energyAccum;

	public const float Speed = 300.0f;
	public const float JumpVelocity = -400.0f;

	public int facing = 1;
	public float moveAxis;

	/// <summary>外部驱动速度（px/s，已按朝向换算）：招式位移、闪避冲刺都写这里，Player 统一应用。
	/// 与 moveAxis 分开，是为了"移动有加减速、招式/闪避是瞬时"。</summary>
	public float externalVelocityX;

	/// <summary>移动自身的速度分量（不含外部驱动）。</summary>
	private float _moveSpeedX;

	/// <summary>地面加速/减速帧数（从静止到全速 / 从全速到静止）。</summary>
	[Export]
	public float groundAccelFrames = 6f;
	[Export]
	public float groundDecelFrames = 6f;
	private bool jumpRequested = false;
	private bool _hitstunRequested = false;
	public void RequestJump() => jumpRequested = true;

	public bool invincible = false;
	public int dodgeCooldown = 0;

	public bool WhiffPunishable = false; // 承诺招可被抓时机判定

	public override void _Ready()
	{
		_hp = maxHP;
		_energy = maxEnergy;
	}

	public override void _PhysicsProcess(double delta)
	{
		// 格挡期间不回复气——否则"耗气"会被回复抵消，格挡就没有代价了
		if (!blocking)
		{
			_energyAccum += (float)delta * energyRegenPerSecond;
		}
		int gained = Mathf.FloorToInt(_energyAccum);
		if (gained > 0)
		{
			_energy = Mathf.Min(maxEnergy, _energy + gained);
			_energyAccum -= gained;
		}

		if (dodgeCooldown > 0)
		{
			dodgeCooldown--;
		}
		if (_hitstunRequested)
		{
			_hitstunRequested = false;
			inputStateMachine.ForceHitstun();

		}
		if (inputStateMachine.TryDodgeInterrupt())
		{
			FeedbackSystem.Instance.CancelHitstop();
		}
		else if (FeedbackSystem.Instance.IsHitstopActive)
		{
			return;
		}
		moveAxis = 0;
		jumpRequested = false;
		externalVelocityX = 0;
		inputStateMachine.Tick(delta);

		if (moveAxis != 0)
		{
			facing = moveAxis > 0 ? 1 : -1;
		}

		Vector2 velocity = Velocity;
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		if (jumpRequested && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}

		// 移动带加减速，但**独立累计**，不能从 Velocity 反读——
		// 否则 externalVelocityX（闪避冲刺）会被逐帧叠加，越闪越快。
		float targetX = moveAxis * Speed;
		float smoothingFrames = Mathf.Abs(targetX) > 0.01f ? groundAccelFrames : groundDecelFrames;
		float rate = Speed / Mathf.Max(1f, smoothingFrames);
		_moveSpeedX = Mathf.MoveToward(_moveSpeedX, targetX, rate * 60f * (float)delta);

		// 外部驱动（闪避冲刺 / 招式位移）优先：它是一个**定速**，不是叠加量
		velocity.X = Mathf.Abs(externalVelocityX) > 0.01f ? externalVelocityX : _moveSpeedX;

		Velocity = velocity;
		MoveAndSlide();
	}

	/// <summary>是否处于格挡态（由 BlockHandler 维护）。</summary>
	public bool blocking;

	/// <summary>被破防的硬直帧数（0 = 普通受击）。</summary>
	public int pendingStunFrames;

	public bool TrySpendEnergy(int energyCost)
	{
		if (_energy < energyCost)
		{
			return false;
		}
		_energy -= energyCost;
		return true;
	}

	public bool TakeDamage(int damageAmount, MoveData move)
	{
		if (invincible)
		{
			DebugLog.V($"无敌状态，无法受到伤害 {damageAmount}");
			return false;
		}

		string tag = "被攻击";
		bool guardBroken = false;

		if (blocking && move != null)
		{
			if (move.guardBreak)
			{
				// 破防：无视减伤，全额伤害 + 长硬直
				guardBroken = true;
				pendingStunFrames = 40;
				blocking = false;
				tag = "破防命中";
			}
			else
			{
				// 普通格挡：只吃削血，不进硬直
				damageAmount = Mathf.Max(1, Mathf.RoundToInt(damageAmount * 0.2f));
				tag = "格挡";
			}
		}

		_hp = Mathf.Max(0, _hp - damageAmount);
		if (!blocking)
		{
			_hitstunRequested = true;
		}
		string from = move != null ? move.displayName : "?";
		GD.Print($"{tag}（{from}）： -{damageAmount}, HP: {_hp} / {maxHP}");
		_ = guardBroken;
		if (_hp <= 0)
		{
			GD.Print("Game Over!");
			GetTree().CreateTimer(2.0f).Timeout += () => GetTree().ReloadCurrentScene();
		}
		return true;
	}
}
