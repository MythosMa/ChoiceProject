# Choice Project — 交接文档（HANDOFF）

> 版本：v1.0  |  日期：2026-09-13  |  用途：把当前进度交接给**新工作区里的 Agent**，使其在**零对话历史**下能无缝接手。
>
> 新 Agent 请先完整读完本文，再按 §5「立即要做的事」开工。本文与 `GAME_DESIGN.md` / `DEV_GUIDE.md` / `PROJECT_DESIGN.md` 配合使用，冲突时以那三份正式文档为准、本文为进度快照。

---

## 0. 最重要的角色约束（务必遵守，违反即失败）

**用户本人编写所有代码。Agent 绝不把代码文件（`.cs` / `.tscn` / `.tres`）写进工程。** Agent 的职责严格限定为：

1. **设计 / 规划 / 技术顾问 / 美术方向**——出主意、定方案、拧设计理念。
2. **代码骨架只写在对话里**，由用户自己敲进工程。
3. **代码 review**——读用户的文件、找 bug、给修改建议（同样只给在对话里，不代写）。
4. **唯一可由 Agent 直接编辑的文件 = `doc/*.md`**（GAME_DESIGN.md、DEV_GUIDE.md、PROJECT_DESIGN.md、本 HANDOFF.md）。

其他约定：

- 用户用**中文版 Godot 编辑器**。所有编辑器操作指令用**中文 UI 术语**（如"唯一化""本地到场景""检查器"），但**节点类名保留英文**（Area2D / CollisionShape2D / CharacterBody2D / Node 等，Godot 不翻译类名）。
- **工具坑**：`Grep` / `Glob` 默认指向主工作目录，**不是**本项目目录。调用时**必须显式传 `path`** 到 `/Users/mayuan/workroom/person_game/choice_project/...`；`Read` 用绝对路径正常。
- 诚实优先：用户说"已完成"但文件没变化时（曾发生过编辑器未保存），如实指出、不要假装通过。术语上尊重用户的工作方法（例：用户用"唯一化"，不要硬套"本地到场景"）。

---

## 1. 项目是什么

- **名称**：choice_project
- **技术栈**：Godot 4.x + C#（.NET），2D
- **类型**：类银河城（Metroidvania）
- **差异化卖点**：格斗游戏式的**「择」**——每次交手是一次**有风险的即时判断**（读对高回报），**不是搓招/连招执行**。执行层（搓招）只是"表达选择"的工具，必须克制。
- **项目路径**：`/Users/mayuan/workroom/person_game/choice_project/`
- **是否 Git 仓库**：主工作目录非 git 仓库（项目目录情况未核实）。

---

## 2. 文档地图（接手先读）

| 文档 | 版本 | 内容 |
|------|------|------|
| `doc/GAME_DESIGN.md` | v0.8 | 产品设计：择的定义(§2.1)、搓招≠择(§2.1.1)、**择=破招决策记录(§2.1.2)**、三层择(§2.2)、控制映射(§3.2)、敌人设计(§5)、**敌人行为准则/出招选择规则(§5.3)**、核心风险与止损线(§9) |
| `doc/DEV_GUIDE.md` | v0.13 | 开发文档：目录结构、消费契约(§4.1)、反馈系统修订(§4.4)、**实现约定/形状独立化/碰撞分层(§4.5)**、Phase 进度与回溯(§七) |
| `doc/PROJECT_DESIGN.md` | — | 项目级设计（本文未展开，需要时自行阅读） |
| `doc/HANDOFF.md` | v1.0 | 本文，进度快照 + 交接 |

---

## 3. 方法论与 Phase 状态

- **方法论 = tracer-bullet（曳光弹）**：先把**一个直球型敌人端到端**打通（出题→读→择→payoff 全链路），从做的过程里**反推/提取**框架（EnemyBase、AI、信号），**不预先搭抽象**。出招表（roster）待第一个敌人定型后**反推**，不提前设计（§2.1.2："在知道问题之前先写答案"是错的）。
- **Phase 1**：✅ 完成（垂直切片 + 出招系统基础池：帧数据驱动播放、命中判定、打击反馈、连段路由 substrate + 手感）。
- **Phase 2**：🔄 进行中。切入序列 S1→S4：

| 步 | 内容 | 状态 |
|----|------|------|
| **S1** | 玩家可受击（Hurtbox + IDamageable + HP + 碰撞分层） | ✅ 完成（运行时验证并入 S2） |
| **S2** | 一个直球型敌人端到端（预兆→攻击→固定破绽）+ 玩家受击闭环 | 🔄 进行中（见 §4） |
| **S3** | 反推最小确反 roster（出招表由敌人破绽逼出） | ⏳ 未开始 |
| **S4** | 确反 payoff 差异化（确反窗口 → 伤害/反馈强度差） | ⏳ 未开始 |

---

## 4. S2 细分进度

| 子步 | 内容 | 状态 |
|------|------|------|
| **S2-1** | 敌人本体 + 出题循环（`Idle→预警→攻击→破绽`，颜色当预兆） | ✅ 完成并测试通过 |
| **S2-2** | 敌人攻击驱动器（EnemyStateMachine 驱动自己的 Hitbox）+ hitstop 单计数重构 | ✅ 完成并测试通过 |
| **S2-3** | 玩家受击态（HitstunHandler + 请求化中断 + 优先级门） | 🔄 **已接线，但有时序缺陷，修复方案已给出、尚未应用/验证**（见 §5） |
| **S2-4** | 闪避冷却 + i-frames（防御臂闭合，"读错但闪掉=免伤"生效） | ⏳ 未开始 |
| **S2-5** | 敌人可受击（Hurtbox）+ 确反 payoff（破绽窗内命中伤害×2） | ⏳ 未开始 |
| **S2-6** | 碰撞分层收口 + 调试场景 + S1 遗留运行时验证 | ⏳ 未开始 |

---

## 5. 立即要做的事（接手第一件）：修 S2-3 受击态时序缺陷

### 缺陷现象
被敌人命中后，`TakeDamage` 立即置 `_hitstunRequested=true` 并打印伤害，但**过了很多帧才真正切到 HitstunHandler**，期间玩家仍冻结在原状态（出招中就是暗红），不符合 §2.1.2「被命中 = 立即中断当前招式」。

### 根因（两条叠加）
1. **门的顺序错**（`Player.cs` `_PhysicsProcess`，当前仍是旧版）：
   ```csharp
   if (_hitstunRequested) {
       if (FeedbackSystem.Instance.IsHitstopActive) return;  // ← 等顿帧结束才切，导致延迟
       _hitstunRequested = false;
       inputStateMachine.ForceHitstun();
       return;
   }
   ```
   受击切换被"等顿帧结束"挡住。
2. **`Enemy01Attack.tres` 的 `hitstopFrames = 60`**（1 秒，调试用放大值；规格占位是 **6**）。旧门下这 60 帧全成了切换前的延迟。

### 修复方案（给用户敲，Agent 不代写）

**(a) 改 `Player.cs` 的 `_PhysicsProcess` 顶部** —— 受击请求一来就**立即** `ForceHitstun`（当场打断当前招），随后让顿帧在**受击态里**冻结；其余移动逻辑不动：

```csharp
public override void _PhysicsProcess(double delta)
{
    // 受击请求:立即切受击态,当场打断当前招(不等顿帧)——符合 §2.1.2「被命中=立即中断」
    if (_hitstunRequested)
    {
        _hitstunRequested = false;
        inputStateMachine.ForceHitstun();
    }

    // 顿帧/闪避门:此刻已在受击态,TryDodgeInterrupt 被 guard 拒绝 → 直接进顿帧冻结(定格受击红姿势)
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
    inputStateMachine.Tick(delta);
    if (moveAxis != 0) { facing = moveAxis > 0 ? 1 : -1; }
    Vector2 velocity = Velocity;
    if (!IsOnFloor()) { velocity += GetGravity() * (float)delta; }
    if (jumpRequested && IsOnFloor()) { velocity.Y = JumpVelocity; }
    velocity.X = moveAxis != 0 ? moveAxis * Speed : Mathf.MoveToward(Velocity.X, 0, Speed);
    Velocity = velocity;
    MoveAndSlide();
}
```

**(b) 把 `Enemy01Attack.tres` 的 `hitstopFrames` 从 60 调回 ~6**（60 是观察用，真实手感太长）。

### 修复后期望日志顺序
```
被攻击： -12, HP: 88 / 100
Transitioned to HitstunHandler      ← 紧接着(下一帧),不再隔很多帧
（顿帧定格~6帧 + 硬直20帧）
Transitioned to MovementHandler     ← 变白
```
出招中(暗红)被打 → **立刻**变受击红并定格 → ~26 帧后变白回 movement。

### 验证清单
- [ ] 玩家**出招中**被命中 → 当前招**当场被打断**（判定框关、颜色从出招色立即切受击红）。
- [ ] 硬直期间狂按闪避 → **闪不出去**（`TryDodgeInterrupt` 的 guard 已含 `|| currentInputReceiver == hitstunHandler`）。
- [ ] 硬直期间方向/攻击键 → 无响应（锁输入）。
- [ ] HP 下降；连被命中 → 硬直重置（不会被无限锁，敌人 cadence ≫ 硬直）。

> 调完让用户测"出招中被打断"，确认日志顺序对了，S2-3 才算闭合，再进 S2-4。

---

## 6. 本会话锁定的设计决策（已落档 GAME_DESIGN，勿重开）

1. **受击必须打断当前动作（受击硬直）** —— §2.1.2 决策记录(2026-09-12)。被命中 = 中断当前招式 + 进受击硬直 + 扣血。堵死"硬拼换血"最优解（进攻侧）。
2. **闪避 = 无责释放 + 冷却** —— §2.1.2 决策记录(2026-09-13)。不耗资源、出手不被惩罚，唯一代价是冷却；定位是"择错后的**反应性弥补**"，非默认站姿。**承重旋钮 = 「闪避冷却 > 敌人攻击间隔」**，以此保证存在"闪避在 CD、敌招已到"的窗口，逼出确反的必要性（堵死"啥都闪"最优解，防御侧）。直球敌人 #1 验证不了这条（单发可反应、压不到冷却），敌人 #2 才显形。
3. **敌人出招选择 = 有界招式池 + 上下文加权随机 + 可读倾向 + 轻微反重复** —— §5.3 决策记录(2026-09-13)。"不随机"的体感来自分布**有界 + 可读**而非确定性。反直觉技巧：纯 i.i.d. 随机在人感知里更扎堆，加轻微反重复（刚出过的招本轮权重压低）反而**感觉更随机更公平**。
4. **「缝」模型** —— 择 = 「必须提交(commit)的时刻」与「答案揭晓的时刻」之间那段缝。缝=0 → QTE；缝=全程 → 抛硬币；择活在中间。预警(telegraph)拆两维：**「何时」永远清晰**（公平），**「何种」可延迟揭晓**（择的来源）。roster 难度曲线 = 逐个敌人调缝宽。
5. **中断优先级** —— 受击 > 闪避 > 顿帧门 > 普通 Tick；例外：闪避 i-frame 内被命中 → 命中无效（S2-4 在 `TakeDamage` 层实现，非无敌帧才被命中触发受击）。
6. **payoff 三角** —— 读对→确反打伤害；读错但闪掉→免伤；读错没闪→被打断+扣血。

---

## 7. 架构速查（新 Agent 上手必读）

### 玩家侧状态机
- `IInputReceiver` 接口（`Enter()` / `Exit()` / `Tick(double)`），定义在 `InputStateMachine.cs`。
- `InputStateMachine`（Node）持有 `currentInputReceiver`，`TransitionTo(next)` 先 `Exit()` 旧态再 `Enter()` 新态。**`Exit()` 是清理契约**（如 CombatHandler.Exit 关判定框 + 复位颜色）——这是"任意状态可被安全中断"的基础，加新中断源不回炉。
- Handlers（各自 `.tscn` + `.cs`，实例化为 InputStateMachine 子节点，NodePath 接 export）：
  - `MovementHandler` — 移动/跳跃。
  - `CombatHandler` — 出招/连段。enum `Segment{Startup,Active,Recovery}` 由帧数算；读 `InputManager.Buffer` 做连段路由（`ResolveNext`）；驱动玩家 `Hitbox`（`Configure` + `SetActive`）。
  - `DodgeHandler` — 闪避（`dodgeFrames`，已修过"恒真"bug）。
  - `HitstunHandler` — 受击硬直（`hitstunFrames=20`，Enter 闪红、Exit 复位白、Tick 数帧→回 movement）。
- `InputStateMachine` 关键方法：`TryDodgeInterrupt()`（可在顿帧门前调用，活边缘；guard 拒绝"已在闪避/已在受击"）、`ForceHitstun()`（= `TransitionTo(hitstunHandler)`）、`Tick()`（movement 态遇攻击边沿 → combat）。
- `Player.cs`：`IDamageable`，`maxHP=100`；`TakeDamage` 扣血 + 置 `_hitstunRequested`（**请求化**，不在信号回调里直接转状态）；`_PhysicsProcess` 顶部按优先级门消费。

### 战斗判定件（哑组件，驱动器无关）
- `Hitbox`（Area2D，`Monitoring=true`）：`Configure(MoveData)` 设位置/尺寸/记录 move；`SetActive(bool)` 开关判定；`OnAreaEntered` → 若撞到 `Hurtbox` 则 `hurtbox.Receiver.TakeDamage(move.damage)` + `RequestHitstop` + `RequestShake`。**玩家和敌人共用同一个 Hitbox 组件**，区别只在"谁驱动它"（玩家=CombatHandler，敌人=EnemyStateMachine）。
- `Hurtbox`（Area2D，`Monitoring=false / Monitorable=true`）：`Receiver = GetParent() as IDamageable`（**必须是 IDamageable 节点的直接子节点**）。
- `IDamageable` 接口（文件名有 typo `IDamanageable.cs`）：`void TakeDamage(int)`。

### MoveData（`scripts/data/MoveData.cs`，`[GlobalClass]` Resource）
字段：`moveName`；`startupFrames/activeFrames/recoveryFrames`（=预警/攻击/破绽，`TotalFrames` = 三者之和）；`damage/hitstopFrames/shakeMagnitude/knockback`；`hitboxOffset/hitboxSize`；`lockMovement`；`canBeCancelled/cancelWindowStart/cancelWindowEnd`（Phase 2 留字段**不接线**）。玩家和敌人的攻击都用它。

### 碰撞分层（`project.godot` 已命名，值 = 2^(层-1)）
| 层 | 名 | 值 | 占用 |
|----|----|----|------|
| 1 | world | 1 | 地形 + CharacterBody2D 身体碰撞 |
| 2 | player_hurtbox | 2 | 玩家 Hurtbox（layer=2, mask=0） |
| 3 | player_hitbox | 4 | 玩家 Hitbox（layer=4, mask=8） |
| 4 | enemy_hurtbox | 8 | 敌人 Hurtbox（layer=8, mask=0）— **S2-5 待建** |
| 5 | enemy_hitbox | 16 | 敌人 Hitbox（layer=16, mask=2） |

规则：monitoring 的 Hitbox 的 **mask** 要覆盖 monitorable 的 Hurtbox 的 **layer**。

### FeedbackSystem（Autoload，`core/FeedbackSystem.cs`）
- **hitstop 单计数**（S2-2 重构）：`_hitstopFrame` 在 `_PhysicsProcess` 里**自减**（与 shake 同构），对外只暴露 `public bool IsHitstopActive => _hitstopFrame > 0;`。`RequestHitstop(frames)`（取较大值）、`CancelHitstop()`（清零）。**已删除 `ConsumeHitstop()`**——任何消费者只读 `IsHitstopActive` 冻结，绝不自己减（否则双重递减）。玩家与敌人都在各自 `_PhysicsProcess` 顶部读它冻结。
- shake：`RequestShake(frames, magnitude)`，`_PhysicsProcess` 里衰减 `cam.Offset`， magnitude 用 `_shakeMagnitude * t`（不是 `_shakeFrames * t`，曾修过）。
- Autoload：`InputManager`、`FeedbackSystem`。

### InputManager（Autoload，`core/InputManager.cs`）
- 每物理帧采样按键位掩码 → `Current`，上一帧 → `Previous`；变化时追加 `InputRecord` 到 `Buffer`（容量 60）。
- `Tools.GetPressed(prev,cur) = cur & ~prev`（**上升沿**，按住不重复触发）；`GetReleased = prev & ~cur`。
- `InputButtons`：方向 + LP/HP/LK/HK（攻击占 bit4~7）+ Jump/Dodge/Block。输入动作：left/right/up/down、light_punch(Z)/heavy_punch(X)/light_kick(A)/heavy_kick(S)、jump(Space)、dodge(Shift)、block(C)。

### 敌人侧（`scenes/characters/enemy/enemy01/`）
- `Enemy01`（CharacterBody2D）：`[Export] enemyStateMachine`、`[Export] attackHitbox`（NodePath→Hitbox，运行时由 Godot 赋值）；`_PhysicsProcess` 先 `if (IsHitstopActive) return;` 再 `enemyStateMachine.Tick(delta)`（顿帧期间敌人也冻结）。
- `EnemyStateMachine`（Node）：enum `Phase{Idle,Startup,Active,Recovery}` 循环；`attackMove`(MoveData) + `idleFrames`；`EnterPhase` 里 `Startup` 时 `attackHitbox.Configure(attackMove)`、并 `attackHitbox.SetActive(next==Active)`；`ApplyTelegraph` 用 Sprite 颜色画四相位（**颜色语言与 CombatHandler 一致**：暗红/橙/灰 + Idle 白）。
- **注意**：用 enum 相位机，**没有**复用 `IInputReceiver`（那是玩家输入专用）。敌人 #2 需多态行为时再抽 `IEnemyState`。

---

## 8. S2 数值表（占位，全部待反复调优；锚定街霸6 + 为银河城可读性放宽 ~1.5-2x）

前提：Godot 物理 60Hz，帧 ↔ SF6 帧 1:1。

**敌人 #1 攻击（`Enemy01Attack.tres` 当前实际值）**
| 字段 | 当前值 | 规格占位 | 说明 |
|------|--------|----------|------|
| startupFrames（预警） | 30 | 30 | SF6 Drive Impact ~26f，放宽到 0.5s 可读 |
| activeFrames（攻击） | 6 | 6 | 攻击判定窗 |
| recoveryFrames（破绽=确反窗） | 40 | 40 | ⭐承重；SF6 whiff 确反 ~15-20f，放宽 2x 教学，S4 收紧 |
| damage | **12** | 10 | 用户改成 12 |
| hitstopFrames | **60** | **6** | ⚠️ 60 是调试放大值，**修 S2-3 时调回 ~6** |
| shakeMagnitude | 8(默认) | 8 | |
| knockback | (0,0) | (0,0) | A4-3 击退仍押后 |
| hitboxOffset / hitboxSize | (20,0) / (24,16) | 同 | #1 朝 +X 固定出招（不转身），玩家须站敌人右侧才吃得到 |

**玩家受击 / 闪避 / 经济（规格占位，部分待 S2-4/5 接线）**
| 项 | 值 | 状态 |
|----|----|------|
| hitstunFrames（受击硬直） | 20 | 已在 HitstunHandler |
| 是否全可打断 | 全可打断（#1 无霸体） | 霸体留作后期单点守卫 |
| dodgeFrames（闪避总位移） | 12 | 已有 |
| iFrames（无敌帧） | 前 10f | **S2-4 待接** |
| cooldownFrames（闪避冷却） | 60 | **S2-4 待接**；⭐承重旋钮，须 > 敌人攻击间隔 |
| 玩家 maxHP | 100 | 已有 |
| 敌人 #1 maxHP | 50（建议） | **S2-5 待接**（敌人 Hurtbox + IDamageable） |
| 玩家普攻 damage | 10（MoveData 默认） | 已有 |
| 确反倍率（破绽窗内命中） | 2.0 → 20 | **S2-5 待接**；S4 payoff 差异化先留此 hook |

敌人 #1 单发节奏 ≈ 30+6+40 + idle(~30) ≈ 106f ≈ 1.77s/次。

---

## 9. 已知小问题 / typo / 遗留

- **`HitstunHandler.tscn` 根节点名 = `HistunHandler`**（少个 t）。export 已对上（`InputStateMachine.tscn` 里 `hitstunHandler = NodePath("HistunHandler")`）能跑；若改名须同步该 NodePath。
- **输入吞边沿**（独立于 S2-3 bug，先记着）：`Player._PhysicsProcess` 在顿帧门/受击门会提前 `return`（不调 `Tick`），而 `InputManager` 仍每帧推进 `Previous/Current`。结果：**顿帧或硬直期间按下的攻击，其上升沿会被吞掉**（回到 movement 时 `Previous==Current`，无新边沿）。现在无碍；S3 做"硬直/顿帧后接招"或输入缓冲时会变成"我按了却没出招"的手感问题——届时要么在受击/顿帧期暂存攻击边沿，要么改成消费 Buffer 而非实时边沿。
- **死亡无处理**：`TakeDamage` 里 `_hp<=0` 只打印 "Game Over!"，无死亡态/停输入。后续补。
- **DodgeHandler 曾有"恒真"bug**（`_frame>=0`）→ 用户已修（现用 `dodgeFrames`）。
- 已修复项（见 DEV_GUIDE §4.4 / §七）：shake magnitude、CancelHitstop 只清顿帧不清震屏、调试打印清理、闪避修复+DodgeHandler 状态化、形状独立化（玩家 Hurtbox 用"唯一化"生成私有 `RectangleShape2D`）。

---

## 10. 下一步路线

1. **修 S2-3**（§5）→ 用户测"出招中被打断"通过。
2. **S2-4**：闪避冷却 + i-frames。要点：`TakeDamage` 在 i-frame 内直接不触发受击（免伤）；冷却闸门实现"闪避不能连用"；承重旋钮「冷却 > 敌人 cadence」。
3. **S2-5**：敌人加 Hurtbox（layer=8, mask=0）+ 实现 `IDamageable`；玩家确反在敌人 Recovery 段命中 → 伤害×2（payoff hook）。
4. **S2-6**：碰撞分层收口 + 调试场景（玩家 + Enemy01 + 木桩）+ 补 S1 遗留运行时验证（双向命中、无自伤、无误伤）。
5. **S3**：反推最小确反 roster（出招表由敌人破绽逼出）。
6. **S4**：确反 payoff 差异化（确反窗口大小 → 伤害/反馈强度差）。

**押后/不做（避免 §9.2 漂移：择→搓招）**：正式出招表、序列匹配器(MoveResolver)、task6 路由树、task8 cancel、A4-3 击退、FeedbackConfig.tres、完整动画手感调校——这些待敌人定型、破招语义清楚后再说。**每次迭代对照 §2.1.1 标尺："我在增加执行难度，还是选择空间？"**

---

## 附：S2-3 之后建议同步更新

- 修完 S2-3，把"受击立即打断（顿帧在受击态内定格）"的实现约定补进 `DEV_GUIDE.md §4.x`，并把 `hitstopFrames` 调回 6 的事记一笔。
- S2 全部完成后，在 `DEV_GUIDE.md §七` 更新 Phase 2 进度、勾选 S2、记录 tracer-bullet 提取出的可复用件。
