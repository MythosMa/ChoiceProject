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

    private int _hp;

    public override void _Ready()
    {
        _hp = maxHP;
        enemyStateMachine.Initialize(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (FeedbackSystem.Instance.IsHitstopActive)
        {
            return;
        }
        enemyStateMachine.Tick(delta);
    }

    public bool TakeDamage(int damageAmount)
    {
        bool isCurrentHit = enemyStateMachine.CurrentPhase == EnemyStateMachine.Phase.Recovery;
        int finalDamage = isCurrentHit ? Mathf.RoundToInt(damageAmount * counterHitMultiplier) : damageAmount;

        _hp = Mathf.Max(0, _hp - finalDamage);
        GD.Print(isCurrentHit ? $"确反！ 敌人 -{finalDamage}, HP: {_hp}/{maxHP}" : $"敌人 -{finalDamage}, HP: {_hp}/{maxHP}");
        if (_hp <= 0)
        {
            GD.Print("敌人倒下！");
        }
        return true;
    }
}
