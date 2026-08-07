# API Reference — Perk & Endowment / 技能树与天赋 API

> **模块**：Perk 技能树、Perk 行为、天赋注册/解锁/选择
> **教程**：[USAGE.md Perk 技能树](../USAGE.md#10-perk-技能树--perk-trees)、[USAGE.md 天赋系统](../USAGE.md#11-天赋系统--endowment)

---

## 目录

- [PerkTreeUtils — 技能树工具](#perktreeutils)
- [PerkConfig — Perk 配置](#perkconfig)
- [PerkBehaviourConfig — Perk 行为配置](#perkbehaviourconfig)
- [EndowmentUtils — 天赋工具](#endowmentutils)
- [EndowmentConfig / EndowmentModifier — 天赋配置](#endowmentconfig--endowmentmodifier)
- [EndowmentRegistry — 索引分配](#endowmentregistry)

---

## PerkTreeUtils

**命名空间**：`FeatherMod` | **源码**：`PerkTrees/PerkTreeUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterPerkTree` | `static PerkTree RegisterPerkTree(Identifier id, bool horizontal = false)` | 注册自定义技能树（Domain=modid, Path=treeID） |
| `AddPerk` | `static Perk AddPerk(Identifier treeId, PerkConfig config)` | 添加 Perk（树不存在抛 ArgumentException） |
| `ConnectPerks` | `static void ConnectPerks(Identifier fromPerkId, Identifier toPerkId)` | 建立前置关系（支持跨 mod / 原版懒注册） |
| `AddPerkBehaviour<T>` | `static T AddPerkBehaviour<T>(Identifier perkId) where T : PerkBehaviour` | 挂载自定义 PerkBehaviour |
| `ForceUnlock` | `static void ForceUnlock(Identifier perkId)` | 强制解锁 |
| `IsPerkUnlocked` | `static bool IsPerkUnlocked(Identifier perkId)` | 是否已解锁（Machine 门控等场景） |
| `RemovePerk` | `static bool RemovePerk(Identifier id)` | 移除单个 |
| `RemoveAllPerks` | `static int RemoveAllPerks(string modid)` | 批量移除 |
| `DumpAllPerkTrees` | `static void DumpAllPerkTrees()` | 调试输出 |

**Identifier 语义**：
- 自定义树：`("mymod", "combat_perks")` — Domain=modid, Path=treeID
- 原版树注入：`("duckov", "CombatTree")` — 往原版树添加 Perk
- 原版 Perk 引用：`("duckov", "CombatTree/Marksman")` — Path = `treeID/perkName`（首次引用自动懒注册）

---

## PerkConfig

**命名空间**：`FeatherMod` | **源码**：`PerkConfig.cs`

| 字段 | 类型 | 说明 |
|------|------|------|
| `PerkId` | `Identifier` | 必填 |
| `Icon` | `Sprite?` | 图标 |
| `DisplayNameKey` | `string` | 显示名 I18n key（默认"未命名技能"） |
| `HasDescription` | `bool` | 有描述 |
| `Quality` | `DisplayQuality` | 品质 |
| `DefaultUnlocked` | `bool` | 默认解锁 |
| `RequiredLevel` | `int` | 等级门槛 |
| `CostItems` | `ItemEntry[]?` | 解锁材料 |
| `Money` | `long` | 解锁金币 |
| `RequireTimeTicks` | `long` | 解锁耗时（`TimeSpan.FromMinutes(30).Ticks`） |
| `RequiredPerks` | `Identifier[]?` | 前置 Perk（支持 `duckov:treeID/perkName`） |
| `Behaviours` | `PerkBehaviourConfig[]?` | 行为配置 |
| `Position` | `Vector2?` | 树内位置 |

---

## PerkBehaviourConfig

**命名空间**：`FeatherMod` | **源码**：`PerkTrees/PerkBehaviourConfigs.cs`

声明式 Perk 行为（7 种封装）：

| 类 | 字段 | 效果 |
|----|------|------|
| `UnlockFormulaConfig` | — | 自动解锁 `requirePerk` 匹配的配方 |
| `UnlockAchievementConfig` | `AchievementKey`(string) | 解锁成就 |
| `ModifyStatsConfig` | `Entries`(StatModifierEntry[]) | 属性修正 |
| `StatModifierEntry` | `Key`(string) / `Value`(float) / `Percentage`(bool) | 单条属性修正 |
| `AddPlayerStorageConfig` | `Capacity`(int) | 增加玩家储物容量 |
| `BlackMarketRefreshTimeConfig` | `Amount`(float=-0.1f) | 黑市刷新时间 |
| `BlackMarketRefreshChanceConfig` | `AddAmount`(int=1) | 黑市刷新次数 |
| `UnlockShopItemConfig` | `ItemId`(Identifier) | 解锁商店物品 |

---

## EndowmentUtils

**命名空间**：`FeatherMod` | **源码**：`Endowment/EndowmentUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterEndowment` | `static void RegisterEndowment(Identifier id, EndowmentConfig config, string? modid = null)` | 注册（推荐，纯 C# DTO） |
| `RegisterEndowmentWithIndex` | `static void RegisterEndowmentWithIndex(Identifier id, EndowmentEntry endowment, EndowmentIndex explicitIndex, string modid)` | 兜底：强指定枚举空间 |
| `UnregisterEndowment` | `static bool UnregisterEndowment(Identifier id)` | 移除 |
| `UnregisterAllEndowments` | `static int UnregisterAllEndowments(string modid)` | 批量移除 |
| `GetEndowment` | `static EndowmentEntry? GetEndowment(Identifier id)` | 查询 |
| `TryGetEndowment` | `static bool TryGetEndowment(Identifier id, out EndowmentEntry entry)` | 安全查询 |
| `GetAllEndowments` | `static IReadOnlyList<Identifier> GetAllEndowments(string modid)` | 列出全部 |
| `IsEndowmentUnlocked` | `static bool IsEndowmentUnlocked(Identifier id)` | 是否已解锁 |
| `UnlockEndowment` | `static bool UnlockEndowment(Identifier id)` | 解锁 |
| `SelectEndowment` | `static void SelectEndowment(Identifier id)` | 选择/激活 |
| `GetCurrentSelection` | `static Identifier? GetCurrentSelection()` | 当前选中（未选中返回 null） |

---

## EndowmentConfig / EndowmentModifier

**命名空间**：`FeatherMod` | **源码**：`Endowment/EndowmentConfig.cs`

| 类型 | 字段 | 说明 |
|------|------|------|
| `EndowmentConfig` | `Modifiers`(EndowmentModifier[]) / `Icon`(Sprite?) / `UnlockedByDefault`(bool=false) / `RequirementTextKey`(string) | 天赋配置 |
| `EndowmentModifier` | `StatKey`(string) / `Type`(ModifierType) / `Value`(float) | 单条属性修正 |

---

## EndowmentRegistry

| 方法 | 签名 | 说明 |
|------|------|------|
| `AllocateIndex` | `EndowmentIndex AllocateIndex(Identifier id)` | 分配内部索引 |
| `TryGetIndex` | `bool TryGetIndex(Identifier id, out EndowmentIndex index)` | Identifier → 索引 |
| `TryGetIdentifier` | `bool TryGetIdentifier(EndowmentIndex index, out Identifier id)` | 索引 → Identifier |
| `GetAllEntries` | `IEnumerable<EndowmentEntry> GetAllEntries()` | |

---

## 废弃 API / Obsolete

| 成员 | 替代 |
|------|------|
| `RegisterEndowment(Identifier, EndowmentEntry, ...)` | `RegisterEndowment(Identifier, EndowmentConfig, ...)` |
| `RegisterEndowment(Identifier, object[] modifiers, ...)` | `RegisterEndowment(Identifier, EndowmentConfig, ...)` |
