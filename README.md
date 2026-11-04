# Choice Project

横版动作原型：在类银河城的容器里，做**格斗游戏式的「择」**——不是"能不能按出来"，而是"这一下该不该出"。

- 引擎：Godot 4.7.2（.NET / C#）
- 阶段：**M0 基座收口**（见 [ROADMAP](doc/ROADMAP.md)）
- 一句话现状：核心战斗机制已跑通，但「择」还没被外部验证过；当前版本仍存在"交替两招即可零伤通关"的免费解，M1 解决。

> **只有 5 分钟？** 读 [GAME_DESIGN §0 一页读懂](doc/GAME_DESIGN.md)——这游戏是什么、一场战斗长什么样、现在在哪。

---

## 快速开始

```bash
dotnet build                      # 编译 C#
godot --path .                    # 或用 Godot 编辑器打开项目后 F5
```

主场景是战斗测试场景 `scenes/test_scenes/test_scene_enemy_attack.tscn`（M0 会拆出独立的 build 场景与沙盒）。

**当前在验证什么：** 打一场，感受"我有没有被逼着做选择"。结论回给维护者，一句话即可。

---

## 操作

| 键 | 功能 | 备注 |
|----|------|------|
| ← → | 移动 | 只有左右移动 |
| ↑ ↓ | **攻击方向**，不参与移动 | 设计决策，见 GAME_DESIGN §3.2 |
| Z | **轻拳（jab）**（快、低伤、可闪避取消） | 2026-11-04 起轻/重由按键决定 |
| X | **重拳（heavy）**（慢、高伤、**不可闪避取消**，挥空会被反斩） | |
| 空格 | 跳跃 | 独立键 |
| Shift | 闪避（全程无敌 + 冷却，**可穿过敌人**） | |
| C | 格挡 | 已绑定、**M2 实现** |
| A / S | — | 脚键**暂时失效**（拳/脚区分押后） |

> 攻击判定框的朝向以**出招瞬间按下的水平方向**为准（2026-11-04 修复）；没有水平方向时沿用当前朝向。

---

## 文档

| 文档 | 作用 |
|------|------|
| [ROADMAP](doc/ROADMAP.md) | **现在在哪、下一步做什么、怎么算做完**（排期与验收门的唯一依据） |
| [GAME_DESIGN](doc/GAME_DESIGN.md) | 产品与玩法设计。**先读 §0 一页读懂**；§2.5 是短促交锋的完整推理，其余是展开 |
| [COMBAT_SPEC](doc/COMBAT_SPEC.md) | **战斗规格书**：角色完整动作表、操作、手感参数、敌人数值、分阶段落地 |
| [DEV_GUIDE](doc/DEV_GUIDE.md) | 技术：架构、模块边界、引擎坑、实现约定、变更日志 |
| [BACKLOG](doc/BACKLOG.md) | 待办池：想到的、暂缓的、待拍板的 |
| [doc/stages](doc/stages/) | 每个阶段的目标 / 效果 / 验收标准 + 分支策略 |
| [doc/archive](doc/archive/) | 历史决策存档（**不再是现行依据**） |

---

## 目录速览

```
core/         全局 Autoload：InputManager（采集）、FeedbackSystem（顿帧/震屏）
scenes/       characters（player / enemy）、combat（判定件 / 特效）、ui、test_scenes
scripts/      data（输入与招式数据）、tools（静态工具）
resources/    招式 .tres（所有帧数据/伤害/判定框都在这里调）
tests/        headless 机制断言（M0 建立）
doc/          见上表
```

---

## 开发方式（2026-11-04 起）

实现由 AI 完成，人负责**方向与验收**。每批一个 commit，交付四样：可跑 build、一句话验法、断言结果、已知未做。
详见 [ROADMAP §一](doc/ROADMAP.md)。
