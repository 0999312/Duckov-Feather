# API Reference — Building / 建筑 API

> **模块**：建筑注册、放置、成本、回调、建筑设备配方（MachineRecipe）、建筑 UI 配置、行为组件、时间工具
> **教程**：[USAGE.md 建筑系统](../USAGE.md#9-建筑系统--building)

---

## 目录

- [BuildingUtils — 建筑工具](#buildingutils)
- [BuildingConfig — 注册配置](#buildingconfig)
- [BuildingUIConfig — 建筑 UI 配置](#buildinguiconfig)
- [MachineRecipe — 建筑设备配方](#machinerecipe)
- [SimpleMachineRecipe — 声明式配方](#simplemachinerecipe)
- [BuildingBehaviour — 行为组件](#buildingbehaviour)
- [TimeUtils — 游戏时间](#timeutils)
- [BuildingRegistry](#buildingregistry)

---

## BuildingUtils

**命名空间**：`FeatherMod` | **源码**：`Buildings/BuildingUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| **注册** | | |
| `RegisterBuilding` | `static void RegisterBuilding(Identifier id, BuildingInfo info, Building prefab)` | 原始 API（手动 prefab） |
| | `static void RegisterBuilding(BuildingConfig config, Building? prefab = null)` | 推荐：自动建 prefab + 注册 |
| `UnregisterBuilding` | `static bool UnregisterBuilding(Identifier id)` | 卸载单个 |
| `UnregisterAllBuildings` | `static int UnregisterAllBuildings(string modid)` | 批量卸载 |
| **查询** | | |
| `GetBuildingInfo` | `static BuildingInfo? GetBuildingInfo(Identifier id)` | 查询 BuildingInfo |
| `GetAllBuildingIds` | `static IReadOnlyList<Identifier> GetAllBuildingIds()` | 全部建筑 Identifier |
| `GetBuildingPrefab` | `static Building? GetBuildingPrefab(Identifier buildingId)` | 查询 prefab |
| **放置** | | |
| `PlaceBuilding` | `static BuildingBuyAndPlaceResults PlaceBuilding(Identifier areaId, Identifier buildingId, Vector2Int coord, BuildingRotation rotation)` | 放置（自动扣费） |
| **成本** | | |
| `CreateCost` | `static Cost CreateCost(long money, params ItemEntry[] items)` | 构建成本 |
| `GetBuildingCost` | `static Cost? GetBuildingCost(Identifier buildingId)` | 成本明细 |
| `CanAffordBuilding` | `static bool CanAffordBuilding(Identifier buildingId)` | 是否负担得起 |
| `SpendBuildingCost` | `static bool SpendBuildingCost(Identifier buildingId)` | 手动扣费（通常不需要） |
| **回调** | | |
| `OnBuildingBuilt` | `static void OnBuildingBuilt(Identifier buildingId, Action<Building> callback)` | 建造完成回调 |
| `OffBuildingBuilt` | `static void OffBuildingBuilt(Identifier buildingId, Action<Building> callback)` | 取消回调 |
| `OnBuildingDemolished` | `static void OnBuildingDemolished(Identifier buildingId, Action<Building> callback)` | 拆除回调 |
| `OffBuildingDemolished` | `static void OffBuildingDemolished(Identifier buildingId, Action<Building> callback)` | 取消回调 |
| **Prefab / 模型** | | |
| `CreateSimpleBuilding` | `static Building CreateSimpleBuilding(Identifier id, Vector2Int dimensions, string? existingPrefabName = null)` | 手动建 prefab |
| `SetBuildingModel` | `static void SetBuildingModel(Identifier buildingId, GameObject modelPrefab, bool replaceExisting = true)` | 注入模型（须先注册） |
| `GetFunctionContainer` | `static GameObject? GetFunctionContainer(Building building)` | 交互层容器 |
| `GetGraphicsContainer` | `static GameObject? GetGraphicsContainer(Building building)` | 视觉层容器 |
| **建筑 UI / 设备** | | |
| `ConfigureBuildingUI` | `static void ConfigureBuildingUI(Identifier buildingId, BuildingUIConfig config, string modid)` | 配置 DetailsView 布局 |
| `RegisterMachineRecipe` | `static void RegisterMachineRecipe(Identifier buildingId, string machineKey, MachineRecipe recipe, string modid)` | 运行时挂载设备配方 |
| `UnregisterMachineRecipe` | `static bool UnregisterMachineRecipe(Identifier buildingId, string machineKey)` | 移除设备配方 |
| `IsMachineAvailableByKey` | `static bool IsMachineAvailableByKey(string machineKey)` | 机器是否可用 |
| **行为组件** | | |
| `AttachBehaviour<T>` | `static void AttachBehaviour<T>(Identifier buildingId, T? behaviour = null) where T : BuildingBehaviour` | 挂载自定义行为组件 |

---

## BuildingConfig

**命名空间**：`FeatherMod` | **源码**：`Buildings/BuildingConfig.cs`

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `Id` | `Identifier` | — | 必填（domain=modid, path=建筑名） |
| `Dimensions` | `Vector2Int` | `(2,2)` | 占地网格 |
| `PrefabName` | `string` | `""` | 注册标识 |
| `ExistingPrefabName` | `string?` | — | 克隆游戏已有建筑结构（如 `"Building_Workbench"`） |
| `MaxAmount` | `int` | `1` | 最多同时建造数 |
| `Icon` | `Sprite?` | — | 图标 |
| `Money` | `long` | — | 金币成本 |
| `CostItems` | `ItemEntry[]?` | — | 物品成本（支持 `ByTag` / `WithDurabilityCost`） |
| `UnlockedByDefault` | `bool` | `true` | 默认解锁 |
| `RequireBuildings` | `Identifier[]?` | — | 前置建筑 |
| `RequireQuests` | `Identifier[]?` | — | 前置任务 |

静态构造：`BuildingConfig.Create(Identifier id)` / `Create(string idString)`。

> `RegisterBuilding(BuildingConfig)` 自动完成：创建 Prefab（Cube + 碰撞体）→ 构建 BuildingInfo → Identifier 解析 TypeID → 写入游戏 + FML 双注册表。

---

## BuildingUIConfig

**命名空间**：`FeatherMod` | **源码**：`Buildings/BuildingUIConfig.cs`

| 类型 | 字段 | 说明 |
|------|------|------|
| `BuildingUIConfig` | `DisplayName`(string?) / `Machines`(MachineDef[]?) | 主面板配置 |
| `MachineDef` | `MachineKey`(string) / `DisplayName`(string) / `UnlockedByDefault`(bool=true) / `RequiredPerk`(Identifier?) / `SubInventories`(SubInventoryDef[]?) / `Recipe`(MachineRecipe?) / `ProgressBars`(ProgressBarDef[]?) / `Buttons`(BuildingButtonDef[]?) | 单个机器 |
| `SubInventoryDef` | `SubKey`(string) / `DisplayName`(string) / `SlotCount`(int=4) / `SlotTags`(string[]?) / `ReadOnly`(bool) | 子库存 |
| `ProgressBarDef` | `Label`(string) / `GetProgress`(Func<float>) | 进度条（0~1） |
| `BuildingButtonDef` | `Label`(string) / `OnClick`(Action<Inventory>?) | 按钮 |

> `RequiredPerk` 在 `UnlockedByDefault=false` 时生效（Perk 门控）。

---

## MachineRecipe

**命名空间**：`FeatherMod` | **源码**：`Buildings/MachineRecipe.cs`

抽象基类——建筑自动执行的"配方"（区别于玩家手动合成）。

| 成员 | 签名 | 说明 |
|------|------|------|
| 属性 | `Identifier Id { get; set; }` | 合成表标识 |
| 属性 | `protected internal Inventory? MainInventory` | 主库存（运行时注入） |
| 属性 | `protected internal IReadOnlyDictionary<string, Inventory>? SubInventories` | 子库存字典（运行时注入） |
| 抽象 | `abstract bool CanExecute()` | 槽位满足条件？ |
| 抽象 | `abstract void Execute()` | 执行配方 |
| 虚方法 | `virtual float GetProgress()` | 进度 0~1（UI 进度条） |
| 虚方法 | `virtual bool IsRunning` | 是否生产中 |
| 保护方法 | `void SetState<T>(string key, T value)` | 存状态（**自动参与存档序列化**） |
| 保护方法 | `T GetState<T>(string key, T defaultValue = default!)` | 取状态 |

> 关键：`SetState<T>` / `GetState<T>` 的值自动存档，modder 无需写序列化代码。

---

## SimpleMachineRecipe

**命名空间**：`FeatherMod` | **源码**：`Buildings/SimpleMachineRecipe.cs`

覆盖 80% 场景的声明式实现：

| 类型 | 字段 | 说明 |
|------|------|------|
| `SimpleMachineRecipe` | `Inputs`(MachineInput[]) / `Outputs`(MachineOutput[]) / `Byproducts`(MachineOutput[]?) / `DurationSeconds`(float?) / `DurabilityCosts`(DurabilityCost[]?) | 配方配置 |
| `MachineInput` | `FromSubKey`(string) / `ItemId`(Identifier) / `Amount`(int=1) / `Consume`(bool=true) | 输入（Consume=false = 仅检测） |
| `MachineOutput` | `ToSubKey`(string?) / `ItemId`(Identifier) / `Amount`(int=1) / `Chance`(float=1.0f) | 产物（ToSubKey=null → 主库存） |
| `DurabilityCost` | `SubKey`(string) / `DurabilityPerCycle`(float=0.01f) | 耐久消耗 |

---

## BuildingBehaviour

**命名空间**：`FeatherMod` | **源码**：`Buildings/BuildingBehaviour.cs`

| 成员 | 签名 | 说明 |
|------|------|------|
| 属性 | `protected Building? Building { get; }` | 绑定建筑 |
| 属性 | `protected Inventory? MainInventory { get; }` | 主库存 |
| 虚方法 | `virtual void OnBuildingPlaced()` | 放置回调 |
| 虚方法 | `virtual void OnBuildingDemolished()` | 拆除回调 |

挂载：`BuildingUtils.AttachBehaviour<MyBuildingLogic>(new Identifier("mymod", "forge"))`。

---

## TimeUtils

**命名空间**：`FeatherMod.Utils` | **源码**：`Utils/TimeUtils.cs`

| 成员 | 签名 | 说明 |
|------|------|------|
| 属性 | `static TimeSpan Now` | 当前游戏内时间 |
| `NowAsString` | `static string NowAsString()` | 序列化为字符串（存档用） |
| `TimeSpanToString` | `static string TimeSpanToString(TimeSpan time)` | TimeSpan → 字符串 |
| `TryStringToTimeSpan` | `static bool TryStringToTimeSpan(string timeStr, out TimeSpan result)` | 反序列化 |
| `GetPositiveHoursSince` | `static float GetPositiveHoursSince(TimeSpan pastTime)` | 距过去时间的正小时差 |
| `GetPositiveSecondsSince` | `static float GetPositiveSecondsSince(TimeSpan pastTime)` | 正秒差 |
| `GetPositiveHoursBetween` | `static float GetPositiveHoursBetween(TimeSpan start, TimeSpan end)` | 两时刻正小时差 |

---

## BuildingRegistry

| 成员 | 签名 | 说明 |
|------|------|------|
| `Register` | `void Register(Identifier id, BuildingInfo info, Building prefab, string modid)` | |
| `TryGetPrefab` | `bool TryGetPrefab(string buildingId, out Building prefab)` | |
| `GetAllInfos` | `IEnumerable<BuildingInfo> GetAllInfos()` | |

---

## 废弃 API / Obsolete

| 成员 | 替代 |
|------|------|
| `GetBuildingInfo(string buildingId)` | `GetBuildingInfo(Identifier)` |
| `GetAllBuildingIdStrings()` | `GetAllBuildingIds()` |
| `PlaceBuilding(string areaID, string buildingID, ...)` | `PlaceBuilding(Identifier, Identifier, ...)` |
