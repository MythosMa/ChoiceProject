# Choice Project - 开发文档（Development Guide）

> 版本：v0.13  |  日期：2026-09-13  |  状态：Phase 1 实现期（垂直切片完成，出招基础池开发中）

---

## 一、技术选型

| 项目 | 选择 |
|------|------|
| 引擎 | Godot 4.x |
| 语言 | C# |
| 版本控制 | Git |
| Demo阶段美术 | 代码驱动色块/几何体 |

---

## 二、项目目录结构

按实体/节点归类，每个单元自成一个文件夹，包含其场景文件（.tscn）、脚本（.cs）和专属资源。以下为当前实际结构（2026-08-15）：

```
choice_project/
├── project.godot
├── choice_project.sln / .csproj      # C# 工程文件
├── .editorconfig
├── icon.svg
│
├── core/                             # 全局 Autoload 系统
│   └── InputManager.cs               # 输入采集：位掩码快照 + 输入缓冲（纯记录，不仲裁）
│
├── scenes/                           # 按实体归类，.tscn 与 .cs 放一起
│   ├── characters/
│   │   └── player/
│   │       ├── Player.tscn/.cs               # CharacterBody2D：物理积分、意图接口
│   │       ├── InputStateMachine.tscn/.cs    # 输入仲裁/路由：全局触发器、状态切换
│   │       ├── MovementHandler.tscn/.cs      # 移动状态处理器
│   │       ├── DodgeHandler.tscn/.cs         # 闪避状态处理器（位移/无敌帧的未来承载点）
│   │       └── CombatHandler.tscn/.cs        # 战斗状态处理器（择窗口、招式路由）
│   ├── ui/
│   │   └── input_test/               # 输入调试 UI（InputScreen + InputInfo）
│   └── test_scenes/                  # 测试场景（test_scene_move 等）
│
├── scripts/                          # 纯逻辑/数据，不依赖场景节点
│   ├── data/                         # 输入数据类型（InputButtons/Direction/AttackType/InputRecord）
│   └── tools/                        # 静态工具函数（Tools.cs）
│
├── assets/                           # 共享素材
│   └── textures/white.png            # 唯一占位纹理（色块 = 白纹理 + Scale + Modulate）
│
└── doc/                              # 项目文档（GAME_DESIGN / DEV_GUIDE / PROJECT_DESIGN）
```

**目录原则：**
- scenes/ 下每个实体单元自包含，场景与脚本放在一起；未来的敌人、Boss、机关按同样原则增设（如 scenes/characters/enemies/、scenes/mechanisms/）
- 需要注册为 Autoload 的全局系统放 core/
- 不属于任何场景的纯数据结构和静态工具放 scripts/
- 测试场景放 scenes/test_scenes/，正式关卡后续单设
- 配置参数（.tres）随使用方模块存放；全局手感/数值配置届时在 core/config/ 下集中（Phase 1 引入）

---

## 三、系统架构

### 3.1 模块职责与边界

```
┌─────────────────────────────────────────────────────────┐
│                     GameManager                          │
│  职责：游戏生命周期管理、暂停/运行状态、场景加载          │
└───────────────────────┬─────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┐
        │               │               │
        ▼               ▼               ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│ InputManager │ │ CombatSystem │ │ FeedbackSys  │
│              │ │              │ │              │
│ 职责：       │ │ 职责：       │ │ 职责：       │
│ · 采集原始输入│ │ · 连招路由   │ │ · 顿帧       │
│ · 位掩码快照 │ │ · 伤害计算   │ │ · 震屏       │
│ · 输入缓冲   │ │ · 确反判定   │ │ · 击退       │
│ (纯记录,不仲裁)│ │ · 资源管理   │ │ · 音效触发   │
└──────┬───────┘ └──────┬───────┘ └──────┬───────┘
       │                │                │
       ▼                ▼                ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│   Player     │ │    Enemy     │ │     UI       │
│              │ │              │ │              │
│ 职责：       │ │ 职责：       │ │ 职责：       │
│ · 输入仲裁路由│ │ · AI状态机   │ │ · 血条/气槽  │
│   (状态机侧) │ │ · 出招预兆   │ │ · 调试信息   │
│ · 角色移动   │ │ · 出题策略   │ │              │
│ · 战斗执行   │ │              │ │              │
│ · 动画表现   │ │              │ │              │
└──────────────┘ └──────────────┘ └──────────────┘
```

### 3.2 核心数据流

```
玩家按键输入
    │
    ▼
InputManager（全局采集：Current 位掩码快照 + Buffer 历史，不做仲裁）
    │
    ▼
Player._PhysicsProcess → InputStateMachine.Tick（仲裁路由）
    │
    ├──→ MovementHandler（移动意图：moveAxis / 跳跃请求）
    │         │
    │         ▼
    │    Player 物理积分（重力 + 意图 → MoveAndSlide，唯一调用点）
    │
    └──→ CombatHandler（择窗口 → 招式路由）
              │
              ▼
         HitboxManager（生成攻击判定）
              │
              ▼
         DamageCalculator（计算伤害 + 确反判定）
              │
              ├──→ FeedbackSystem（触发打击反馈）
              ├──→ EventBus（发出事件：命中/确反/连招开始结束）
              └──→ UI（更新显示）
```

---

## 四、核心模块设计要点

### 4.1 输入系统（InputManager + InputStateMachine）

这是本项目技术风险最高的模块。架构上分为两层（2026-08 决策，已实现）：

**InputManager（全局 Autoload，纯采集层）**
- 每物理帧采样 Input Map 动作，压缩为 InputButtons 位掩码（Current/Previous）
- 输入变化时生成 InputRecord 写入 Buffer（60 帧容量）；未变化时累加末条记录的 DurationFrames
- 不含任何游戏逻辑、不知道玩家状态——同一帧内任何消费方读到的数据一致
- 跳跃/闪避/格挡为系统键位（bit 8~10），与方向、攻击键分区隔离
- **消费契约（核心原理，2026-09-11 顿帧修复实战印证）：** 凡是 tick 可能被冻结的消费场景（如顿帧期间的连段判定），消费者**必须从 Buffer 日志回看输入，不能在自己 tick 里用 `GetPressed(Previous, Current)` 重新采样实时边缘**——该 tick 被冻结时边缘会丢失（顿帧吞连段输入即此因，已修复，见 §七）。InputManager 的记录通道独立于 Player、不被顿帧冻结，意图得以保留。一句话：**输入只管记录，消费只管从记录里拿，不另起炉灶再查一遍实时输入。**
  - **同一原理下的两种手法（2026-09-12 闪避修复厘清）：** 契约的本质是"把输入采集与会被冻结的时钟解耦"，落地有两条路——
    - **(1) 消费者留在冻结区内 → 回读 Buffer 日志**：连段攻击走这条（`ResolveNext` 回扫 Buffer，见 §七 顿帧修复）。适用于消费者本身必须在 tick 内、且 `InputRecord` 记录了对应键位（方向 + 攻击）的情况。
    - **(2) 消费者可移到冻结区外 → 把判定前移到顿帧闸门之前，直接读实时边缘**：闪避打断走这条。`Player._PhysicsProcess` 顶部、`ConsumeHitstop()` 之前先调 `InputStateMachine.TryDodgeInterrupt()`，命中即切到闪避态并 `CancelHitstop()`，否则才进顿帧 `return`。
    - **为何闪避用 (2) 而非 (1)：** `InputRecord` **只存方向 + 攻击（AttackType），不存系统键（闪避/跳跃/格挡）**，Buffer 根本无法重建闪避边缘；且闪避判定天然可外提（它只需"这一帧有没有按闪避"，不依赖招式播放进度）。故不强行扩 Buffer，而是把检查点搬到冻结之外——更省、更直接。

**InputStateMachine（Player 子节点，仲裁路由层）**
- 不挂自己的 _PhysicsProcess，由 Player._PhysicsProcess 显式调用 Tick，保证帧内顺序：清意图 → 状态机路由 → 物理积分（MoveAndSlide 只在 Player 调用）
- 持有当前活跃的 IInputReceiver，TransitionTo 统一执行 Exit → Enter，是所有状态切换的唯一入口
- 全局触发器由状态机直接处理：攻击键按下边缘 → 进入战斗；UI 焦点闸门（GuiGetFocusOwner 非空时停止派发）
- **闪避打断独立成 `TryDodgeInterrupt()`（2026-09-12）：** 闪避判定**不再放在 `Tick` 内**（`Tick` 会被顿帧冻结），而是抽成单独方法，由 `Player._PhysicsProcess` 顶部、顿帧闸门**之前**调用——闪避键按下边缘 → `TransitionTo(dodgeHandler)` 并返回 true，调用方据此 `CancelHitstop()`。这样顿帧期间也能即时闪避。`Tick` 中原有的闪避分支已移除（手法 (2)，见上方消费契约）。
- handler 引用在状态机的 _Ready 中注入（子节点 _Ready 先于父节点，handler 不得自行向上取引用）；`dodgeHandler` 同样在 _Ready 经 `Initialize(player, this)` 注入

**状态与方向键职责：**
- **MovementHandler（移动）**：方向键 = 移动，跳跃 = 独立键（空格），攻击键不在此处理
- **CombatHandler（战斗）**：方向键 = 招式路由。攻击键边缘进入战斗 → 按 MoveData 帧数据播放招式（Startup→Active→Recovery）→ **播放结束帧立即二选一**：有预输入攻击且未达链长上限则无缝接续下一段，否则即刻 `TransitionTo(movementHandler)` 还移动，**无独立 Listening 空窗**（切片期"开窗约30帧、超时返回"模型已于 2026-09-11 移除，见 §七 手感修复）。闪避边缘始终优先，可即时中断播放。
- **DodgeHandler（闪避，2026-09-12 状态化）**：闪避从原先的"`TransitionTo(movementHandler)` 即退即回"**升级为独立的 `IInputReceiver` 状态**，自带时长 `dodgeFrames`——这是承载未来闪避内容（位移、无敌帧）的**接缝**。当前仅落地：`Enter` 复位帧计数、`Tick` 内 `player.moveAxis = player.facing`（按朝向位移）、计时到则 `TransitionTo(movementHandler)` 还移动。**未做（押后 Phase 2）：** 无敌帧（`Enter`/`Exit` 已留 TODO 钩子，需玩家 Hurtbox + 敌人才有验证载体）、位移速度覆盖（暂复用移动 Speed，手感调校时再单独给闪避加速度）。

> **最小 facing（2026-09-12 引入，仅为闪避方向服务）：** `Player` 加 `int facing`（1=右 / -1=左），在 `InputStateMachine.Tick` 之后由 `moveAxis` 符号更新（`if (moveAxis != 0) facing = moveAxis > 0 ? 1 : -1;`）。DodgeHandler 据此实现"面朝哪边、往哪边闪"。
> **边界（重要）：** 这是**最小种子**，不是完整朝向系统。**Sprite 翻转表现 + 面向相对的"前/后"招式路由仍押后 Phase 2**——与 §支柱二 的结论一致：当前无对手，选招用绝对方向（Up/Down/Left/Right），"前/后"语义留到对敌时再上。facing 现阶段只喂闪避位移，不接管任何视觉或选招逻辑。

**关键参数（全部外部化配置，不硬编码）：**
- 择窗口帧数、输入缓冲帧数、状态切换延迟、变招cancel窗口、方向输入阈值

**验证重点：** Phase 1 全力投入调校窗口仲裁与路由的手感，这是整个项目的地基。

### 4.2 ComboSystem（连招系统）

**核心问题：** 玩家输入序列如何映射到连招路由，以及如何实现连段中的变招（cancel）。

**架构要点：**
- 连招定义为一棵路由树，每个节点关联一个招式
- 支持在特定窗口内cancel当前连段，切换到新分支
- 路由树结构通过配置文件定义，不硬编码

**待定内容（需进一步设计）：**
- 连招种类与数量
- 搓招表（输入序列与招式的映射关系）
- 各层招式的帧数据（前摇、持续、后摇）
- 变招cancel窗口的具体位置

### 4.3 敌人AI

**核心问题：** 敌人需要能"出题"，让玩家做"择"的判断。

**AI状态机：** Idle → Signal（出招预兆）→ Attack → Recovery（破绽）→ Idle

**出招预兆系统：** 每个敌人在攻击前必须发出可读取的视觉信号，信号与最优确反招式关联。

**出题策略：** 初期做确定性出题（固定pattern），验证"择"成立后再考虑随机性和适应性AI。

### 4.4 FeedbackSystem（反馈系统）

**核心问题：** 让"择对了"有足够的体感回报。

**反馈手段：** 顿帧（hitstop）、屏幕震动、击退位移、音效、视觉特效（色块变化）。

**关键设计：** 最优确反的反馈强度要明显高于普通命中，让玩家能通过体感区分"择对了"和"只是打到了"。

> **实现进度（2026-09-03）：** A4-1 顿帧、A4-2 震屏已落地并验收。FeedbackSystem 作为全局 Autoload 引入（`core/FeedbackSystem.cs`，单例样板同 InputManager），承载 hitstop + shake。
> - **顿帧机制：** 命中（`Hitbox.OnAreaEntered`）→ `RequestHitstop(move.hitstopFrames)`；`Player._PhysicsProcess` 顶部 `ConsumeHitstop()` 返回 true 即 `return` 冻结整帧（不清意图、不派发 tick、不积分）。**用帧计数器递减，不用 `Engine.time_scale`**，与状态切换/招式帧同一时钟。多次请求取较大帧数，避免短顿帧打断长顿帧。
> - **震屏机制：** 命中 → `RequestShake(move.hitstopFrames, move.shakeMagnitude)`（时长暂复用 hitstopFrames，强度走 MoveData 字段）；由 **FeedbackSystem 自己的 `_PhysicsProcess`** 驱动相机 `Offset`——不在 Player tick 内，故顿帧冻结玩家时相机照常抖（定格 + 抖动同步 = 打击感）。强度随剩余帧线性衰减，收尾帧复位 `Offset = Zero` 防画面卡偏；用 `Offset` 不用 `Position`，将来相机跟随玩家时互不干扰。相机经 `GetViewport().GetCamera2D()` 动态获取，免去挂脚本 + 注册。测试场景已加 Camera2D（固定摆位，仅验证震屏，未做跟随）。
> - **命中结算位置：** 伤害 + 顿帧 + 震屏暂时直接在 Hitbox 内结算（同 A3 直接 TakeDamage），将来可抽到 CombatSystem/EventBus，Phase 1 不引入。
> - **未做（击退）：** 押后到**多招系统（支柱二）之后**再做。理由：① 击退是**招式差异化反馈**——不同招应给出不同击退（力度/方向/是否浮空等），单招阶段做体现不出差异、也没有验证载体；② 静态假人（Node2D，不走物理）表现不出位移。故顺序为：先支柱二多招 → 再 A4-3 击退（届时受击者大概率已是可位移敌人）。

> **修订（2026-09-12，随闪避打断接线一并处理）：**
> - **震屏强度算错已修：** `_PhysicsProcess` 原写 `mag = _shakeFrames * t`（用**剩余帧数**当强度），导致 MoveData 上的 `shakeMagnitude` 从未生效、震屏幅度只跟"还剩几帧"挂钩。改为 `mag = _shakeMagnitude * t`——按配置强度起震、随帧线性衰减，符合 §4.4 原设计意图。
> - **`CancelHitstop()` 只清顿帧、不清震屏（决策）：** 闪避打断顿帧时一度想让 `CancelHitstop` 连 `_shakeFrames` 一并置 0。**否决**：① 顶部 guard `if (_shakeFrames <= 0) return;` 会让手动置 0 跳过 `cam.Offset = Vector2.Zero` 归位，**相机卡偏在最后一次抖动偏移上回不来**；② 震屏是纯表现层、不阻塞任何逻辑，闪避照常执行，留着几帧余韵反而读感更好（"这一拳确实打中了，然后你闪走了"）。故 `CancelHitstop()` 维持 `{ _hitstopFrame = 0; }`，让震屏自然衰减到归位。注：实测时把 hitstop 拉到 120 帧才会觉得"震屏没停"突兀，真实调校 hitstop≈6 帧，残余震屏一闪即过。
> - **清理调试打印：** 删掉 `ConsumeHitstop` 内每帧刷屏的 `GD.Print("Consuming hitstop frame.")`（顿帧每帧一行，长顿帧会淹没控制台）。

### 4.5 实现约定

- **可复用组件的形状 = 编辑器预设 + 按实例独立，不用代码 new。** Hitbox/Hurtbox 的 CollisionShape2D 形状在编辑器里预设（RectangleShape2D 等），但要让每个场景实例各持一份、互不共享，Godot 有**两种等价手法**，二选一即可：
  - **唯一化（Make Unique）：** 在 .tscn 里选中被引用的资源（如 CollisionShape2D 的 Shape）→ 右键 `唯一化 / Make Unique`。Godot 会把共享资源**就地拷贝成本场景私有的一份**（.tscn 里生成一条本场景专属 sub_resource）。玩家 Hurtbox 用的就是这个——Player.tscn 里生成了私有的 `RectangleShape2D_lxifr`，与 Hurtbox.tscn 共享的 `RectangleShape2D_7xy7u` 彻底脱钩。**每个实例都要手动唯一化一次。**
  - **本地到场景（Local To Scene）：** 在**源资源**（Hurtbox.tscn 里那份 Shape）Inspector 勾选 `本地到场景 / Local To Scene`。这是一个标志位，让该资源在**每个引用它的场景加载时自动复制**，无需逐个手动唯一化。
  - **为什么必须二选一：** Godot 的 .tscn 内联 sub_resource 默认被所有场景实例共享——改一个 size 会影响全部。代码里 `_rect.Size = x` 若不做独立化，等于在改所有实例共用的同一份形状。
  - **为什么不用代码 new：** 未来不同敌人需要**手编辑**的判定形状（尺寸/偏移各异），"编辑器预设 + 按实例独立"既保证可手编、又消除共享，优于运行时 `new RectangleShape2D()`。
  - 运行时代码只**读取/微调**预设形状（如按 MoveData 覆盖 size/offset），不负责创建。
  - **S2 建议（多敌人时）：** 敌人一多，逐个唯一化容易漏。**在 Hurtbox.tscn / Hitbox.tscn 的源 Shape 上勾一次 `本地到场景`**，之后所有实例自动独立、零心智负担；已唯一化的玩家实例保持不变即可，两者结果等价、可共存。

- **战斗判定碰撞分层（S1 约定，2026-09-12）：** Hitbox/Hurtbox 都是 Area2D，靠 `area_entered` 互相检测。Godot 默认所有 Area 都在物理层 1 / 掩码 1——这会让**玩家自己的 Hitbox 检测到自己的 Hurtbox（出招即自伤）**，且敌我判定全混在一层、无法区分。故给战斗判定 Area 单独分层（在 **项目设置 → 层名称 → 2D 物理** 命名）：

  | 层 | 命名 | 占用者 |
  |----|------|--------|
  | 1 | world | 地形 + CharacterBody2D 身体碰撞（`move_and_slide` 用，**维持现状不动**） |
  | 2 | player_hurtbox | 玩家 Hurtbox |
  | 3 | player_hitbox | 玩家 Hitbox |
  | 4 | enemy_hurtbox | 训练假人 + 未来敌人的 Hurtbox |
  | 5 | enemy_hitbox | 未来敌人的 Hitbox |

  - **掩码规则（谁检测谁）：** 攻击方 Hitbox 的**掩码 = 对方 Hurtbox 所在层**。玩家 Hitbox：层=3、掩码=**4**（enemy_hurtbox，不含 2 → 不自伤，仍能打到假人）；敌人 Hitbox（S2）：层=5、掩码=**2**（player_hurtbox）。
  - **Hurtbox 只需设对"层"：** 它是 monitorable、不 monitoring（`Hurtbox.cs` 里 `Monitoring=false / Monitorable=true`），掩码用不上。命中由 **monitoring 的 Hitbox** 发 `area_entered`，故检测关系完全由"Hitbox 的掩码指向哪个 Hurtbox 层"决定。
  - **身体碰撞不分进这套：** Area 间走 `area_entered`、CharacterBody2D 身体走 `move_and_slide`（body 碰撞），两套互不干扰。只给 Area 组件（Hitbox/Hurtbox）分层即可，玩家/敌人身体的层与掩码维持现状。

---

## 五、事件总线（EventBus）

系统间通过事件解耦通信，以下为核心事件分类：

| 类别 | 事件 |
|------|------|
| 输入 | 状态切换、连招路由变更、cancel触发 |
| 战斗 | 命中确认、确反命中、最优确反命中、连招开始/结束、蓄力开始/释放 |
| 敌人 | 出招预兆发出、阶段切换、被击败 |
| 机关 | 激活（成功/失败）、重置 |
| 反馈 | 顿帧请求、震屏请求 |

---

## 六、配置化原则

所有影响手感和平衡的参数外部化为 Godot Resource（.tres），不在代码中硬编码。

| 配置 | 内容 | 存放位置 |
|------|------|---------|
| InputConfig | 缓冲帧、切换延迟、cancel窗口、方向阈值 | Core/Config/ |
| BalanceConfig | 确反倍率、气槽积攒速度、大招消耗 | Core/Config/ |
| FeedbackConfig | 顿帧帧数、震屏强度/时长、击退力度 | Core/Config/ |
| 招式数据 | 伤害、帧数、hitbox参数（待设计） | 待定 |
| 连招路由 | 路由树结构、cancel节点（待设计） | 待定 |
| 敌人参数 | 血量、攻击间隔、信号持续时间、破绽帧数 | 各敌人文件夹内 |

---

## 七、开发阶段与任务明细

> 排期留空，各阶段完成后再规划下一阶段的具体时间安排。

---

### Phase 0：基础设施搭建

**目标：** 项目能跑起来，角色能在空场景中正常移动。

| # | 任务 | 涉及文件 | 说明 |
|---|------|---------|------|
| 1 | Godot项目初始化 | project.godot, .gitignore | C#项目配置、目录结构搭建 |
| 2 | 全局管理器 | Core/GameManager.cs | 游戏生命周期、暂停/运行状态切换 |
| 3 | 事件总线 | Core/EventBus.cs | 全局信号注册/分发机制 |
| 4 | 输入管理器（基础） | Core/InputManager.cs | 先实现Idle状态下的移动输入采集 |
| 5 | 输入配置 | Core/Config/InputConfig.tres | 基础输入参数（后续持续调整） |
| 6 | 玩家角色场景 | Player/Player.tscn | 色块角色节点树搭建 |
| 7 | 玩家移动控制 | Player/PlayerController.cs | 水平移动、跳跃、重力、落地检测 |
| 8 | 玩家色块动画 | Player/PlayerAnim.cs | 简单的颜色/形状变化表示状态（待机/移动/跳跃） |
| 9 | 测试场景 | Scenes/TestArena.tscn | 一个带地面和平台的封闭房间 |
| 10 | 调试覆盖层 | UI/DebugOverlay.tscn + .cs | 显示输入状态、帧率、物理状态 |
| 11 | Git初始化 | .gitignore | 首次提交 |

**完成标志：**
- [x] 角色在场景中跑、跳、落地手感自然
- [x] DebugOverlay正常显示输入状态
- [x] 项目可编译运行，Git仓库就绪

**Phase 0 回溯记录（2026-08-15）：**
- GameManager、EventBus 未引入——当前没有场景管理与跨系统通信的实际需求，推迟到出现真实需求时再建，避免过早脚手架
- 输入仲裁未按原计划放在 InputManager 内，改为 Player 侧 InputStateMachine，InputManager 降级为纯采集（详见 4.1）
- 目录结构重构为当前实际形态（详见第二章）
- 调试覆盖层由 scenes/ui/input_test/ 的输入历史面板承担（SF6 训练模式样式），帧率等监控项后续按需补充
- 玩家移动控制采用"handler 写意图、Player 统一积分"模式，未使用原计划的 PlayerController 单脚本结构

---

### Phase 1：战斗核心原型

**目标：** 角色能在移动中流畅出招，连段和变招可操作，打击反馈有体感。

**前置：招式设计与搓招表待定。** 本阶段先用临时招式（如：上/下/前/后各一个基础招式 + 2-3个临时连段）搭建框架，验证系统架构和手感。正式招式设计在Phase 1完成后、Phase 2开始前进行。

**首要里程碑：战斗循环垂直切片**（2026-08-15 定，任务表展开前的先行工作）

不做伤害与判定，先用最便宜的代价让"进入战斗 → 择方向 → 回到移动"的循环转起来：

1. **攻击键触发切换**：状态机全局触发器检测攻击键按下边缘 → TransitionTo(CombatHandler)；Enter 时 Sprite 变色作为视觉标记，Exit 恢复
2. **窗口计时与超时返回**：CombatHandler 开窗约30帧（首个手感参数），窗口内不写 moveAxis（移动自然刹住），归零返回移动状态
3. **窗口内方向路由**：检测方向键按下边缘，首个非中立方向即"提交"，用临时 Label/颜色显示所选路线后关窗——择的骨架
4. **闪避全局打断**：闪避键边缘优先于一切，任何状态下无条件返回移动；重点验证窗口内闪避可立即打断

四步全部通过验收后，再展开下方任务表（hitbox/hurtbox、训练假人、真实判定与反馈）。

> **进度（2026-09-03）：** 四步已全部实现并通过验收。闪避触发落在 InputStateMachine.Tick 中，排在攻击触发之前、带 `currentInputReceiver != movementHandler` 守卫（移动态按闪避不空转状态切换）。战斗态所有易变状态（连段计数、相位、倒计时、pending 攻击/方向）统一在 CombatHandler.Enter 重置——清理责任收敛在"入口"而非 Exit，闪避强退这条最野蛮的中断路径即为其压力测试。

**已修复（2026-09-03，随支柱二方向选招一并处理）：**

- **pending 方向归属错位**（原"已知问题"）：旧 Listening 分支中攻击判定位于最前、命中即 `AdvanceChain(); return;`，跑在方向消费之前，残留 pending 方向会被带入下一段。方向一旦接入选招，这就从"日志错位"升级为"选错招"的真 bug，故在支柱二第一步一并修复。
  - **修复方式**：方向解析并入 `AdvanceChain` → `ResolveAttackDirection()`（当帧持续方向 `Current` 优先、中立则取出招期 `pending`），先绑定到**当前这一击**再 `StartMove`（其内部清 pending），攻击触发不再绕过方向消费。`TickListening` 无攻击分支改为把方向边缘**存入** pending（预输入下一招），而非旧版 print 后清零。

**出招系统基础池（Phase 1 正式任务表，2026-09-03 重排，替换切片前的平铺表）**

垂直切片证明了"输入→状态路由→循环"骨架能转。Phase 1 的实质是给骨架包肉：让招式有真实帧数据、攻击能真的打到、打中有体感。排序策略为**先深后宽**——先用一发真招打通全链路（tracer bullet），再横向铺开路由树；把最不确定、最影响手感的反馈环节优先验证，避免拖到最后返工。

> **范围纪律：** 成品出招表（承载破招意义的完整 roster）押后到 Phase 1→2 交界、有对手可破之时再设计（详见 GAME_DESIGN §2.1.2）。Phase 1 只交付 substrate + 手感 + 出招表契约。**明确不做**：完整 roster、破招 payoff、敌人 AI、气槽/大招、正式美术动画。

**支柱一 · 输入 → 输出效果的链路（先用一招打通脊柱）**

| # | 任务 | 涉及位置 | 说明 |
|---|------|---------|------|
| 1 | MoveData 帧数据契约 | 招式数据 Resource（.tres） | 前摇/活跃/后摇帧数 + hitbox 形状偏移 + 伤害 + hitstop 帧 + 击退力度；cancelability 留**可选字段不接线**。字段表由"打通一招"的过程逼出，不预先抽象设计 |
| 2 | CombatHandler 帧数据驱动 | scenes/characters/player/CombatHandler.cs | 用 MoveData 三段（Startup→Active→Recovery）替换占位 showFrames；保留帧计数器原则；择窗口（Listening）维持独立 tick 计时，**不绑死到招式帧** |
| 3 | Hitbox / Hurtbox 组件 | 共享战斗组件（**不置于 player/ 下**） | Hitbox=Area2D 仅活跃帧 monitoring；Hurtbox=Area2D 受击区。具体路径开发中定，敌人将来复用 |
| 4 | DamageCalculator（基础） | 共享战斗系统 | 重叠→扣血；确反加成留接口**不启用** |
| 5 | 训练假人 | 测试场景 | 带 Hurtbox + HP 的静态目标，命中可观察（HP 变化 / 受击变色） |

> **进度（2026-09-03）：**
> - 任务 1–2（MoveData 帧数据契约 + CombatHandler 帧驱动播放）**已完成**：三段 Startup/Active/Recovery 由帧计数器驱动，色块分段可视化（暗红/亮橙/灰）；择窗口（Listening）保持独立 tick 计时、未绑死招式帧。
> - 任务 3（Hitbox/Hurtbox 组件）**已完成并验证**：Hitbox=Area2D 仅活跃帧 monitoring（CollisionShape2D.Disabled 切换），Hurtbox=Area2D（monitoring=false / monitorable=true），命中经 hitbox 的 `area_entered` 信号 + `is Hurtbox` 过滤，已打到训练假人。
> - 任务 4–5（伤害 / 训练假人）**进行中**：引入 `IDamageable` 接口（`void TakeDamage(int amount)`），假人挂 HP + 受击闪红。**DamageCalculator 抽象推迟**——本步直接 `TakeDamage(move.damage)`，不建独立计算器；任务 4 的"确反加成留接口不启用"随之押后到 Phase 2 真要做确反倍率/最优确反时再引入。

**支柱二 · 连段时机（横向铺开路由）**

| # | 任务 | 涉及位置 | 说明 |
|---|------|---------|------|
| 6 | ComboSystem 路由树 | 共享战斗系统 | 树结构：节点=一招，边=输入（方向+攻击）；把现有线性 chain 计数改为在树上走节点 |
| 7 | 临时 4 方向招式 | 招式数据 Resource | 上/下/前/后各一 MoveData，挂成树第一层，验证方向路由真的选到不同招。脚手架性质，held loosely |
| 8 | 变招 cancel 机制 | ComboSystem | cancel 窗口内允许切分支。**Phase 1 仅留机制接口，payoff 验证属 Phase 2** |

> **进度（2026-09-03）：**
> - 任务 7（临时多方向招式）+ 任务 6 最小版**已完成并验证**：CombatHandler 加 `moveUp/Down/Left/Right` 四个 `[Export]` 槽 + `SelectMove(Direction)`，首击（`Enter`）与连段（`AdvanceChain`）均按方向选招，未配方向 / 中立 / 对角回落 `openerMove`。4 个临时 `.tres`（不同 hitboxOffset/Size/damage/moveName）验证"方向真的选到不同招"。
> - **方向语义**：第一步用**绝对方向**（Up/Down/Left/Right），不做面向相对的"前/后"——当前无面向翻转系统，"前/后"留到对敌（Phase 2）。选招方向取**攻击触发瞬间的持续方向**（`Current` 优先、中立 fallback 出招期 `pending`），兼容"按住方向再按攻击"与"出招动画中预拨方向"两种输入。
> - **未做**：任务 6 完整 **ComboSystem 路由树**（带连段路径依赖，如 ↓↘→+拳）——第一步只是"方向→招"直接映射（`switch`，held loosely），等招式变多、需要路径输入时再上树；任务 8（cancel 接口）未做，与树耦合（cancel = 树上切分支），倾向同正式连招表一起在 Phase 1→2 交界处理。

**支柱三 · 手感（打击反馈）**

| # | 任务 | 涉及位置 | 说明 |
|---|------|---------|------|
| 9 | FeedbackSystem | 共享战斗系统 | 命中触发 hitstop + 震屏 + 击退。hitstop 用**帧计数器冻结 tick**（不用 Engine.time_scale），与状态切换计时同一时钟 |
| 10 | 反馈/手感参数外部化 | 配置 Resource（.tres） | 顿帧帧数、震屏强度时长、击退力度、招式帧数、cancel 窗口、输入缓冲均可调，不硬编码 |
| 11 | 手感调校 + 阶段验收 | — | 反复调校，对照下方完成标志逐项打分 |

> **进度（2026-09-03）：**
> - 任务 9（FeedbackSystem）：顿帧、震屏已实现并验收，FeedbackSystem 以 Autoload 落地（机制详见 §4.4）。击退押后到**多招系统（支柱二）之后**——击退是招式差异化反馈，需多招才有验证载体（理由详见 §4.4）。
> - 任务 10（参数外部化）：`hitstopFrames`、`shakeMagnitude` 已在 MoveData 上可调（Inspector 改 openerMove 资源），尚未抽 FeedbackConfig.tres。
>
> **已修复（2026-09-11）· 顿帧吞连段输入：** 顿帧冻结 Player tick，旧 `CombatHandler.CapturePending` 在 tick 内用 `GetPressed(Previous, Current)` **单帧边缘**采样攻击——顿帧期间 tick 不跑，且按住的键在顿帧结束后 Previous/Current 都为"按着"无边缘，故顿帧中按下的攻击被吞、连段接不上（实测：`hitstopFrames` 拉到 120、冻结中按攻击，结束后不出招）。删除 Listening 兜底窗口后连段全靠此缓存，问题从"偶发"升为"主要断连原因"。
>   - **修复：** `ResolveNext` 不再读 tick 内边缘，改**回扫 `InputManager.Buffer`**——以 `StartMove` 记录的 `_moveStartInputId`（`InputRecord.Id` 单调递增）为基线，只扫本招开始后产生的记录，用 `(prev & rec.Attack) != rec.Attack` 从状态记录重建"按下边缘"，方向取该记录 `rec.Direction`（攻击与方向绑定于按下那一刻，一并消解了连段方向时序问题）。删除 `CapturePending` / `_pendingAttack` / `_pendingDirection` / `ResolveAttackDirection`。
>   - **原理：** 见 §4.1 "消费契约"——消费者只从输入日志取，不在会被冻结的 tick 里重新采样实时输入。
>   - **遗留同源项 · 顿帧期间按闪避被吞：已修复（2026-09-12）。** 与顿帧吞连段同一根因（闪避判定原在 `InputStateMachine.Tick` 内，顿帧也冻结它）。但**修法不同**：连段攻击走"回扫 Buffer"（手法 (1)），闪避走"把判定前移到顿帧闸门之前、读实时边缘"（手法 (2)）——因为 `InputRecord` 不存系统键（闪避/跳跃/格挡），Buffer 无法重建闪避边缘。详见下方「闪避修复 + DodgeHandler 状态化（2026-09-12）」与 §4.1 消费契约。

**手感修复 + 可行性回顾（2026-09-11）**

> **卡手修复（删除"死窗口"）：** 切片期 CombatHandler 在招式播放结束后进入约 30 帧 `Listening` 窗口干等连段输入，期间角色钉在原地——这是"战斗↔移动"切换卡手的**根因（非播放锁定本身）**。已移除 `Listening` 相位与 `checkFrames`/`_counter`/`AdvanceChain`，改为：`TickPlaying` 在播放结束帧直接判定——有预输入攻击（`_pendingAttack`，播放期由 `CapturePending` 暂存）且未达 `chainCap` 则 `StartMove(ResolveNext())` 无缝接续，否则立即 `TransitionTo(movementHandler)`，**零空窗还移动**。连段计数折进 `TickPlaying`。（注：此处 `_pendingAttack`/`CapturePending` 的边缘缓存随后在顿帧修复中改为回扫 `Buffer`，详见上文「已修复 · 顿帧吞连段输入」。）
> - **每招移动锁定数据化：** `MoveData` 新增 `lockMovement`（默认 `true`）。"播放期是否锁走位"从全局架构决策**下沉为每招的数据属性**——绝大多数招锁定，少数快速招可设 `false` 实现"边走边打"。
>   - **当前落实范围：** 仅"锁定"路径生效（战斗态本就不写 `moveAxis`）。`!lockMovement` 的播放中走位仍是 **TODO**——`TickPlaying` 内对应分支体为空、且条件写成了 `if (_move.lockMovement)`（应为 `!_move.lockMovement`），待接 `ReadHorizontalAxis`（抄 movementHandler 读水平轴那段）。因所有招默认 `true`，暂不阻塞手感。
> - **GAME_DESIGN §3.1 同步校准：** 快速招式层原写"不锁定移动，随时可以走位"，已校准为"播放极短、结束即还移动、无死窗口；播放期是否锁定由每招 `lockMovement` 决定"。

> **可行性回顾结论（第一次）：** 本轮一度计划引入"序列匹配器（`MoveResolver`）+ →↓↘ / →↘↓ 两条易串测试搓招"做"架构兼容性验证、避免后续 rework"。经对照 GAME_DESIGN §2.1.1 / §9.2 判定为**漂移信号并已叫停**：
> - **判据：** 选这两条指令的理由是"街霸里容易串招、可拿来调手感"——而调校近似指令的串招区分度本质是**执行层（手指精度）工作**，命中 §9.2 自查信号"想的是'这招没按出来'而非'该不该用'"；且序列匹配器与已押后的 task 6（路由树）/ task 8（cancel）同属"在知道出招表前先建路由"，违背同一纪律。"避免 rework"的动机也被高估：现 `SelectMove` 的 `switch` 本就 held loosely，将来换序列匹配是增量替换。
> - **更深的可行性结论：** 按 §2.1.2，**择 = 破招，payoff（造成伤害 / 避免伤害）需对手**；Phase 1 无敌人 → **结构上无法验证"择"**。故 Phase 1 的健康姿态是"substrate 够用即止、手感做到位"，**不在执行层精雕**，尽快推进到 Phase 2 第一个会出题的敌人——那是本项目第一次能真正回答"择成不成立"。
> - **处置：** 序列 / 搓招匹配器押后到 Phase 1→2 交界，与正式出招表、task 6 / task 8 一并设计。`InputManager.Buffer(60)` 已为**顿帧输入恢复**接线（见 §4.1 消费契约 / §七 已修复），但**序列匹配（搓招）仍未接线**，留待出招表设计时顺着同一条 Buffer 管线扩展。

**闪避修复 + DodgeHandler 状态化（2026-09-12）**

> **问题（顿帧期间闪避打不断）：** 闪避判定原在 `InputStateMachine.Tick` 内，而顿帧冻结的正是 Player tick（连带 `Tick`）——故顿帧期间按闪避无响应。与"顿帧吞连段输入"同根因，但属另一条修复路径（见 §4.1 消费契约手法 (2)）。
> - **修复（判定前移到顿帧闸门之前）：** 把闪避检查从 `Tick` 抽出为独立方法 `TryDodgeInterrupt()`，由 `Player._PhysicsProcess` 顶部、`ConsumeHitstop()` **之前**调用：闪避键按下边缘且当前不在闪避态 → `TransitionTo(dodgeHandler)` 并返回 true，调用方据此 `FeedbackSystem.Instance.CancelHitstop()`；否则才进顿帧 `return`。`Tick` 内原闪避分支移除。这样顿帧也能即时闪避——闪避判定移出了被冻结的区域，读实时边缘即可，无需回扫 Buffer（`InputRecord` 本就不存系统键）。
> - **闪避升级为独立状态（DodgeHandler）：** 原先闪避只是"`TransitionTo(movementHandler)` 即退即回"，没有自身时长，无法承载位移/无敌帧。现新建 `DodgeHandler : Node, IInputReceiver`（Player 子节点，编辑器挂脚本 + 拖入状态机 `Dodge Handler` 槽，`player`/`inputStateMachine` 经 `Initialize` 注入），自带 `dodgeFrames` 时长，作为**未来闪避内容（位移、无敌帧）的接缝**。当前仅 `Tick` 内 `moveAxis = facing` 产生位移、计时到还移动；`Enter`/`Exit` 留好无敌帧 TODO 钩子。
> - **最小 facing 种子：** `Player` 加 `int facing`（1=右/-1=左），`Tick` 后由 `moveAxis` 符号更新，DodgeHandler 据此实现"面朝哪边往哪边闪"。**边界：仅最小种子**——Sprite 翻转 + 面向相对的"前/后"招式路由仍押后 Phase 2（同 §支柱二 绝对方向结论），facing 现阶段不接管视觉或选招。
> - **FeedbackSystem 同批修订：** 震屏强度 bug（`_shakeFrames * t` → `_shakeMagnitude * t`）、`CancelHitstop` 只清顿帧不清震屏（清震屏会因顶部 guard 跳过 `Offset` 归位致相机卡偏）、删除 `ConsumeHitstop` 调试打印——详见 §4.4「修订（2026-09-12）」。
> - **验收状态：** 顿帧打断已实测生效（顿帧期间按闪避可立即切闪避态并清顿帧）。**未做（押后）：** 无敌帧（需玩家 Hurtbox + 敌人，Phase 2）、闪避位移速度覆盖（暂复用移动 Speed，task 11 手感调校再单独给加速度）。

**完成标志：**
- [x] 移动中出招不卡顿，状态切换自然（卡手修复删死窗口 + 顿帧可被闪避打断，2026-09-11/12）
- [x] 一发真招打通全链路：帧数据驱动 → 活跃帧 hitbox → 命中假人 → 伤害 + 反馈（支柱一 task 1–5，已打到训练假人）
- [ ] 连段可搓出来，方向路由选到不同招；cancel 机制接口就位 —— **连段 + 方向选招已达成**（顿帧吞输入修复 + 支柱二 SelectMove）；**cancel 接口有意押后**（task 8，§2.1.2：待 Phase 2 破招语义清楚再填，非未完成）
- [x] 打到假人有明显打击感（顿帧 + 震动；震屏强度 bug 已修；击退随多招系统后补，见 §4.4）
- [ ] 连续操作 5 分钟不觉疲惫 —— **唯一剩余的手感闸门**，属主观实测项，待 task 11 正式试玩打分（卡手/顿帧两大碍手项已清除，预期可通过）

**Phase 1 回溯记录（2026-09-12，阶段间回溯节点）：**
- **substrate"够用即止"达成。** 进攻面（帧数据驱动播放 + 方向选招 + 连段路由）、防御面（闪避全局打断、顿帧期可打断）、打击反馈（顿帧 + 震屏）三块地基已转起来，手感三大碍手项（卡手死窗口、顿帧吞连段、顿帧锁闪避）已逐一修复并记录。
- **对照设计假设：** Phase 1 验证了"输入→状态路由→招式播放→命中反馈"骨架可行，但**按 §2.1.2，Phase 1 结构上无法验证"择"本身**（无对手 → 破招无从发生）。这与设计预期一致，非意外。
- **有意押后项（纪律性推迟，非未完成）：** cancel 接口（task 8）、完整 ComboSystem 路由树（task 6）、序列/搓招匹配器、击退（A4-3）、确反加成接口——全部属"在知道出招表前先建路由"或"需对手才有验证载体"，按 §2.1.2 / §9.2 押后到 Phase 2 由敌人出题反推。
- **结论：Phase 1 可收口，进入 Phase 2。** 下一步是把"择"从空转变为有赌注——补"玩家受击 + 敌人出题"两台机器（详见 Phase 2 切入序列）。

---

### Phase 2：敌人原型

**目标：** 敌人能"出题"，玩家的"择"有实际意义——选对了和乱按有体感差异。

**前置（2026-09-12 校准）：** ~~Phase 1 完成后需先设计招式/搓招表再进本阶段~~ —— **此表述作废**。按 GAME_DESIGN §2.1.2，**正式出招表待第一个敌人定型后反推**，不是进 Phase 2 的前置条件；先设计 roster 等于"在知道问题之前先写答案"。Phase 2 的真正前置是 Phase 1 substrate 收口（已达成，见上方回溯记录）。

**Phase 2 切入序列（tracer-bullet，2026-09-12 定）：** 承 Phase 1"先深后宽、不预先抽象"纪律，**先把一个直球型敌人端到端打通**，通用 EnemyBase / AI 状态机 / Signal 系统（下方 task 1–3）由这个过程**逼出后再提取**，不先建框架。择 = 破招需"赌注 + 对手"，故序列先补这两台机器：

| 步 | 做什么 | 对应缺口 / 依据 |
|---|--------|----------------|
| **S1** | **玩家可受击**：Player 挂 Hurtbox 子节点 + 实现 `IDamageable` + HP + 受击反馈（闪红/顿帧）。复用现成 `scenes/combat/components/`（假人已在用） | 缺口 A——没有"对己避免伤害"的代价，押错=0后果，违反 §1.4 原则3"有风险才有快感" |
| **S2** | **一个直球型敌人端到端**：预兆(可读视觉 Signal) → 攻击(带 Hitbox，能打到玩家) → 固定破绽窗口(Recovery)。命中玩家须**中断其当前招式 + 受击硬直**（择错惩罚，2026-09-12 确认，见 GAME_DESIGN §2.1.2 决策记录）。先**确定性出题**（固定 pattern） | 缺口 B——破招需"有一招被破"；§4.3 先确定性验证择成立，再上随机 |
| **S3** | **反推最小确反 roster**：此时才动出招表——"破这一招需要什么"，由敌人破绽类型逼出 1–2 个确反招 | §2.1.2 反推纪律，不凭空设计 roster |
| **S4** | **确反 payoff 差异化**：确反窗口判定 → 伤害 / 反馈强度差，接线 Phase 1 押后的确反加成接口 | §4.4"择对了"体感须明显高于普通命中 |

> **验证闸门：** S1–S4 跑通后，对照 GAME_DESIGN §9.4 止损线 Q1（面对敌人会犹豫用哪招吗）/ Q4（愿意反复打吗）——这是项目**第一次能真正回答"择成不成立"**。
> **下方 task 1–12 仍是 Phase 2 的完整结构**，但其"先框架（task 1–3）后敌人（task 6）"的字面顺序**被本切入序列取代**：框架是 S2 打通后提取的产物，不是起点。

| # | 任务 | 涉及文件 | 说明 |
|---|------|---------|------|
| 1 | 敌人基础框架 | Enemies/Base/EnemyBase.tscn + .cs | 敌人节点树、通用状态机、受击逻辑 |
| 2 | AI状态机 | Enemies/Base/EnemyAI.cs | Idle→Signal→Attack→Recovery 循环 |
| 3 | 出招预兆系统 | Enemies/Base/EnemySignal.cs | 视觉信号（颜色变化、缩放、方向指示）+ 信号数据Resource |
| 4 | 确反判定完善 | Combat/DamageCalculator.cs | 接入确反窗口判定、最优确反加成逻辑 |
| 5 | 数值平衡配置 | Core/Config/BalanceConfig.tres | 确反倍率、伤害数值、资源积攒速度 |
| 6 | 直球型敌人 | Enemies/Direct/DirectEnemy.tscn + .cs | 单一前摇攻击，固定破绽，验证"确反择" |
| 7 | 多段型敌人 | Enemies/Multi/MultiEnemy.tscn + .cs | 连续攻击+最后大破绽，验证"押注择" |
| 8 | 变招型敌人 | Enemies/Switch/SwitchEnemy.tscn + .cs | 攻击中切换模式，验证"变招择" |
| 9 | 敌人参数配置 | 各敌人文件夹内 .tres | 每种敌人的攻击间隔、信号持续时间、破绽帧数 |
| 10 | 气槽系统 | Player/PlayerCombat.cs | 命中/确反积气，大招消耗气槽 |
| 11 | HUD | UI/HUD.tscn + .cs | 显示血量、气槽 |
| 12 | 对战测试场景 | Scenes/TestArena.tscn | 场景中配置各种敌人，逐个对战测试 |

**完成标志：**
- [ ] 每种敌人至少有一种"出题"方式清晰可读
- [ ] 玩家面对敌人时有意识地选择连招，而非无脑攻击
- [ ] 最优确反和普通命中的体感差异明显（伤害差距+反馈差距）
- [ ] 三种"择"层次各有对应的敌人验证

---

### Phase 3：Boss与机关

**目标：** 完成一轮完整体验流程（跑图→小怪→机关→Boss），核心循环成立。

| # | 任务 | 涉及文件 | 说明 |
|---|------|---------|------|
| 1 | Boss基础框架 | Boss/Boss.tscn + BossController.cs | Boss节点树、移动、基础行为 |
| 2 | Boss阶段管理 | Boss/BossPhaseManager.cs | 多阶段切换逻辑、每阶段不同出题策略 |
| 3 | Boss出题模式库 | Boss/BossPatterns.cs | 综合三种"择"的出题方式，不同阶段侧重不同 |
| 4 | Boss视觉预兆 | Boss/Boss.tscn | Boss级别的出招预兆表现（比小怪更复杂） |
| 5 | 机关基础框架 | Mechanisms/Base/MechanismBase.tscn + .cs | 机关通用逻辑：激活、判定、重置 |
| 6 | 喂招型机关 | Mechanisms/Counter/CounterMechanism.tscn + .cs | 机关释放信号→玩家用正确连招回应 |
| 7 | 押注型机关 | Mechanisms/Bet/BetMechanism.tscn + .cs | 机关要求蓄力大招→判断真假 |
| 8 | 变招型机关（可选） | Mechanisms/Switch/SwitchMechanism.tscn + .cs | 机关交手中变招→玩家跟变（视时间决定是否纳入） |
| 9 | 大招系统 | Player/PlayerCombat.cs | 蓄力释放大招，消耗气槽 |
| 10 | 线性测试关卡 | Scenes/TestArena.tscn | 串联：跑图段→小怪遭遇→机关→Boss房 |
| 11 | 节奏调校 | 各Config文件 | 调整各段节奏，确保张弛有度 |

**完成标志：**
- [ ] Boss战能完整打完，三个阶段节奏有变化
- [ ] Boss战中三个层次的"择"都有出现
- [ ] 至少一个机关可正常交互
- [ ] 从跑图到Boss结束的完整流程节奏合理，不累不空

---

### Phase 4：打磨与验证

**目标：** 反复测试调优，达到验收标准。

| # | 任务 | 涉及文件 | 说明 |
|---|------|---------|------|
| 1 | 手感精调 | Core/Config/*.tres | 输入参数、招式帧数、反馈强度的精细调整 |
| 2 | 数值平衡 | Core/Config/BalanceConfig.tres | 伤害、血量、气槽、确反窗口等全面调平 |
| 3 | 敌人AI微调 | 各敌人 .cs + .tres | 出题节奏、预兆清晰度、破绽窗口调整 |
| 4 | Boss战打磨 | Boss/*.cs + .tres | 阶段节奏、出题密度、难度曲线 |
| 5 | Bug修复 | 全局 | 判定异常、状态机卡死、输入丢失等问题 |
| 6 | 外部测试 | - | 找3-5人试玩，观察是否能"自然"做出择的判断 |
| 7 | 收集反馈 | - | 整理测试者反馈，确定需要调整的点 |
| 8 | 根据反馈迭代 | 全局 | 针对反馈做最后一轮修改 |
| 9 | 验收评估 | - | 对照产品设计文档的5项验收标准逐项打分 |

**完成标志：**
- [ ] 验收标准5项中至少3项获得肯定回答
- [ ] 外部测试者能在没有指导的情况下理解"择"的概念
- [ ] 连续游玩20分钟无认知疲劳
- [ ] 核心玩法验证结论：**成立 / 需调整 / 不成立**

---

### 阶段间的回溯节点

每个Phase完成后，暂停开发，执行以下回溯：

1. **回顾产品设计文档**（GAME_DESIGN.md）：当前Phase的实现是否验证或推翻了某个设计假设？更新文档。
2. **回顾开发文档**（本文档）：实际实现中架构是否需要调整？新增了什么发现？
3. **规划下一阶段排期**：基于当前进度和发现的问题，给下一Phase制定具体时间计划。
4. **招式/搓招表设计**（Phase 1完成后）：根据手感调校结果，正式设计连招种类和搓招表。

---

## 八、开发节奏

> **架构 → 实现 → 回溯 → 再规划**

当前处于 **Phase 1 实现期**（Phase 0 已于 2026-08-15 完成回溯）。本文档已定义模块边界、职责划分和各阶段开发任务明细，并随实现进展修订。

**待定内容（需在开发中逐步填充）：**
- 连招种类与搓招表（Phase 1完成后设计）
- 各招式帧数据
- 具体排期（各Phase完成后规划下一阶段）

每个Phase实现完毕后，回溯更新：
1. 产品设计文档（GAME_DESIGN.md）：验证设计假设是否成立，调整或推翻
2. 本文档（DEV_GUIDE.md）：记录实际实现中的架构调整、新增发现
3. 规划下一Phase的具体时间排期

---

*本文档为活文档，随开发进展持续更新。*
