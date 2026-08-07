# API Reference — Interaction & UI / 交互与 UI API

> **模块**：交互点、View 调度、UI 桥接、UI 构建器、设置面板
> **教程**：[USAGE.md 交互系统](../USAGE.md#16-交互系统--interaction)、[USAGE.md UI 系统](../USAGE.md#17-ui-系统与控件桥接--gameui)

---

## 目录

- [InteractionUtils — 交互点](#interactionutils)
- [ViewDispatcher / GameViews — View 调度](#viewdispatcher--gameviews)
- [InteractionGroupBuilder — 多交互组合](#interactiongroupbuilder)
- [Interact 组件](#interact-组件)
- [GameUIUtils — UI 桥接](#gameuiutils)
- [SimpleViewBuilder — UI 构建器](#simpleviewbuilder)
- [ModOptionsRegistry — 设置面板](#modoptionsregistry)

---

## InteractionUtils

**命名空间**：`FeatherMod.Interaction` | **源码**：`Interaction/InteractionUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `SpawnViewInteract` | `static GameObject SpawnViewInteract(Identifier id, Vector3 position, Identifier viewType, string? viewParam = null, Quaternion? rotation = null, Vector3? colliderSize = null, string? interactNameKey = null, Vector3? markerOffset = null, float coolTime = 0f)` | 世界坐标创建 View 交互点 |
| `SpawnCustomInteract` | `static GameObject SpawnCustomInteract(Identifier id, Vector3 position, Action onInteract, Quaternion? rotation = null, Vector3? colliderSize = null)` | 世界坐标创建委托交互点 |
| `AttachViewInteract` | `static void AttachViewInteract(Identifier id, GameObject target, Identifier viewType, string? viewParam = null, bool addColliderIfMissing = true, string? interactNameKey = null, Vector3? markerOffset = null, float coolTime = 0f)` | 挂载到已有 GO |
| `AttachCustomInteract` | `static void AttachCustomInteract(Identifier id, GameObject target, Action onInteract, bool addColliderIfMissing = true)` | 挂载委托交互 |
| `AttachToNPC` | `static bool AttachToNPC(Identifier id, string npcName, Identifier viewType, string? viewParam = null)` | 按名查找 NPC 挂载 |
| `SetupInteractionGroup` | `static void SetupInteractionGroup(InteractableBase primary, params InteractableBase[] members)` | 编组（主交互 + 成员） |
| `GetInteractPoint` | `static GameObject? GetInteractPoint(Identifier id)` | 查询 |
| `TryGetInteractPoint` | `static bool TryGetInteractPoint(Identifier id, out GameObject point)` | 安全查询 |
| `RemoveInteract` | `static bool RemoveInteract(Identifier id)` | 移除（自动 Destroy GO） |
| `RemoveAllInteracts` | `static int RemoveAllInteracts(string modid)` | 批量移除 |

---

## ViewDispatcher / GameViews

**命名空间**：`FeatherMod.Interaction` | **源码**：`Interaction/ViewDispatcher.cs`

### ViewDispatcher

| 方法 | 签名 | 说明 |
|------|------|------|
| `Register` | `static void Register(Identifier viewType, Action<string?> openAction, string modid)` | 注册自定义 View 打开方法 |
| `Open` | `static void Open(Identifier viewType, string? viewParam = null)` | 打开 View |
| `IsRegistered` | `static bool IsRegistered(Identifier viewType)` | |
| `Unregister` | `static bool Unregister(Identifier viewType)` | |
| `UnregisterAll` | `static int UnregisterAll(string modid)` | |

### GameViews（内置常量，`fml:xxx`）

| 常量 | 值 | 打开效果 |
|------|-----|----------|
| `PerkTree` | `fml:perktree` | Perk 技能树 |
| `Building` | `fml:building` | 建造面板（BuilderView） |
| `Endowment` | `fml:endowment` | 天赋选择面板 |
| `Crafting` | `fml:crafting` | 过滤式合成界面 |
| `Shop` | `fml:shop` | 商店（自动查找 NPC 的 StockShop） |
| `Quest` | `fml:quest` | 任务（QuestView.Show） |
| `Formulas` | `fml:formulas` | 配方索引浏览 |
| `FormulasRegister` | `fml:formulasregister` | 配方注册/研究（viewParam = 标签过滤） |
| `Decompose` | `fml:decompose` | 分解界面 |
| `Machine` | `fml:machine` | 机器界面 |

---

## InteractionGroupBuilder

**命名空间**：`FeatherMod.Interaction` | **源码**：`Interaction/InteractionGroupBuilder.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `Add` | `InteractionGroupBuilder Add(Identifier id, Identifier viewType, string? viewParam = null, string? interactNameKey = null, Vector3? markerOffset = null)` | 添加交互条目 |
| `WithPrimary` | `InteractionGroupBuilder WithPrimary(int index)` | 指定主交互 |
| `BuildOn` | `ViewInteractHandler BuildOn(GameObject target)` | 构建到目标 GO |

> 多条目时自动创建子节点 + BoxCollider + `ViewInteractHandler`，编组为 `interactableGroup`（主交互体可交互，成员碰撞体禁用）。单条目直接挂载。

---

## Interact 组件

**命名空间**：`FeatherMod.Interaction.Components`

| 组件 | 关键成员 | 用途 |
|------|----------|------|
| `ViewInteractHandler` | `ViewType` / `ViewParam` / `InteractNameKey` / `MarkerOffset` / `CoolTime` / `FinishWhenTimeOut` | View 交互 |
| `DelegateInteractHandler` | `OnInteract`(Action?) | 委托交互 |
| `FeatherShopInteract` | `Attach(id, target, merchantId, interactNameKey?)` / `MerchantId` | 商店交互 |
| `FeatherPerkTreeInteract` | `Attach(id, target, perkTreeId, interactNameKey?)` / `PerkTreeId` | 技能树交互 |
| `FeatherQuestGiverInteract` | `Attach(id, target, questGiverId, interactNameKey?)` / `QuestGiverId` | 任务交互 |
| `FeatherFormulasRegisterInteract` | `Attach(id, target, registerTag?, interactNameKey?)` / `RegisterTag` | 蓝图研究台交互 |

---

## GameUIUtils

**命名空间**：`FeatherMod.UI` | **源码**：`UI/GameUIUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `CloneButton` | `static Button CloneButton(Transform parent, string label, Action onClick)` | 克隆原生按钮 |
| `CloneItemDisplay` | `static ItemDisplay CloneItemDisplay(Transform parent)` | 克隆物品图标 |
| `CloneSlotDisplay` | `static SlotDisplay CloneSlotDisplay(Transform parent)` | 克隆物品槽位 |
| `CloneInventoryEntry` | `static InventoryEntry CloneInventoryEntry(Transform parent)` | 克隆库存条目 |
| `CloneScrollRect` | `static ScrollRect CloneScrollRect(Transform parent)` | 克隆滚动区域 |
| `GetGameFont` | `static TMP_FontAsset? GetGameFont()` | 游戏主字体 |
| `GetColorPalette` | `static GameUIColorPalette GetColorPalette()` | UI 配色方案 |
| `OpenCraftingView` | `static void OpenCraftingView(string[]? tags = null)` | 打开过滤合成界面 |
| `OpenInventoryDevice` | `static void OpenInventoryDevice(Inventory inventory)` | 库存设备面板 |
| `OpenFormulasIndexView` | `static void OpenFormulasIndexView()` | 配方索引 |
| `OpenFormulasRegisterView` | `static void OpenFormulasRegisterView()` | 配方注册 |
| `OpenDecomposeView` | `static void OpenDecomposeView()` | 分解界面 |

**GameUIColorPalette**：`TextPrimary` / `PanelBackground` / `ButtonNormal` / `ButtonHighlight`。

> 克隆自 `GameplayDataSettings.UIPrefabs`，自动继承精灵/材质/字体/着色器。

---

## SimpleViewBuilder

**命名空间**：`FeatherMod.UI` | **源码**：`UI/SimpleViewBuilder.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `Create` | `static SimpleViewBuilder Create(string viewName)` | 创建构建器 |
| `AddTitle` | `SimpleViewBuilder AddTitle(string text, int fontSize = 28)` | 标题 |
| `AddText` | `SimpleViewBuilder AddText(string text, int fontSize = 18, FontStyle style = FontStyle.Normal)` | 文本 |
| `AddButton` | `SimpleViewBuilder AddButton(string text, Action onClick)` | 普通按钮 |
| `AddGameButton` | `SimpleViewBuilder AddGameButton(string text, Action onClick)` | 原生风格按钮 |
| `AddGamePanel` | `SimpleViewBuilder AddGamePanel(string title)` | 半透明面板 |
| `AddCloseButton` | `SimpleViewBuilder AddCloseButton(string text = "关闭")` | 关闭按钮 |
| `Build` | `GameObject Build()` | 构建 |

> 适用 15% 的简单 UI 场景；复杂 UI 用 Harmony Postfix 注入或 `GameUIUtils` 控件克隆。

---

## ModOptionsRegistry

**命名空间**：`FeatherMod.Options` | **源码**：`Options/ModOptionsRegistry.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterPanel` | `static void RegisterPanel(string modId, string displayName, Action<ModOptionsBuilder> build)` | 注册设置面板 |
| `UnregisterPanel` | `static void UnregisterPanel(string modId)` | |
| `UnregisterAllPanels` | `static void UnregisterAllPanels()` | |

### ModOptionsBuilder

| 方法 | 签名 |
|------|------|
| `AddToggle` | `void AddToggle(string key, bool defaultValue, string label)` |
| `AddSlider` | `void AddSlider(string key, float defaultValue, float min, float max, string label)` |
| `AddDropdown` | `void AddDropdown(string key, string[] options, int defaultIndex, string label)` |
| `AddButton` | `void AddButton(string label, Action onClick)` |

> 面板出现在 游戏设置 → Custom Options 标签页，值经 `OptionsManager` 自动持久化。

---

## 废弃 API / Obsolete

| 成员 | 替代 |
|------|------|
| （本模块暂无） | |
