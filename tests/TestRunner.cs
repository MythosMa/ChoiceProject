using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 机制断言（headless）。
///
/// 运行：godot --headless --path . res://tests/tests.tscn
/// 退出码：0 = 全过；1 = 有失败。
///
/// 约定：
/// - 每条断言必须能在无窗口、无人工操作的情况下复现；
/// - 断言失败只抛异常，由 RunSafe 统一记录，不影响后续用例；
/// - 场景类断言自己 new 一个 fixture，用完销毁，**用例之间不共享状态**；
/// - **任何用例都不允许让玩家死亡**：Player 死亡会 ReloadCurrentScene，
///   测试场景会被重载并重跑，表现为"测试无限循环"。所以战斗类用例一律先置 invincible。
/// </summary>
public partial class TestRunner : Node
{
    private const string FixturePath = "res://tests/fixtures/combat_fixture.tscn";
    private const string JabPath = "res://resources/move/test_light.tres";

    private int _passed;
    private int _failed;
    private bool _wasHeavy;

    public override async void _Ready()
    {
        GD.Print("=== Choice Project · 机制断言 ===");

        await RunSafe("A1 攻击框不会指向自己 / 且能指向对手", TestNoSelfHit);
        await RunSafe("A2 双向命中：玩家能打伤敌人、敌人能打伤玩家", TestBidirectionalHit);
        await RunSafe("A3 基线：交替 jab/heavy 能击杀 50HP 敌人", TestBaseline);

        GD.Print($"=== RESULT: {_passed} passed, {_failed} failed ===");
        GetTree().Quit(_failed > 0 ? 1 : 0);
    }

    // ---------------------------------------------------------------- infra

    private async Task RunSafe(string name, Func<Task> test)
    {
        try
        {
            await test();
            _passed++;
            GD.Print($"[PASS] {name}");
        }
        catch (Exception e)
        {
            _failed++;
            GD.Print($"[FAIL] {name} → {e.Message}");
        }
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
    }

    private async Task<Node2D> SpawnFixture()
    {
        PackedScene packed = GD.Load<PackedScene>(FixturePath);
        if (packed == null)
        {
            throw new Exception($"找不到 fixture：{FixturePath}");
        }
        Node2D fixture = packed.Instantiate<Node2D>();
        AddChild(fixture);
        await Frames(2);
        return fixture;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }

    /// <summary>测试用战斗夹具：冻结敌人 AI、玩家无敌。返回 (player, enemy)。</summary>
    private async Task<(Node2D fixture, Player player, Enemy01 enemy)> SpawnPassiveFight()
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");
        enemy.SetPhysicsProcess(false);   // 只测玩家输出，排除 AI 干扰
        player.invincible = true;         // 防止被霸体反击打死 → 重载场景 → 测试无限重跑
        await Frames(1);
        return (fixture, player, enemy);
    }

    // ---------------------------------------------------------------- tests

    /// <summary>
    /// A1：攻击框的 mask 不能覆盖自己的 hurtbox 层，但必须覆盖对手的 hurtbox 层。
    /// 这是"不会自伤 / 不会误伤"的构造性保证。
    /// </summary>
    private Task TestNoSelfHit()
    {
        PackedScene packed = GD.Load<PackedScene>(FixturePath);
        Node2D fixture = packed.Instantiate<Node2D>();

        Player player = fixture.GetNode<Player>("Player");
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");

        Hitbox playerHit = player.GetNode<Hitbox>("Hitbox");
        Hurtbox playerBox = player.GetNode<Hurtbox>("Hurtbox");
        Hitbox enemyHit = enemy.GetNode<Hitbox>("Hitbox");
        Hurtbox enemyBox = enemy.GetNode<Hurtbox>("Hurtbox");

        Check((playerHit.CollisionMask & playerBox.CollisionLayer) == 0,
            $"玩家攻击框 mask={playerHit.CollisionMask} 覆盖了自身受击层={playerBox.CollisionLayer}");
        Check((enemyHit.CollisionMask & enemyBox.CollisionLayer) == 0,
            $"敌人攻击框 mask={enemyHit.CollisionMask} 覆盖了自身受击层={enemyBox.CollisionLayer}");

        Check((playerHit.CollisionMask & enemyBox.CollisionLayer) != 0,
            $"玩家攻击框 mask={playerHit.CollisionMask} 没指向敌人受击层={enemyBox.CollisionLayer}（打不到敌人）");
        Check((enemyHit.CollisionMask & playerBox.CollisionLayer) != 0,
            $"敌人攻击框 mask={enemyHit.CollisionMask} 没指向玩家受击层={playerBox.CollisionLayer}（敌人打不到玩家）");

        fixture.QueueFree();
        return Task.CompletedTask;
    }

    /// <summary>
    /// A2：真正走一遍 Hitbox → Hurtbox → TakeDamage 的链路，两个方向各一次。
    /// 冻结两个角色的物理，只留判定框生效，避免 AI 和重力干扰。
    /// </summary>
    private async Task TestBidirectionalHit()
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");
        Hitbox playerHit = player.GetNode<Hitbox>("Hitbox");
        Hitbox enemyHit = enemy.GetNode<Hitbox>("Hitbox");

        player.SetPhysicsProcess(false);
        enemy.SetPhysicsProcess(false);
        player.invincible = true; // 被反击时不掉血，避免意外死亡重载场景

        // 玩家 → 敌人
        player.GlobalPosition = new Vector2(0, 64);
        enemy.GlobalPosition = new Vector2(30, 64);
        await Frames(2);

        int enemyHpBefore = enemy.HP;
        MoveData jab = GD.Load<MoveData>(JabPath);
        Check(jab != null, $"找不到招式资源：{JabPath}");
        playerHit.Configure(jab, 1);
        playerHit.SetActive(true);
        await Frames(4);
        playerHit.SetActive(false);
        Check(enemy.HP < enemyHpBefore,
            $"玩家攻击没有生效（敌人 HP {enemyHpBefore} → {enemy.HP}）");

        // 敌人 → 玩家（先解除无敌，否则扣不到血，测不到链路）
        player.invincible = false;
        enemy.GlobalPosition = new Vector2(-40, 64);
        await Frames(2);

        int playerHpBefore = player.HP;
        enemyHit.Configure(enemy.enemyStateMachine.attackMove, 1);
        enemyHit.SetActive(true);
        await Frames(4);
        enemyHit.SetActive(false);
        Check(player.HP < playerHpBefore,
            $"敌人攻击没有生效（玩家 HP {playerHpBefore} → {player.HP}）");

        player.invincible = true;
        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>
    /// A3：基线，不是调参断言。
    ///
    /// 为什么不用"同名 jab 连打"做基线：**它打不死**——第 3 下之后敌人识破 jab，
    /// 之后每一发都被霸体挡掉（这本身就是要修的免费解的反面）。所以基线用
    /// "交替 jab/heavy"，那是当前唯一能稳定输出的脚本。
    ///
    /// 同时记录"同名 jab 连打 240 帧后剩多少血"，作为识破机制的对照。
    /// </summary>
    private async Task TestBaseline()
    {
        int killFrames = await RunScriptAlternating(900);
        GD.Print($"[INFO] A3 基线（冻结 AI / 玩家无敌）：交替 jab+heavy 击杀 50HP 敌人 = {killFrames} 帧（{killFrames / 60.0:F2} 秒）");
        Check(killFrames < 900, "900 帧内没击杀，输出链路可能断了");

        int remaining = await RunScriptJabOnly(240);
        GD.Print($"[INFO] A3 对照（冻结 AI / 玩家无敌）：同名 jab 连打 240 帧后敌人剩余 {remaining}/50 HP");
    }

    private async Task<int> RunScriptAlternating(int maxFrames)
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        int frames = 0;
        bool pressed = false;

        while (enemy.HP > 0 && frames < maxFrames)
        {
            if (frames % 20 == 0)
            {
                bool heavy = (frames / 20) % 2 == 1;
                _wasHeavy = heavy;
                player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 45, enemy.GlobalPosition.Y);
                if (heavy)
                {
                    Input.ActionPress("right");
                }
                Input.ActionPress("light_punch");
                pressed = true;
            }
            else if (pressed)
            {
                Input.ActionRelease("light_punch");
                if (_wasHeavy)
                {
                    Input.ActionRelease("right");
                    _wasHeavy = false;
                }
                pressed = false;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            frames++;
        }

        Input.ActionRelease("light_punch");
        Input.ActionRelease("right");
        bool killed = enemy.HP <= 0;

        fixture.QueueFree();
        await Frames(1);
        return killed ? frames : maxFrames;
    }

    private async Task<int> RunScriptJabOnly(int maxFrames)
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        int frames = 0;
        bool pressed = false;

        while (frames < maxFrames)
        {
            if (frames % 20 == 0)
            {
                player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 45, enemy.GlobalPosition.Y);
                Input.ActionPress("light_punch");
                pressed = true;
            }
            else if (pressed)
            {
                Input.ActionRelease("light_punch");
                pressed = false;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            frames++;
        }

        Input.ActionRelease("light_punch");
        int hp = enemy.HP;

        fixture.QueueFree();
        await Frames(1);
        return hp;
    }
}
