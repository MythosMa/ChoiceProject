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

    private int _passed;
    private int _failed;
    private bool _wasHeavy;

    public override async void _Ready()
    {
        GD.Print("=== Choice Project · 机制断言 ===");

        await RunSafe("A1 攻击框不会指向自己 / 且能指向对手", TestNoSelfHit);
        await RunSafe("A2 双向命中：玩家能打伤敌人、敌人能打伤玩家", TestBidirectionalHit);
        await RunSafe("A3 基线：交替 jab/heavy 能击杀 50HP 敌人", TestBaseline);
        await RunSafe("A4 闪避可以穿过敌人身体", TestDodgePassThrough);
        await RunSafe("A5 被识破的第 3 次同招不造成伤害", TestReadNullifiesThirdHit);
        await RunSafe("A6 架住之后敌人不再挥空踉跄", TestNoStaggerAfterArmorPunish);
        await RunSafe("A7 招式身份可配置且唯一（AI 读招不依赖代码硬编码）", TestMoveIdentity);
        await RunSafe("A8 交替两招连打会触发解围（免费解消失）", TestBurstOnAlternating);
        await RunSafe("A9 打两下就收手不触发解围", TestNoBurstWhenBackingOff);
        await RunSafe("A10 一次压力周期只触发一次解围", TestSingleBurstPerCycle);
        await RunSafe("A12 识破后的反击能打到重拳最远射程", TestReadCounterReachesHeavyRange);
        await RunSafe("A13 被架住的反伤随招式重量放大", TestPunishScalesWithMoveWeight);
        await RunSafe("A14 敌人两招可区分", TestTwoMovesDistinguishable);
        await RunSafe("A15 格挡减伤且不进硬直", TestBlockReducesDamage);
        await RunSafe("A16 格挡持续耗气，气空自动破防", TestBlockDrainsEnergy);
        await RunSafe("A17 破防招能打破格挡", TestGuardBreakBreaksBlock);
        await RunSafe("A18 一局内两招都要出", TestBothMovesUsed);
        await RunSafe("A19 硬直中按下格挡，硬直结束后能接上", TestBlockHeldThroughHitstun);
        await RunSafe("A20 无脑连打不能赢（设计目标）", TestMashCannotWin);
        await RunSafe("A21 闪避方向听当前输入，不听旧朝向", TestDodgeFollowsInput);

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

    /// <summary>从玩家身上取招式资源，避免测试硬编码资源路径（改文件名不会让测试失效）。</summary>
    private static MoveData LightMoveOf(Player player)
    {
        CombatHandler combat = player.GetNode<InputStateMachine>("InputStateMachine").combatHandler;
        return combat?.lightMove;
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
        // 不写死资源路径：直接问玩家"你的轻拳是哪个资源"
        MoveData light = LightMoveOf(player);
        Check(light != null, "玩家没有配置 lightMove");
        playerHit.Configure(light, 1);
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
    /// 交替 jab/heavy 贴脸连打，返回这段期间新增的解围次数。
    /// 交替是为了绕开"同名读"，专测压力读。
    /// </summary>
    private async Task<int> RunAlternatingAndCountBursts(int maxFrames)
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        int before = enemy.BurstCount;
        int frames = 0;
        bool pressed = false;

        while (frames < maxFrames)
        {
            if (frames % 20 == 0)
            {
                bool heavy = (frames / 20) % 2 == 1;
                _wasHeavy = heavy;
                player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 45, enemy.GlobalPosition.Y);
                Input.ActionPress(heavy ? "heavy_punch" : "light_punch");
                pressed = true;
            }
            else if (pressed)
            {
                Input.ActionRelease(_wasHeavy ? "heavy_punch" : "light_punch");
                pressed = false;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            frames++;
        }

        Input.ActionRelease("light_punch");
        Input.ActionRelease("heavy_punch");
        int bursts = enemy.BurstCount - before;

        fixture.QueueFree();
        await Frames(1);
        return bursts;
    }

    /// <summary>
    /// A13：被霸体架住的反伤必须随"你打过来的招"放大——
    /// 重拳被读要比轻拳被读疼得多，否则"用重招"没有代价。
    /// </summary>
    private async Task TestPunishScalesWithMoveWeight()
    {
        int lightLoss = await RunThreeHitsAndMeasureLoss("light_punch");
        int heavyLoss = await RunThreeHitsAndMeasureLoss("heavy_punch");

        GD.Print($"[INFO] A13 反伤：轻拳被架住 -{lightLoss}，重拳被架住 -{heavyLoss}");
        Check(heavyLoss > lightLoss,
            $"重拳被读的代价没有更高（轻 {lightLoss} / 重 {heavyLoss}）—— 重招变成了无风险高收益");
        Check(heavyLoss >= 40, $"重拳被读只掉 {heavyLoss}，惩罚不足以抵消一次重拳的收益（26）");
    }

    private async Task<int> RunThreeHitsAndMeasureLoss(string action)
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        player.invincible = false;
        int hp0 = player.HP;

        for (int i = 0; i < 3; i++)
        {
            player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 45, enemy.GlobalPosition.Y);
            Input.ActionPress(action);
            await Frames(1);
            Input.ActionRelease(action);
            await Frames(39);
        }
        await Frames(6);

        int loss = hp0 - player.HP;
        fixture.QueueFree();
        await Frames(1);
        return loss;
    }


    /// <summary>
    /// A21：闪避方向必须取"按下瞬间的输入方向"，不能取上次朝向。
    /// 实测 bug：按住左、朝向还停在右，闪避会向右侧冲。
    /// </summary>
    private async Task TestDodgeFollowsInput()
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        player.invincible = true;

        // 先朝右走，把朝向变成 +1
        Input.ActionPress("right");
        await Frames(10);
        Input.ActionRelease("right");
        await Frames(10);
        Check(player.facing == 1, $"朝向没转右（facing={player.facing}）");

        // 按住左 + 闪避 → 必须向左
        Input.ActionPress("left");
        await Frames(2);
        float x0 = player.GlobalPosition.X;
        Input.ActionPress("dodge");
        await Frames(1);
        Input.ActionRelease("dodge");
        await Frames(16);
        Input.ActionRelease("left");
        float leftDelta = player.GlobalPosition.X - x0;

        await Frames(70); // 冷却

        // 反过来：朝左后按住右 + 闪避 → 必须向右
        Input.ActionPress("left");
        await Frames(10);
        Input.ActionRelease("left");
        await Frames(10);
        Input.ActionPress("right");
        await Frames(2);
        float x1 = player.GlobalPosition.X;
        Input.ActionPress("dodge");
        await Frames(1);
        Input.ActionRelease("dodge");
        await Frames(16);
        Input.ActionRelease("right");
        float rightDelta = player.GlobalPosition.X - x1;

        GD.Print($"[INFO] A21 按住左闪避 {leftDelta:F1}px，按住右闪避 {rightDelta:F1}px");
        Check(leftDelta < -60, $"按住左闪避却没向左（{leftDelta:F1}px）");
        Check(rightDelta > 60, $"按住右闪避却没向右（{rightDelta:F1}px）");

        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>
    /// A20【设计目标断言】：只按 jab、不闪不挡，**不能赢**。
    /// 之前的问题是"靠血量淹死敌人"——这条把"能不能淹死"变成可测的数字。
    /// 如果这条是绿的，说明玩家必须做点什么（闪/挡/换招）才能赢。
    /// </summary>
    private async Task TestMashCannotWin()
    {
        (int pHp, int eHp, int frames) jab = await RunMasher("jab");
        (int pHp, int eHp, int frames) alt = await RunMasher("alternate");
        (int pHp, int eHp, int frames) heavy = await RunMasher("heavy");

        GD.Print($"[INFO] A20 单招 jab：玩家 {jab.pHp} / 敌人 {jab.eHp}（{jab.frames} 帧）");
        GD.Print($"[INFO] A20 交替 jab/重拳：玩家 {alt.pHp} / 敌人 {alt.eHp}（{alt.frames} 帧）");
        GD.Print($"[INFO] A20 重拳为主：玩家 {heavy.pHp} / 敌人 {heavy.eHp}（{heavy.frames} 帧）");

        Check(jab.eHp > 0, $"单招 jab 连打能赢（玩家剩 {jab.pHp}）");
        Check(alt.eHp > 0, $"交替 jab/重拳 连打能赢（玩家剩 {alt.pHp}）—— 免费解仍在");
        Check(heavy.eHp > 0, $"重拳为主 连打能赢（玩家剩 {heavy.pHp}）");
    }

    /// <summary>三种"无脑打法"脚本：只攻击，不闪不挡。</summary>
    private async Task<(int pHp, int eHp, int frames)> RunMasher(string pattern)
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");

        player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 45, enemy.GlobalPosition.Y);
        await Frames(2);

        int frames = 0;
        int step = 0;
        while (frames < 2400 && player.HP > 0 && enemy.HP > 0)
        {
            bool fire = false;
            string action = "light_punch";

            if (pattern == "jab")
            {
                fire = frames % 20 == 0;
            }
            else if (pattern == "alternate")
            {
                fire = frames % 20 == 0;
                action = (step++ % 2 == 0) ? "light_punch" : "heavy_punch";
                if (action == "heavy_punch" && player.Energy < 25)
                {
                    action = "light_punch";
                }
            }
            else // heavy
            {
                fire = frames % 34 == 0;
                action = player.Energy >= 25 ? "heavy_punch" : "light_punch";
            }

            if (fire)
            {
                Input.ActionPress(action);
                await Frames(1);
                Input.ActionRelease(action);
            }

            await Frames(1);
            frames++;
        }

        Input.ActionRelease("light_punch");
        Input.ActionRelease("heavy_punch");
        int pHp = player.HP;
        int eHp = enemy.HP;

        fixture.QueueFree();
        await Frames(1);
        return (pHp, eHp, frames);
    }

    /// <summary>
    /// A19：格挡是"按住"式——在硬直中按下并保持，硬直结束后必须能接上格挡。
    /// 这条防的是"按下边缘被吞掉，之后一直按着也进不去"（实测踩过）。
    /// </summary>
    private async Task TestBlockHeldThroughHitstun()
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");
        EnemyStateMachine esm = enemy.GetNode<EnemyStateMachine>("EnemyStateMachine");

        // 先制造一次硬直（不格挡）
        player.TakeDamage(esm.attackMove.damage, esm.attackMove);
        await Frames(2);

        // 硬直中按下格挡并一直按住
        Input.ActionPress("block");
        await Frames(6);
        Check(!player.blocking, "硬直中不应该立刻进入格挡");

        await Frames(30); // 等硬直结束

        Check(player.blocking, "硬直结束后没能接上格挡（按住式格挡失效）");

        Input.ActionRelease("block");
        await Frames(2);
        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>A14：两招必须可区分——不同 moveId、不同颜色、不同帧数。</summary>
    private async Task TestTwoMovesDistinguishable()
    {
        Node2D fixture = await SpawnFixture();
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");
        EnemyStateMachine esm = enemy.GetNode<EnemyStateMachine>("EnemyStateMachine");

        MoveData primary = esm.attackMove;
        MoveData secondary = esm.secondaryAttack;

        Check(primary != null && secondary != null, "第二招没配");
        Check(primary.moveId != secondary.moveId, "两招 moveId 相同");
        Check(primary.accentColor != secondary.accentColor, "两招颜色相同，玩家分不出来");
        Check(primary.startupFrames != secondary.startupFrames,
            $"两招前摇相同（{primary.startupFrames}），节奏上也没区别");
        Check(secondary.guardBreak, "第二招没有破防标记");

        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>A15：格挡吃到的伤害必须显著降低，且不进硬直。</summary>
    private async Task TestBlockReducesDamage()
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");
        EnemyStateMachine esm = enemy.GetNode<EnemyStateMachine>("EnemyStateMachine");

        MoveData normal = esm.attackMove;
        int raw = normal.damage;

        int hp0 = player.HP;
        player.blocking = true;
        player.TakeDamage(raw, normal);
        int blockedLoss = hp0 - player.HP;

        int hp1 = player.HP;
        player.blocking = false;
        player.TakeDamage(raw, normal);
        int openLoss = hp1 - player.HP;

        GD.Print($"[INFO] A15 格挡吃 {blockedLoss}，裸吃 {openLoss}（原伤害 {raw}）");
        Check(blockedLoss <= Mathf.CeilToInt(raw * 0.15f),
            $"格挡没减伤（吃 {blockedLoss} / 原 {raw}）");
        Check(openLoss == raw, $"裸吃伤害不对（{openLoss} / {raw}）");

        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>A16：一直按着格挡，气会被烧空并自动破防。</summary>
    private async Task TestBlockDrainsEnergy()
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        player.invincible = true;

        int e0 = player.Energy;
        Input.ActionPress("block");

        bool broke = false;
        for (int i = 0; i < 600; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (player.Energy <= 0 && !player.blocking)
            {
                broke = true;
                break;
            }
        }

        Input.ActionRelease("block");
        int minEnergy = player.Energy;
        GD.Print($"[INFO] A16 起始气 {e0}，按满格挡后气 {minEnergy}，自动破防={broke}");
        Check(broke, $"一直格挡没有把气烧空（气仍为 {minEnergy}）");

        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>A17：破防招命中格挡要打穿（全额伤害 + 长硬直）。</summary>
    private async Task TestGuardBreakBreaksBlock()
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");
        EnemyStateMachine esm = enemy.GetNode<EnemyStateMachine>("EnemyStateMachine");

        MoveData br = esm.secondaryAttack;
        int hp0 = player.HP;
        player.blocking = true;
        player.TakeDamage(br.damage, br);
        int loss = hp0 - player.HP;

        Check(loss == br.damage, $"破防招被格挡后只掉 {loss}（应为全额 {br.damage}）");
        Check(!player.blocking, "破防后玩家还在格挡状态");
        Check(player.pendingStunFrames >= 30, $"破防硬直太短（{player.pendingStunFrames}）");

        player.pendingStunFrames = 0;
        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>A18：敌人不能只会一招——两招都要出现。</summary>
    private async Task TestBothMovesUsed()
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        player.invincible = true;
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");
        EnemyStateMachine esm = enemy.GetNode<EnemyStateMachine>("EnemyStateMachine");

        // 站在近处，两招都有机会被选中
        player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 60, enemy.GlobalPosition.Y);

        for (int i = 0; i < 1800; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }

        GD.Print($"[INFO] A18 1800 帧内：直拳 {esm.PrimaryUsed} 次，破防 {esm.SecondaryUsed} 次");
        Check(esm.PrimaryUsed >= 2 && esm.SecondaryUsed >= 2,
            $"两招使用不均（直拳 {esm.PrimaryUsed} / 破防 {esm.SecondaryUsed}）");

        fixture.QueueFree();
        await Frames(1);
    }
    /// <summary>
    /// A12：站在重拳最远射程上连打两下重拳，识破后的反击必须能打到玩家。
    /// 这条防的是"被识破反而安全"——反击够不到 → 打空 → 踉跄 → 玩家白拿 ×3。
    /// 敌人 AI 必须运行，反击才会真正打出来。
    /// </summary>
    private async Task TestReadCounterReachesHeavyRange()
    {
        Node2D fixture = await SpawnFixture();
        Player player = fixture.GetNode<Player>("Player");
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");

        // 站在重拳最远射程附近（重拳能打到 150 左右，敌人普攻只能到 110）
        player.GlobalPosition = new Vector2(enemy.GlobalPosition.X + 150, enemy.GlobalPosition.Y);
        await Frames(2);

        Input.ActionPress("left");
        await Frames(4);
        Input.ActionRelease("left");
        await Frames(4);
        Check(player.facing == -1, $"朝向没转过去（facing={player.facing}）");

        int playerHp0 = player.HP;
        int enemyHp0 = enemy.HP;

        for (int i = 0; i < 2; i++)
        {
            Input.ActionPress("heavy_punch");
            await Frames(1);
            Input.ActionRelease("heavy_punch");
            await Frames(40);
        }

        Check(enemy.HP < enemyHp0, $"重拳在 150 距离没打到敌人（{enemyHp0} → {enemy.HP}）");

        await Frames(80);

        Check(player.HP < playerHp0,
            $"识破后的反击没打到站在重拳射程上的玩家（HP {playerHp0} → {player.HP}）—— 被识破反而安全，读招变成送分");

        Input.ActionRelease("heavy_punch");
        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>A8：交替两招连打必须触发解围——这是"免费解消失"的核心断言。</summary>
    private async Task TestBurstOnAlternating()
    {
        int bursts = await RunAlternatingAndCountBursts(400);
        Check(bursts >= 1, $"交替连打 400 帧没有触发任何解围（免费解仍在）");
    }

    /// <summary>A9：只打两下就收手，压力到不了顶，不应触发解围。</summary>
    private async Task TestNoBurstWhenBackingOff()
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        int before = enemy.BurstCount;

        for (int i = 0; i < 2; i++)
        {
            player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 45, enemy.GlobalPosition.Y);
            Input.ActionPress("light_punch");
            await Frames(1);
            Input.ActionRelease("light_punch");
            await Frames(21);
        }

        Check(enemy.Pressure < enemy.PressureMax, $"打两下压力就满了（{enemy.Pressure}/{enemy.PressureMax}）");
        int afterHits = enemy.BurstCount;

        await Frames(300); // 收手观望

        Check(enemy.BurstCount == before && afterHits == before,
            $"收手期间触发了 {enemy.BurstCount - before} 次解围");

        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>A10：一次压力周期只该结算一次解围，后续命中不能重复触发。</summary>
    private async Task TestSingleBurstPerCycle()
    {
        int bursts = await RunAlternatingAndCountBursts(600);
        Check(bursts == 1, $"一次压力周期触发了 {bursts} 次解围（应为 1）");
    }

    /// <summary>
    /// A7：招式身份必须来自资源里的 moveId，且互不重复。
    /// 这条防的是"AI 读招依赖代码硬编码 / 两个招式共用同一身份"。
    /// </summary>
    private Task TestMoveIdentity()
    {
        PackedScene packed = GD.Load<PackedScene>(FixturePath);
        Node2D fixture = packed.Instantiate<Node2D>();
        Player player = fixture.GetNode<Player>("Player");
        Enemy01 enemy = fixture.GetNode<Enemy01>("Enemy01");

        CombatHandler combat = player.GetNode<InputStateMachine>("InputStateMachine").combatHandler;
        EnemyStateMachine esm = enemy.GetNode<EnemyStateMachine>("EnemyStateMachine");

        MoveData[] moves = { combat.lightMove, combat.heavyMove, esm.attackMove, esm.punishMove };
        var seen = new System.Collections.Generic.HashSet<string>();

        foreach (MoveData m in moves)
        {
            Check(m != null, "有招式槽位是空的");
            Check(!string.IsNullOrEmpty(m.moveId) && m.moveId != "unnamed",
                $"招式「{m.displayName}」没有配置 moveId");
            Check(seen.Add(m.Id), $"moveId 重复：{m.Id}（AI 会把它们当成同一招）");
        }

        fixture.QueueFree();
        return Task.CompletedTask;
    }

    /// <summary>
    /// A6：霸体反打命中后，敌人不能再继续挥这一刀（否则玩家被罚一次还能白拿 ×3）。
    /// 敌人 AI 冻结，手动 Tick 相位机，把玩家拉开 400px 排除"打中/打空"的干扰。
    /// </summary>
    private async Task TestNoStaggerAfterArmorPunish()
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        EnemyStateMachine esm = enemy.GetNode<EnemyStateMachine>("EnemyStateMachine");
        player.invincible = false; // 要真的吃到那 18 点，才会走"架住"分支

        for (int i = 0; i < 3; i++)
        {
            player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 45, enemy.GlobalPosition.Y);
            Input.ActionPress("light_punch");
            await Frames(1);
            Input.ActionRelease("light_punch");
            await Frames(21);
        }

        int expectedDamage = LightMoveOf(player).damage * 2;
        Check(enemy.HP == enemy.maxHP - expectedDamage,
            $"第 3 下没有被架住（敌人 HP={enemy.HP}，期望 {enemy.maxHP - expectedDamage}）");

        player.invincible = true;
        int hpBefore = enemy.HP;
        player.GlobalPosition = new Vector2(enemy.GlobalPosition.X + 400, enemy.GlobalPosition.Y);

        bool sawStagger = false;
        for (int i = 0; i < 90; i++)
        {
            esm.Tick(1.0 / 60.0);
            if (esm.CurrentPhase == EnemyStateMachine.Phase.Stagger)
            {
                sawStagger = true;
            }
            await Frames(1);
        }

        Check(!sawStagger, "架住之后敌人仍进入踉跄（因果倒挂：被罚的玩家反而白拿 ×3）");
        Check(enemy.HP == hpBefore, $"架住之后敌人又掉血：{hpBefore} → {enemy.HP}");

        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>
    /// A5：识破语义——允许 2 次同名命中，**第 3 次同招被架住，伤害无效**（2026-11-04）。
    /// 这条是针对"被识破了却还造成伤害"的回归断言。
    /// </summary>
    private async Task TestReadNullifiesThirdHit()
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        player.invincible = false; // 要验证被架住时玩家会吃到反击

        int enemyHp0 = enemy.HP;
        int playerHp0 = player.HP;
        int expectedDamage = LightMoveOf(player).damage * 2; // 前两下命中，第 3 下被架住

        for (int i = 0; i < 3; i++)
        {
            player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 45, enemy.GlobalPosition.Y);
            Input.ActionPress("light_punch");
            await Frames(1);
            Input.ActionRelease("light_punch");
            await Frames(21);
        }
        await Frames(6);

        Check(enemyHp0 - enemy.HP == expectedDamage,
            $"第 3 次同招不该造成伤害：敌人 {enemyHp0} → {enemy.HP}（期望只掉 {expectedDamage}）");
        Check(player.HP < playerHp0,
            $"被架住后玩家没有吃到反击伤害（玩家 {playerHp0} → {player.HP}）");

        fixture.QueueFree();
        await Frames(1);
    }

    /// <summary>
    /// A4：闪避必须能穿过敌人身体（2026-11-04 追加）。
    /// 做法：把敌人身体放在独立物理层，闪避期间临时把自己的 mask 去掉该层。
    /// </summary>
    private async Task TestDodgePassThrough()
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        uint maskBefore = player.CollisionMask;
        float enemyX = enemy.GlobalPosition.X;

        player.GlobalPosition = new Vector2(enemyX - 40, enemy.GlobalPosition.Y);
        await Frames(2);

        // 先朝右
        Input.ActionPress("right");
        await Frames(3);
        Input.ActionRelease("right");
        await Frames(2);
        Check(player.facing == 1, $"朝向没转过去（facing={player.facing}）");

        // 闪避
        Input.ActionPress("dodge");
        await Frames(2);
        Input.ActionRelease("dodge");
        await Frames(22);

        Check(player.GlobalPosition.X > enemyX,
            $"闪避没能穿过敌人：玩家 X={player.GlobalPosition.X:F1}，敌人 X={enemyX:F1}");
        Check(player.CollisionMask == maskBefore,
            $"闪避结束后碰撞掩码没还原：{player.CollisionMask} != {maskBefore}");

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
        GD.Print($"[INFO] A3 基线（冻结 AI / 玩家无敌）：交替 jab+heavy 击杀满血敌人 = {killFrames} 帧（{killFrames / 60.0:F2} 秒）");
        Check(killFrames < 900, "900 帧内没击杀，输出链路可能断了");
        Check(killFrames >= 150 && killFrames <= 540,
            $"纯输出击杀 {killFrames} 帧（{killFrames / 60.0:F1}s），超出 [2.5s, 9s] 合理区间——检查 HP 与伤害数值");

        int remaining = await RunScriptJabOnly(240);
        GD.Print($"[INFO] A3 对照（冻结 AI / 玩家无敌）：同名 jab 连打 240 帧后敌人剩余 {remaining} HP");
    }

    private async Task<int> RunScriptAlternating(int maxFrames)
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        EnemyStateMachine esm = enemy.GetNode<EnemyStateMachine>("EnemyStateMachine");
        int frames = 0;
        bool pressed = false;

        while (enemy.HP > 0 && frames < maxFrames)
        {
            esm.Pressure.Reset(); // 基线只测纯输出，不让解围介入
            if (frames % 20 == 0)
            {
                bool heavy = (frames / 20) % 2 == 1;
                _wasHeavy = heavy;
                player.GlobalPosition = new Vector2(enemy.GlobalPosition.X - 45, enemy.GlobalPosition.Y);
                Input.ActionPress(heavy ? "heavy_punch" : "light_punch");
                pressed = true;
            }
            else if (pressed)
            {
                Input.ActionRelease(_wasHeavy ? "heavy_punch" : "light_punch");
                pressed = false;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            frames++;
        }

        Input.ActionRelease("light_punch");
        Input.ActionRelease("heavy_punch");
        bool killed = enemy.HP <= 0;

        fixture.QueueFree();
        await Frames(1);
        return killed ? frames : maxFrames;
    }

    private async Task<int> RunScriptJabOnly(int maxFrames)
    {
        (Node2D fixture, Player player, Enemy01 enemy) = await SpawnPassiveFight();
        EnemyStateMachine esm = enemy.GetNode<EnemyStateMachine>("EnemyStateMachine");
        int frames = 0;
        bool pressed = false;

        while (frames < maxFrames)
        {
            esm.Pressure.Reset(); // 对照口径同上
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
