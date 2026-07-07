# 问题记录与修复计划

> 创建日期：2026-07-03
> 基于对 Phase 4 Endowment 系统和全局代码审计的发现。

---

## 设计原则：Publicizer 分层

在进入具体问题之前，先明确 FML 架构中 Publicizer 的正确使用边界：

```
┌─────────────────────────────────────────────────────┐
│ Modder（框架使用者）                                   │
│ → 只接触 FML 自有 DTO（EndowmentConfig /               │
│   EnemyPresetData / BuffConfig / BuildingConfig 等）   │
│ → 永远不接触游戏原生类型（EndowmentEntry /              │
│   CharacterRandomPreset / Buff 等）                    │
│ → Publicizer 对 modder 无效且不应被 modder 依赖         │
└──────────────────────┬──────────────────────────────┘
                       │ FML public API（DTO 入口）
┌──────────────────────▼──────────────────────────────┐
│ FML 内部实现（框架层）                                  │
│ → 接受 DTO，内部调用 ToNative() 转换为游戏原生类型       │
│ → 可直接使用 Publicizer 公开的成员（entry.index = ...）  │
│ → 可直接调用游戏原生 API（EndowmentManager.Unlock）     │
│ → Patch 层可直接访问游戏私有字段（__instance.entries）   │
└─────────────────────────────────────────────────────┘
```

**规则**：
- Publicizer 覆盖 `TeamSoda.Duckov.Core` 等 = **正确且必要**，供 FML 内部使用
- modder 的 public API 接受游戏原生类型（`EndowmentEntry`、`CharacterRandomPreset` 等）= **设计缺陷**，必须替换为 FML DTO
- modder 的 public API 接受 `object[]` = **设计缺陷**，必须替换为强类型 DTO
- `EnemyPresetData` DTO 已为敌人系统做了正确示范（modder 用 `EnemyPresetData` 配置，FML 内部 `ToNative()` 转 `CharacterRandomPreset`）

---

## 问题 1：Endowment 系统 modder 视角 API 设计缺陷

### 严重程度：🔴 高（架构级）

### 现状

当前 Endowment 的 public API 迫使 modder 操作游戏内部类型：

```csharp
// ❌ 当前 API：modder 被迫操作游戏原生类型
RegisterEndowment(Identifier id, EndowmentEntry endowment, string? modid);
RegisterEndowment(Identifier id, object[] modifiers, ...);
```

**modder 视角的痛苦**：
1. 需要 `new GameObject` + `AddComponent<EndowmentEntry>`（游戏原生 MonoBehaviour）
2. 需要反射设置 `EndowmentEntry` 的 private 字段（`modifiers`、`nameKey`、`requirementText`）
3. `object[]` 参数类型不安全——modder 需要逆向工程才知道实际是 `EndowmentEntry.ModifierDescription[]`
4. 嵌套类型 `EndowmentEntry.ModifierDescription` 是游戏内部 struct，Publicizer 对 modder 不可见

### 修复方案

#### 第一步：创建 FML 自有 DTO

新建 `FeatherMod/Endowment/EndowmentConfig.cs`：

```csharp
namespace FeatherMod
{
    public class EndowmentModifier
    {
        public string StatKey { get; set; } = "";
        public ModifierType Type { get; set; }
        public float Value { get; set; }
    }

    public class EndowmentConfig
    {
        public EndowmentModifier[] Modifiers { get; set; } = Array.Empty<EndowmentModifier>();
        public bool UnlockedByDefault { get; set; }
        public string RequirementTextKey { get; set; } = "";
    }
}
```

#### 第二步：重构 public API

```csharp
// ✅ 修复后：modder 用纯 FML DTO 配置
EndowmentUtils.RegisterEndowment(id, new EndowmentConfig
{
    Modifiers = new[]
    {
        new EndowmentModifier { StatKey = "moveSpeed", Type = ModifierType.PercentageAdd, Value = 0.15f },
        new EndowmentModifier { StatKey = "maxHealth", Type = ModifierType.PercentageAdd, Value = -0.1f }
    },
    UnlockedByDefault = false,
    RequirementTextKey = "endowment_assassin_requirement"
});
```

**API 变更**：
- 新增 `RegisterEndowment(Identifier, EndowmentConfig, string?)` —— modder 唯一入口
- 删除 `RegisterEndowment(Identifier, object[], ...)` —— 去掉 `object[]`
- 保留 `RegisterEndowment(Identifier, EndowmentEntry, string?)` 但标记 `[Obsolete]`

#### 第三步：FML 内部实现（Publicizer 直接访问）

```csharp
// FML 内部：DTO → 游戏原生 EndowmentEntry
// 这里的 entry.index = idx、entry.modifiers = ... 利用了 Publicizer 公开的字段
private static EndowmentEntry CreateNativeEntry(EndowmentConfig config, Identifier id)
{
    var go = new GameObject($"Endowment_{id.Path}");
    var entry = go.AddComponent<EndowmentEntry>();

    entry.requirementTextKey = config.RequirementTextKey;

    var nativeModifiers = new EndowmentEntry.ModifierDescription[config.Modifiers.Length];
    for (int i = 0; i < config.Modifiers.Length; i++)
    {
        nativeModifiers[i] = new EndowmentEntry.ModifierDescription
        {
            statKey = config.Modifiers[i].StatKey,
            type = config.Modifiers[i].Type,
            value = config.Modifiers[i].Value
        };
    }
    entry.modifiers = nativeModifiers;

    return entry;
}
```

#### 第四步：清理 FML 内部无效反射

| 文件 | 当前反射 | 改为（利用 Publicizer） |
|------|---------|------------------------|
| `EndowmentUtils.cs:83-95` | `GetField("modifiers"/"nameKey"/"requirementText")` | `entry.modifiers = ...; entry.requirementTextKey = ...` |
| `EndowmentUtils.cs:143-146` | `GetMethod("IsUnlocked")` | `EndowmentManager.GetEndowmentUnlocked(index)` |
| `EndowmentUtils.cs:154-165` | `GetMethod("UnlockEndowment")` | `EndowmentManager.UnlockEndowment(index)` |
| `EndowmentUtils.cs:174-176` | `GetMethod("SelectIndex")` | `EndowmentManager.Instance.SelectIndex(index)` |
| `EndowmentUtils.cs:182-187` | `GetField("currentIndex")` | `EndowmentManager.CurrentIndex` |
| `EndowmentManagerPatch.cs:32-35` | `GetField("entries")` | `__instance.entries`（Publicizer） |
| `EndowmentManagerPatch.cs:46-48` | `GetField("index")` | `entry.index = idx`（Publicizer） |
| `EndowmentRegistry.cs:68-75` | `GetField("currentIndex")` + `GetMethod("SelectIndex")` | `EndowmentManager.CurrentIndex` + `EndowmentManager.Instance.SelectIndex(EndowmentIndex.None)` |

---

## 问题 2：Endowment 注册后游戏内 UI 无法正常选择

### 严重程度：🔴 高

### 现状

通过 `EndowmentManagerPatch.Awake_Postfix` 注入到 `EndowmentManager.entries` 的自定义天赋（EndowmentIndex ≥10），在游戏内的 `EndowmentSelectionPanel` UI 中可能无法正常显示和选择。

### 分析

1. `EndowmentManager.SelectIndex(EndowmentIndex index)` 方法本身**不检查 index 范围**（见反编译源码第 194-199 行）
2. UI 层 `EndowmentSelectionPanel` 可能对 `EndowmentIndex` 做了枚举范围检查（0-4），拒绝 ≥10 的索引
3. `EndowmentManager.MakeSureEndowmentAchievementsUnlocked()` 硬编码 `for (int i = 0; i < 5; i++)` 仅处理原生 5 个天赋

### 需要进一步调查

- `DecompiledDLL/Core/Duckov.Endowment.UI/` 中 `EndowmentSelectionPanel` 的源码
- UI 层是否只显示 `EndowmentManager.Entries` 中的所有条目，还是有过滤逻辑
- 如果有 index 范围检查 → 添加 Harmony Prefix 拦截

---

## 问题 3：全局 `internal static Registry` 设计缺陷（7 个模块）

### 严重程度：🔴 高

### 现状

7 个模块的 `*Utils` 类将 `Registry` 属性声明为 `internal static`：

| # | 文件 | 行号 | 成员 |
|---|------|------|------|
| 1 | `EndowmentUtils.cs` | 23 | `internal static EndowmentRegistry Registry` |
| 2 | `BuildingUtils.cs` | 21 | `internal static BuildingRegistry Registry` |
| 3 | `BuffUtils.cs` | 13 | `internal static BuffRegistry Registry` |
| 4 | `PerkTreeUtils.cs` | 22 | `internal static PerkTreeRegistry Registry` |
| 5 | `EnemyUtils.cs` | 18 | `internal static EnemyRegistry Registry` |
| 6 | `ShopUtils.cs` | 17 | `internal static ShopRegistry Registry` |
| 7 | `QuestUtils.cs` | 15 | `internal static QuestRegistry Registry` |

**直接后果**：`EndowmentManagerPatch.cs:26` 通过反射访问 FML 自己的属性：
```csharp
typeof(EndowmentUtils).GetProperty("Registry", BindingFlags.Static | BindingFlags.NonPublic)
```

`CraftingUtils.cs` 已做了正确示范——它的注册表是 `public static readonly` 字段。

### 修复

```csharp
// 全部 7 个文件：internal → public
public static EndowmentRegistry Registry => _endowmentRegistry;
```

同时将 `EndowmentRegistry` 的 4 个 `internal` 方法（`AllocateIndex`、`TryGetIndex`、`TryGetIdentifier`、`GetAllEntries`）改为 `public`。

---

## 问题 4：其他系统需要类似 DTO 封装

### 严重程度：🟡 中

### 需要审查的模块

| 模块 | 当前暴露的游戏类型 | 是否已有 FML DTO | 状态 |
|------|-------------------|-----------------|------|
| **Endowment** | `EndowmentEntry` | ❌ 无 → 需创建 `EndowmentConfig` | 问题 1 |
| **Enemy** | `CharacterRandomPreset` | ✅ `EnemyPresetData`（已完成） | **正确示范** |
| **Building** | `Building` prefab + `BuildingInfo` struct | ⚠️ 部分（`CreateSimpleBuilding` 代码端） | 需审查 |
| **Buff** | `Buff` prefab | ❌ 无 DTO | 需评估 |
| **PerkTree** | `Perk` GameObject | ⚠️ 有 `AddPerk` 但需传入 `PerkRequirement`、`Sprite` | 可接受 |
| **Shop** | `ShopGoodsData` DTO | ✅ FML 自有 DTO | **正确** |
| **Economy** | 无（纯值类型 API） | ✅ 不需要 DTO | **正确** |

**决策标准**：
- 如果 modder 需要 `new GameObject` + `AddComponent<GameType>` → 需要 DTO
- 如果 modder 需要反射设置 private 字段 → 需要 DTO
- 如果 modder 仅传递值类型参数（string、int、Identifier）→ 不需要 DTO

### 全局审计摘要（explore agent，2026-07-03）

共发现 **55 处**反射调用，分类如下：

| 类别 | 数量 | 处理方式 |
|------|------|---------|
| FML 内部反射游戏类型（Publicizer 已覆盖 → 可直接访问） | ~30 | FML 内部改为直接访问 |
| FML 内部反射外部库 NodeCanvas（合理） | 8 | 保留 |
| modder API 暴露游戏类型（设计缺陷） | ~10 | 创建 FML DTO |
| PerkTree 系统 9 处游戏数据反射 | 9 | 改为直接访问 + DTO 评估 |

---

## 修复优先级

| 优先级 | 问题 | 预估工作量 | 说明 |
|--------|------|-----------|------|
| **P0** | 问题 1：Endowment DTO + API 重构 | ~100 LOC | 核心架构修复，消除 modder 反射 |
| **P0** | 问题 3：7 个 `internal Registry` → `public` | 7 行 | 消除 Patch 层反射 FML 自身 |
| **P1** | 问题 2：Endowment UI 选择修复 | 调查 + ~30 LOC | 需先分析 UI 源码 |
| **P1** | 问题 1 第四步：清理 FML 内部无效反射 | ~30 LOC | 利用 Publicizer 替换反射 |
| **P2** | 问题 4：审查 Building/Buff/PerkTree DTO 需求 | 调查 + ~50 LOC | 逐个模块评估 |

---

## 附录 A：Publicizer 覆盖范围

`FeatherMod.csproj` 第 65-68 行：

```xml
<Publicize Include="TeamSoda.Duckov.Core" IncludeVirtualMembers="false" />
<Publicize Include="TeamSoda.Duckov.Utilities" IncludeVirtualMembers="false" />
<Publicize Include="ItemStatsSystem" IncludeVirtualMembers="false" />
```

| 程序集 | 包含命名空间 | FML 内部可用 |
|--------|------------|-------------|
| `TeamSoda.Duckov.Core` | `Duckov.Endowment`, `Duckov.Buildings`, `Duckov.PerkTrees`, `Duckov.Buffs`, `Duckov.Economy`, `Duckov.Quests`, `Duckov` | ✅ |
| `TeamSoda.Duckov.Utilities` | `Duckov.Utilities` | ✅ |
| `ItemStatsSystem` | `ItemStatsSystem` | ✅ |

**未覆盖**（这些程序集中的反射调用是合理的）：
- `ParadoxNotion.dll` — NodeCanvas 框架
- `FMODUnity.dll` — FMOD 音频引擎
- `Unity*.dll` — Unity 引擎
