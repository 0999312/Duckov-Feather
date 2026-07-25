# TODO: WithDurabilityCost 修复清单

> 创建时间: 2026-07-25
> 状态: ⏳ 待修复
> 影响模块: Crafting (TagCostValidator, CraftingUtils)

---

## Bug 1: 耐久消耗模式下物品被直接消耗而非降低耐久

### 症状

使用 `ItemEntry.WithDurabilityCost(true)` 注册的合成配方，合成时物品的 **StackCount 被直接扣减**，而非优先降低物品的 Durability。物品的耐久值完全被忽略。

### 根因

`FastModdingLib/Crafting/TagCostValidator.cs` → `ConsumeFromItems()` (L59-67):

```csharp
float remaining = cost.Amount;
foreach (var (item, _) in candidates)
{
    if (remaining <= 0) break;
    int toRemove = Mathf.CeilToInt(Mathf.Min(remaining, (float)item.StackCount));
    item.StackCount -= toRemove;       // ← BUG: 永远扣 StackCount
    if (item.StackCount <= 0) item.DestroyTree();
    remaining -= toRemove;
}
```

`DurabilityCost` 标志仅在 `GetEffectiveAmount()` 方法中参与"等效数量"的计算（用于验证和排序），但实际消耗逻辑**完全忽略该标志**，永远走 `item.StackCount -= toRemove` 路径。

### 期望行为

当 `cost.DurabilityCost == true` 时：
1. 优先降低 `item.Durability`（如从 100 降到 50）
2. 仅当物品耐久降至 0 时才扣减 StackCount 或销毁物品
3. 需要确认游戏物品的耐久修改方式（`ItemUtils.SetItemProperty` 或直接访问 Publicizer 公开的字段）

### 修复方向

1. 在 `ConsumeFromItems` 中增加 `DurabilityCost` 分支判断
2. 耐久模式下用 `GetEffectiveAmount` 的逆运算反推需要降低多少耐久值
3. 测试：创建耐久物品 → 注册 WithDurabilityCost 配方 → 合成 → 验证物品耐久降低但未被销毁

### 涉及文件

- `FastModdingLib/Crafting/TagCostValidator.cs` — 主要修复
- `FastModdingLib/Crafting/TagCostRegistry.cs` — `TagItemCost.DurabilityCost` 数据结构（无需改）
- `FastModdingLib/Items/ItemUtils.cs` — `SetItemProperties` 耐久设置工具（参考用）

---

## Bug 2: 基于标签的合成配方不显示物品图标

### 症状

使用标签匹配（`ItemEntry.Of(ItemTag, amount)`）作为成本的合成配方，在 CraftingView 的消耗品列表中**不显示任何物品图标**。只有使用具体物品（`Identifier` 或数字 typeID）的成本才显示图标。

### 根因

`FastModdingLib/CraftingUtils.cs` → `AddCraftingFormulaInternal()` (L120-168):

```csharp
// 只有 standardEntries（已解析为 typeID 的条目）写入原生配方
var array = new Cost.ItemEntry[costItems.Length];
for (int i = 0; i < costItems.Length; i++)
    array[i] = new Cost.ItemEntry { id = costItems[i].id, amount = costItems[i].amount };
item.cost = array;
```

标签成本被分离存到 `TagCostRegistry`，**不写入游戏原生的 `CraftingFormula.cost.items`**。游戏 CraftView 渲染消耗品图标时只读取 `formula.cost.items`，因此标签成本永不会出现在 UI 中。

### 修复方向

**方案 A（推荐）**: 在注册配方时，为每个标签成本解析一个代表物品的 typeID，注入到 `formula.cost.items` 中作为"预览项"（amount=0 或标记为非消耗占位）。这样游戏 UI 至少能显示一个代表性图标。

**方案 B**: Harmony Patch CraftView 的渲染方法，额外从 `TagCostRegistry` 读取标签成本并动态渲染图标。改动较大，需要深入理解游戏 UI 组件结构。

### 涉及文件

- `FastModdingLib/CraftingUtils.cs` — `ResolveItems()` / `AddCraftingFormulaInternal()`（方案 A）
- 或 CraftView 相关 Harmony Patch（方案 B，需分析反编译代码）

---

## 额外发现：标准成本（非标签）的 DurabilityCost 被静默丢弃

`FastModdingLib/CraftingUtils.cs` → `ResolveItems()` (L109-118) 在将 `ItemEntry` 转为游戏原生 `Cost.ItemEntry` 时，只提取 `typeID` 和 `amount`，`DurabilityCost` 标志**完全丢失**。这意味着非标签成本的 `WithDurabilityCost(true)` 也无效。

此问题与 Bug 1 同源——需要先决定是否修复消耗逻辑，再决定是否需要为原生成本路径也保留 DurabilityCost。
