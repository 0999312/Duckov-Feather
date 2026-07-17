# DockovDrinks v0.5.0 BUG 审查报告 — 提交 FeatherMod 框架方

**提交方**：DockovDrinks 模组（`dockov_drinks`）
**审查日期**：2026-07-17
**审查范围**：4 个核心 BUG + 2 个额外问题
**审查方法**：通读项目全部 7 个 .cs 源码文件 + FeatherMod USAGE.md 完整 API 文档（2821 行）+ Framework DLL 元数据分析

---

## 背景

DockovDrinks 添加了一个饮品制作台建筑和一名友善 NPC（老政），NPC 同时担任商人和任务提供方。建筑建成后通过 `FriendlyNpcUtils.SpawnFriendlyNpcAsync()` 生成 NPC。

当前版本存在 4 个核心 BUG，本报告逐一分析根因、判定框架与项目的责任归属、并列出框架侧 API 缺失。

---

## BUG 1：建筑回收时 NPC 不消失，再放置时重复刷新

### 现象
玩家在基地中回收/拆除饮品制作台后，已生成的老政 NPC 残留在场景中不被清理。再次放置建筑时，第二个（第三个…）老政被生成，NPC 数量只增不减。

### 根因
项目在 `BuildingConfig.cs` 中通过 `BuildingUtils.OnBuildingBuilt()` 注册了建成回调来生成 NPC，但：**框架没有提供对称的 `OnBuildingDemolished` / `OnBuildingRetrieved` 回调 API。** 同时项目也未通过其他手段（Harmony Patch、游戏原生事件）监听建筑回收来清理 NPC，建成回调内部也不做幂等检查（每次放置都无条件调用 `SpawnFriendlyNpcAsync`）。

### 责任判定

| 方 | 责任 | 说明 |
|---|---|---|
| **框架** | 20%（辅助责任） | `BuildingUtils` 只提供了 `OnBuildingBuilt` / `OffBuildingBuilt`，缺少 `OnBuildingDemolished` 钩子。项目的建筑回收清理需求完全无法通过框架标准 API 实现。 |
| **项目** | 80%（主要责任） | `FriendlyNpcUtils.RemoveNpc()` API 已存在但未被调用。项目既未通过其他手段实现建筑回收监听，也未在生成前做存在性检查。 |

### 框架 API 缺失

> **`BuildingUtils` 缺少 `OnBuildingDemolished(Identifier buildingId, Action<Building> callback)` 回调注册方法。**
> 建议与 `OnBuildingBuilt` 对称实现，在游戏原生建筑回收/拆除逻辑中注入钩子。

### 项目侧待修
- [ ] 在建筑回收时调用 `FriendlyNpcUtils.RemoveNpc(NpcConfig.Id)`
- [ ] `OnBuildingBuilt` 回调内增加幂等检查，避免重复生成

---

## BUG 2：对话 UI 完全没显示（面板不出现）

### 现象
建筑建成后应播放老政的初次对话字幕，但对话 UI 面板完全不出现。本地化链路已确认畅通（4 条 `dialogue_laozheng_greet_*` key 在 `zh_cn.json` 中均正确存在）。

### 根因
根据框架文档 §30.3，`DialogueUtils.PlaySubtitles()` 执行链路为：

```
PlaySubtitles → DuckovDialogueActor.Get(actorId) → DialogueTree.OnDialogueStarted → DialogueUI 显示面板
```

面板完全没出现，说明 `OnDialogueStarted` 未触发，断点最可能在第一步——`DuckovDialogueActor.Get("laozheng")` 返回 null。

原因在于时序：`SpawnFriendlyNpcAsync` 返回 NPC GameObject 后，项目代码立即 `await` 播放对话。但此时 NPC 的 Unity 生命周期（`Awake` → `OnEnable`）可能尚未完成，`DuckovDialogueActor.OnEnable()` 中的 `Register(this)` 还未执行，Actor 未注册到全局查找表。**`SpawnFriendlyNpcAsync` 的"完成"语义不包含"所有组件已初始化就绪"。**

### 责任判定

| 方 | 责任 | 说明 |
|---|---|---|
| **框架** | 70%（主要责任） | `SpawnFriendlyNpcAsync` 的完成契约不够强——返回 GameObject 不等于 Actor 已注册。框架应保证 spawn 完成时 NPC 的核心组件（至少 `DuckovDialogueActor`）已就绪，或提供就绪回调。 |
| **项目** | 30%（次要责任） | spawn 后未做任何等待就直接触发对话。插入一帧延迟（`await UniTask.Yield()`）可能缓解此问题。 |

### 框架 API 缺失

> **`SpawnFriendlyNpcAsync` 缺乏"NPC 初始化就绪"的语义保证。**
> 建议方案：(a) spawn 内部确保 `DuckovDialogueActor.OnEnable` 已完成注册后再 resolve Task；(b) 提供 `onReady` 回调参数；(c) 至少在文档中明确说明 spawn 返回后需要等待一帧再操作 NPC。

### 项目侧待修
- [ ] spawn 后增加延迟等待 NPC 就绪
- [ ] 对话触发应从建成回调移至玩家接近 NPC 后（设计调整）

---

## BUG 3：交互 NPC 时打开笔记，而非商店和任务界面

### 现象
玩家点击老政 NPC 进行交互时，打开的是游戏原版 NPC 默认信息面板（笔记/角色信息卡），而非商店界面或任务界面。NPC 已配置 `Role = NpcRole.Merchant`、`ShopId`、`QuestGiverId`，商店商品和任务数据均已正确注册。

### 根因
框架文档存在断层：

**§26.3 NpcRole 行为表** 声称：
> `Merchant` — 交互打开商店 UI（需 `ShopId`）

**§19.5 GameViews 内置 View 类型** 注明：
> `GameViews.Shop` — 商店（**需自行注册打开方法**）
> `GameViews.Quest` — 任务（**需自行注册打开方法**）

`GameViews.Shop` 和 `GameViews.Quest` 虽为内置枚举值，但其 View 打开方法**未**由框架自动注册。当 NPC 交互时框架尝试调度 `GameViews.Shop`，发现 ViewDispatcher 中无对应 handler，回退到游戏原版 NPC 默认交互行为（显示信息面板/笔记）。

项目的 `NpcConfig` 设置了 Role 和 ShopId，但未调用 `ViewDispatcher.Register()`、`FriendlyNpcUtils.BindShop()` 或 `InteractionUtils.AttachToNPC()` 完成交互链路的手动注册。

此外，`NpcRole` 是单选枚举（`Merchant | QuestGiver | Companion | DialogueOnly`），不支持一个 NPC 同时承担多个角色（老政需同时为商人 + 任务提供方）。

### 责任判定

| 方 | 责任 | 说明 |
|---|---|---|
| **框架** | 30%（文档误导 + 设计限制） | §26.3 行为表承诺与 §19.5 的手动注册要求自相矛盾，形成误导。`NpcRole` 单选设计无法表达复合身份。框架提供的底层交互 API 本身是完整的，但文档断层和默认行为缺失导致预期偏差。 |
| **项目** | 70%（未走完手动链路） | 框架虽文档有断层，但完整的手动链路 API 均存在。项目未调用 `ViewDispatcher.Register()`、`BindShop()`、`InteractionUtils`。 |

### 框架 API 缺失 / 建议

> **1. `GameViews.Shop` / `GameViews.Quest` 的 View 打开方法应自动注册。**
> 既然 §26.3 承诺 Merchant 角色会打开商店 UI，框架应在内部处理好 `GameViews.Shop` 的 ViewDispatcher 注册，而非依赖 modder 手动补全。

> **2. `NpcRole` 应支持复合角色（Flags 枚举）。**
> 当前单选枚举意味着模组无法表达"此 NPC 既是商人也是任务提供方"这一常见设计。建议改为 `[Flags]` 枚举，或提供 `SecondaryRole` 字段。

> **3. 文档断层修复。**
> 如短期不便改代码，至少应更新 §26.3 行为表，注明需配合 `ViewDispatcher.Register()` 和 `BindShop()` 使用，而非仅设 Role 和 ShopId。

### 项目侧待修
- [ ] 调用 `ViewDispatcher.Register()` 注册 Shop/Quest 的 View 打开方法
- [ ] 调用 `FriendlyNpcUtils.BindShop()` 和 `BindQuestGiver()`
- [ ] 如框架短期内不修复 NpcRole 限制，需通过 `InteractionUtils` 自定义交互替代框架默认行为

---

## BUG 4：NPC 不会自动跟随玩家旋转（面向玩家）

### 现象
老政 NPC 生成后保持初始朝向（`SpawnRotation = Quaternion.identity`），玩家移动时 NPC 不会面向玩家，始终面朝同一方向。

### 根因
`FriendlyNpcConfig.SpawnRotation` 仅设置生成瞬间的初始朝向。FeatherMod 框架没有提供任何运行时 NPC 面向玩家的功能——没有相关配置项、没有相关工具方法、没有可挂载的组件。

游戏核心库 `TeamSoda.Duckov.Core.dll` 中存在 `AimToPlayer`（NodeCanvas 行为树 Action）、`ForceTurnTo`、`RotateTowards` 等底层能力，但框架未将其暴露为 FriendlyNpcUtils 的可用功能。

### 责任判定

| 方 | 责任 | 说明 |
|---|---|---|
| **框架** | 0% | 框架的任务是提供 NPC 注册/生成/交互的数据层与注册层能力，运行时行为逻辑不属于框架职责范围。但作为使用频率极高的共性需求，建议框架内置此功能以减少模组重复实现。 |
| **项目** | 100% | 项目从头到尾没有实现任何 NPC 运行时面向玩家的逻辑——没有 MonoBehaviour、没有 Update、没有利用游戏底层能力。 |

### 框架 API 建议（非必须）

> **建议在 `FriendlyNpcConfig` 中新增 `AutoFacePlayer`（bool，默认 false）选项。**
> 设为 `true` 时，`SpawnFriendlyNpcAsync` 在 NPC 上自动挂载内部组件，在运行时持续面向玩家。向后兼容，不影响现有模组。

### 项目侧待修
- [ ] 实现 NPC 面向玩家的逻辑（挂载 MonoBehaviour / 调用游戏底层 API）

---

## 汇总

### 责任分布总表

| BUG | 框架责任 | 项目责任 | 框架 API 缺失 |
|---|---|---|---|
| 1. 建筑回收 NPC 残留 + 重复 | 20% | 80% | `OnBuildingDemolished` 回调 |
| 2. 对话 UI 不显示 | 70% | 30% | `SpawnFriendlyNpcAsync` 就绪保证 |
| 3. 交互开笔记非商店 | 30% | 70% | `GameViews.Shop/Quest` 自动注册；`NpcRole` Flags 支持 |
| 4. NPC 不面向玩家 | 0% | 100% | —（建议新增 `AutoFacePlayer` 选项） |

### 框架需处理事项清单

| # | 事项 | 类型 | 优先级 |
|---|---|---|---|
| 1 | `BuildingUtils` 新增 `OnBuildingDemolished` 回调注册方法 | API 新增 | 中 |
| 2 | `SpawnFriendlyNpcAsync` 增加 NPC 初始化就绪保证（或明确文档说明时序契约） | API 修复 / 文档补充 | **高** |
| 3 | `GameViews.Shop` / `GameViews.Quest` 的 ViewDispatcher 应自动注册（消除 §26.3 与 §19.5 之间的文档断层） | 框架行为修正 | **高** |
| 4 | `NpcRole` 改为 Flags 枚举，或新增 `SecondaryRole` 字段，支持复合角色 | API 增强 | 低 |
| 5 | `FriendlyNpcConfig` 新增 `AutoFacePlayer` 选项（建议，非必须） | API 增强 | 低 |
| 6 | 更新 §26.3 NpcRole 行为表文档，若手动步骤不可避免则明确列出完整配置链路 | 文档修正 | 中 |

### 项目侧待修（仅供框架方参考，非转交内容）

| # | 事项 |
|---|---|
| 1 | 建筑回收时清理 NPC + 建成回调幂等检查（可用 Harmony Patch 绕过框架 API 缺失） |
| 2 | spawn 后增加帧延迟确保 NPC 就绪再播放对话；对话触发移至玩家接近 NPC 后 |
| 3 | 完成 `ViewDispatcher.Register` + `BindShop` + `BindQuestGiver` 手动链路 |
| 4 | 实现 NPC 面向玩家的 MonoBehaviour 组件 |

---

*本报告由 DockovDrinks 开发者提供。如需补充测试用例或进一步技术细节，请联系模组开发者。*
