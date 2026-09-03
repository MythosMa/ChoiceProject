# Choice Project - 开发文档（Development Guide）

> 版本：v0.4  |  日期：2026-08-15  |  状态：Phase 0 完成，进入 Phase 1

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

**InputStateMachine（Player 子节点，仲裁路由层）**
- 不挂自己的 _PhysicsProcess，由 Player._PhysicsProcess 显式调用 Tick，保证帧内顺序：清意图 → 状态机路由 → 物理积分（MoveAndSlide 只在 Player 调用）
- 持有当前活跃的 IInputReceiver，TransitionTo 统一执行 Exit → Enter，是所有状态切换的唯一入口
- 全局触发器由状态机直接处理：攻击键按下边缘 → 进入战斗；闪避键按下边缘 → 无条件回到移动；UI 焦点闸门（GuiGetFocusOwner 非空时停止派发）
- handler 引用在状态机的 _Ready 中注入（子节点 _Ready 先于父节点，handler 不得自行向上取引用）

**状态与方向键职责：**
- **MovementHandler（移动）**：方向键 = 移动，跳跃 = 独立键（空格），攻击键不在此处理
- **CombatHandler（战斗）**：方向键 = 招式路由。攻击键开窗 → 首个方向输入提交路线 → 超时/闪避/不匹配则返回移动（窗口计时与路由逻辑为 Phase 1 实现内容）

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

| # | 任务 | 涉及文件 | 说明 |
|---|------|---------|------|
| 1 | 输入仲裁状态机 | scenes/characters/player/InputStateMachine.cs | 骨架已完成（handler路由、TransitionTo、UI闸门）；剩余：战斗窗口计时、攻击/闪避全局触发器 |
| 2 | 输入缓冲 | core/InputManager.cs | 已完成：60帧Buffer + DurationFrames；剩余：战斗语境下的预读窗口调校 |
| 3 | 快速招式系统 | scenes/characters/player/CombatHandler.cs | 方向+攻击键触发单段招式（临时4方向招式） |
| 4 | 连招路由框架 | Combat/ComboSystem.cs | 路由树数据结构、输入匹配、节点流转 |
| 5 | 变招（cancel）机制 | Combat/ComboSystem.cs | cancel窗口检测、连段中断与分支切换 |
| 6 | 攻击判定 | Combat/HitboxManager.cs | 招式激活时生成攻击判定框 |
| 7 | 受击判定 | Combat/HurtboxManager.cs | 角色受击区域管理 |
| 8 | 伤害计算（基础） | Combat/DamageCalculator.cs | 基础伤害计算（确反加成逻辑留接口，本阶段不启用） |
| 9 | 打击反馈 | Combat/FeedbackSystem.cs | 顿帧、屏幕震动、击退位移 |
| 10 | 反馈配置 | Core/Config/FeedbackConfig.tres | 各招式反馈参数可调 |
| 11 | 招式配置框架 | 待定 | 招式数据Resource结构定义（具体招式内容待定） |
| 12 | 训练假人 | Scenes/TestArena.tscn | 场景中放一个不动的假人，用于测试攻击判定和反馈 |
| 13 | 手感调校 | Core/Config/*.tres | 反复调整输入缓冲、状态切换延迟、cancel窗口、招式帧数 |

**完成标志：**
- [ ] 移动中出招不卡顿，状态切换自然
- [ ] 连段可搓出来，中途可cancel变招
- [ ] 打到假人有明显的打击感（顿帧+震动+击退）
- [ ] 连续操作5分钟不觉疲惫

---

### Phase 2：敌人原型

**目标：** 敌人能"出题"，玩家的"择"有实际意义——选对了和乱按有体感差异。

**前置：** Phase 1完成后，需进行招式/搓招表设计，确定正式连招结构后再进入本阶段。

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
