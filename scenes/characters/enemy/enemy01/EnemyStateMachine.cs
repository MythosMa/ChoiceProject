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

    /// <summary>解围招：压力满时用的快反（带霸体）。</summary>
    [Export]
    public MoveData burstMove;

    /// <summary>压力每掉 1 格需要的秒数。</summary>
    [Export]
    public float pressureDecaySeconds = 1.0f;

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
    private MoveData _lastMove;
    private bool _burstArmor;
    private Sprite2D _outline;

    /// <summary>压力读：不看招名，只看被压了几下。</summary>
    public PressureModel Pressure { get; } = new PressureModel(3);

    /// <summary>累计触发过几次解围（供断言与调试用）。</summary>
    public int BurstCount { get; private set; }

    /// <summary>当前霸体是否来自"解围"（全体霸体），而不是"识破某招"。</summary>
    public bool IsBurstArmor => _burstArmor;
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
        _outline = enemy.GetNodeOrNull<Sprite2D>("Outline");
        EnterPhase(Phase.Idle);
    }

    public void Tick(double delta)
    {
        Pressure.Decay((float)delta, pressureDecaySeconds);

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
        // 解围是"全体霸体"（不看招名）；识破是"针对某招"（看招名）。
        bool armoredPhase = _phase == Phase.Startup
            || _phase == Phase.Active
            || _phase == Phase.Recovery;
        bool match = _burstArmor || (_predictedMoveId != null && _predictedMoveId == moveId);
        DebugLog.V($"[霸体检查] 来招={moveId} 预判={_predictedMoveId ?? "无"} 解围={_burstArmor} 阶段={_phase} 匹配={match} 霸体段={armoredPhase}");
        return match && armoredPhase;
    }

    public void RegisterHit(MoveData move)
    {
        string moveId = move != null ? move.Id : "Unknown";
        if (moveId == _lastMoveId)
        {
            _repeatCount++;
        }
        else
        {
            _lastMoveId = moveId;
            _repeatCount = 1;
        }
        _lastMove = move;
        Pressure.Add();

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
        if (Pressure.IsFull)
        {
            StartBurst();
            return;
        }
        if (_repeatCount >= greedThreshold)
        {
            _predictedMoveId = _lastMoveId;
            _repeatCount = 0;
            string display = _lastMove != null ? _lastMove.displayName : _predictedMoveId;
            GD.Print($"[识破] 敌人预判了你的 {display}（{_predictedMoveId}）—— 再打同一招会被架住并反击");
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

    /// <summary>
    /// 解围：压力满时强行打断自身硬直，起一记带霸体的快反。
    /// 与"识破某招"的区别：**它霸体一切招**，所以玩家的正确应对只有"躲/收手"。
    /// 解围打空后进入 recoveryFrames 的后摇（35 帧），那是玩家的奖励窗口。
    /// </summary>
    private void StartBurst()
    {
        BurstCount++;
        _predictedMoveId = null;
        _repeatCount = 0;
        _burstArmor = true;
        _nextAttack = burstMove != null ? burstMove : attackMove;
        Pressure.Reset();
        GD.Print("[解围] 压力满了 —— 敌人强行反击！");
        EnterPhase(Phase.Startup);
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
            _burstArmor = false;
        }
        else if (next == Phase.Recovery || next == Phase.Stagger || next == Phase.Hitstun)
        {
            // 解围的霸体只覆盖 Startup/Active；后摇是它应得的破绽
            _burstArmor = false;
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
        // 通道一：色相只表示相位（"何时"）
        _sprite.Modulate = _phase switch
        {
            Phase.Idle => new Color(1.0f, 1.0f, 1.0f),
            Phase.Startup => new Color(1.0f, 0.85f, 0.0f),
            Phase.Active => new Color(1.0f, 0.1f, 0.1f),
            Phase.Recovery => new Color(0.1f, 1.0f, 0.4f),
            Phase.Stagger => (_frame / 4) % 2 == 0 ? new Color(0.3f, 1.0f, 1.0f) : new Color(1.0f, 1.0f, 1.0f),
            Phase.Hitstun => new Color(0.7f, 0.2f, 1.0f),
            _ => Colors.White,
        };

        // 通道二：描边 = "免疫"（能不能打）。两种免疫必须一眼分得开：
        //   白色细边（常亮）  = 识破某招 —— 换招可打穿
        //   金色粗边（会闪）  = 解围    —— 只能躲
        bool immune = _burstArmor || _predictedMoveId != null;
        if (_outline != null)
        {
            _outline.Visible = immune;
            if (immune)
            {
                if (_burstArmor)
                {
                    bool blink = (_frame / 3) % 2 == 0;
                    _outline.Scale = new Vector2(2.6f, 2.6f);
                    _outline.Modulate = new Color(1.0f, 0.72f, 0.05f, blink ? 0.45f : 1.0f);
                }
                else
                {
                    _outline.Scale = new Vector2(2.35f, 2.35f);
                    _outline.Modulate = new Color(0.95f, 0.95f, 0.95f, 0.85f);
                }
            }
        }

        switch (_phase)
        {
            case Phase.Startup:
                _enemy.attackHitbox.ShowThreat(new Color(1.0f, 0.85f, 0.0f, 0.35f));
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
