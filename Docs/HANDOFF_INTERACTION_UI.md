# Handoff — FML 交互与 UI 系统实施

> 供新会话继续执行。本文件包含完整的上下文还原和实施指令。

## 背景

你是 FML（Feather Modding Lib，逃离鸭科夫 Mod 框架）的开发者。当前任务：实施交互与 UI 系统的 API 补充。

## 前置状态

- 已有设计文档：`Docs/DESIGN_INTERACTION_API.md`（交互系统）、`Docs/DESIGN_UI_SYSTEM_API.md`（UI 系统）
- 已有落地文档：`Docs/IMPLEMENTATION_INTERACTION_UI.md`（本任务的具体规格）
- 已完成反编译审计：`DecompiledDLL/Core/` 下有游戏原生代码
- 关键发现：`GameplayDataSettings.UIPrefabs`（Publicizer 已公开）提供全游戏 UI 控件预制体引用

## 任务目标

按以下顺序实施，完成后**不提交 git**，仅确保编译通过 + LSP diagnostics 清洁。

### Wave 1（5 个文件，可并行）

1. **`FastModdingLib/Interaction/InteractionRegistry.cs`**
   - `InteractionRegistry : SimpleRegistry<InteractionEntry>`
   - `OnRemoved` 中 `Object.Destroy(entry.Target)`
   - `InteractionEntry` 类含 `Target`（GameObject）+ `Modid`

2. **`FastModdingLib/Interaction/Components/ViewInteractHandler.cs`**
   - 继承 `InteractableBase`
   - `public Identifier ViewType` + `public string? ViewParam`
   - `OnInteractFinished()` → `ViewDispatcher.Open(ViewType, ViewParam)`

3. **`FastModdingLib/Interaction/Components/DelegateInteractHandler.cs`**
   - 继承 `InteractableBase`
   - `public Action? OnInteract`
   - `OnInteractFinished()` → `OnInteract?.Invoke()`

4. **`FastModdingLib/UI/GameUIUtils.cs`**
   - 核心文件。实现：
     - `CloneButton(Transform parent, string label, Action onClick)` — 从 `GameplayDataSettings.UIPrefabs.Button` 克隆
     - `CloneItemDisplay(Transform parent)` — 从 `GameplayDataSettings.UIPrefabs.ItemDisplay` 克隆
     - `CloneSlotDisplay(Transform parent)` — 从 `GameplayDataSettings.UIPrefabs.SlotDisplay` 克隆
     - `CloneInventoryEntry(Transform parent)` — 从 `GameplayDataSettings.UIPrefabs.InventoryEntry` 克隆
     - `GetGameFont()` — 从活跃 `TextMeshProUGUI` 实例提取
     - `GetColorPalette()` → `GameUIColorPalette` — 从活跃 View `[SerializeField]` 字段提取
     - `OpenCraftingView(string[]? tags)` — 调用 `CraftView.SetupAndOpenView(Predicate<CraftingFormula>)`
   - 颜色提取策略：遍历 `GameplayUIManager.Instance.views`，反射读 `Color` 类型 `[SerializeField]` 字段

5. **`FastModdingLib/Interaction/ViewDispatcher.cs`**
   - `Dictionary<Identifier, Action<string?>>` 映射
   - `Register` / `Open` / `Unregister` / `UnregisterAll` / `IsRegistered`
   - `GameViews` 静态类：`PerkTree` / `Building` / `Endowment` / `Crafting` / `Shop` / `Quest`

### Wave 2（2 个文件，依赖 Wave 1）

6. **`FastModdingLib/Interaction/InteractionUtils.cs`**
   - `Init()`：注册 `InteractionRegistry` 到元表 + 注册内置 View
   - Spawn 方法：创建 `GameObject` → `BoxCollider(Trigger)` + `"Interact"` 图层 + `AddComponent<ViewInteractHandler/DelegateInteractHandler>` → 注册到 Registry
   - Attach 方法：给已有 GameObject 挂载 Handler
   - `AttachToNPC`：`GameObject.Find(npcName)` 兜底遍历 `AICharacterController`
   - Query / Cleanup 方法

7. **`FastModdingLib/Containers/ContainerUtils.cs`**
   - `Dictionary<Identifier, ItemContainerConfig>` 内部存储
   - `CreateContainer` / `GetContainer` / `DestroyContainer`
   - `PutItem` / `TakeItem` 委托给 `ItemUtilities.SendToPlayer()` 等游戏 API
   - `BindDeviceToBuilding`：`BuildingUtils.OnBuildingBuilt` + 挂载 `ViewInteractHandler`
   - `RemoveAllContainers`

### Wave 3（4 个修改现有文件）

8. **`FastModdingLib/UI/InteractTemplates.cs`**
   - `PerkTreeInteractTemplate.perkTreeID` → `public string? PerkTreeID`
   - 三个模板的 `OnInteractFinished` 改为调用 `ViewDispatcher.Open()`

9. **`FastModdingLib/Register/RegisterBootstrap.cs`**
   - `Init()` 末尾添加 `InteractionUtils.Init();`

10. **`FastModdingLib/UI/SimpleViewBuilder.cs`**
    - 新增 `AddGameButton(string, Action)` — 委托 `GameUIUtils.CloneButton`
    - 新增 `AddGamePanel(string)` — 降级为纯色面板

11. **`FastModdingLib/Crafting/CraftingUtils.cs`**
    - 新增 `OpenFilteredCraftingView(params string[] tags)` — 委托 `GameUIUtils.OpenCraftingView`

### Wave 4（验证）

12. 编译整个项目
13. 对改动的每个文件运行 `lsp_diagnostics`
14. 报告结果

## 关键约束

- **Identifier 优先**：所有 public API 签名使用 `Identifier`，不暴露原生数字 ID
- **Publicizer**：游戏内部字段已通过 Krafs.Publicizer 公开，直接访问，无需反射
- **生命周期模式 A**：Registry → SimpleRegistry<T> → OnRemoved 自动清理
- **命名空间**：交互系统用 `FeatherMod.Interaction.Components`；UI 系统用 `FeatherMod.UI`
- **现有 Pattern 参考**：PerkTreeRegistry 的 OnRemoved 模式、BuildingUtils 的 CreateSimpleBuilding 模式

## 已知技术事实（来自反编译审计）

- 游戏 View 基类：`Duckov.UI.View : ManagedUIElement`，有 `OnOpen()`/`OnClose()` 虚方法
- `GameplayUIManager.Instance.views`（List<View>）— 所有 View 实例
- `GameplayUIManager.GetViewInstance<T>()` — 按类型获取 View
- `GameplayDataSettings.UIPrefabs` — ScriptableObject，含 ItemDisplay/SlotDisplay/InventoryEntry/Button/ScrollRect
- `CraftView.SetupAndOpenView(Predicate<CraftingFormula>)` — 直接支持过滤，无需 Patch
- `InventoryDisplay.Setup(Inventory)` — 库存到 UI 的绑定
- `ItemUtilities.SendToPlayer(Item)` — 物品转移到玩家
- 字体：`TextMeshProUGUI`，颜色在 View 的 `[SerializeField]` Color 字段
