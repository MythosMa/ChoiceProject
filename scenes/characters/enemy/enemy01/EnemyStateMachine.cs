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
        Stagger,
        Hitstun,
    }

    [Export]
    public MoveData attackMove;

    [Export]
    public MoveData punishMove;

    [Export]
    public float punishRange = 140.0f;

    /// <summary>
    /// 允许的"同名命中"次数。达到后敌人武装识破：
    /// 下一次**同一招**会被架住并反击（换招则不受影响）。
    /// 语义是"允许次数"，所以 2 = 第 3 次同招被架住（2026-11-04 由 3 改为 2）。
    /// </summary>
    [Export]
    public int greedThreshold = 2;

    [Export]
    public int greedWindowFrames = 120; // 预研ai, 未被同一招打中持续时长后，放弃预判

    private string _lastMoveId;
    private int _repeatCount;
    private int _greedTimer;
    private string _predictedMoveId; // 反制针对的招式i
    private MoveData _currentMove;
    private MoveData _nextAttack;

    [Export]
    public int idleFrames = 30;

    [Export]
    public int staggerFrames = 50;

    [Export]
    public int hitstunFrames = 15;

    private Enemy01 _enemy;
    private Sprite2D _sprite;
    private Phase _phase = Phase.Idle;
    private int _frame;

    public Phase CurrentPhase => _phase;

    private Node2D _player;
    private int _facing = 1;

    public void Initialize(Enemy01 enemy)
    {
        _enemy = enemy;
        _sprite = enemy.GetNode<Sprite2D>("Sprite2D");
        EnterPhase(Phase.Idle);
    }

    public void Tick(double delta)
    {
        if (_greedTimer > 0)
        {
            _greedTimer--;
            if (_greedTimer == 0)
            {
                _repeatCount = 0;
                _lastMoveId = null;
            }
        }
        _frame++;
        if ((_phase == Phase.Idle || _phase == Phase.Recovery) && TryStartWhiffPunish())
        {
            ApplyTelegraph();
            return;
        }
        switch (_phase)
        {
            case Phase.Idle:
                if (_frame >= idleFrames)
                {
                    EnterPhase(Phase.Startup);
                }
                break;
            case Phase.Startup:
                if (_frame >= _currentMove.startupFrames)
                {
                    EnterPhase(Phase.Active);
                }
                break;
            case Phase.Active:
                if (_frame >= _currentMove.activeFrames)
                {
                    if (_predictedMoveId != null && !_enemy.attackHitbox.hasConnected)
                    {
                        EnterPhase(Phase.Stagger);
                    }
                    else
                    {
                        EnterPhase(Phase.Recovery);
                    }
                }
                break;
            case Phase.Recovery:
                if (_frame >= _currentMove.recoveryFrames)
                {
                    EnterPhase(Phase.Idle);
                }
                break;
            case Phase.Hitstun:
                if (_frame >= hitstunFrames)
                {
                    EnterPhase(Phase.Idle);
                }
                break;
            case Phase.Stagger:
                if (_frame >= staggerFrames)
                {
                    EnterPhase(Phase.Idle);
                }
                break;
        }
        ApplyTelegraph();
    }

    public bool IsArmoredAgainst(string moveId)
    {
        // if (_predictedMoveId != null || _predictedMoveId != moveId)
        // {
        //     return false;
        // }
        // return _phase == Phase.Startup || _phase == Phase.Active || _phase == Phase.Recovery;
        bool match = _predictedMoveId != null && _predictedMoveId == moveId;
        bool armoredPhase = _phase == Phase.Startup
            || _phase == Phase.Active
            || _phase == Phase.Recovery;
        DebugLog.V($"[霸体检查] 来招={moveId} 预判={_predictedMoveId ?? "无"} 阶段={_phase} 匹配={match} 霸体段={armoredPhase}");
        return match && armoredPhase;
    }

    public void RegisterHit(string moveId)
    {
        if (moveId == _lastMoveId)
        {
            _repeatCount++;
        }
        else
        {
            _lastMoveId = moveId;
            _repeatCount = 1;
        }

        _greedTimer = greedWindowFrames;
    }


    public void Interrupt()
    {
        if (_phase == Phase.Stagger)
        {
            return;
        }
        if (IsArmoredAgainst(_lastMoveId))
        {
            return;
        }
        if (_repeatCount >= greedThreshold)
        {
            _predictedMoveId = _lastMoveId;
            _repeatCount = 0;
            GD.Print($"[识破] 敌人预判了你的 {_predictedMoveId} —— 再打同一招会被架住并反击");
            EnterPhase(Phase.Startup);
            return;
        }
        EnterPhase(Phase.Hitstun);
    }

    /// <summary>
    /// 霸体反打命中后的收尾（2026-11-04）。
    ///
    /// 一次"架住"就是这一回合的结算，敌人不再继续挥这一刀。否则会出现**因果倒挂**：
    /// 玩家被架住（-18）→ 敌人这刀打空 → 敌人踉跄 → 玩家白拿 ×3。
    ///
    /// 后摇里的"架住"只清预判标记（后摇是它应得的破绽，不打断）；
    /// 前摇/判定里的"架住"直接收招回 Idle —— 玩家此时仍在硬直，回合归敌人。
    /// </summary>
    public void OnArmorPunishResolved()
    {
        _predictedMoveId = null;
        _nextAttack = null;
        _enemy.attackHitbox.SetActive(false);

        if (_phase == Phase.Startup || _phase == Phase.Active)
        {
            EnterPhase(Phase.Idle);
        }
    }

    private bool TryStartWhiffPunish()
    {
        if (punishMove == null)
        {
            return false;
        }
        _player ??= _enemy.GetTree().GetFirstNodeInGroup("Player") as Node2D;
        Player player = _player as Player;
        if (player == null || !player.WhiffPunishable)
        {
            return false;
        }

        float dist = Mathf.Abs(player.GlobalPosition.X - _enemy.GlobalPosition.X);
        if (dist > punishRange)
        {
            return false;
        }
        player.WhiffPunishable = false;
        _nextAttack = punishMove;
        _predictedMoveId = null;
        EnterPhase(Phase.Startup);
        GD.Print("[反制] 挥空被抓住，反斩！");
        return true;
    }

    private void EnterPhase(Phase next)
    {
        _phase = next;
        _frame = 0;

        if (next == Phase.Startup)
        {
            _currentMove = _nextAttack ?? attackMove;
            _nextAttack = null;
            UpdateFacing();
            _enemy.attackHitbox.Configure(_currentMove, _facing);
        }
        else if (next == Phase.Stagger)
        {
            FeedbackSystem.Instance.RequestShake(8, 6f);
            GD.Print("敌人扑空踉跄！大破绽，快确反×3！");
        }
        else if (next == Phase.Idle)
        {
            _predictedMoveId = null;
        }
        _enemy.attackHitbox.SetActive(next == Phase.Active);
    }

    private void UpdateFacing()
    {
        _player ??= _enemy.GetTree().GetFirstNodeInGroup("Player") as Node2D;
        if (_player != null)
        {
            _facing = _player.GlobalPosition.X >= _enemy.GlobalPosition.X ? 1 : -1;
        }
    }

    private void ApplyTelegraph()
    {
        _sprite.Modulate = _phase switch
        {
            Phase.Idle => new Color(1.0f, 1.0f, 1.0f),
            Phase.Startup => _predictedMoveId != null ? new Color(1.0f, 0.6f, 0.0f) : new Color(1.0f, 0.85f, 0.0f),
            Phase.Active => new Color(1.0f, 0.1f, 0.1f),
            Phase.Recovery => _predictedMoveId != null ? new Color(0.9f, 1.0f, 0.1f) : new Color(0.1f, 1.0f, 0.4f),
            Phase.Stagger => (_frame / 4) % 2 == 0 ? new Color(0.3f, 1.0f, 1.0f) : new Color(1.0f, 1.0f, 1.0f),
            Phase.Hitstun => new Color(0.7f, 0.2f, 1.0f),
            _ => Colors.White,
        };

        switch (_phase)
        {
            case Phase.Startup:
                _enemy.attackHitbox.ShowThreat(_predictedMoveId != null ? new Color(1.0f, 0.6f, 0.0f, 0.35f) : new Color(1.0f, 0.85f, 0.0f, 0.35f));
                break;
            case Phase.Active:
                _enemy.attackHitbox.ShowThreat(new Color(1.0f, 0.1f, 0.1f, 0.6f));
                break;
            default:
                _enemy.attackHitbox.HideThreat();
                break;
        }
    }

}
