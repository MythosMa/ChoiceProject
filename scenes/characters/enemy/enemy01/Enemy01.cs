using Godot;
using System;

public partial class Enemy01 : CharacterBody2D, IDamageable
{
    [Export]
    public EnemyStateMachine enemyStateMachine;

    [Export]
    public Hitbox attackHitbox { get; private set; }

    [Export]
    public string displayName = "敌人";

    [Export]
    public int maxHP = 50;

    /// <summary>练习木桩模式：血量改用 trainingHP，方便反复练读招/骗招而不打死敌人。</summary>
    [Export]
    public bool trainingMode = false;

    [Export]
    public int trainingHP = 5000;

    [Export]
    public float counterHitMultiplier = 2.0f;

    /// <summary>被霸体架住时的反伤倍率：以**你打过来的招**的伤害为基数（重招被读代价更大）。</summary>
    [Export]
    public float armorPunishMultiplier = 2.0f;

    [Export]
    public float staggerMultiplier = 3.0f;

    private int _hp;
    private int _maxHp;
    private bool _dead;

    public int HP => _hp;
    public int MaxHP => _maxHp;

    /// <summary>压力值（HUD 用）。</summary>
    public int Pressure => enemyStateMachine.Pressure.Value;
    public int PressureMax => enemyStateMachine.Pressure.Max;
    /// <summary>累计解围次数（断言/调试用）。</summary>
    public int BurstCount => enemyStateMachine.BurstCount;

    public override void _Ready()
    {
        _maxHp = trainingMode ? trainingHP : maxHP;
        _hp = _maxHp;
        enemyStateMachine.Initialize(this);
    }

    /// <summary>招式位移速度（px/s，已按朝向换算），由 EnemyStateMachine 每帧写入。</summary>
    public float externalVelocityX;

    public override void _PhysicsProcess(double delta)
    {
        if (_dead)
        {
            return;
        }
        if (FeedbackSystem.Instance.IsHitstopActive)
        {
            return;
        }
        enemyStateMachine.Tick(delta);

        // 敌人现在会走：出招突进也走这条路（数据在 MoveData 的三段位移里）
        Velocity = new Vector2(externalVelocityX, Velocity.Y);
        MoveAndSlide();
    }

    public bool TakeDamage(int damageAmount, MoveData move)
    {
        if (_dead)
        {
            return false;
        }
        string moveId = move != null ? move.Id : "Unknown";
        if (enemyStateMachine.IsArmoredAgainst(moveId))
        {
            if (enemyStateMachine.IsBurstArmor)
            {
                // 解围是"全体霸体"：你这一下白打，但**不额外扣血、也不结束解围**，
                // 解围会继续打出来——玩家必须躲或停手。
                string label = move != null ? move.displayName : "?";
                GD.Print($"[解围] 你的 {label} 被霸体挡下，伤害无效");
                FeedbackSystem.Instance.RequestHitstop(4);
                return false;
            }

            // 识破是"针对某招"：架住 + 反击，并且这一回合到此结算
            PunishRead(move);
            return false;
        }
        EnemyStateMachine.Phase phase = enemyStateMachine.CurrentPhase;

        int finalDamage = 0;
        string tag = "";

        if (phase == EnemyStateMachine.Phase.Stagger)
        {
            finalDamage = Mathf.RoundToInt(damageAmount * staggerMultiplier);
            tag = "踉跄确反 * 3";
        }
        else if (phase == EnemyStateMachine.Phase.Recovery && !attackHitbox.hasConnected)
        {
            // 只有"它这刀打空了"才给双倍——否则盲打撞后摇也能白拿奖励
            finalDamage = Mathf.RoundToInt(damageAmount * counterHitMultiplier);
            tag = "确反 * 2（扑空）";
        }
        else
        {
            finalDamage = damageAmount;
            tag = "普通命中";
        }

        _hp = Mathf.Max(0, _hp - finalDamage);
        string moveLabel = move != null ? move.displayName : "?";
        GD.Print($"{tag}（{moveLabel}），敌人 -{finalDamage}, HP: {_hp}/{_maxHp}");   // 先报伤害，再报识破（因果顺序）
        enemyStateMachine.RegisterHit(move);
        enemyStateMachine.Interrupt();

        if (_hp <= 0)
        {
            GD.Print("敌人倒下！");
            _dead = true;
            attackHitbox.SetActive(false);
            attackHitbox.HideThreat();
            GetNode<Sprite2D>("Sprite2D").Modulate = new Color(0.0f, 0.0f, 0.0f);
        }
        return true;
    }

    private void PunishRead(MoveData move)
    {
        string moveLabel = move != null ? $"{move.displayName}（{move.Id}）" : "Unknown";
        string moveId = move != null ? move.Id : "Unknown";
        Player player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (player == null)
        {
            GD.Print("玩家不存在！");
            return;
        }
        MoveData counter = enemyStateMachine.attackMove;
        // 反伤按"你打过来的招"缩放：jab 被架住 -20，heavy 被架住 -52
        int incoming = move != null ? move.damage : counter.damage;
        int punishDamage = Mathf.RoundToInt(incoming * armorPunishMultiplier);

        GD.Print($"[架住] 你的 {moveLabel}已被识破，伤害无效，遭到 {punishDamage} 点反击");

        FeedbackSystem.Instance.RequestHitstop(counter.hitstopFrames);
        FeedbackSystem.Instance.RequestShake(counter.hitstopFrames, counter.shakeMagnitude);
        player.TakeDamage(punishDamage, counter);
        enemyStateMachine.OnArmorPunishResolved();
    }
}
