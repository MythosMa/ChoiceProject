using Godot;
using System;

public partial class EnemyStateMachine : Node
{
    public enum Phase
    {
        Idle,
        Startup,
        Active,
        Recovery,
    }

    [Export]
    public MoveData attackMove;

    [Export]
    public int idleFrames = 30;

    private Enemy01 _enemy;
    private Sprite2D _sprite;
    private Phase _phase = Phase.Idle;
    private int _frame;

    public Phase CurrentPhase => _phase;

    public void Initialize(Enemy01 enemy)
    {
        _enemy = enemy;
        _sprite = enemy.GetNode<Sprite2D>("Sprite2D");
        EnterPhase(Phase.Idle);
    }

    public void Tick(double delta)
    {
        _frame++;
        switch (_phase)
        {
            case Phase.Idle:
                if (_frame >= idleFrames)
                {
                    EnterPhase(Phase.Startup);
                }
                break;
            case Phase.Startup:
                if (_frame >= attackMove.startupFrames)
                {
                    EnterPhase(Phase.Active);
                }
                break;
            case Phase.Active:
                if (_frame >= attackMove.activeFrames)
                {
                    EnterPhase(Phase.Recovery);
                }
                break;
            case Phase.Recovery:
                if (_frame >= attackMove.recoveryFrames)
                {
                    EnterPhase(Phase.Idle);
                }
                break;
        }
        ApplyTelegraph();
    }

    private void EnterPhase(Phase next)
    {
        _phase = next;
        _frame = 0;

        if (next == Phase.Startup)
        {
            _enemy.attackHitbox.Configure(attackMove);
        }
        _enemy.attackHitbox.SetActive(next == Phase.Active);
    }

    private void ApplyTelegraph()
    {
        _sprite.Modulate = _phase switch
        {
            Phase.Idle => new Color(1.0f, 1.0f, 1.0f),
            Phase.Startup => new Color(0.4f, 0.0f, 0.0f),
            Phase.Active => new Color(1.0f, 0.5f, 0.0f),
            Phase.Recovery => new Color(0.5f, 0.5f, 0.5f),
            _ => Colors.White,
        };
    }

}
