# UI 系统 API 设计文档 — 高级交互场景

> **状态**: 设计提案 | **版本**: v1.0 | **日期**: 2026-07-11
> **前置依赖**: `Docs/DESIGN_INTERACTION_API.md`（交互系统设计）

---

## 1. 场景分析

本文档基于三个真实 Mod 开发需求设计 FML 的 UI 系统 API 补充方案：

| # | 场景 | 核心需求 | 复杂点 |
|---|------|---------|--------|
| 1 | 打开过滤特定标签的合成界面 | 对现有 CraftingView 做标签过滤 | 需要拦截/扩展游戏原生 CraftingView |
| 2 | 类似比特币矿机的设备（UI 与物品栏绑定） | 自定义 UI 绑定物品槽位 | 物品槽渲染、Container 系统对接 |
| 3 | 模块化自由组装产线系统 | 建筑间互联、输入→生产→输出 | 建筑通信、生产计时、物品流转 |

**约束**：FML 作为框架不添加实质游戏内容，所有 API 均为基础能力层，不做具体"矿机""产线"实现。

**新增约束（2026-07-11）**：所有 UI 渲染必须与原游戏视觉风格完全一致。不能使用 `SimpleViewBuilder` 的纯色块风格（黑底白字/灰色按钮）。

---

## 2. 视觉一致性策略（核心架构）

### 2.1 现状

通过代码审计确认：FML 当前**完全不支持**游戏原生 UI 组件克隆。现有两种 UI 方式均为问题：

| 方式 | 视觉效果 | 问题 |
|------|---------|------|
| `SimpleViewBuilder` | 独立 Canvas，黑底白字纯色块 | 完全不像游戏 UI |
| `ModOptionsBuilder` | 嵌入 `CustomOptionsPanel`，白字灰按钮 | 仅嵌入了一个容器，内部元素仍是纯色 |

### 2.2 策略：游戏原生 UI 克隆 + View 注入

不再从零创建 UI，而是走两条互补路径：

**路径 A — View 层注入（推荐）**：Harmony Postfix 游戏 View 的 `Setup()` / `OnEnable()`，在已有 UI 层级内追加自定义内容。自定义内容通过**克隆**同 View 内已有的 UI 控件（按钮、面板、物品槽）获得——自动继承游戏的颜色/字体/精灵。

**路径 B — 控件克隆工厂**：提供一个 `GameUIFactory`，从游戏运行时 UI 中提取并缓存常用控件模板（按钮模板、面板模板、物品槽模板），供 modder 在自定义画布上复用。

### 2.3 新增核心 API

#### 2.3.1 `GameUIFactory` — 游戏原生 UI 控件克隆

```csharp
namespace FeatherMod.UI
{
    /// <summary>
    /// 游戏原生 UI 控件工厂。从游戏运行时 UI 中提取并缓存控件模板，
    /// 克隆后自动继承游戏的颜色/字体/精灵/材质，实现像素级视觉一致性。
    /// </summary>
    public static class GameUIFactory
    {
        /// <summary>从指定游戏 View 中提取控件模板。
        /// ViewType = typeof(BuilderView) 等游戏原生 View 类。</summary>
        /// <param name="templatePath">
        /// 在 View 层级中的路径，如 "Panel/Body/Button_Template"。
        /// FML 通过 transform.Find 定位。</param>
        public static GameObject? ExtractTemplate(Type viewType, string templatePath);

        /// <summary>克隆已提取的模板。自动处理字体/材质引用。
        /// 克隆体默认处于未激活状态，由调用方激活并设置父节点。</summary>
        public static GameObject CloneTemplate(GameObject template);

        /// <summary>快捷方法：ExtractTemplate + CloneTemplate 一步完成。</summary>
        public static GameObject? CloneWidget(Type viewType, string templatePath);

        // ── 常用控件快捷克隆 ──

        /// <summary>克隆游戏标准按钮模板。</summary>
        public static Button CloneButton(Transform parent, string label, Action onClick);

        /// <summary>克隆游戏标准面板模板（含背景/边框/阴影）。</summary>
        public static GameObject ClonePanel(Transform parent, string title);

        /// <summary>克隆游戏标准物品槽模板。</summary>
        public static GameObject CloneItemSlot(Transform parent);

        /// <summary>克隆游戏标准进度条模板。</summary>
        public static GameObject CloneProgressBar(Transform parent);

        // ── 样式查询 ──

        /// <summary>获取游戏主字体。</summary>
        public static Font? GetGameFont();

        /// <summary>获取游戏 UI 配色方案。</summary>
        public static GameUIColorPalette GetColorPalette();
    }

    /// <summary>游戏 UI 配色方案（运行时自动提取）。</summary>
    public class GameUIColorPalette
    {
        public Color TextPrimary;
        public Color TextSecondary;
        public Color PanelBackground;
        public Color ButtonNormal;
        public Color ButtonHover;
        public Color ButtonPressed;
        public Color BorderColor;
        public Color HighlightColor;
        public Color DangerColor;
    }
}
```

**模板提取策略**：
1. FML 初始化时，扫描已打开的游戏 View（或通过 Harmony 首次打开时捕获），用 `transform.Find()` 定位典型控件
2. 缓存找到的 GameObject 作为模板（`DontDestroyOnLoad` + 保持 Inactive）
3. 每次 modder 请求克隆时，`Instantiate(template)` → 获得继承所有原生样式的副本

#### 2.3.2 `ViewInjector` — View 层注入

```csharp
namespace FeatherMod.UI
{
    /// <summary>
    /// View 注入器。在游戏原生 View 打开时，通过 Harmony Postfix
    /// 向指定容器追加自定义 UI 内容。
    /// </summary>
    public static class ViewInjector
    {
        /// <summary>
        /// 注册一个 View 注入规则。当 gameViewType 对应的原生 View
        /// 完成 Setup/OnEnable 时，FML 自动调用 inject 回调，
        /// 传入 View 的根 Transform。
        /// </summary>
        /// <param name="gameViewType">游戏原生 View 类型（如 typeof(CraftingView)）。</param>
        /// <param name="containerPath">容器在 View 层级中的路径（如 "Content/RecipeList"）。null = 注入到根。</param>
        /// <param name="inject">注入回调。参数为目标容器的 Transform。</param>
        /// <param name="modid">归属 modid，用于卸载时清理。</param>
        public static void OnViewOpened(
            Type gameViewType,
            string? containerPath,
            Action<Transform> inject,
            string modid
        );

        /// <summary>注销指定 View 的注入规则。</summary>
        public static void UnregisterViewInjection(Type gameViewType, string modid);

        /// <summary>注销指定 mod 的全部注入规则。</summary>
        public static int UnregisterAllInjections(string modid);
    }
}
```

**内部实现**：`ViewInjector` 通过 Roslyn Emit 或 Harmony `DynamicMethod` 动态生成每个 `gameViewType` 的 Patch，避免手工为每个 View 类型编写 Patch 类。

#### 2.3.3 `SimpleViewBuilder` 的视觉一致性升级

`SimpleViewBuilder` 保留，但新增方法使其使用游戏原生控件而非纯色块：

```csharp
// ── 使用游戏原生控件的构建方法 ──

/// <summary>添加一个使用游戏原生按钮样式的按钮。</summary>
public SimpleViewBuilder AddGameButton(string text, Action onClick);

/// <summary>添加一个使用游戏原生面板样式的子面板。</summary>
public SimpleViewBuilder AddGamePanel(string title);

/// <summary>结束子面板，回到父级。</summary>
public SimpleViewBuilder EndPanel();
```

当调用 `AddGameButton` 时，内部通过 `GameUIFactory.CloneButton(parent, text, onClick)` 实现——外观与游戏完全一致。

### 2.4 渐进降级策略

```
优先级 1: 游戏原生 View 注入（ViewInjector.OnViewOpened）
          ↓ 若目标 View 无法 Patch 或不适合
优先级 2: 游戏控件克隆 + 独立 Canvas（GameUIFactory + SimpleViewBuilder.GameButton）
          ↓ 若游戏控件模板无法提取
优先级 3: 纯色块降级（SimpleViewBuilder 原有方法，标注 [UglyFallback]）
```

每个新增 UI API 都有降级路径。`GameUIFactory` 初始化时输出可用模板列表，modder 据此决定采用哪一级策略。

---

## 3. 场景 1 — 过滤式合成界面

### 3.1 需求分析

游戏原生 CraftingView 展示了全部已解锁配方，标签（`Tags`）决定配方归属哪个工作台。modder 需要：

- 打开一个只展示特定标签配方（如 `["Forge"]`）的合成面板
- 可能挂载到一个自定义 Building/NPC/场景物件上
- 面板内配方来自 FML 注册 + 游戏原生配方

### 3.2 现有能力

- `CraftingFormulaData.Tags = string[]` — FML 已支持标签注册
- `CraftingManagerPatch` — 仅 patch `Craft()` 方法，不触及 UI
- `TagCostRegistry` — 内部使用的标签成本验证
- **缺失**：无任何 CraftingView 相关的 UI Hook

### 3.3 设计方案

**策略**：不重新实现 CraftingView（太重），而是通过 Harmony 补丁注入过滤能力。FML 提供一套"过滤规则"抽象，由内部补丁桥接到游戏原生 UI。

#### 3.3.1 核心概念：`CraftingViewFilter`

```csharp
/// <summary>配方过滤条件。组合使用，多条件 AND 逻辑。</summary>
public class CraftingViewFilter
{
    /// <summary>仅显示含指定标签的配方。null = 不过滤标签。</summary>
    public string[]? Tags;

    /// <summary>仅显示指定 mod 注册的配方。null = 不过滤来源。</summary>
    public string? Modid;

    /// <summary>仅显示特定配方 ID 集合。null = 不过滤 ID。</summary>
    public Identifier[]? FormulaIds;

    /// <summary>是否包含原生配方。默认 true。</summary>
    public bool IncludeVanilla = true;

    /// <summary>是否包含 FML 注册配方。默认 true。</summary>
    public bool IncludeFML = true;
}
```

#### 3.3.2 API 规格

```csharp
// ── 注册过滤视图 ──

/// <summary>注册一个过滤式合成面板入口。
/// 当 modder 通过 InteractionUtils 触发此 viewType 时，
/// FML 内部打开 CraftingView 并应用过滤。</summary>
/// <param name="viewType">唯一 View 标识（如 new Identifier("mymod", "forge_crafting")）。</param>
/// <param name="filter">过滤条件。</param>
/// <param name="modid">归属 modid。</param>
public static void RegisterCraftingView(
    Identifier viewType,
    CraftingViewFilter filter,
    string modid
);

/// <summary>注销过滤式合成面板。</summary>
public static bool UnregisterCraftingView(Identifier viewType);
```

#### 3.3.3 内部实现 — Harmony 桥接

```
ViewDispatcher.Open("mymod:forge_crafting")
  → CraftingViewRegistry 查找 filter
  → Harmony Patch: CraftingView.OnEnable Postfix
      → 读取 CraftingView 内的配方列表
      → 按 filter.Tags / filter.Modid / filter.FormulaIds 过滤
      → 移除不匹配的 UI 条目
```

**需要新增的 Harmony Patch**：`CraftingViewFilterPatch`

```
[HarmonyPatch(typeof(CraftingView), "OnEnable")] 或等效方法
Postfix: 检查当前是否有活跃的 filter，有则过滤列表
```

#### 3.3.4 使用示例

```csharp
// 注册"锻造台"过滤视图
CraftingUtils.RegisterCraftingView(
    new Identifier("mymod", "forge_crafting"),
    new CraftingViewFilter
    {
        Tags = new[] { "Forge" },
        IncludeVanilla = true,
        IncludeFML = true
    },
    "mymod"
);

// 通过 ViewDispatcher 打开（可绑定到任意交互点）
ViewDispatcher.Open(new Identifier("mymod", "forge_crafting"));
```

---

## 4. 场景 2 — 设备 UI + 物品容器绑定

### 5.1 需求分析

比特币矿机类设备的特征：
- 设备有一个"容器"——可放入/取出物品
- 设备有特定的输入槽和输出槽
- UI 面板绑定到这个容器，显示物品
- 可能存在处理逻辑（如放入 GPU 产出 BTC）

技术上需要：
1. **物品容器（ItemContainer）** — 一个可存储物品的抽象
2. **物品槽 UI** — 渲染物品图标、数量、品质
3. **设备模型** — 将容器绑定到 Building/场景物件

### 3.2 现有能力

- `SimpleViewBuilder` — 基础文本/按钮 Canvas，**无物品槽渲染**
- `BuildingUtils.CreateSimpleBuilding` — 创建 Building + functionContainer
- **缺失**：无物品容器系统、无物品槽 UI 组件、无容器-UI 绑定

### 4.3 设计方案

**核心思路**：FML 提供"声明式设备"抽象——modder 声明设备的槽位配置和 UI 布局，FML 处理底层 Canvas 创建和物品渲染。

游戏内部的容器/物品系统需要反查（游戏可能已有 `ItemContainer` 或类似概念），FML 通过反射/Harmony 桥接。如果游戏无此系统，FML 需要从零构建。

#### 4.3.1 核心数据模型

```csharp
/// <summary>物品容器配置。描述一个可存储物品的容器。</summary>
public class ItemContainerConfig
{
    /// <summary>容器唯一标识。</summary>
    public Identifier Id;

    /// <summary>槽位总数。</summary>
    public int SlotCount;

    /// <summary>每个槽位的最大堆叠数。0 = 不限制。</summary>
    public int MaxStackPerSlot;

    /// <summary>各槽位的物品类型限制（可选）。null = 无限制。</summary>
    public ItemFilter[]? SlotFilters;

    /// <summary>容器内物品（运行时状态）。</summary>
    public ContainerSlot[] Slots;
}

/// <summary>单个容器槽位。</summary>
public class ContainerSlot
{
    /// <summary>槽位索引。</summary>
    public int Index;

    /// <summary>当前物品（Identifier + 数量）。null = 空。</summary>
    public ItemEntry? Item;

    /// <summary>品质。</summary>
    public int Quality;

    /// <summary>耐久度（0-1）。</summary>
    public float Durability;

    /// <summary>是否锁定（不可取出）。</summary>
    public bool Locked;
}

/// <summary>物品类型过滤规则。</summary>
public class ItemFilter
{
    /// <summary>允许的物品标签。null = 不限标签。</summary>
    public string[]? Tags;

    /// <summary>允许的具体物品。null = 不限具体。</summary>
    public Identifier[]? ItemIds;

    /// <summary>是否允许放入。</summary>
    public bool AllowInput = true;

    /// <summary>是否允许取出。</summary>
    public bool AllowOutput = true;
}
```

#### 4.3.2 Device UI 配置

```csharp
/// <summary>设备 UI 面板配置。</summary>
public class DeviceUILayout
{
    /// <summary>面板标题。</summary>
    public string Title;

    /// <summary>关联的容器 Identifier。</summary>
    public Identifier ContainerId;

    /// <summary>输入槽位索引（高亮显示为"放入"区域）。</summary>
    public int[] InputSlotIndices;

    /// <summary>输出槽位索引（高亮显示为"取出"区域）。</summary>
    public int[] OutputSlotIndices;

    /// <summary>槽位排列（网格列数）。</summary>
    public int GridColumns = 4;

    /// <summary>额外的自定义按钮。</summary>
    public DeviceButton[]? Buttons;
}

/// <summary>设备 UI 按钮。</summary>
public class DeviceButton
{
    public string Label;
    public Action<ItemContainerConfig>? OnClick;  // 回调中可读写容器
}
```

#### 4.3.3 API 规格

```csharp
// ── 容器管理 ──

/// <summary>创建物品容器。</summary>
/// <param name="id">容器 Identifier。</param>
/// <param name="slotCount">槽位数。</param>
/// <param name="modid">归属 modid。</param>
/// <returns>容器配置对象（可用于后续操作）。</returns>
public static ItemContainerConfig CreateContainer(
    Identifier id,
    int slotCount,
    string modid
);

/// <summary>获取容器当前状态。</summary>
public static ItemContainerConfig? GetContainer(Identifier id);

/// <summary>销毁容器。</summary>
public static bool DestroyContainer(Identifier id);

// ── 容器操作 ──

/// <summary>向指定槽位放入物品。返回实际放入数量（0 = 失败）。</summary>
public static int PutItem(Identifier containerId, int slot, ItemEntry item, int quality = 0);

/// <summary>从指定槽位取出物品。返回取出的物品，槽位变空。</summary>
public static ItemEntry? TakeItem(Identifier containerId, int slot, int amount);

/// <summary>锁定/解锁槽位。</summary>
public static void SetSlotLocked(Identifier containerId, int slot, bool locked);

/// <summary>设置槽位过滤规则。</summary>
public static void SetSlotFilter(Identifier containerId, int slot, ItemFilter? filter);

// ── 设备 UI 注册 ──

/// <summary>注册一个设备 UI 面板。
/// 当通过 ViewDispatcher 打开时，自动渲染物品槽 + 按钮。</summary>
/// <param name="viewType">View 标识。</param>
/// <param name="layout">UI 布局配置。</param>
/// <param name="modid">归属 modid。</param>
public static void RegisterDeviceUI(
    Identifier viewType,
    DeviceUILayout layout,
    string modid
);

/// <summary>注销设备 UI。</summary>
public static bool UnregisterDeviceUI(Identifier viewType);

// ── 将容器绑定到 Building ──

/// <summary>将物品容器绑定到建筑。
/// 绑定后，玩家交互建筑时自动打开关联的设备 UI。</summary>
/// <param name="buildingId">已注册的建筑 Identifier。</param>
/// <param name="containerId">容器 Identifier。</param>
/// <param name="viewType">设备 UI 的 View 标识（由 RegisterDeviceUI 注册）。</param>
public static void BindContainerToBuilding(
    Identifier buildingId,
    Identifier containerId,
    Identifier viewType
);
```

#### 4.3.4 内部实现要点

1. **容器持久化**：`ContainerRegistry : SimpleRegistry<ItemContainerConfig>`，OnRemoved 销毁
2. **UI 渲染**：`DeviceUIRenderer` 内部类，使用 Unity UI 动态生成物品槽 Grid
   - 物品图标从 `ItemUtils.LoadSprite()` 加载
   - 数量、品质叠加显示
   - 拖放交互委托给游戏原生输入系统（需探查）
3. **建筑绑定**：`OnBuildingBuilt` 回调中自动挂载 `ViewInteractHandler` + 设置为对应 viewType
4. **Harmony 桥接**：如游戏有原生 `ItemContainer`，FML 通过 Patch 同步状态；否则 FML 自维护

---

## 5. 场景 3 — 模块化产线系统

### 5.1 需求分析

典型模块化产线：
- 多个建筑自由摆放，形成"生产线"
- 建筑有输入口和输出口
- 原料放入输入建筑 → 经过若干加工站 → 产物从输出建筑取出
- 每个加工站有处理时间、配方配置

### 5.2 现有能力

- `BuildingUtils.RegisterBuilding` — 注册建筑
- `BuildingUtils.PlaceBuilding` — 放置建筑
- `BuildingUtils.OnBuildingBuilt` — 建造完成回调
- `BuildingUtils.CreateSimpleBuilding` — 代码端创建建筑
- **缺失**：无建筑间通信、无生产计时器、无物品流转

### 5.3 设计方案

模块化产线可分解为三个独立能力：

1. **生产配方注册** — 定义"输入→输出"的转换规则（区别于标准 CraftingFormula：有处理时间、有中间产物）
2. **建筑状态机** — 建筑可持有库存、有生产进度
3. **建筑连接** — 建筑间可传递物品（物理连接或逻辑连接）

FML 作为框架提供前两项的能力层，第三项由 modder 基于能力层自行实现（因为连接方式高度依赖游戏机制和 modder 创意）。

#### 5.3.1 核心概念：`ProcessRecipe`

```csharp
/// <summary>工序配方。定义输入→输出的转换规则。</summary>
public class ProcessRecipe
{
    /// <summary>配方唯一标识。</summary>
    public Identifier Id;

    /// <summary>输入物品（消耗）。</summary>
    public ItemEntry[] Inputs;

    /// <summary>输出物品（生成）。</summary>
    public ItemEntry[] Outputs;

    /// <summary>副产品（按概率生成，可选）。</summary>
    public Byproduct[]? Byproducts;

    /// <summary>处理时间（秒）。</summary>
    public float Duration;

    /// <summary>可执行此配方的工作站标签。</summary>
    public string[] WorkstationTags;

    /// <summary>powerCost等</summary>
    public float PowerCost;
}

public class Byproduct
{
    public ItemEntry Item;
    public float Chance; // 0-1
}
```

#### 5.3.2 核心概念：`ProductionBuilding`

```csharp
/// <summary>生产型建筑配置。附加到已注册的 Building 上。</summary>
public class ProductionBuildingConfig
{
    /// <summary>关联的建筑 Identifier。</summary>
    public Identifier BuildingId;

    /// <summary>设备标签（用于匹配可执行的 ProcessRecipe）。</summary>
    public string[] Tags;

    /// <summary>输入容器（放入原料）。</summary>
    public Identifier? InputContainerId;

    /// <summary>输出容器（取出产物）。</summary>
    public Identifier? OutputContainerId;

    /// <summary>当前激活的配方（null = 无）。</summary>
    public Identifier? ActiveRecipeId;

    /// <summary>当前生产进度（0~1）。</summary>
    public float Progress;

    /// <summary>是否正在生产。</summary>
    public bool IsProducing;
}
```

#### 4.3.3 API 规格

```csharp
// ── 工序配方注册 ──

/// <summary>注册工序配方。区别于 CraftingFormula（手动合成），
/// ProcessRecipe 由生产建筑自动执行。</summary>
public static void RegisterProcessRecipe(
    ProcessRecipe recipe,
    string modid
);

/// <summary>按工作站标签查询可用配方。</summary>
public static ProcessRecipe[] GetRecipesForTags(params string[] tags);

/// <summary>注销配方。</summary>
public static bool UnregisterProcessRecipe(Identifier id);

// ── 生产建筑 ──

/// <summary>将已注册建筑配置为生产型建筑。</summary>
public static void ConfigureProductionBuilding(
    Identifier buildingId,
    ProductionBuildingConfig config,
    string modid
);

/// <summary>获取生产建筑状态。</summary>
public static ProductionBuildingConfig? GetProductionState(Identifier buildingId);

// ── 生产控制 ──

/// <summary>启动生产（异步，FML 内部用 UniTask 驱动计时器）。</summary>
/// <param name="buildingId">建筑 Identifier。</param>
/// <param name="recipeId">工序配方 Identifier。</param>
/// <returns>true = 启动成功（输入材料充足）。false = 材料不足。</returns>
public static bool StartProduction(Identifier buildingId, Identifier recipeId);

/// <summary>停止生产。已消耗材料不退还。</summary>
public static void StopProduction(Identifier buildingId);

/// <summary>投产完成回调。</summary>
public static void OnProductionComplete(
    Identifier buildingId,
    Action<Identifier> callback  // 参数 = buildingId
);

// ── 建筑内置容器快捷操作 ──

/// <summary>获取生产建筑的输入容器。</summary>
public static ItemContainerConfig? GetInputContainer(Identifier buildingId);

/// <summary>获取生产建筑的输出容器。</summary>
public static ItemContainerConfig? GetOutputContainer(Identifier buildingId);

/// <summary>从输入容器消耗材料（内部由 StartProduction 调用）。</summary>
public static bool ConsumeInputs(Identifier buildingId, ItemEntry[] required);

/// <summary>向输出容器添加产物。</summary>
public static void ProduceOutputs(Identifier buildingId, ItemEntry[] outputs);

// ── 批量运维 ──

/// <summary>获取指定 mod 的全部生产建筑状态。</summary>
public static ProductionBuildingConfig[] GetAllProductions(string modid);

/// <summary>批量卸载指定 mod 的全部工序配方和生产配置。</summary>
public static int RemoveAllProductions(string modid);
```

#### 5.3.4 内部实现要点

1. **生产计时器**：使用 `UniTask.Delay()` + `PlayerLoopTiming.Update` 驱动。每次 tick 更新 Progress，完成后调用回调。
2. **物品验证**：`ConsumeInputs` 检查输入容器内是否有足够材料，不足返回 false。
3. **容器绑定**：`ConfigureProductionBuilding` 自动创建 `InputContainerConfig` / `OutputContainerConfig`，受 `ItemContainerRegistry` 追踪。
4. **Registry**：`ProcessRecipeRegistry` + `ProductionRegistry` 均为 `SimpleRegistry<T>`，遵循现有生命周期模式 A。
5. **存档**：配方定义不存档（每次加载注册），但容器内容和生产进度需要持久化（通过 EventBus 的 `CollectSaveDataEvent` 或 FML 内置存档序列化）。

---

## 6. 新增 UI 组件扩展

上述三个场景都需要比 `SimpleViewBuilder` 更丰富的 UI 渲染能力。建议扩展。

### 6.1 `SimpleViewBuilder` 扩展

```csharp
// ── 渲染物品槽列表 ──

/// <summary>添加一个物品槽网格（用于 Device UI 渲染）。</summary>
/// <param name="containerId">关联的容器 Identifier。</param>
/// <param name="slotIndices">要显示的槽位索引。</param>
/// <param name="columns">网格列数。</param>
/// <param name="readOnly">是否只读（禁止拖放）。</param>
/// <returns>this（链式调用）。</returns>
public SimpleViewBuilder AddItemSlotGrid(
    Identifier containerId,
    int[] slotIndices,
    int columns = 4,
    bool readOnly = false
);

/// <summary>添加进度条。</summary>
/// <param name="label">标签文字。</param>
/// <param name="getProgress">获取进度的委托（0~1）。被轮询更新。</param>
public SimpleViewBuilder AddProgressBar(
    string label,
    Func<float> getProgress
);

/// <summary>添加配方列表（点击选择配方）。</summary>
/// <param name="recipes">可显示的配方列表。</param>
/// <param name="onSelect">选择回调。</param>
public SimpleViewBuilder AddRecipeList(
    ProcessRecipe[] recipes,
    Action<ProcessRecipe> onSelect
);

/// <summary>从 CraftingViewFilter 生成配方列表视图。</summary>
public SimpleViewBuilder AddCraftingRecipeList(
    CraftingViewFilter filter,
    Action<CraftingFormulaData> onSelect
);
```

### 6.2 `ItemSlotRenderer`（独立组件）

```csharp
/// <summary>单个物品槽渲染器。挂载到 UI GameObject 上。</summary>
public class ItemSlotRenderer : MonoBehaviour
{
    /// <summary>关联的容器。</summary>
    public Identifier ContainerId;

    /// <summary>槽位索引。</summary>
    public int SlotIndex;

    /// <summary>显示物品图标。</summary>
    public Image Icon;

    /// <summary>显示数量。</summary>
    public Text CountText;

    /// <summary>显示品质边框。</summary>
    public Image QualityBorder;

    /// <summary>刷新显示。</summary>
    public void Refresh(ContainerSlot slot);

    /// <summary>处理点击（放入/取出）。</summary>
    public void OnClick();
}
```

---

## 7. 补充 API 总清单

### 6.1 新增文件

| 优先级 | 文件 | 行数估算 | 场景 |
|--------|------|---------|------|
| 🔴 P0 | `Crafting/CraftingViewFilter.cs` | ~50 | 场景 1 |
| 🔴 P0 | `Crafting/CraftingViewRegistry.cs` | ~80 | 场景 1 |
| 🔴 P0 | `Crafting/Patches/CraftingViewFilterPatch.cs` | ~80 | 场景 1 |
| 🔴 P0 | `Containers/ItemContainerConfig.cs` | ~80 | 场景 2+3 |
| 🔴 P0 | `Containers/ContainerRegistry.cs` | ~60 | 场景 2+3 |
| 🔴 P0 | `Containers/DeviceUILayout.cs` | ~50 | 场景 2 |
| 🔴 P0 | `Containers/ContainerUtils.cs` | ~200 | 场景 2+3 |
| 🟡 P1 | `Production/ProcessRecipe.cs` | ~50 | 场景 3 |
| 🟡 P1 | `Production/ProductionBuildingConfig.cs` | ~50 | 场景 3 |
| 🟡 P1 | `Production/ProcessRecipeRegistry.cs` | ~60 | 场景 3 |
| 🟡 P1 | `Production/ProductionRegistry.cs` | ~60 | 场景 3 |
| 🟡 P1 | `Production/ProductionUtils.cs` | ~200 | 场景 3 |
| 🟡 P1 | `UI/ItemSlotRenderer.cs` | ~120 | 场景 2+3 |
| 🟢 P2 | `UI/DeviceUIRenderer.cs` | ~250 | 场景 2 |
| 🟢 P2 | `Production/ProductionTimer.cs` | ~100 | 场景 3 |
| 🟢 P2 | `Production/Patches/ProductionSavePatch.cs` | ~80 | 场景 3 |

### 6.2 修改现有文件

| 优先级 | 文件 | 改动 |
|--------|------|------|
| 🔴 P0 | `UI/SimpleViewBuilder.cs` | 新增 `AddItemSlotGrid`、`AddProgressBar`、`AddRecipeList`、`AddCraftingRecipeList` |
| 🔴 P0 | `Crafting/CraftingUtils.cs` | 新增 `RegisterCraftingView`、`UnregisterCraftingView` |
| 🟡 P1 | `Buildings/BuildingUtils.cs` | 新增 `BindContainerToBuilding`、链式注册生产建筑 |
| 🟢 P2 | `Register/RegisterBootstrap.cs` | 新增 ContainerRegistry、ProcessRecipeRegistry、ProductionRegistry 的 Init 调用 |

### 6.3 新增 Public API 方法总览

| # | 方法 | 所属类 | 场景 |
|---|------|--------|------|
| 1 | `RegisterCraftingView(viewType, filter, modid)` | `CraftingUtils` | 1 |
| 2 | `UnregisterCraftingView(viewType)` | `CraftingUtils` | 1 |
| 3 | `CreateContainer(id, slotCount, modid)` | `ContainerUtils` | 2 |
| 4 | `GetContainer(id)` | `ContainerUtils` | 2 |
| 5 | `DestroyContainer(id)` | `ContainerUtils` | 2 |
| 6 | `PutItem(containerId, slot, item, quality)` | `ContainerUtils` | 2 |
| 7 | `TakeItem(containerId, slot, amount)` | `ContainerUtils` | 2 |
| 8 | `SetSlotLocked(containerId, slot, locked)` | `ContainerUtils` | 2 |
| 9 | `SetSlotFilter(containerId, slot, filter)` | `ContainerUtils` | 2 |
| 10 | `RegisterDeviceUI(viewType, layout, modid)` | `ContainerUtils` | 2 |
| 11 | `UnregisterDeviceUI(viewType)` | `ContainerUtils` | 2 |
| 12 | `BindContainerToBuilding(buildingId, containerId, viewType)` | `ContainerUtils` | 2+3 |
| 13 | `RegisterProcessRecipe(recipe, modid)` | `ProductionUtils` | 3 |
| 14 | `GetRecipesForTags(tags)` | `ProductionUtils` | 3 |
| 15 | `UnregisterProcessRecipe(id)` | `ProductionUtils` | 3 |
| 16 | `ConfigureProductionBuilding(buildingId, config, modid)` | `ProductionUtils` | 3 |
| 17 | `GetProductionState(buildingId)` | `ProductionUtils` | 3 |
| 18 | `StartProduction(buildingId, recipeId)` | `ProductionUtils` | 3 |
| 19 | `StopProduction(buildingId)` | `ProductionUtils` | 3 |
| 20 | `OnProductionComplete(buildingId, callback)` | `ProductionUtils` | 3 |
| 21 | `GetInputContainer(buildingId)` | `ProductionUtils` | 3 |
| 22 | `GetOutputContainer(buildingId)` | `ProductionUtils` | 3 |
| 23 | `ConsumeInputs(buildingId, required)` | `ProductionUtils` | 3 |
| 24 | `ProduceOutputs(buildingId, outputs)` | `ProductionUtils` | 3 |
| 25 | `GetAllProductions(modid)` | `ProductionUtils` | 3 |
| 26 | `RemoveAllProductions(modid)` | `ProductionUtils` | 3 |
| 27 | `AddItemSlotGrid(...)` | `SimpleViewBuilder` | 2+3 |
| 28 | `AddProgressBar(...)` | `SimpleViewBuilder` | 3 |
| 29 | `AddRecipeList(...)` | `SimpleViewBuilder` | 2+3 |
| 30 | `AddCraftingRecipeList(...)` | `SimpleViewBuilder` | 1 |

---

## 8. 三个场景的完整使用示例

### 7.1 场景 1：过滤式锻造台

```csharp
protected override void OnAfterSetup()
{
    // 1. 注册过滤视图
    CraftingUtils.RegisterCraftingView(
        new Identifier("mymod", "forge_view"),
        new CraftingViewFilter { Tags = new[] { "Forge" } },
        "mymod"
    );

    // 2. 在世界中生成交互点（依赖 §2 交互系统设计）
    InteractionUtils.SpawnViewInteract(
        new Identifier("mymod", "forge_anvil"),
        new Vector3(100, 0, 50),
        new Identifier("mymod", "forge_view")  // ← 打开过滤后的锻造合成界面
    );
}
```

### 7.2 场景 2：比特币矿机设备

```csharp
protected override void OnAfterSetup()
{
    // 1. 创建容器（6 槽：4 输入 + 2 输出）
    var container = ContainerUtils.CreateContainer(
        new Identifier("mymod", "miner_inventory"), 6, "mymod");

    // 输入槽只接受 GPU 标签物品，输出槽只输出不可放入
    ContainerUtils.SetSlotFilter(new Identifier("mymod", "miner_inventory"), 0,
        new ItemFilter { Tags = new[] { "GPU" }, AllowOutput = false });
    // ... 槽位 1-3 同上
    ContainerUtils.SetSlotFilter(new Identifier("mymod", "miner_inventory"), 4,
        new ItemFilter { AllowInput = false });  // 输出只读
    ContainerUtils.SetSlotFilter(new Identifier("mymod", "miner_inventory"), 5,
        new ItemFilter { AllowInput = false });

    // 2. 注册设备 UI
    ContainerUtils.RegisterDeviceUI(
        new Identifier("mymod", "miner_ui"),
        new DeviceUILayout
        {
            Title = "比特币矿机",
            ContainerId = new Identifier("mymod", "miner_inventory"),
            InputSlotIndices = new[] { 0, 1, 2, 3 },
            OutputSlotIndices = new[] { 4, 5 },
            GridColumns = 3,
            Buttons = new[]
            {
                new DeviceButton
                {
                    Label = "开始挖矿",
                    OnClick = container => StartMining(container)
                }
            }
        },
        "mymod"
    );

    // 3. 注册建筑并绑定（或直接生成场景交互点）
    BuildingUtils.RegisterBuilding(
        new Identifier("mymod", "bitcoin_miner"),
        new BuildingInfo { id = "bitcoin_miner", /* ... */ },
        minerPrefab
    );
    ContainerUtils.BindContainerToBuilding(
        new Identifier("mymod", "bitcoin_miner"),
        new Identifier("mymod", "miner_inventory"),
        new Identifier("mymod", "miner_ui")
    );
}
```

### 7.3 场景 3：模块化产线

```csharp
protected override void OnAfterSetup()
{
    // 1. 注册工序配方
    ProductionUtils.RegisterProcessRecipe(new ProcessRecipe
    {
        Id = new Identifier("mymod", "smelt_iron"),
        Inputs = new[] { ItemEntry.Of("duckov:IronOre", 3) },
        Outputs = new[] { ItemEntry.Of("duckov:IronIngot", 1) },
        Duration = 10f,
        WorkstationTags = new[] { "furnace" }
    }, "mymod");

    ProductionUtils.RegisterProcessRecipe(new ProcessRecipe
    {
        Id = new Identifier("mymod", "forge_blade"),
        Inputs = new[] { ItemEntry.Of("duckov:IronIngot", 2) },
        Outputs = new[] { ItemEntry.Of("duckov:IronBlade", 1) },
        Byproducts = new[] { new Byproduct { Item = ItemEntry.Of("duckov:Scrap", 1), Chance = 0.2f } },
        Duration = 15f,
        WorkstationTags = new[] { "forge" }
    }, "mymod");

    // 2. 配置高炉建筑
    var buildingId = new Identifier("mymod", "blast_furnace");
    BuildingUtils.RegisterBuilding(buildingId, furnaceInfo, furnacePrefab);

    ProductionUtils.ConfigureProductionBuilding(buildingId,
        new ProductionBuildingConfig
        {
            BuildingId = buildingId,
            Tags = new[] { "furnace" },
            InputContainerId = ContainerUtils.CreateContainer(
                new Identifier("mymod", "furnace_input"), 6, "mymod").Id,
            OutputContainerId = ContainerUtils.CreateContainer(
                new Identifier("mymod", "furnace_output"), 4, "mymod").Id,
        }, "mymod");

    // 3. 注册设备 UI（展示输入/输出 + 配方选择 + 进度条）
    ContainerUtils.RegisterDeviceUI(
        new Identifier("mymod", "furnace_ui"),
        new DeviceUILayout
        {
            Title = "高炉",
            ContainerId = new Identifier("mymod", "furnace_inout"),
            InputSlotIndices = new[] { 0, 1, 2, 3, 4, 5 },
            OutputSlotIndices = new[] { 6, 7, 8, 9 },
            GridColumns = 5,
            Buttons = new[]
            {
                new DeviceButton { Label = "开始冶炼", OnClick = c => {
                    ProductionUtils.StartProduction(buildingId,
                        new Identifier("mymod", "smelt_iron"));
                }}
            }
        }, "mymod");

    // 4. 监听生产完成
    ProductionUtils.OnProductionComplete(buildingId, bid =>
    {
        Debug.Log($"高炉冶炼完成！");
    });
}
```

---

## 9. 与现有模块的关系图

```
                    ┌─────────────────────────────────────┐
                    │          InteractionUtils             │
                    │  (SpawnView / AttachToNPC / Dispatch) │
                    └──────────┬──────────────────────────┘
                               │ opens
                    ┌──────────▼──────────────────────────┐
                    │         ViewDispatcher                │
                    │  Identifier("mymod","forge_view")     │
                    │  Identifier("mymod","miner_ui")       │
                    │  Identifier("mymod","furnace_ui")     │
                    └──┬──────────┬──────────┬─────────────┘
                       │          │          │
          ┌────────────▼──┐ ┌─────▼──────┐ ┌▼──────────────┐
          │CraftingView   │ │Container   │ │Production     │
          │FilterPatch    │ │Utils       │ │Utils          │
          │(Harmony)      │ │(Registry)  │ │(Registry)     │
          └──────┬────────┘ └──────┬──────┘ └──────┬────────┘
                 │                │                │
          ┌──────▼────────┐ ┌─────▼──────┐ ┌──────▼────────┐
          │ Game Native   │ │ItemSlot    │ │BuildingUtils  │
          │ CraftingView  │ │Renderer    │ │(PlaceBuilding)│
          └───────────────┘ └────────────┘ └───────────────┘
```

---

## 10. 实施路线图

### Phase 1 — 场景 1 过滤式合成（P0，约 3 小时）

1. 创建 `CraftingViewFilter.cs`（数据模型）
2. 创建 `CraftingViewRegistry.cs`（Registry）
3. 创建 `CraftingViewFilterPatch.cs`（Harmony 桥接）
4. `CraftingUtils` 新增 `RegisterCraftingView` / `UnregisterCraftingView`
5. 验证端到端：注册 → ViewDispatcher.Open → 过滤后的 CraftingView

### Phase 2 — 场景 2 容器 + 设备 UI（P0，约 6 小时）

1. 创建 `ItemContainerConfig.cs` / `ContainerSlot.cs` / `ItemFilter.cs`
2. 创建 `ContainerRegistry.cs`
3. 创建 `ContainerUtils.cs`（Create/Get/Destroy/PutItem/TakeItem/...）
4. 创建 `DeviceUILayout.cs` / `DeviceButton.cs`
5. `SimpleViewBuilder` 扩展 `AddItemSlotGrid`
6. 创建 `ItemSlotRenderer.cs`（GameObject 组件）
7. `ContainerUtils` 新增 `RegisterDeviceUI` / `UnregisterDeviceUI` / `BindContainerToBuilding`
8. 创建 `DeviceUIRenderer.cs`（内部类，解析 layout → Canvas）
9. 验证端到端：创建容器 → 放入物品 → 打开 UI → 看到物品槽

### Phase 3 — 场景 3 产线系统（P1，约 5 小时）

1. 创建 `ProcessRecipe.cs` / `Byproduct.cs`
2. 创建 `ProcessRecipeRegistry.cs`
3. 创建 `ProductionBuildingConfig.cs`
4. 创建 `ProductionRegistry.cs`
5. 创建 `ProductionUtils.cs`（RegisterRecipe / Configure / Start / Stop / OnComplete）
6. 创建 `ProductionTimer.cs`（UniTask 驱动）
7. `SimpleViewBuilder` 扩展 `AddProgressBar` / `AddRecipeList`
8. 验证端到端：注册配方 → 配置建筑 → 启动 → 等待完成 → 取出产物

### Phase 4 — 存档与运维（P2，约 2 小时）

1. `ProductionSavePatch.cs`（通过 EventBus CollectSaveData 持久化进度和容器内容）
2. `RegisterBootstrap` 新增所有新模块 Init
3. 文档：`Docs/USAGE.md` 新增场景 1/2/3 章节
4. 编写 `ContainerTest.cs`、`ProductionTest.cs`

---

## 11. 设计决策记录

### Q: 为什么不直接提供"矿机"或"产线"预制体？

FML 定位为**框架**而非内容包。预制体依赖美术资源（模型、贴图），这些由 modder 提供。FML 提供"容器 + UI 渲染 + 生产计时"的能力层，modder 在此基础上创建具体设备。

### Q: 为什么需要 FML 自己的 ItemContainer 而不直接用游戏原生容器？

游戏原生容器可能绑定到具体 UI 管线（如 LootBox 绑定到 LootingView）。FML 的抽象容器独立于 UI，可与 ViewDispatcher 对接任意 View，也可绑定到 Building/NPC/场景物件。若后续发现游戏有通用容器系统，可通过适配层桥接。

### Q: SimpleViewBuilder 的 AddItemSlotGrid 够用吗？需要完整的 UI 框架吗？

足够覆盖当前场景。`SimpleViewBuilder` 的设计目标是覆盖约 85% 的 UI 需求（当前仅覆盖 15%）。增加物品槽、进度条、配方列表后可达约 60%。对于更复杂的 UI（如完整的拖放交互），建议 modder 使用 Unity 编辑器制作 Prefab，通过 `SpawnPrefabInteract` 挂载。

### Q: 建筑间物品传递（场景 3 的连接）需要 FML 直接支持吗？

不需要。连接方式高度依赖 modder 创意（物理管道、无线传输、物流机器人等）。FML 提供的是"容器→容器"的物品转移 API（`PutItem` / `TakeItem`），modder 自行编写连接逻辑。如果后续需求强烈，可增加 `ConnectBuildings(from, to)` 抽象。

---

## 12. 文件结构总览

```
FastModdingLib/
├── Crafting/
│   ├── CraftingUtils.cs              ← [修改] 新增 RegisterCraftingView
│   ├── CraftingViewFilter.cs         ← [新增] 过滤条件
│   ├── CraftingViewRegistry.cs       ← [新增] 过滤视图 Registry
│   └── Patches/
│       └── CraftingViewFilterPatch.cs ← [新增] Harmony 桥接
├── Containers/
│   ├── ContainerUtils.cs             ← [新增] 容器管理 API
│   ├── ContainerRegistry.cs          ← [新增] SimpleRegistry<ItemContainerConfig>
│   ├── ItemContainerConfig.cs        ← [新增] 容器+槽位+过滤
│   ├── DeviceUILayout.cs             ← [新增] 设备 UI 布局
│   └── DeviceUIRenderer.cs           ← [新增] UI 渲染引擎
├── Production/
│   ├── ProductionUtils.cs            ← [新增] 产线管理 API
│   ├── ProductionRegistry.cs         ← [新增] SimpleRegistry<ProductionBuildingConfig>
│   ├── ProcessRecipeRegistry.cs      ← [新增] SimpleRegistry<ProcessRecipe>
│   ├── ProcessRecipe.cs              ← [新增] 工序配方
│   ├── ProductionBuildingConfig.cs   ← [新增] 生产建筑配置
│   ├── ProductionTimer.cs            ← [新增] UniTask 计时器
│   └── Patches/
│       └── ProductionSavePatch.cs    ← [新增] 存档持久化
├── UI/
│   ├── SimpleViewBuilder.cs          ← [修改] 扩展 AddItemSlotGrid 等
│   └── ItemSlotRenderer.cs           ← [新增] 物品槽组件
└── Register/
    └── RegisterBootstrap.cs          ← [修改] 新增 Init 调用
```
