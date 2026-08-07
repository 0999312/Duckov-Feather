# API Reference — Crafting / 合成 API

> **模块**：合成配方、分解配方、物品引用（ItemEntry）、标签成本、耐久折算
> **教程**：[USAGE.md 合成系统](../USAGE.md#4-合成系统--crafting)

---

## 目录

- [ItemEntry — 物品引用](#itementry)
- [CraftingUtils — 合成工具](#craftingutils)
- [CraftingFormulaData — 合成配方数据](#craftingformuladata)
- [DecomposeFormulaData — 分解配方数据](#decomposeformuladata)
- [TagCostEntry / TagItemCost — 标签成本](#tagcostentry--tagitemcost)
- [废弃 API](#废弃-api--obsolete)

---

## ItemEntry

**命名空间**：`FeatherMod.Crafting` | **源码**：`CraftingData.cs`

配方/成本中引用物品的统一结构，同时支持 Identifier、typeID 与标签匹配。

| 字段 | 类型 | 说明 |
|------|------|------|
| `ItemId` | `Identifier?` | 物品 Identifier |
| `Amount` | `int` | 数量 |
| `ItemTag` | `string?` | 标签匹配模式（非精确 typeID） |
| `MinQuality` | `int?` | 最低品质（仅标签匹配生效） |
| `DurabilityCost` | `bool` | 耐久度折算 |

| 方法 | 签名 | 说明 |
|------|------|------|
| `Of` | `static ItemEntry Of(Identifier id, int amount)` | 从 Identifier 创建 |
| | `static ItemEntry Of(string idString, int amount)` | 从 `"domain:path"` 字符串创建 |
| `ByTag` | `static ItemEntry ByTag(string tag, int amount, int? minQuality = null)` | 按标签匹配（任意带该标签物品） |
| `WithDurabilityCost` | `ItemEntry WithDurabilityCost(bool enabled = true)` | 启用耐久折算（满耐久=1 个，50%=0.5 个） |

> `ByTag` + `WithDurabilityCost` 由内部 `TagCostRegistry` + `TagCostValidator` + `CraftingManagerPatch` 拦截合成流程实现。

---

## CraftingUtils

**命名空间**：`FeatherMod` | **源码**：`CraftingUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `AddCraftingFormula` | `static void AddCraftingFormula(CraftingFormulaData data)` | 添加合成配方（推荐） |
| `AddDecomposeFormula` | `static void AddDecomposeFormula(DecomposeFormulaData data)` | 添加分解配方（推荐） |
| `RemoveAllAddedFormulas` | `static void RemoveAllAddedFormulas(string? modid = null)` | 卸载合成配方 |
| `RemoveAllAddedDecomposeFormulas` | `static void RemoveAllAddedDecomposeFormulas(string? modid = null)` | 卸载分解配方 |
| `OpenFilteredCraftingView` | `static void OpenFilteredCraftingView(params string[] tags)` | 打开过滤式合成界面 |
| 字段 | `static readonly CraftingFormulaRegistry craftingFormulaRegistry` | 合成配方注册表 |
| 字段 | `static readonly DecomposeRegistry decomposeRegistry` | 分解配方注册表 |

---

## CraftingFormulaData

**命名空间**：`FeatherMod.Crafting` | **源码**：`CraftingData.cs`

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `Id` | `Identifier` | — | 配方标识（必填） |
| `Money` | `long` | — | 金币成本 |
| `CostItems` | `ItemEntry[]` | — | 成本物品 |
| `Result` | `ItemEntry` | — | 产物 |
| `Tags` | `string[]` | `["WorkBenchAdvanced"]` | 工作台过滤标签（纯字符串匹配） |
| `RequirePerk` | `Identifier?` | — | 前置 Perk（如 `duckov:hacker/cooking`） |
| `UnlockByDefault` | `bool` | `true` | 默认解锁 |
| `HideInIndex` | `bool` | — | 隐藏于配方索引 |
| `LockInDemo` | `bool` | — | Demo 锁定 |

**创建方式**：

```csharp
// 1. 对象初始化
var data = new CraftingFormulaData
{
    Id = new Identifier("mymod", "coffee"),
    Money = 100,
    CostItems = new[] { ItemEntry.Of(1001, 5) },
    Result = ItemEntry.Of("mymod:coffee", 1),
    Tags = new[] { "WorkBenchAdvanced" }
};

// 2. Builder 方式
var data2 = CraftingFormulaData.Builder
    .Create("mymod:coffee")
    .Money(100)
    .AddCost(1001, 5)
    .AddCost("mymod:beans", 2)
    .Result("mymod:coffee", 10)
    .Tags("WorkBenchAdvanced")
    .RequirePerk(new Identifier("duckov", "hacker/cooking"))
    .Build();
```

**Builder 方法**：`Create(string|Identifier)` / `Money(long)` / `AddCost(Identifier|string, int)` / `CostItems(ItemEntry[])` / `Result(Identifier|string, int)` / `Tags(params string[])` / `RequirePerk(Identifier?|string)` / `UnlockByDefault(bool)` / `HideInIndex(bool)` / `LockInDemo(bool)` / `Build()`

---

## DecomposeFormulaData

**命名空间**：`FeatherMod.Crafting` | **源码**：`CraftingData.cs`

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | `Identifier` | 配方标识（必填） |
| `SourceItemId` | `Identifier?` | 被分解物品 |
| `Money` | `long` | 分解费用 |
| `ResultItems` | `ItemEntry[]` | 产出 |

**创建方式**：

```csharp
var data = new DecomposeFormulaData
{
    Id = new Identifier("mymod", "scrap_old_gun"),
    SourceItemId = new Identifier("mymod", "old_gun"),
    Money = 50,
    ResultItems = new[] { ItemEntry.Of(1001, 3) }
};

// Builder 方式
var data2 = DecomposeFormulaData.Builder
    .Create("mymod:scrap_old_gun")
    .Source("mymod:old_gun")
    .Money(50)
    .AddResult(1001, 3)
    .AddResult(1002, 1)
    .Build();
```

**Builder 方法**：`Create(Identifier|string)` / `SourceItem(Identifier|string)` / `Money(long)` / `AddResult(Identifier|string, int)` / `ResultItems(ItemEntry[])` / `Build()`

---

## TagCostEntry / TagItemCost

**命名空间**：`FeatherMod.Crafting` | **源码**：`Crafting/TagCostRegistry.cs`

耐久折算的持久化载体（`WithDurabilityCost` 的内部实现，一般无需直接使用）：

| 类型 | 字段 |
|------|------|
| `TagCostEntry` | `FormulaId`(string) / `Costs`(TagItemCost[]) / `Modid`(string) |
| `TagItemCost` | `Tag`(string?) / `Amount`(int) / `MinQuality`(int?) / `DurabilityCost`(bool) |

---

## 废弃 API / Obsolete

| 成员 | 替代 |
|------|------|
| `CraftingUtils.AddCraftingFormula(Identifier id, long money, (Identifier,int)[] costItems, ...)`（传统签名） | `AddCraftingFormula(CraftingFormulaData)` |
| `CraftingUtils.AddDecomposeFormula(itemId, money, resultItems, modid)`（传统签名） | `AddDecomposeFormula(DecomposeFormulaData)` |

> 传统签名保留兼容，新项目禁用。
