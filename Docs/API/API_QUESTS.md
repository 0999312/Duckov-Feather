# API Reference — Quests / 任务 API

> **模块**：任务注册、任务关系、任务/奖励数据体系、自定义 QuestGiver
> **教程**：[USAGE.md 任务系统](../USAGE.md#5-任务系统--quests)

---

## 目录

- [QuestUtils — 任务工具](#questutils)
- [QuestData — 任务数据](#questdata)
- [TaskData — 任务目标体系](#taskdata)
- [RewardData — 奖励体系](#rewarddata)
- [QuestGiverUtils — 自定义 QuestGiver](#questgiverutils)
- [QuestDialogue — 任务对话](#questdialogue)
- [废弃 API](#废弃-api--obsolete)

---

## QuestUtils

**命名空间**：`FeatherMod` | **源码**：`Quests/QuestUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterQuest` | `static void RegisterQuest(Identifier id, QuestData data)` | 注册任务（domain 推导 owner） |
| | `static void RegisterQuest(QuestData data, string modid = FeatherMod)` | 注册任务（显式 modid） |
| `UnregisterQuest` | `static bool UnregisterQuest(Identifier id)` | 卸载单个 |
| `UnregisterQuestAll` | `static void UnregisterQuestAll(string modID)` | 批量卸载 |
| `AddQuestRelation` | `static void AddQuestRelation(Identifier id, Identifier? before = null, Identifier? after = null)` | 设置前置/后置关系 |
| `TryGetQuestId` | `static bool TryGetQuestId(Identifier id, out int questId)` | Identifier → 数字 ID |
| `TryGetQuestIdentifier` | `static bool TryGetQuestIdentifier(int questId, out Identifier id)` | 数字 ID → Identifier（O(1) 反查） |

> **数字 ID 全自动**：`QuestData.ID`（从 1000 起）、`TaskData.id`、`RewardData.id`（从 1 起）由 FML 自动分配 + 冲突检测，modder 无需设置。
> **关系图必须手动**：`RegisterQuest` 只登入 QuestCollection，**不会**自动建立前置/后置关系——忘记 `AddQuestRelation` 会导致任务链断裂。

---

## QuestData

**命名空间**：`FeatherMod.Quests` | **源码**：`Quests/QuestData.cs`

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | `Identifier?` | 任务标识（推荐方式） |
| `displayName` | `string` | 显示名 I18n key |
| `description` | `string` | 描述 I18n key |
| `questGiver` | `QuestGiverID` | 原生任务发放者枚举 |
| `QuestGiverIdentifier` | `Identifier?` | 自定义 QuestGiver（推荐，自动 BindQuest） |
| `requireLevel` | `int` | 等级门槛 |
| `requireScene` | `string` | 场景门槛 |
| `tasks` | `List<TaskData>` | 任务目标列表 |
| `rewards` | `List<RewardData>` | 奖励列表 |
| `onActivateDialogue` | `QuestDialogue?` | 接取时对话 |
| `onCompleteDialogue` | `QuestDialogue?` | 完成时对话 |

---

## TaskData

抽象基类，子类化实现不同目标类型：

| 类 | 关键字段 | 效果 |
|----|----------|------|
| `TaskRequireItem` | `itemIdentifier`(Identifier?) / `requiredAmount`(int) | 提交物品 |
| `TaskRequireMoney` | `money`(int) | 提交金钱 |
| `TaskRequireUseItem` | `itemIdentifier`(Identifier?) / `amount`(int) | 使用物品 |
| `TaskKillCount` | `requireAmount` / `weaponIdentifier`(Identifier?) / `requireEnemy`(string) / `requireHeadshot`(bool) / `withoutHeadShot`(bool) | 击杀目标 |
| `TaskKillByTagData` | `requireAmount` / `weaponTag`(string?) / `requireEnemyName`(string?) / `requireHeadShot`(bool) | 按武器标签击杀 |
| `TaskSubmitItemByTagData` | `itemTag`(string?) / `requireAmount` / `minQuality`(int?) / `durabilityCost`(bool) | 按标签提交物品 |
| `TaskCustomTask<T>` | `Initialization`(Action<T>?) | 挂载自定义 Task |

---

## RewardData

抽象基类，子类化实现不同奖励类型：

| 类 | 关键字段 | 效果 |
|----|----------|------|
| `RewardGiveItem` | `itemIdentifier`(Identifier?) / `amount`(int) | 给予物品 |
| `RewardEXP` | `amount`(int) | 给予经验 |
| `RewardMoney` | `amount`(int) | 给予金钱 |
| `RewardUnlockItem` | `itemIdentifier`(Identifier?) | 解锁商店物品 |
| `RewardUnlockEndowmentData` | `endowmentId`(Identifier) | 任务完成自动解锁天赋（AutoClaim） |
| `RewardUnlockBuildingData` | `buildingId`(Identifier) / `buildingInfo`(BuildingInfo) / `prefabName`(string) | 任务完成自动解锁建筑 |

---

## QuestGiverUtils

**命名空间**：`FeatherMod` | **源码**：`QuestGivers/QuestGiverUtils.cs`

自定义 QuestGiver ID（从 **50** 起分配，与原生枚举 0~11 无冲突）。

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterQuestGiver` | `static int RegisterQuestGiver(Identifier id, string? displayNameKey = null, string? modid = null)` | 注册并分配 int ID |
| `CreateQuestGiver` | `static GameObject? CreateQuestGiver(Identifier id, Vector3 position, bool spawnPOI = true)` | 创建独立交互点 GO |
| `BindQuest` | `static bool BindQuest(Identifier questGiverId, Identifier questId)` | 绑定任务到 QuestGiver |
| `TryGetQuestGiver` | `static bool TryGetQuestGiver(Identifier id, out GameObject go)` | 查询 GO |
| `TryGetQuestGiverId` | `static bool TryGetQuestGiverId(Identifier id, out int questGiverId)` | Identifier → int ID |
| `IsCustomQuestGiverId` | `static bool IsCustomQuestGiverId(int questGiverId)` | 是否自定义 ID |
| `UnregisterQuestGiver` | `static bool UnregisterQuestGiver(Identifier id)` | 卸载 |
| `UnregisterAllQuestGivers` | `static int UnregisterAllQuestGivers(string modid)` | 批量卸载 |

> **设计原则**：QuestGiver 是纯交互层（ID 映射 + 交互点）。模型/捏脸/对话由 `FriendlyNpcUtils` 管理，两者经 `FriendlyNpcUtils.BindQuestGiver` 关联。

---

## QuestDialogue

| 字段 | 类型 | 说明 |
|------|------|------|
| `npcId` | `Identifier` | 对话 NPC |
| `sequence` | `DialogueSequence` | 对话序列 |
| `actorId` | `string?` | 发言者（缺省用 NPC 的 ActorId） |

---

## 废弃 API / Obsolete

| 成员 | 替代 |
|------|------|
| （本模块暂无） | |
