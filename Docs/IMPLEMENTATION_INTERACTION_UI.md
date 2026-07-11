# FML 交互与 UI 系统 — 落地实施文档

> **日期**: 2026-07-11 | **基础**: `DESIGN_INTERACTION_API.md` + `DESIGN_UI_SYSTEM_API.md` + 反编译审计

---

## 变更清单总览

### 新增文件（7 个）

| # | 文件 | 行数估算 | 来源 |
|---|------|---------|------|
| 1 | `FastModdingLib/Interaction/InteractionUtils.cs` | ~250 | DESIGN_INTERACTION_API.md |
| 2 | `FastModdingLib/Interaction/InteractionRegistry.cs` | ~30 | DESIGN_INTERACTION_API.md |
| 3 | `FastModdingLib/Interaction/Components/ViewInteractHandler.cs` | ~50 | DESIGN_INTERACTION_API.md |
| 4 | `FastModdingLib/Interaction/Components/DelegateInteractHandler.cs` | ~30 | DESIGN_INTERACTION_API.md |
| 5 | `FastModdingLib/Interaction/ViewDispatcher.cs` | ~80 | DESIGN_INTERACTION_API.md |
| 6 | `FastModdingLib/UI/GameUIUtils.cs` | ~120 | 反编译审计 + DESIGN_UI_SYSTEM_API.md §2 |
| 7 | `FastModdingLib/Containers/ContainerUtils.cs` | ~200 | DESIGN_UI_SYSTEM_API.md §4 |

### 修改文件（4 个）

| # | 文件 | 改动 |
|---|------|------|
| 8 | `FastModdingLib/UI/InteractTemplates.cs` | `perkTreeID` → public；旧模板 `OnInteractFinished` 改为调用 `ViewDispatcher.Open()` |
| 9 | `FastModdingLib/Register/RegisterBootstrap.cs` | 新增 `InteractionUtils.Init()` |
| 10 | `FastModdingLib/UI/SimpleViewBuilder.cs` | 新增 `AddGameButton` / `AddGamePanel`（委托 `GameUIUtils`） |
| 11 | `FastModdingLib/Crafting/CraftingUtils.cs` | 新增 `OpenFilteredCraftingView(tags)` |

### 已有设计文档（2 个，无需修改）

| # | 文件 | 状态 |
|---|------|------|
| — | `Docs/DESIGN_INTERACTION_API.md` | ✅ 完整，可用 |
| — | `Docs/DESIGN_UI_SYSTEM_API.md` | ✅ 完整，§2 视觉一致性策略已基于反编译审计更新 |

### 暂缓 / 本期不实施

| 内容 | 原因 |
|------|------|
| `ProductionUtils`（产线系统） | 纯逻辑层，无新 UI 渲染，可独立实施 |
| `InteractionFactory.cs` | 逻辑简单，并入 `InteractionUtils` 内部方法 |
| `DeviceUIRenderer.cs` | BitcoinMinerView 模式表明游戏原生组件已可用，FML 封装 `ContainerUtils` 即可 |
| `ViewInjector`（Harmony 动态 Patch） | 场景 1/2 均有无需 Patch 的更简单路径 |

---

## 详细实施规格

### 文件 1：`InteractionUtils.cs`

**职责**：交互系统主入口。Spawn / Attach / Query / Cleanup。

```csharp
// namespace FeatherMod
public static class InteractionUtils
{
    // —— Lifecycle ——
    internal static void Init();  // 由 RegisterBootstrap 调用

    // —— Spawn ——
    // 在世界坐标生成 View 交互点（自动创建 GameObject + Collider + ViewInteractHandler）
    public static GameObject SpawnViewInteract(
        Identifier id, Vector3 position, Identifier viewType,
        string? viewParam = null, Quaternion? rotation = null, Vector3? colliderSize = null);

    // 在世界坐标生成自定义交互点（DelegateInteractHandler）
    public static GameObject SpawnCustomInteract(
        Identifier id, Vector3 position, Action onInteract,
        Quaternion? rotation = null, Vector3? colliderSize = null);

    // —— Attach ——
    // 给已有 GameObject 挂载 View 交互
    public static void AttachViewInteract(
        Identifier id, GameObject target, Identifier viewType,
        string? viewParam = null, bool addColliderIfMissing = true);

    // 挂载自定义交互
    public static void AttachCustomInteract(
        Identifier id, GameObject target, Action onInteract,
        bool addColliderIfMissing = true);

    // 按名称找 NPC 并挂载交互
    public static bool AttachToNPC(
        Identifier id, string npcName, Identifier viewType, string? viewParam = null);

    // —— Query ——
    public static GameObject? GetInteractPoint(Identifier id);
    public static bool TryGetInteractPoint(Identifier id, out GameObject point);

    // —— Cleanup ——
    public static bool RemoveInteract(Identifier id);
    public static int RemoveAllInteracts(string modid);
}
```

**简化点**：`InteractionFactory` 逻辑（创建 GameObject + BoxCollider + "Interact" 图层 + 挂载 Handler）合并为 `InteractionUtils` 内部 private 方法。

---

### 文件 2：`InteractionRegistry.cs`

```csharp
// namespace FeatherMod.Interaction
public sealed class InteractionRegistry : SimpleRegistry<InteractionEntry>
{
    protected override void OnRemoved(Identifier id, InteractionEntry entry, string? modid)
    {
        if (entry?.Target != null)
            UnityEngine.Object.Destroy(entry.Target);
    }
}

public class InteractionEntry
{
    public GameObject Target;
    public string Modid;
}
```

---

### 文件 3：`ViewInteractHandler.cs`

```csharp
// namespace FeatherMod.Interaction.Components
public class ViewInteractHandler : InteractableBase
{
    public Identifier ViewType;      // ← public，可直接赋值
    public string? ViewParam;        // ← public

    protected override void OnInteractFinished()
        => ViewDispatcher.Open(ViewType, ViewParam);
}
```

---

### 文件 4：`DelegateInteractHandler.cs`

```csharp
// namespace FeatherMod.Interaction.Components
public class DelegateInteractHandler : InteractableBase
{
    public Action? OnInteract;       // ← public

    protected override void OnInteractFinished()
        => OnInteract?.Invoke();
}
```

---

### 文件 5：`ViewDispatcher.cs`

```csharp
// namespace FeatherMod.Interaction
public static class ViewDispatcher
{
    // 注册 View 打开方法
    public static void Register(Identifier viewType, Action<string?> openAction, string modid);

    // 打开 View
    public static void Open(Identifier viewType, string? viewParam = null);

    // 查询 / 注销
    public static bool IsRegistered(Identifier viewType);
    public static bool Unregister(Identifier viewType);
    public static int UnregisterAll(string modid);
}

// namespace FeatherMod
public static class GameViews
{
    public static readonly Identifier PerkTree  = new("fml", "perktree");
    public static readonly Identifier Building  = new("fml", "building");
    public static readonly Identifier Endowment = new("fml", "endowment");
    public static readonly Identifier Shop      = new("fml", "shop");
    public static readonly Identifier Crafting  = new("fml", "crafting");
    public static readonly Identifier Quest     = new("fml", "quest");
}
```

**内置 View 自动注册**（在 `InteractionUtils.Init()` 中）：

```csharp
ViewDispatcher.Register(GameViews.PerkTree,  param => {
    var tree = PerkTreeManager.GetPerkTree(param!);
    if (tree != null) PerkTreeView.Show(tree);   // 需确认 PerkTreeView.Show 的可用性
}, FMLConstants.Domain);

ViewDispatcher.Register(GameViews.Building, param =>
    BuilderView.Show(null), FMLConstants.Domain);

ViewDispatcher.Register(GameViews.Endowment, _ => {
    // EndowmentSelectionPanel 由游戏原生触发，FML 不直接打开
}, FMLConstants.Domain);

ViewDispatcher.Register(GameViews.Crafting, param => {
    // 委托给 CraftingUtils.OpenFilteredCraftingView
    if (!string.IsNullOrEmpty(param))
        CraftingUtils.OpenFilteredCraftingView(param.Split(','));
}, FMLConstants.Domain);
```

---

### 文件 6：`GameUIUtils.cs`（**本次最关键文件**）

**职责**：桥接游戏原生 UI 系统。提供控件克隆 + 样式查询 + 快捷 View 打开。

```csharp
// namespace FeatherMod.UI
public static class GameUIUtils
{
    // ═══════════════════════════════════════════════
    //  控件克隆（来源：GameplayDataSettings.UIPrefabs）
    // ═══════════════════════════════════════════════

    /// <summary>克隆游戏原生物品图标显示。</summary>
    public static ItemDisplay CloneItemDisplay(Transform parent);

    /// <summary>克隆游戏原生物品槽位显示。</summary>
    public static SlotDisplay CloneSlotDisplay(Transform parent);

    /// <summary>克隆游戏原生库存条目显示。</summary>
    public static InventoryEntry CloneInventoryEntry(Transform parent);

    /// <summary>克隆游戏原生按钮（含正确的精灵/颜色/字体）。</summary>
    public static Button CloneButton(Transform parent, string label, Action onClick);

    /// <summary>克隆游戏原生滚动区域。</summary>
    public static ScrollRect CloneScrollRect(Transform parent);

    // ═══════════════════════════════════════════════
    //  样式查询
    // ═══════════════════════════════════════════════

    /// <summary>获取游戏主字体（从 TextMeshProUGUI 提取）。</summary>
    public static TMP_FontAsset? GetGameFont();

    /// <summary>提取游戏 UI 配色方案。</summary>
    public static GameUIColorPalette GetColorPalette();

    // ═══════════════════════════════════════════════
    //  快捷 View 打开
    // ═══════════════════════════════════════════════

    /// <summary>打开过滤式合成界面。tags 为标签数组。</summary>
    public static void OpenCraftingView(string[]? tags = null);

    /// <summary>打开库存设备面板。</summary>
    public static void OpenInventoryDevice(Inventory inventory, InventorySlot[]? slots = null);
}

public class GameUIColorPalette
{
    public Color TextPrimary;
    public Color PanelBackground;
    public Color ButtonNormal;
    public Color ButtonHighlight;
    // 运行时从活跃 View 实例中提取
}
```

**实施关键点**：

1. `GameplayDataSettings.UIPrefabs`（Publicizer 已公开）→ `UIPrefabsReference.ItemDisplay/SlotDisplay/InventoryEntry/Button/ScrollRect`
2. 克隆 = `Object.Instantiate(prefab, parent)` — 自动继承精灵/材质/着色器
3. 颜色提取 = 遍历 `View.ActiveView` 链，反射读取 `[SerializeField]` 颜色字段
4. `OpenCraftingView` = 调用 `CraftView.SetupAndOpenView(predicate)` — **无需 Harmony Patch**

---

### 文件 7：`ContainerUtils.cs`

**职责**：轻量物品容器管理（不实现完整的 Inventory 系统，仅包装游戏原生 API）。

```csharp
// namespace FeatherMod
public static class ContainerUtils
{
    // —— 容器 CRUD ——
    public static ItemContainerConfig CreateContainer(Identifier id, int slotCount, string modid);
    public static ItemContainerConfig? GetContainer(Identifier id);
    public static bool DestroyContainer(Identifier id);

    // —— 物品转移 ——
    public static bool PutItem(Identifier containerId, int slot, ItemEntry item);
    public static ItemEntry? TakeItem(Identifier containerId, int slot, int amount);

    // —— 绑定到建筑 ——
    public static void BindDeviceToBuilding(
        Identifier buildingId, Identifier containerId, Identifier viewType);

    // —— 生命周期 ——
    public static int RemoveAllContainers(string modid);
}
```

**简化点**：原设计中的 `ItemFilter` / `DeviceButton` / `DeviceUILayout` / `DeviceUIRenderer` 全部暂缓。`ContainerUtils` 只需要：
1. 维护内部 `Dictionary<Identifier, ItemContainerConfig>` 
2. `PutItem`/`TakeItem` 委托给游戏原生 `ItemUtilities.SendToPlayer()` / `Inventory.TryAdd()` 等方法
3. `BindDeviceToBuilding` = `BuildingUtils.OnBuildingBuilt` → 挂载 `ViewInteractHandler`

---

### 文件 8：`InteractTemplates.cs`（修改）

| 类 | 改动 |
|---|------|
| `PerkTreeInteractTemplate` | `private string? perkTreeID` → `public string? PerkTreeID` |
| `BuildingInteractTemplate` | `OnInteractFinished` 改为 `ViewDispatcher.Open(GameViews.Building, buildingIdentifier)` |
| `PerkTreeInteractTemplate` | `OnInteractFinished` 改为 `ViewDispatcher.Open(GameViews.PerkTree, PerkTreeID)` |

---

### 文件 9：`RegisterBootstrap.cs`（修改）

在 `Init()` 末尾添加：
```csharp
InteractionUtils.Init();
```

---

### 文件 10：`SimpleViewBuilder.cs`（修改）

新增两个方法，内部委托 `GameUIUtils`：

```csharp
public SimpleViewBuilder AddGameButton(string text, Action onClick)
{
    GameUIUtils.CloneButton(_contentParent, text, onClick);
    return this;
}

public SimpleViewBuilder AddGamePanel(string title)
{
    // 克隆游戏原生面板模板（如有），否则降级为纯色面板
    return this;
}
```

---

### 文件 11：`CraftingUtils.cs`（修改）

新增：

```csharp
/// <summary>打开过滤式合成界面。tags 为工作台标签（如 "Forge"）。</summary>
public static void OpenFilteredCraftingView(params string[] tags)
{
    GameUIUtils.OpenCraftingView(tags);
}
```

---

## 实施顺序（强依赖）

```
Wave 1（可并行）:
  ├── 文件 2: InteractionRegistry.cs      (独立，无依赖)
  ├── 文件 3: ViewInteractHandler.cs      (依赖: InteractableBase)
  ├── 文件 4: DelegateInteractHandler.cs  (依赖: InteractableBase)
  ├── 文件 6: GameUIUtils.cs              (依赖: GameplayDataSettings.UIPrefabs)
  └── 文件 5: ViewDispatcher.cs           (独立，纯逻辑)

Wave 2（依赖 Wave 1）:
  ├── 文件 1: InteractionUtils.cs         (依赖: 2+3+4+5)
  └── 文件 7: ContainerUtils.cs           (依赖: 6+1)

Wave 3（修改现有文件）:
  ├── 文件 8: InteractTemplates.cs        (依赖: 3+5)
  ├── 文件 9: RegisterBootstrap.cs        (依赖: 1)
  ├── 文件 10: SimpleViewBuilder.cs       (依赖: 6)
  └── 文件 11: CraftingUtils.cs           (依赖: 6)

Wave 4（验证）:
  └── 编译 + LSP diagnostics
```

---

## 关键技术参考

| 需求 | 游戏原生 API（反编译确认） | FML 使用方式 |
|------|--------------------------|-------------|
| 克隆按钮 | `GameplayDataSettings.UIPrefabs.Button` | `Object.Instantiate()` |
| 克隆物品槽 | `GameplayDataSettings.UIPrefabs.SlotDisplay` | `Object.Instantiate()` |
| 克隆库存条目 | `GameplayDataSettings.UIPrefabs.InventoryEntry` | `Object.Instantiate()` |
| 打开过滤合成 | `CraftView.SetupAndOpenView(Predicate)` | 直接调用 |
| 库存绑定 | `InventoryDisplay.Setup(Inventory)` | 直接调用 |
| 物品转移 | `ItemUtilities.SendToPlayer(item)` | 直接调用 |
| 获取 View 实例 | `GameplayUIManager.GetViewInstance<T>()` | 直接调用 |
| 字体 | TextMeshProUGUI 组件 | 从活跃 View 中提取 `font` / `TMP_FontAsset` |
