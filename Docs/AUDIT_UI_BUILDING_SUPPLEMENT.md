# Building & UI 系统功能审计报告 — 补充方案

> **审计日期**: 2026-07-22  
> **补充日期**: 2026-07-22（回应开发者三项反馈）  
> **前置阅读**: `Docs/AUDIT_UI_BUILDING_GAP.md`

---

## 回应一：反射清理计划

### 1.1 BuildingManager 事件直接订阅

**现状**（`BuildingUtils.cs:432-458`）：

```csharp
// ❌ 当前：通过 GetEvent + AddEventHandler 反射订阅，无法 RemoveEventHandler
var builtEvt = typeof(BuildingManager).GetEvent("OnBuildingBuiltComplex",
    BindingFlags.Public | BindingFlags.Static);
builtEvt.AddEventHandler(null, handler);
```

**确认**：`BuildingManager` 在 `TeamSoda.Duckov.Core` 程序集中，已受 Publicizer 覆盖（`FeatherMod.csproj:75`）。事件原本就是 `public static event`，Publicizer 后完全可直接访问。

**修复方案**（↓ ~15 行净减少）：

```csharp
// ✅ 修复后：直接订阅，天然支持取消订阅
private static void HookBuildingEvents()
{
    if (_buildingEventsHooked) return;
    _buildingEventsHooked = true;
    BuildingManager.OnBuildingBuiltComplex += OnBuildingBuiltHandler;
    BuildingManager.OnBuildingDestroyedComplex += OnBuildingDemolishedHandler;
}

// ✅ 新增：对等的卸载方法（RegistryManager.RemoveAllByOwner 时调用）
internal static void UnhookBuildingEvents()
{
    if (!_buildingEventsHooked) return;
    _buildingEventsHooked = false;
    BuildingManager.OnBuildingBuiltComplex -= OnBuildingBuiltHandler;
    BuildingManager.OnBuildingDestroyedComplex -= OnBuildingDemolishedHandler;
}
```

**验证**：直接在 IDE 中写入 `BuildingManager.OnBuildingBuiltComplex`，编译通过即确认可访问。

### 1.2 PlaceBuilding 反射清理

**现状**（`BuildingUtils.cs:20-21`）：

```csharp
private static readonly MethodInfo? _buyAndPlaceMethod = typeof(BuildingManager)
    .GetMethod("BuyAndPlace", BindingFlags.NonPublic | BindingFlags.Static);
```

**检查**：`BuyAndPlace` 是 `private static` → Publicizer 后应为 `public static`。

**修复方案**：将 `_buyAndPlaceMethod.Invoke(null, ...)` 替换为 `BuildingManager.BuyAndPlace(...)` 直接调用，删除 `_buyAndPlaceMethod` 缓存字段。

### 1.3 BuildingCollectionPatch.Sanitize 反射清理

**现状**（`BuildingCollectionPatch.cs:22-35`）：`_requireBuildingsField` 等三个 `FieldInfo` 用于对 `BuildingInfo` struct 执行 `Sanitize`。

**分析**：`BuildingInfo` 是 struct，`requireBuildings` / `requireQuests` / `alternativeFor` 字段在 Publicizer 后可直接赋值。但 `Sanitize` 的问题在于它接收 `ref BuildingInfo` 并需要就地修改 struct 字段——如果 Publicizer 已公开字段，直接赋值即可，无需反射。

**修复方案**：删除三个 `FieldInfo` 缓存，`Sanitize` 方法改为直接字段赋值：

```csharp
private static void Sanitize(ref BuildingInfo info)
{
    info.requireBuildings ??= Array.Empty<string>();
    info.requireQuests ??= Array.Empty<int>();
    info.alternativeFor ??= Array.Empty<string>();
}
```

---

## 回应二：建筑 UI 布局与 Inventory 系统设计

### 2.1 目标

modder 建造一个自定义建筑后，交互时打开一个**游戏原生风格的 DetailsView**，其中包含：
- 建筑的默认 Inventory（游戏原生库存面板）
- 自定义的子库存区（如 "GPU 插槽" "产出槽"）
- 进度条（显示生产/处理进度）
- 自定义按钮（"开始生产"、"取出产物"）

### 2.2 技术架构

建筑 UI 的核心问题是：游戏原生 `DetailsView` 是一个已存在的 View，modder 需要**注入**自定义元素而不是从零创建。

#### 2.2.1 游戏原生 DetailsView 结构（基于 ACB 逆向 + DecompiledDLL 确认）

```
DetailsView (MonoBehaviour)
├── InventoryDisplay      // 主库存面板（游戏原生）
│   ├── DisplayNameText   // 建筑名称
│   ├── SubTitleText      // 副标题
│   └── SlotDisplays[]    // 物品槽列表
├── ContentDisplay        // 可滚动内容区（FML 注入目标）
│   ├── Add(display)      // 向底部追加
│   └── AddToTop(display) // 向顶部追加
├── NotesText[]           // 物品说明区域
├── OnViewOpen(Inventory) // 打开回调
├── OnViewClose(Inventory)// 关闭回调
└── OnSelectionItem(Inventory, Item) // 选中物品回调
```

**关键**：`ContentDisplay` 是 `VerticalLayoutGroup`，所有通过 `Add()` / `AddToTop()` 注入的 UI 元素会自动垂直排列，与原生物品槽风格一致。

#### 2.2.2 可用 UI 组件（来自 GameplayDataSettings.UIPrefabs）

基于 ACB 使用 `UIPrefabsReference.Instance` 和 FML 已有的 `GameplayDataSettings.UIPrefabs`：

| 组件 | 类 | 用途 |
|------|---|------|
| `SlotCollectionDisplay` | MonoBehaviour | 一组带标签的物品槽（标题 + N 个槽） |
| `ProgressBarDisplay` | MonoBehaviour | 进度条 + 文字标签 |
| `SlotDisplay` | MonoBehaviour | 单个物品槽（物品图标 + 数量 + 品质） |
| `ItemDisplay` | MonoBehaviour | 物品图标（不可交互） |
| `InventoryEntry` | MonoBehaviour | 库存条目（用于列表显示） |

### 2.3 FML 需要实现的 API

#### 2.3.1 建筑 DetailsView 注册

```csharp
/// <summary>
/// 为指定建筑注册自定义 DetailsView 配置。
/// 当玩家交互该建筑（打开 Inventory 面板）时，FML 自动注入配置的 UI 元素。
/// </summary>
/// <param name="buildingId">已注册的建筑 Identifier。</param>
/// <param name="config">DetailsView 配置。</param>
/// <param name="modid">归属 modid。</param>
public static void ConfigureBuildingUI(
    Identifier buildingId,
    BuildingUIConfig config,
    string modid
);
```

#### 2.3.2 BuildingUIConfig — 声明式 UI 布局（Machine 层级）

```csharp
public class BuildingUIConfig
{
    /// <summary>主库存面板标题。null = 使用 Building 注册名。</summary>
    public string? DisplayName;

    /// <summary>
    /// 建筑上的机器列表。每个 Machine 是独立的处理单元，
    /// 拥有自己的子库存、Recipe、进度条和按钮。
    /// 多个 Machine 在同一建筑上并行运行，互不干扰。
    /// </summary>
    public MachineDef[]? Machines;
}

/// <summary>
/// 机器定义。描述建筑上一个独立的生产/处理单元。
/// 每个 Machine 有独立的 UI 区域（在 DetailsView 中从上到下排列）和独立的 Recipe 逻辑。
/// </summary>
public class MachineDef
{
    /// <summary>机器标识（在同一建筑内唯一，用于存档 key）。</summary>
    public string MachineKey;

    /// <summary>UI 中显示的机器名称。</summary>
    public string DisplayName;

    /// <summary>是否默认解锁（无需 Perk 即可使用）。默认 true。</summary>
    public bool UnlockedByDefault = true;

    /// <summary>需要解锁的 Perk。仅在 UnlockedByDefault=false 时生效。</summary>
    public Identifier? RequiredPerk;

    /// <summary>本机器的子库存定义。</summary>
    public SubInventoryDef[]? SubInventories;

    /// <summary>本机器的 Recipe（MachineRecipe 子类实例）。</summary>
    public MachineRecipe? Recipe;

    /// <summary>本机器的进度条。</summary>
    public ProgressBarDef[]? ProgressBars;

    /// <summary>本机器的自定义按钮。</summary>
    public BuildingButtonDef[]? Buttons;
}

/// <summary>子库存定义：描述一个独立的物品容器及其 UI 显示。</summary>
public class SubInventoryDef
{
    /// <summary>子库存标识（在同一 Machine 内唯一）。</summary>
    public string SubKey;

    /// <summary>UI 中显示的标题。</summary>
    public string DisplayName;

    /// <summary>槽位数量。</summary>
    public int SlotCount;

    /// <summary>每个槽位的标签过滤（可选）。null = 无过滤。</summary>
    public string[]? SlotTags;

    /// <summary>是否只读（禁止玩家放入/取出）。默认 false。</summary>
    public bool ReadOnly;
}

/// <summary>进度条定义。</summary>
public class ProgressBarDef
{
    /// <summary>进度条标签。</summary>
    public string Label;

    /// <summary>获取进度的回调（0~1）。FML 每帧轮询。</summary>
    public Func<float> GetProgress;
}

/// <summary>建筑 UI 自定义按钮。</summary>
public class BuildingButtonDef
{
    /// <summary>按钮文字。</summary>
    public string Label;

    /// <summary>点击回调。参数为建筑主 Inventory。</summary>
    public Action<Inventory> OnClick;
}
```

#### 2.3.3 使用示例：多功能咖啡机

一个建筑上有两个独立的 Machine——咖啡机和烤面包机，各自有独立的子库存和 Recipe。

```csharp
// 1. 注册建筑
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "kitchen_station"),
    Dimensions = new Vector2Int(2, 2),
    Money = 8000,
    UnlockedByDefault = true
});

// 2. 配置建筑 UI（两个独立 Machine）
BuildingUtils.ConfigureBuildingUI(
    new Identifier("mymod", "kitchen_station"),
    new BuildingUIConfig
    {
        DisplayName = "厨房工作站",
        Machines = new[]
        {
            // ── Machine 1: 咖啡机（默认解锁） ──
            new MachineDef
            {
                MachineKey = "coffee_maker",
                DisplayName = "咖啡机",
                UnlockedByDefault = true,
                SubInventories = new[]
                {
                    new SubInventoryDef { SubKey = "water",   DisplayName = "水箱", SlotCount = 1, SlotTags = new[] { "Water" } },
                    new SubInventoryDef { SubKey = "beans",   DisplayName = "咖啡豆", SlotCount = 1, SlotTags = new[] { "CoffeeBean" } },
                    new SubInventoryDef { SubKey = "output",  DisplayName = "出品", SlotCount = 3, ReadOnly = true },
                },
                Recipe = new SimpleMachineRecipe
                {
                    Id = new Identifier("mymod", "brew_coffee"),
                    Inputs = new[]
                    {
                        new MachineInput { FromSubKey = "water", ItemId = Identifier("duckov", "Water"), Amount = 1 },
                        new MachineInput { FromSubKey = "beans", ItemId = Identifier("duckov", "CoffeeBean"), Amount = 2 }
                    },
                    Outputs = new[]
                    {
                        new MachineOutput { ToSubKey = "output", ItemId = Identifier("mymod", "coffee_cup"), Amount = 1 }
                    },
                    DurationSeconds = 300f, // 5 游戏分钟
                },
                ProgressBars = new[]
                {
                    new ProgressBarDef { Label = "萃取进度", GetProgress = () => /* Machine 1 的 recipe.GetProgress() */ 0f }
                }
            },

            // ── Machine 2: 烤面包机（需 Perk 解锁） ──
            new MachineDef
            {
                MachineKey = "toaster",
                DisplayName = "烤面包机",
                UnlockedByDefault = false,
                RequiredPerk = new Identifier("mymod", "perk_toast_master"),
                SubInventories = new[]
                {
                    new SubInventoryDef { SubKey = "bread",  DisplayName = "面包", SlotCount = 2, SlotTags = new[] { "Bread" } },
                    new SubInventoryDef { SubKey = "output", DisplayName = "出品", SlotCount = 2, ReadOnly = true },
                },
                Recipe = new SimpleMachineRecipe
                {
                    Id = new Identifier("mymod", "toast_bread"),
                    Inputs = new[]
                    {
                        new MachineInput { FromSubKey = "bread", ItemId = Identifier("duckov", "Bread"), Amount = 1 }
                    },
                    Outputs = new[]
                    {
                        new MachineOutput { ToSubKey = "output", ItemId = Identifier("duckov", "Toast"), Amount = 1 }
                    },
                    DurationSeconds = 120f,
                }
            }
        }
    },
    "mymod"
);
```

### 2.4 内部实现流程

```
玩家点击建筑 → 游戏打开 DetailsView
  ↓
BuildingCollectionPatch 检测到该建筑有 BuildingUIConfig
  ↓
遍历 BuildingUIConfig.Machines[]，为每个 MachineDef:
  1. 检查 IsMachineAvailable(machine):
     - UnlockedByDefault=true → 始终可用
     - UnlockedByDefault=false → 检查 PerkTreeUtils.IsPerkUnlocked(RequiredPerk)
  2. 若不可用 → 跳过此 Machine（不渲染任何 UI + 不启动 Watcher）
  3. 若可用 → 渲染 Machine UI 区域:
     a. 添加 Machine 标题分隔线（克隆原生 Divider）
     b. 为每个 SubInventoryDef 创建 BuildingInventory（key="buildingId/machineKey/subKey"）
        克隆 SlotCollectionDisplay → Setup(Slots, Inventory) → 设置 DisplayName
     c. 为每个 ProgressBarDef 克隆 ProgressBarDisplay
     d. 为每个 ButtonDef 克隆 Button
  4. 创建独立 BuildingSlotsWatcher 实例，绑定该 Machine 的 Recipe + 子库存
  ↓
[OnViewClose] 清理所有注入的 UI 元素
```

---

## 回应三：建筑内交互逻辑 — 物品格子联动 + Recipe 系统

### 3.1 需求场景

以 ACB 的算力服务器为例：
- 玩家将 **GPU** 放入 "GPU 插槽" 子库存
- 玩家将 **发电机** 放入 "发电机" 子库存
- 建筑在后台计算：GPU 算力 ≤ 发电机供电 → 产出 CatCoin 货币物品
- 产出放入主 Inventory（或专门的 "产出" 子库存）

泛化为框架能力：**建筑内的物品格子联动** = "建筑 Recipe"（MachineRecipe），本质是从多个输入槽读取物品 → 执行配方 → 输出产物。

### 3.2 与 CraftingFormula 的区别

| 维度 | CraftingFormula（合成台） | MachineRecipe（建筑设备） |
|------|--------------------------|-------------------------|
| 触发方式 | 玩家在 CraftingView 中手动点击 "合成" | 建筑自动检测槽位内容，自动或手动触发 |
| 处理时间 | 即时 | 有处理时间（秒/游戏内小时） |
| 输入来源 | 玩家背包 | 建筑子库存中的物品 |
| 产物去向 | 玩家背包 | 建筑子库存或主库存 |
| 消耗方式 | 一次消耗全部材料 | 按时间/单位逐步消耗（耐久、燃料） |
| 持久化 | 无中间状态 | 需要保存处理进度、剩余时间 |

### 3.3 MachineRecipe — 抽象基类 + 内置简单实现

设计原则：与 `PerkBehaviour` 模式一致。FML 提供抽象基类 `MachineRecipe`，modder 继承它实现自己的机器逻辑。对于常见的"放入材料→时间→产出"场景，FML 提供内置的 `SimpleMachineRecipe`，modder 只需填配置即可。

#### 3.3.1 抽象基类

```csharp
/// <summary>
/// 建筑设备配方抽象基类。modder 继承此类实现自定义机器逻辑。
/// BuildingSlotsWatcher 在槽位变化时调用 CanExecute → 通过后调用 Execute。
///
/// 生命周期：一个 MachineRecipe 实例绑定到一个建筑实例。
/// 支持序列化（存档）。</summary>
public abstract class MachineRecipe
{
    /// <summary>配方唯一标识（用于存档识别）。</summary>
    public Identifier Id { get; internal set; }

    /// <summary>绑定的建筑实例 Inventory 引用。Execute 中可读写。</summary>
    protected Inventory MainInventory { get; internal set; }

    /// <summary>绑定的子库存字典（SubKey → BuildingInventory）。Execute 中可读写。</summary>
    protected IReadOnlyDictionary<string, Inventory> SubInventories { get; internal set; }

    /// <summary>
    /// 检查当前槽位状态是否满足配方条件。
    /// 子库存内容变化时 BuildingSlotsWatcher 自动调用。
    /// </summary>
    /// <returns>true = 可以开始生产。</returns>
    public abstract bool CanExecute();

    /// <summary>
    /// 执行配方逻辑。由 BuildingSlotsWatcher 在 CanExecute 返回 true 后调用。
    /// 实现者负责：消耗输入物品、创建/销毁物品、更新产物槽。
    /// 如需要异步处理时间，内部使用 UniTask + GameClock。
    /// </summary>
    public abstract void Execute();

    /// <summary>
    /// 获取当前生产进度（0~1）。用于 UI 进度条绑定。
    /// 默认返回 0（即时配方或无进度概念）。
    /// </summary>
    public virtual float GetProgress() => 0f;

    /// <summary>
    /// 是否正在生产中。BuildingSlotsWatcher 在 Execute 期间忽略新的槽位变化。
    /// </summary>
    public virtual bool IsRunning => false;

    /// <summary>
    /// 存档：序列化当前状态。默认返回 null（无状态）。
    /// 有中间进度的配方（如等待计时器）需覆写。
    /// </summary>
    public virtual string? SerializeState() => null;

    /// <summary>
    /// 存档：恢复状态。
    /// </summary>
    public virtual void DeserializeState(string? json) { }
}
```

#### 3.3.2 FML 内置：SimpleMachineRecipe

覆盖 80% 的场景：声明式 Input → Output，有处理时间。

```csharp
/// <summary>
/// 内置简单配方：声明式 "输入→时间→输出" 模式。
/// 覆盖大多数建筑设备场景，modder 无需写代码。
/// </summary>
public class SimpleMachineRecipe : MachineRecipe
{
    /// <summary>输入要求：从哪些子库存获取物品。</summary>
    public MachineInput[] Inputs;

    /// <summary>产物。</summary>
    public MachineOutput[] Outputs;

    /// <summary>副产品（按概率生成）。</summary>
    public MachineOutput[]? Byproducts;

    /// <summary>处理时间（游戏内秒数）。null = 即时。</summary>
    public float? DurationSeconds;

    /// <summary>每周期耐久消耗。</summary>
    public DurabilityCost[]? DurabilityCosts;

    // ── 内部状态 ──
    private float _progress;
    private float _lastGameTime;

    public override bool CanExecute()
    {
        // 检查每个 Input 的 FromSubKey 子库存是否有足够物品
        foreach (var input in Inputs)
        {
            if (!SubInventories.TryGetValue(input.FromSubKey, out var inv)) return false;
            var count = inv.Content.Count(item => item != null && item.TypeID == input.ItemId.ResolveTypeId());
            if (count < input.Amount) return false;
        }
        return true;
    }

    public override void Execute()
    {
        // 1. 消耗输入物品
        foreach (var input in Inputs)
        {
            if (!input.Consume) continue;
            if (!SubInventories.TryGetValue(input.FromSubKey, out var inv)) continue;
            int remaining = input.Amount;
            foreach (var item in inv.Content)
            {
                if (item == null || item.TypeID != input.ItemId.ResolveTypeId()) continue;
                int take = Math.Min(remaining, item.StackCount);
                item.StackCount -= take;
                remaining -= take;
                if (remaining <= 0) break;
            }
        }

        // 2. 扣除耐久
        if (DurabilityCosts != null)
        {
            foreach (var dc in DurabilityCosts)
            {
                if (!SubInventories.TryGetValue(dc.SubKey, out var inv)) continue;
                foreach (var item in inv.Content)
                {
                    if (item != null) item.Durability -= dc.DurabilityPerCycle;
                }
            }
        }

        // 3. 生成产物
        foreach (var output in Outputs)
        {
            if (UnityEngine.Random.value > output.Chance) continue;
            var targetInv = output.ToSubKey != null && SubInventories.TryGetValue(output.ToSubKey, out var si) ? si : MainInventory;
            var resultItem = ItemAssetsCollection.InstantiateSync(output.ItemId.ResolveTypeId());
            if (resultItem != null)
            {
                resultItem.StackCount = output.Amount;
                targetInv.AddAndMerge(resultItem);
            }
        }

        // 4. 副产品
        if (Byproducts != null)
        {
            foreach (var bp in Byproducts)
            {
                if (UnityEngine.Random.value > bp.Chance) continue;
                var targetInv = bp.ToSubKey != null && SubInventories.TryGetValue(bp.ToSubKey, out var si2) ? si2 : MainInventory;
                var bpItem = ItemAssetsCollection.InstantiateSync(bp.ItemId.ResolveTypeId());
                if (bpItem != null)
                {
                    bpItem.StackCount = bp.Amount;
                    targetInv.AddAndMerge(bpItem);
                }
            }
        }
    }

    public override float GetProgress() => _progress;
    public override bool IsRunning => _progress > 0 && _progress < 1;
    // ... start/update timer via ProductionTimer, serialize/deserialize _progress
}

/// <summary>配方输入定义（SimpleMachineRecipe 使用）。</summary>
public class MachineInput
{
    public string FromSubKey;
    public Identifier ItemId;
    public int Amount;
    public bool Consume = true;
}

/// <summary>配方输出定义。</summary>
public class MachineOutput
{
    public string? ToSubKey;
    public Identifier ItemId;
    public int Amount;
    public float Chance = 1.0f;
}

/// <summary>耐久消耗定义。</summary>
public class DurabilityCost
{
    public string SubKey;
    public float DurabilityPerCycle;
}
```

### 3.4 格子联动触发器：BuildingSlotsWatcher

`BuildingSlotsWatcher` 是一个 MonoBehaviour，`ConfigureBuildingUI` 或 `RegisterMachineRecipe` 时自动挂载到 Building prefab。每个 Machine 有独立的 Watcher 实例，监听自己的子库存变化后调用本 Machine Recipe 的 `CanExecute()` → `Execute()`。

```csharp
internal class BuildingSlotsWatcher : MonoBehaviour
{
    private MachineRecipe _recipe;                         // 本 Machine 的 Recipe
    private Dictionary<string, Inventory> _subs;            // SubKey → Inventory

    private void Start()
    {
        foreach (var sub in _subs.Values)
            sub.onContentChanged += OnSlotChanged;
    }

    private void OnSlotChanged(Inventory inv, int index)
    {
        if (_recipe.IsRunning) return;
        _recipe.MainInventory = GetComponent<BuildingInventory>();
        _recipe.SubInventories = _subs;

        if (_recipe.CanExecute())
            _recipe.Execute();
    }
}
```

### 3.5 注册 API

两条路径互补：

| 路径 | API | 适用场景 |
|------|-----|---------|
| **声明式** | `ConfigureBuildingUI(buildingId, config)` → `MachineDef.Recipe` | mod 启动时一次性配置 |
| **动态式** | `RegisterMachineRecipe(buildingId, machineKey, recipe, modid)` | Perk 解锁后运行时挂载 Machine |

```csharp
/// <summary>
/// 为建筑上指定 Machine 注册 Recipe。
/// Recipe 的类型由子类确定（SimpleMachineRecipe 或自定义），Id 为合成表标识。
/// FML 内部自动创建 BuildingSlotsWatcher 并绑定子库存。
///
/// 与 ConfigureBuildingUI 的关系：
/// - ConfigureBuildingUI 中已设置 MachineDef.Recipe 时，无需再调用此方法。
/// - 运行时动态挂载 Machine 时调用此方法（如 Perk 解锁回调中）。
/// </summary>
/// <param name="buildingId">已注册的建筑 Identifier。</param>
/// <param name="machineKey">Machine 标识（与 MachineDef.MachineKey 对应）。</param>
/// <param name="recipe">MachineRecipe 子类实例。类型决定逻辑，Id 为合成表标识。</param>
/// <param name="modid">归属 modid。</param>
public static void RegisterMachineRecipe(
    Identifier buildingId,
    string machineKey,
    MachineRecipe recipe,
    string modid
);

/// <summary>移除建筑上指定 Machine 的 Recipe 及其 Watcher。</summary>
public static bool UnregisterMachineRecipe(Identifier buildingId, string machineKey);
```

### 3.6 使用示例

### 3.6 使用示例

#### 场景 A：简单配方（在 ConfigureBuildingUI 中声明式配置）

已在上方 2.3.3 咖啡机示例中完整展示——`MachineDef.Recipe` 字段直接传入 `SimpleMachineRecipe` 实例。

#### 场景 B：复杂逻辑（继承 MachineRecipe + 运行时动态挂载 Machine）

```csharp
// 1. Modder 写自定义 Recipe
public class GpuMiningRecipe : MachineRecipe
{
    public float BaseProductionRate = 1f;

    public override bool CanExecute()
        => SubInventories["gpu_slots"].Content.Any(i => i != null && i.HasTag("GPU"));

    public override void Execute()
    {
        float totalPower = 0f;
        foreach (var gpu in SubInventories["gpu_slots"].Content)
        {
            if (gpu == null) continue;
            var mod = gpu.Modifiers.Find(m => m.Key == "ComputingPower");
            totalPower += mod?.Value ?? 1f;
        }
        // ... 算力→产出逻辑
        SetState("accumulated_power", GetState<float>("accumulated_power") + totalPower);
    }

    public override float GetProgress()
        => GetState<float>("accumulated_power") / GetState<float>("target_power", 1f);
}

// 2. 在 Perk 解锁回调中动态挂载 Machine
PerkTreeUtils.OnPerkUnlocked(new Identifier("mymod", "perk_mining"), () =>
{
    BuildingUtils.AddMachine(
        new Identifier("mymod", "mining_rig"),
        new MachineDef
        {
            MachineKey = "gpu_miner",
            DisplayName = "GPU 矿机",
            UnlockedByDefault = false, // 已通过 Perk 解锁回调触发，设 false 避免重复检查
            SubInventories = new[]
            {
                new SubInventoryDef { SubKey = "gpu_slots", DisplayName = "GPU 插槽", SlotCount = 8, SlotTags = new[] { "GPU" } },
                new SubInventoryDef { SubKey = "generator", DisplayName = "发电机", SlotCount = 4 },
                new SubInventoryDef { SubKey = "output",    DisplayName = "产出", SlotCount = 4, ReadOnly = true },
            },
            Recipe = new GpuMiningRecipe
            {
                Id = new Identifier("mymod", "gpu_mining"),
                BaseProductionRate = 1.5f
            },
            ProgressBars = new[]
            {
                new ProgressBarDef { Label = "算力进度", GetProgress = () => /* recipe.GetProgress() */ 0f }
            }
        },
        "mymod"
    );
});
```

### 3.7 与 ACB / PerkBehaviour 模式对应

| 层级 | PerkBehaviour 模式 | MachineRecipe 模式 |
|------|-------------------|-------------------|
| 游戏原语 | `PerkBehaviour : MonoBehaviour` | —（无游戏原生等效物） |
| FML 抽象基类 | `PerkBehaviourConfig`（声明式包装） | `MachineRecipe`（抽象基类） |
| FML 内置实现 | `UnlockFormulaConfig` / `ModifyStatsConfig` 等 7 个 | `SimpleMachineRecipe`（声明式输入→输出） |
| Modder 自定义 | 继承 `PerkBehaviour` 写自定义逻辑 | 继承 `MachineRecipe` 写自定义逻辑 |
| 注册方式 | `PerkConfig.Behaviours = new PerkBehaviourConfig[] { ... }` | `BuildingUtils.RegisterMachineRecipe(buildingId, recipe, modid)` |

| ACB 实现 | FML 设计 |
|----------|---------|
| `ServerManager` — 硬编码 GPU + 发电机逻辑 | Modder 写 `GpuMiningRecipe : MachineRecipe` |
| `HydroManager` — 硬编码种子→水培逻辑 | Modder 写 `HydroponicRecipe : MachineRecipe` |
| `RepairManager` — 硬编码修理逻辑 | Modder 写 `RepairRecipe : MachineRecipe`，或用 `SimpleMachineRecipe` |
| `StorageManager` — 纯仓库无逻辑 | 无需 MachineRecipe，仅 `ConfigureBuildingUI` |
| `CacheGPU.SetFloat()` 存中间状态 | `MachineRecipe.SerializeState()` / `DeserializeState()` |
| `ProcessViewOpen/Close` 回调 | `BuildingSlotsWatcher` 驱动 + `ProductionTimer`（见 3.8） |

### 3.8 生产计时器（ProductionTimer）

`SimpleMachineRecipe` 内部使用 `ProductionTimer` 驱动异步生产。自定义 `MachineRecipe` 子类也可直接使用。

```csharp
/// <summary>
/// 异步生产计时器。使用 GameClock 驱动（不受真实时间影响），
/// 支持离线追赶和多周期批量结算。
/// 供 SimpleMachineRecipe 内部使用，自定义 MachineRecipe 子类可按需调用。
/// </summary>
internal class ProductionTimer
{
    // 上次 tick 的游戏时间戳
    private TimeSpan _lastTickTime;

    /// <summary>
    /// 启动计时循环。每秒 tick 一次，计算游戏内时间差。
    /// 累计进度 ≥ 1.0 时触发 onCycleComplete 回调。
    /// DurationSeconds = null 时立即执行一次后退出。
    /// </summary>
    public async UniTask Run(float? durationSeconds, Action onCycleComplete, CancellationToken ct)
    {
        if (durationSeconds == null)
        {
            onCycleComplete();
            return;
        }

        float progress = 0f;
        _lastTickTime = GameClock.Now;

        while (!ct.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: ct);
            var now = GameClock.Now;
            var elapsed = (float)(now - _lastTickTime).TotalSeconds;
            _lastTickTime = now;

            progress += elapsed / durationSeconds.Value;

            while (progress >= 1f)
            {
                progress -= 1f;
                onCycleComplete();
            }
        }
    }
}
```

---

## 四、细化设计 — 四项关键约束

### 4.1 Save/Load 自动化

**原则**：modder 不应手动实现 `SerializeState`/`DeserializeState`。

**方案**：`MachineRecipe` 基类内置自动序列化。任何标记了 `[RecipeState]` 的字段或通过 `SetState<T>/GetState<T>` 存取的值，在存档时自动 JSON 序列化，读档时自动恢复。

```csharp
public abstract class MachineRecipe
{
    // ── 自动序列化状态存储 ──
    [NonSerialized] private Dictionary<string, object?> _autoState = new();

    /// <summary>设置运行时状态（自动参与存档）。</summary>
    protected void SetState<T>(string key, T value)
        => _autoState[key] = value;

    /// <summary>获取运行时状态。</summary>
    protected T GetState<T>(string key, T defaultValue = default)
        => _autoState.TryGetValue(key, out var v) && v is T tv ? tv : defaultValue;

    // ── 框架内部调用，modder 不覆写 ──
    internal string SerializeState()
        => _autoState.Count > 0 ? JsonConvert.SerializeObject(_autoState) : "";

    internal void DeserializeState(string? json)
    {
        if (string.IsNullOrEmpty(json)) return;
        _autoState = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json) ?? new();
    }

    // ── 存档标识（框架自动分配） ──
    internal string SaveKey => Id.ToString();
}
```

**使用示例**：

```csharp
public class GpuMiningRecipe : MachineRecipe
{
    public override void Execute()
    {
        // 存取状态——自动存档，modder 无需覆写任何序列化方法
        float accumulated = GetState<float>("accumulated_power", 0f);
        accumulated += ComputePower();
        SetState("accumulated_power", accumulated);
    }

    public override float GetProgress()
        => GetState<float>("progress", 0f);
}
```

`SimpleMachineRecipe` 的状态（`_progress`, `_lastGameTime`）同样通过 `SetState/GetState` 存储，框架在建筑存档/读档时自动调用。

### 4.2 UI 视觉一致性 — 必须基于原版 UI 组件

**原则**：建筑机器的 UI 面板**不得**使用 `SimpleViewBuilder` 的独立 Canvas（纯色块风格）。所有 UI 元素必须通过克隆游戏原生 `UIPrefabs` 获得。

**约束细化**：

| UI 元素 | 克隆来源 | 类 |
|---------|---------|-----|
| 物品槽面板 | `UIPrefabs.SlotCollectionDisplay` | `SlotCollectionDisplay` |
| 单个物品槽 | `UIPrefabs.SlotDisplay` | `SlotDisplay` |
| 进度条 | `UIPrefabs.ProgressBarDisplay` | `ProgressBarDisplay` |
| 按钮 | `UIPrefabs.Button` | `Button` |
| 文本标签 | 从活跃 View 中提取 `TextMeshProUGUI`（字体/颜色继承） | `TextMeshProUGUI` |

**实现方式**：`ConfigureBuildingUI` 内部完全使用 `GameUIUtils.CloneXxx()` 创建所有 UI 元素，直接挂载到 `DetailsView.ContentDisplay` 中。`SimpleViewBuilder` 保留但标记为 `[Obsolete("Use ConfigureBuildingUI for building UIs")]`。

### 4.3 Perk 门控建筑功能（Machine 级别）

**需求**：玩家必须先解锁特定 Perk，建筑的某个 Machine 才在 UI 中可见、可用。

**方案**：门控提升到 `MachineDef` 级别。整个 Machine（包括其 UI 区域、子库存、Recipe）作为一个整体被门控。

```csharp
public class MachineDef
{
    // ...

    /// <summary>是否默认解锁（无需 Perk 即可使用）。默认 true。</summary>
    public bool UnlockedByDefault = true;

    /// <summary>
    /// 需要解锁的 Perk。仅在 UnlockedByDefault=false 时生效。
    /// 未解锁时：本 Machine 的 UI 完全不在 DetailsView 渲染，Recipe 不参与匹配。
    /// </summary>
    public Identifier? RequiredPerk;
}
```

**判断逻辑**（框架内部）：

```csharp
internal static bool IsMachineAvailable(MachineDef machine)
{
    if (machine.UnlockedByDefault) return true;
    if (machine.RequiredPerk == null) return true;  // 未设门控但 UnlockedByDefault=false → 永不解锁
    return PerkTreeUtils.IsPerkUnlocked(machine.RequiredPerk.Value);
}
```

`MachineRecipe.RequiredPerk` 移除——门控统一在 `MachineDef` 层处理。

### 4.4 一建筑多 Machine 并行

**核心模型**：

```
Building
├── Machine "coffee_maker"  ← 独立的子库存 + Recipe + Watcher
│   ├── SubInventory: water
│   ├── SubInventory: beans
│   ├── SubInventory: output
│   └── Recipe: SimpleMachineRecipe("brew_coffee")
│
├── Machine "toaster"       ← 独立的子库存 + Recipe + Watcher（Perk 门控）
│   ├── SubInventory: bread
│   ├── SubInventory: output
│   └── Recipe: SimpleMachineRecipe("toast_bread")
│
└── Machine "juicer"        ← 另一个独立 Machine
    ├── SubInventory: fruit
    ├── SubInventory: output
    └── Recipe: CustomJuiceRecipe : MachineRecipe
```

**关键**：每个 Machine 有**独立的** `BuildingSlotsWatcher` 实例，监听自己的子库存，完全隔离。

```csharp
internal class BuildingSlotsWatcher : MonoBehaviour
{
    private MachineRecipe _recipe;                      // 本 Machine 的 Recipe（单个，非列表）
    private Dictionary<string, Inventory> _subs;         // 本 Machine 的子库存

    private void Start()
    {
        foreach (var sub in _subs.Values)
            sub.onContentChanged += OnSlotChanged;
    }

    private void OnSlotChanged(Inventory inv, int index)
    {
        if (_recipe.IsRunning) return;
        _recipe.MainInventory = GetComponent<BuildingInventory>();
        _recipe.SubInventories = _subs;

        if (_recipe.CanExecute())
            _recipe.Execute();
    }
}
```

**并行运行**：两个 Machine 的 Recipe 可以同时处于 `Execute()` 中（各自独立运行，FML 内部用 `UniTask` 并行驱动各自的 `ProductionTimer`）。

**存档**：每个 Machine 的 SaveKey = `"{buildingId}/{machineKey}"`，框架在建筑存档时遍历所有 Machine 分别序列化。



---

## 五、更新后的实施路线

在原审计文档基础上，加入全部细化项目：

### Wave 1 — P0 基础设施（~16h）

| 顺序 | 任务 | 工时 | 说明 |
|------|------|------|------|
| **0** | **反射清理** | 2h | `HookBuildingEvents` 直接 `+=` 订阅 + `PlaceBuilding` 直接调用 + `Sanitize` 去反射 |
| 1 | 时间模拟框架 | 3h | `GameClockAccessor` + `TimeSpanSerializer` |
| 2 | DetailsView 注入引擎 | 4h | `ConfigureBuildingUI` + `BuildingUIConfig` + 基于 `GameUIUtils.CloneXxx` 的原版 UI 渲染（约束 4.2） |
| 3 | OnViewOpen/Close 回调 | 2h | 嵌入 P0-1 + 触发离线进度计算 |
| 4 | 进度条组件 | 2h | 克隆原生 `ProgressBarDisplay` + Perk 门控支持（约束 4.3） |
| 5 | Building 子库存 | 3h | `SubInventoryDef` → `BuildingInventory` 创建 + Perk 门控支持（约束 4.3） |

### Wave 2 — MachineRecipe 核心（~13h）

| 顺序 | 任务 | 工时 | 说明 |
|------|------|------|------|
| 6 | `MachineRecipe` 抽象基类 | 2h | 基类 + `CanExecute`/`Execute`/`GetProgress` + `SetState<T>/GetState<T>` 自动 SL（约束 4.1） |
| 7 | `SimpleMachineRecipe` 内置实现 | 2h | 声明式 Input→Output + `ProductionTimer` 驱动 + 通过 `SetState` 自动持久化 |
| 8 | `ProductionTimer` | 1.5h | UniTask + GameClock 驱动 + 离线追赶 |
| 9 | `BuildingSlotsWatcher` + `ConfigureBuildingUI` Machine 渲染 | 3h | 一 Machine 一 Watcher，独立子库存监听（约束 4.4）+ `IsMachineAvailable` Perk 门控（约束 4.3）+ 基于原版 UIPrefabs 渲染（约束 4.2） |
| 10 | `RegisterMachineRecipe` 动态 API | 1.5h | `RegisterMachineRecipe(buildingId, machineKey, recipe, modid)` + `UnregisterMachineRecipe`，与 `ConfigureBuildingUI` 互补 |
| 11 | BuildingBehaviour 模式 | 2h | `BuildingBehaviour : MonoBehaviour` + `AttachBehaviour<T>` |
| 12 | 多 Machine 示例验证 | 1h | 咖啡机 + 烤面包机双 Machine 建筑示例（约束 4.4） |

### Wave 3 — UI 视觉升级 + 测试（~10h）

| 顺序 | 任务 | 工时 | 说明 |
|------|------|------|------|
| 13 | SimpleViewBuilder 游戏控件 | 4h | `AddGameButton` / `AddGamePanel` / `AddGameItemSlot` |
| 14 | CraftingViewFilter | 3h | Harmony Postfix + `RegisterCraftingView` |
| 15 | Building 系统端到端测试 | 3h | 注册→放置→打开UI→验证 Perk 门控隐藏/显示→放入物品→验证 SL 恢复 |

**总计**: ~39h

---

*文档版本: v4.0 | 状态: 待人工确认*
