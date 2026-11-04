using Godot;
using System;

public partial class Enemy01 : CharacterBody2D, IDamageable
{
    [Export]
    public EnemyStateMachine enemyStateMachine;

    [Export]
    public Hitbox attackHitbox { get; private set; }

    [Export]
    public int maxHP = 50;

    [Export]
    public float counterHitMultiplier = 2.0f;

    [Export]
    public float armorPunishMultiplier = 1.5f;

    [Export]
    public float staggerMultiplier = 3.0f;

    private int _hp;
    private bool _dead;

    public override void _Ready()
    {
        _hp = maxHP;
        enemyStateMachine.Initialize(this);
    }

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
    }

    public bool TakeDamage(int damageAmount, MoveData move)
    {
        if (_dead)
        {
            return false;
        }
        string moveId = move?.moveName ?? "Unknown";
        if (enemyStateMachine.IsArmoredAgainst(moveId))
        {
            PunishRead(moveId);
            return false;
        }
        // bool isCurrentHit = enemyStateMachine.CurrentPhase == EnemyStateMachine.Phase.Recovery;
        // int finalDamage = isCurrentHit ? Mathf.RoundToInt(damageAmount * counterHitMultiplier) : damageAmount;

        // _hp = Mathf.Max(0, _hp - finalDamage);
        // enemyStateMachine.RegisterHit(moveId);
        // enemyStateMachine.Interrupt();

        // GD.Print(isCurrentHit ? $"确反！ 敌人 -{finalDamage}, HP: {_hp}/{maxHP}" : $"敌人 -{finalDamage}, HP: {_hp}/{maxHP}");

        EnemyStateMachine.Phase phase = enemyStateMachine.CurrentPhase;

        int finalDamage = 0;
        string tag = "";

        if (phase == EnemyStateMachine.Phase.Stagger)
        {
            finalDamage = Mathf.RoundToInt(damageAmount * staggerMultiplier);
            tag = "踉跄确反 * 3";
        }
        else if (phase == EnemyStateMachine.Phase.Recovery)
        {
            finalDamage = Mathf.RoundToInt(damageAmount * counterHitMultiplier);
            tag = "确反 * 2";
        }
        else
        {
            finalDamage = damageAmount;
            tag = "普通命中";
        }

        _hp = Mathf.Max(0, _hp - finalDamage);
        enemyStateMachine.RegisterHit(moveId);
        enemyStateMachine.Interrupt();
        GD.Print($"{tag}，敌人 -{finalDamage}, HP: {_hp}/{maxHP}");

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

    private void PunishRead(string moveId)
    {
        Player player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (player == null)
        {
            GD.Print("玩家不存在！");
            return;
        }
        MoveData counter = enemyStateMachine.attackMove;
        int punishDamage = Mathf.RoundToInt(counter.damage * armorPunishMultiplier);

        FeedbackSystem.Instance.RequestHitstop(counter.hitstopFrames);
        FeedbackSystem.Instance.RequestShake(counter.hitstopFrames, counter.shakeMagnitude);
        player.TakeDamage(punishDamage, counter);

        GD.Print($"霸体！你的{moveId}被招架了，遭到{punishDamage}点反击伤害");
    }
}
