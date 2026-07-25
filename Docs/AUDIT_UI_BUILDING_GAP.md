# Building & UI 系统功能审计报告

> **审计日期**: 2026-07-22  
> **审计来源**:  
> - ACB (ACBuildingExpanded) 反编译代码 — 远古建筑扩展 Mod  
> - VAE (VanillaAttachmentsExpanded) 反编译代码 — 远古武器附件扩展 Mod  
> - FML 当前源码 (`FastModdingLib/Buildings/`, `FastModdingLib/UI/`)  
> - 设计文档 `DESIGN_UI_SYSTEM_API.md`  
> - 进度文档 `PROGRESS.md`  
> **结论**: 发现 5 个 P0 缺失、4 个 P1 缺失、3 个 P2 缺失、3 个既有问题。以下为逐项详细分析。

---

## 一、ACB 逆向审计 — 功能对照表

ACB (ACBuildingExpanded) 是一个功能丰富的建筑扩展 Mod，为游戏添加了算力服务器、水培种植、修理台、仓库、圣诞树等建筑类型。以下对照 ACB 已实现的功能与 FML 当前支持能力。

### 1.1 Building 子系统对照

| ACB 功能 | ACB 实现方式 | FML 当前状态 | 缺口等级 |
|----------|-------------|-------------|---------|
| **BuildingInventory（每建筑独立库存）** | 每个建筑 prefab 上挂 `BuildingInventory`（继承 Inventory），建筑生成时自动创建 | ❌ 缺失 — FML 的 Building 仅有 `functionContainer`/`graphicsContainer`，无 inventory 抽象 | 🔴 P0 |
| **多子库存（sub-inventory）** | `ServerManager` 维护 4 个独立 `BuildingInventory`（主库存、发电机、GPU、缓存），全部通过 key 隔离持久化 | ❌ 缺失 — FML 无子库存概念 | 🔴 P0 |
| **DetailsView 自定义注册** | `ModUIManager.DetailsViewDic` 字典，键为 building key，值为自定义 `DetailsView` 实例 | ❌ 缺失 — FML 无法自定义建筑的 DetailsView | 🔴 P0 |
| **View 内容注入（SlotCollectionDisplay / ProgressBarDisplay）** | `View.ContentDisplay.Add(UIPrefabsReference.Instance.SlotCollectionDisplay)` / `View.ContentDisplay.AddToTop(UIPrefabsReference.Instance.ProgressBarDisplay)` | ❌ 缺失 — FML 的 `SimpleViewBuilder` 是独立 Canvas，不接入游戏原生 View 体系 | 🔴 P0 |
| **OnViewOpen/OnViewClose 回调** | `View.OnViewOpen += OnViewOpen`，触发时执行计算逻辑（时间差计算、生产结算） | ❌ 缺失 — FML 无法感知建筑 UI 打开/关闭事件 | 🔴 P0 |
| **OnSelectionItem 回调** | `View.OnSelectionItem += OnSelectionItem`，选中物品时在 Notes 区域显示自定义信息 | ❌ 缺失 | 🟡 P1 |
| **Slot + requireTags 过滤** | `Slot.requireTags.Add(GetTargetTag("tagName"))`，限制每个槽位只接受特定标签物品 | ⚠️ 部分 — `ContainerUtils` 有 `SetSlotFilter`（API已设计但未接入 Building） | 🟡 P1 |
| **BaseBuildingManager 模式** | 抽象基类挂载到 Building prefab，子类（Server/Storage/Hydro/Repair）实现不同功能 | ❌ 缺失 — FML 无 Building 行为组件模式 | 🟡 P1 |
| **InteractCrafter + InteractableGroup 自动装配** | `AcBuildings.SetupCrafter(go, tag, localName)` + `InteractableGroupHelper.AddToGroup()`，让建筑同时显示主交互菜单和合成台入口 | ⚠️ 部分 — FML 有 `InteractTemplates` 但无 Building 级别的自动装配 | 🟡 P1 |
| **Scene-locked Building** | `BuildManager` 组件，通过 `allowedSceneNames` 数组限制建筑仅在特定场景激活 | ❌ 缺失 | 🟢 P2 |
| **BuilderView 自定义（建造面板图标/名称）** | 通过 `LocalizationManager.SetOverrideText` 设置建造面板中的显示名和描述 | ✅ 已支持 — `BuildingConfig.Icon` + `BuildingInfo.DisplayNameKey` | — |
| **Prefab 加载（AssetBundle）** | `AcBuildings.LoadPrefab()` → `AssetBundle.LoadFromFile()` → `Instantiate` | ✅ 已支持 — `AssetUtil.LoadBundle()` | — |

### 1.2 时间模拟系统对照

| ACB 功能 | ACB 实现方式 | FML 当前状态 | 缺口等级 |
|----------|-------------|-------------|---------|
| **GameClock 访问** | `GameClock.Now` — 获取游戏内时间 | ✅ 已支持 — FML 可通过 `Duckov.Utilities.GameClock` 访问 | — |
| **时间差计算（离线进度）** | `TimeMachine.GetPositiveHoursSince(pastTime)` → 每建筑 OnViewOpen 时计算自上次关闭以来的游戏内经过时间 | ❌ 缺失 — FML 无离线进度计算框架 | 🔴 P0 |
| **时间序列化/持久化** | `TimeMachine.TimeSpanToString()` / `TryStringToTimeSpan()` — 将 TimeSpan 转为字符串存入 Item | ❌ 缺失 | 🔴 P0 |
| **Item 运行时状态存储** | `item.SetString(key, value)` / `item.GetString(key)` — 将 JSON 状态序列化到物品上，跨存档持久化 | ❌ 缺失 — FML 无物品状态注入机制 | 🔴 P0 |

### 1.3 具体建筑功能（框架能力视角）

ACB 的具体建筑实现展示了 modder 期望框架提供的**能力层**：

| ACB 具体功能 | 需要的框架能力 | FML 当前状态 |
|-------------|--------------|-------------|
| **算力服务器（ServerManager）** | GPU 槽位管理 + 发电机电压计算 + 算力汇总 + 网络API调用 + 产出物品生成 | ❌ 全部缺失 |
| **水培种植（HydroManager + PlantManager）** | 种子物品→时间→产物的转换 + 消耗水/肥料/燃料 + 生命周期管理 | ❌ 全部缺失 — `DESIGN_UI_SYSTEM_API.md` 设计的 `ProcessRecipe` + `ProductionTimer` 能覆盖 |
| **修理台（RepairManager）** | 装备放入→消耗修理材料→按时间恢复耐久度 + DurabilityLoss 追踪 | ❌ 全部缺失 |
| **仓库（StorageManager）** | 专用储物箱 Building（仅提供库存，无生产逻辑） | ❌ 缺失 — `ContainerUtils` 能覆盖基础容器 |
| **算力排名 UI（ComputingPowerRankUI）** | 完整自定义 Canvas + 双 Tab（排名/交易）+ 折线图 + 输入框 + 网络API | ❌ 缺失 — 超出框架范围（Mod 自行实现），但 FML 应提供 `DetailsView` 注入能力让 modder 能挂载此类 UI |

---

## 二、VAE 逆向审计 — UI/Building 相关

VAE (VanillaAttachmentsExpanded) 主要是武器附件扩展 Mod，与 UI/Building 直接相关的内容较少，但其部分模式有参考价值：

| VAE 功能 | 对 FML 的启示 | 优先度 |
|----------|-------------|--------|
| **Harmony Patch 直接嵌入 ModBehaviour** | VAE 的 `ItemAgent_Zombie_Fix` 直接在 `ModBehaviour` 类中定义 `[HarmonyPatch]` 嵌套类 | ℹ️ FML 已有独立 Patch 文件，模式更好 |
| **`WeaponModify` / `WeaponDetails` 配置** | VAE 通过 `ItemConfig` 的 `(int statIndex, float value)[]` 和 `WeaponDetails` 配置武器属性 | ℹ️ FML 的 `ModifierDescriptionWithOffset` 可覆盖 |
| **事件清理（EventSanitizer）** | VAE 有专门的 `EventSanitizer` 类处理事件订阅/清理 | ⚠️ FML 的 `OnBuildingBuilt` 使用反射事件订阅，缺乏对称的清理机制 |

---

## 三、FML 既有问题审计

对照 ACB 的架构，FML 当前实现存在以下具体问题：

### 3.1 既有技术债务（已在 PROGRESS.md 记录）

| # | 问题 | 位置 | 严重度 | 状态 |
|---|------|------|--------|------|
| 1 | `OnBuildingBuilt` 使用反射 `GetEvent` + `AddEventHandler` 订阅原生事件，无法 `RemoveEventHandler` 清理 | `BuildingUtils.cs:432-458` | 🔴 CRITICAL | ❌ 未修复 |
| 2 | `PlaceBuilding` 使用 `GetMethod("BuyAndPlace")` 反射调用，依赖私有方法签名不变量 | `BuildingUtils.cs:20-21` | 🔴 CRITICAL | ❌ 未修复 |
| 3 | Building 系统**从未在游戏中测试过** | PROGRESS.md "待测试模块" 表 | 🔴 CRITICAL | ❌ 未验证 |
| 4 | `OnBuildingBuilt` 回调中 `FindObjectsOfType<Building>()` 查找场景实例，存在性能隐患（O(n) 全场景扫描） | `BuildingUtils.cs:465` | 🟡 HIGH | ❌ 未修复 |
| 5 | `BuildingCollectionPatch.Sanitize` 使用 `__makeref` + `SetValueDirect` 反射修复 `BuildingInfo` struct 字段，仅在 Patch 层应用，`BuildingUtils.RegisterBuilding` 创建的 info 不受保护 | `BuildingCollectionPatch.cs:41-49` | 🟡 HIGH | ⚠️ `BuildingUtils.SanitizeBuildingInfo` 已存在但未在 Patch 层外对所有路径应用 |

### 3.2 架构设计问题（新发现）

| # | 问题 | 描述 |
|---|------|------|
| 6 | **无 BuildingManager 等效层** | ACB 的所有功能型建筑都通过 `BaseBuildingManager`（MonoBehaviour 组件挂在 Building prefab 上）驱动。FML 只有数据层（注册/查询），没有运行时行为层。modder 无法为建筑添加"打开 UI 时计算时间差"这类逻辑。 |
| 7 | **`BuildingConfig` 缺少 DisplayName 字段** | ACB 通过 `LocalizationManager.SetOverrideText` 动态设置建筑名。FML 的 `BuildingConfig` 依赖 `BuildingInfo.DisplayNameKey = "Building_" + id` 约定，但未暴露给 modder 自定义。 |
| 8 | **`SimpleViewBuilder` 是独立 Canvas，无法接入游戏 View 体系** | ACB 的所有 UI 元素（SlotCollectionDisplay、ProgressBarDisplay）都挂在游戏的 `DetailsView.ContentDisplay` 内，视觉风格完全一致。FML 的 `SimpleViewBuilder` 创建独立 Canvas，视觉是纯色块。 |

---

## 四、DESIGN_UI_SYSTEM_API.md 设计文档对照

`DESIGN_UI_SYSTEM_API.md` 已经识别了大部分 UI/Building 缺口并提供了详细设计。以下为实施状态：

| 设计组件 | 设计文档章节 | 实施状态 |
|---------|------------|---------|
| `GameUIFactory` — UI 控件克隆工厂 | §2.3.1 | ❌ 未实施（`GameUIUtils` 仅实现了基础 `UIPrefabs` 克隆，无模板缓存机制） |
| `ViewInjector` — View 层注入 | §2.3.2 | ❌ 未实施 |
| `SimpleViewBuilder` 游戏控件集成 | §2.3.3 | ❌ 未实施（`AddGameButton` / `AddGamePanel` 未实现） |
| `CraftingViewFilter` 过滤式合成 | §3 | ❌ 未实施 |
| `ContainerUtils` + `ItemContainerConfig` | §4.3 | ⚠️ 部分实施（`ContainerUtils` + `ContainerRegistry` 已存在，但未接入 Building） |
| `RegisterDeviceUI` / `DeviceUILayout` | §4.3.2-4.3.3 | ❌ 未实施 |
| `ProcessRecipe` + `ProductionUtils` | §5 | ❌ 未实施 |
| `ItemSlotRenderer` | §6.2 | ❌ 未实施 |
| `SimpleViewBuilder.AddItemSlotGrid` / `.AddProgressBar` / `.AddRecipeList` | §6.1 | ❌ 未实施 |
| `BindContainerToBuilding` | §4.3.3 | ❌ 未实施 |

---

## 五、综合审计结论 — 缺口与修复计划

### 5.1 P0 (CRITICAL) — 阻塞基本使用场景

| # | 缺口 | 依赖 | 预估工时 | 方案 |
|---|------|------|---------|------|
| **P0-1** | **Building DetailsView 自定义注入** — modder 无法向建筑交互面板添加自定义 UI 元素（进度条、额外物品槽） | — | 4h | 实现 `ViewInjector.OnViewOpened(BuildingInventory, callback)` + `DetailsView.ContentDisplay` 桥接 |
| **P0-2** | **Building 子库存 (SubInventory)** — modder 无法为建筑创建额外的隔离 Inventory（例如 GPU 插槽、产出槽） | P0-1 | 3h | 扩展 `BuildingUtils`：`CreateBuildingSubInventory(buildingId, subKey, slotCount, tags)` → 自动注册到 Building prefab |
| **P0-3** | **建筑 OnViewOpen 回调** — modder 无法感知建筑 UI 打开/关闭事件，无法做"打开时计算离线进度"这类核心逻辑 | — | 2h | `BuildingUtils.OnBuildingViewOpened(Identifier, Action<Inventory>)` / `OnBuildingViewClosed` |
| **P0-4** | **时间模拟框架 (TimeMachine)** — modder 无法获取游戏时钟、计算时间差、在物品上持久化时间戳 | — | 3h | 新建 `TimeUtils`：`GameClockAccessor` + `TimeSpanSerializer` + `ItemStateSerializer`（在 Item 上存储/读取 JSON） |
| **P0-5** | **进度条 UI 组件 (ProgressBarDisplay)** — `SimpleViewBuilder` 缺少进度条控件，modder 无法在设备 UI 中展示生产进度 | P0-1 | 2h | 扩展 `SimpleViewBuilder.AddProgressBar(label, getProgress)` → 内部通过 `GameUIUtils` 克隆原生进度条 + 绑定回调 |

### 5.2 P1 (HIGH) — 显著提升框架可用性

| # | 缺口 | 依赖 | 预估工时 | 方案 |
|---|------|------|---------|------|
| **P1-1** | **Building 行为组件模式 (BuildingBehaviour)** — modder 无法将自定义 MonoBehaviour 挂载到 Building prefab 上（ACB 的 `BaseBuildingManager` 模式） | — | 3h | 新建 `BuildingBehaviour : MonoBehaviour` 抽象基类 + `BuildingUtils.AttachBehaviour<T>(buildingId)` → 自动挂载到 Building prefab |
| **P1-2** | **Slot + Tag 过滤 (Building Slot Filters)** — modder 无法限制建筑槽位接受哪些标签的物品（例如 GPU 槽只接受 "GPU" 标签物品） | P0-2 | 2h | 扩展 `ContainerUtils.SetSlotFilter` 接入 Building 子库存 |
| **P1-3** | **`SimpleViewBuilder` 游戏控件可视化** — 当前使用纯色块 Text+Image 渲染，视觉上完全不像游戏原生 UI | — | 4h | 实现 `AddGameButton` / `AddGamePanel` / `AddGameItemSlot` → 内部通过 `GameUIUtils.CloneXxx()` 使用原生预制体 |
| **P1-4** | **OnBuildingBuilt 反射清理** — 使用 `EventInfo.AddEventHandler` 反射 Hook，无法 `RemoveEventHandler` 卸载 | — | 2h | 将事件订阅改为 `[HarmonyPatch]` Postfix 桥接到 EventBus（与框架其他模块保持一致） |

### 5.3 P2 (MEDIUM) — 长期增强

| # | 缺口 | 依赖 | 预估工时 | 方案 |
|---|------|------|---------|------|
| **P2-1** | **生产配方系统 (ProcessRecipe)** — 定义"输入→时间→输出"工序，区别于手动合成（CraftingFormula） | P0-4, P0-1 | 5h | 新建 `ProcessRecipeRegistry` + `ProductionTimer`（UniTask 驱动）+ `ProductionSavePatch`（存档持久化） |
| **P2-2** | **设备 UI 注册 (RegisterDeviceUI)** — modder 声明式定义设备面板布局（输入/输出槽 + 按钮 + 进度条） | P1-3, P0-2 | 4h | 实现 `DeviceUIRenderer` 内部类：解析 `DeviceUILayout` → 动态生成 Canvas + 绑定容器 + 渲染物品槽 |
| **P2-3** | **`CraftingViewFilter` 过滤式合成面板** — modder 打开只展示特定标签配方的合成面板 | — | 3h | 实现 `CraftingViewFilterPatch`（Harmony Postfix `CraftingView.OnEnable`）+ `RegisterCraftingView` |

### 5.4 既有问题修复（与缺口同步处理）

| # | 问题 | 修复方案 | 优先度 |
|---|------|---------|--------|
| **FIX-1** | `OnBuildingBuilt` 反射事件 + `FindObjectsOfType` 性能问题 | 重写为 `[HarmonyPatch(typeof(BuildingManager), "OnBuildingBuiltComplex")]` Postfix → EventBus | 🔴 与 P1-4 合并 |
| **FIX-2** | `PlaceBuilding` 反射调用 | 尝试用 Publicizer 公开 `BuyAndPlace`；若不可行，保留带标记的反射回退 | 🔴 独立修复 |
| **FIX-3** | `BuildingCollectionPatch.Sanitize` 与 `BuildingUtils.SanitizeBuildingInfo` 双路径不一致 | 统合为 `SanitizeBuildingInfo`（已有）+ 确保 `RegisterBuilding` 中调用 + Patch 层检查时也用同方法 | 🟡 独立修复 |
| **FIX-4** | Building 系统完全未测试 | 编写至少一个端到端测试场景：注册建筑→放置→打开UI→验证 | 🔴 独立任务 |

---

## 六、推荐实施路线

### Wave 1 — P0 基础设施（预计 14h）

```
P0-4 时间模拟框架 (3h)
   ↓
P0-1 DetailsView 注入 (4h)  ← 依赖 DUCKOV 逆向确认 ModUIManager.DetailsViewDic / DetailsView 的当前 API
   ↓
P0-3 OnViewOpen 回调 (2h)  ← 依赖 P0-1
   ↓
P0-5 进度条 UI 组件 (2h)   ← 依赖 P0-1
   ↓
P0-2 Building 子库存 (3h)   ← 依赖 P0-1
```

### Wave 2 — 修复既有问题 + P1（预计 11h）

```
FIX-1 + P1-4 OnBuildingBuilt Harmony 重写 (2h)
FIX-2 PlaceBuilding 反射修复 (2h)
FIX-3 Sanitize 统一 (1h)
P1-1 BuildingBehaviour 模式 (3h)
P1-2 Slot Tag 过滤 (1h)     ← 依赖 P0-2
P1-3 SimpleViewBuilder 游戏控件 (4h) ← 可与 FIX-4 并行
```

### Wave 3 — P2 增强 + 测试（预计 14h）

```
FIX-4 Building 系统测试 (3h)
P2-1 生产配方系统 (5h)      ← 依赖 P0-4
P2-2 设备 UI 注册 (4h)      ← 依赖 P1-3, P0-2
P2-3 CraftingViewFilter (3h)
```

**总计**: 约 39h

---

## 七、需要开发者确认的事项

在开始实施前，需要人工确认以下决策点：

### Q1: DUCKOV 游戏 API 逆向确认

ACB 依赖 `ModUIManager.DetailsViewDic`、`DetailsView`、`UIPrefabsReference` 等游戏原生 UI 类。当前 FML 的 `GameplayDataSettings.UIPrefabs` 是否完全等价？需要确认：

- [ ] `GameplayUIManager` / `ModUIManager` 的当前 API 签名（游戏版本更新后是否变化？）
- [ ] `DetailsView.ContentDisplay` 的 `Add()` / `AddToTop()` 是否仍然可用？
- [ ] `UIPrefabsReference.Instance` 是否仍然存在？（FML 使用的是 `GameplayDataSettings.UIPrefabs`，是同一对象吗？）

### Q2: BuildingBehaviour 模式的设计方向

ACB 使用 `BaseBuildingManager : BuildingInventory` 作为建筑行为基类（MonoBehaviour 挂到 prefab）。FML 有两个选择：

**选项 A（轻量）**: FML 提供抽象基类 `BuildingBehaviour : MonoBehaviour`，modder 继承它，FML 负责在建筑生成时挂载到实例。

**选项 B（声明式）**: FML 通过 `BuildingConfig.Behaviours` 字段（参考 `PerkConfig.Behaviours` 模式），modder 声明行为类型，FML 内部 `AddComponent`。

**建议**: 选项 B（与 PerkBehaviour 模式一致，声明式配置，对 modder 更友好）。

### Q3: Building 子库存是否需要独立持久化？

ACB 通过 `BuildingInventory.key` 隔离和引擎自动序列化实现子库存持久化。FML 的 `ContainerUtils` 目前是内存容器，不持久化。实施 P0-2 时需要决定：

- [ ] 是否复用 `ContainerUtils`（当前在内存，需增加存档桥接）？
- [ ] 还是新建 `BuildingInventory` 包装（利用游戏原生 Inventory 序列化）？

**建议**: 复用 `ContainerUtils` + 新增 `ContainerSaveBridge`（通过 EventBus 的 `CollectSaveDataEvent` 持久化）。

### Q4: 是否将 ACB 的完整功能（算力/种植/修理）作为 FML 内置功能？

**不建议**。这些是**Mod 内容**而非框架能力。FML 应提供能力层（时间框架 + UI 注入 + 子库存 + 进度条），由 modder 自行实现具体逻辑。本计划基于此原则设计。

---

## 八、风险提示

1. **ACB 是远古 Mod**，其依赖的 `MoreItemScript` 第三方库已在游戏后续版本中移除/变更。ACB 的 `BuildingInventory`、`ModUIManager.DetailsViewDic` 等 API 在新版游戏中可能不存在或以不同形式存在。**P0-1 实施前必须先确认游戏当前 UI API 签名**。

2. **CraftingViewFilter** 依赖对 `CraftingView.OnEnable` 的 Harmony Patch。如果游戏后续版本重构了 CraftingView，此方案可能失效。建议优先实施 P0（建筑 UI 注入），P2-3 作为后续增强。

3. **生产配方系统 (P2-1)** 需要存档持久化。FML 当前无统一的存档桥接层（仅 `QuestGiverRegistry` 有独立文件持久化）。实施 P2-1 需要先设计通用的存档序列化方案。

---

*文档版本: v1.0 | 状态: 待人工确认*
