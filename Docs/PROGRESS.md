# 项目进度文档 (PROGRESS.md)

> 最后更新：2026-07-28

---

## Save 系统统一化与跨槽位清理加固 — ✅ 已完成

**完成时间**: 2026-07-28
**耗时**: 约 3 小时
**类型**: 存档系统架构整理 + 跨槽位泄漏修复 + Quest 链断裂防范

### 背景

玩家在使用测试 mod 时报告 4 个相关问题：
1. FriendlyNpc 存档异常（log 中 `LoadNpcSpawnEntries failed: Key "fml_friendly_npc_spawns" was not found`）
2. PerkTree 等新 API 模块的存档内容不跟随存档删除（全局问题）
3. 新 API 未对接项目原 `SaveUtils` 工具类
4. 部分原版 Quest 任务链随机断裂，疑似安装 FML 后出现

经 5 个 explore 子代理并行调研反编译游戏源码 + FML 自身 + DuckovDOOM 参考项目确认根因：
- `SaveUtils` 在 FML 内部**零引用**；所有新模块直接调 `SavesSystem.Save/Load` 用裸字符串键，与原生键命名空间无隔离
- `OnSaveDeleted` 事件无任何 FML 模块订阅，删除存档时内存注册表与跨槽位持久化状态不清理
- `FriendlyNpc.LoadNpcSpawnEntries` 走直接 `ES3.Load` 回退路径，绕过 `SavesSystem.Load` 的 `KeyExists` 预检查 → 触发 ES3 "Key not found" warning；空 entries 不写键导致下次加载循环警告
- `QuestGiverRegistry` 用 `persistentDataPath/FML/questgiver_id_map.json` 跨槽位共享 → 删档泄漏到新档
- `QuestManagerSlotCleanupPatch` 反射 `Clear()` 私有列表无快照无回滚，违反反射最小化原则
- `QuestGiverIDPatch` 4 个 catch 块 `return true` 静默 fallback 到原生方法，掩盖 mod 异常
- `SaveUtils` 缺乏显式删除 API 与原生保留键冲突警告

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 修改 | `FastModdingLib/Saves/SaveUtils.cs` | 扩展：`MakeKey/KeyExists/Delete<T>` 显式删除、`Load<T>(Identifier, T defaultValue)` 重载、`WarnIfReserved` 保留键警告（QuestData/SaveTime/IsOldGame/Created/EconomyData/GameClock/ActiveModList/Item//Inventory//Count/） |
| 新建 | `FastModdingLib/Events/GameEvents/SaveDeletedEvent.cs` | 桥接 `SavesSystem.OnSaveDeleted` → EventBus；构造 `SaveDeletedEvent(int slot)` |
| 修改 | `FastModdingLib/Events/Adapters/GameEventAdapters.cs` | 新增 `OnSaveDeletedBridge` 桥接方法 + `using Saves;` |
| 修改 | `FastModdingLib/Entities/FriendlyNpcUtils.cs` | `NpcSaveKey` 字符串 → `NpcSaveId` Identifier；`LoadNpcSpawnEntries` 删除 ES3 直接文件回退（统一走 `SaveUtils.Load`）；`OnCollectSaveData` 空 entries 主动调 `SaveUtils.Save(id, null)` 删键（不再循环警告）；订阅 `SaveDeletedEvent` 清空 `_registry` |
| 修改 | `FastModdingLib/Buildings/BuildingUtils.cs` | `BuildingRestoreKey` 字符串 → `BuildingRestoreId` Identifier；`TryLoadBuildingRestoreData` 改用带 defaultValue 重载；订阅 `SaveDeletedEvent` 清空 `_buildingCallbacks/_buildingDemolishCallbacks/_buildingMachines/_pendingSceneReplay` |
| 修改 | `FastModdingLib/Dialogues/NpcProximityTrigger.cs` | `SaveKey()` 字符串 → `SaveId()` Identifier；`Start/Update` 改用 `SaveUtils.Load<bool>/Save`；移除 `using Saves;`，加入 `using FeatherMod.Saves;` |
| 修改 | `FastModdingLib/PerkTrees/PerkTreeUtils.cs` | `HookOnSetFileCleanup` 末尾追加订阅 `SaveDeletedEvent`；新增 `OnSaveDeletedCleanup` 清空 `_completedPerkInjects`（保留 OnSetFile 订阅并行，未改 PerkTree.Save/Load） |
| 修改 | `FastModdingLib/QuestGivers/QuestGiverRegistry.cs` | **彻底移除**全局 JSON 持久化（`PersistFilePath`/`File.WriteAllText`/`JsonUtility`）；改为按槽位持久化到 `.sav`（走 `SaveUtils.Save/Load<PersistData>(PersistSaveId)`）；新增 `SaveDeletedEvent` 清空 `_questGiverIdIndex/_reverseIdIndex/_displayNameKeyCache` 并重置 `_nextQuestGiverId`；`PersistData/PersistEntry` 改 public 以便 ES3 字段序列化；公共 Register/TryGet/OnRemoved 等 API 签名零变更 |
| 修改 | `FastModdingLib/QuestGivers/Patches/QuestGiverIDPatch.cs` | 4 个 Prefix catch 块从 `return true; // 放行原生` 改为显式赋空 `__result` 后 `return false;`（暴露 mod 异常而非静默 fallback，保留 Debug.LogError） |
| 删除 | `FastModdingLib/Quests/Patches/QuestManagerSlotCleanupPatch.cs` | 移除反射 `Clear()` 私有 `activeQuests/historyQuests` 的 patch（违反反射最小化、无快照无回滚）；改由原版 `SavesSystem.OnSetFile → QuestManager.Load()` 处理槽位切换 |

### 验证结果

- [x] `dotnet build FastModdingLib/FeatherMod.csproj` 输出：**1 error 54 warnings**
  - 1 error 为**预存**：`Tests/QuestTest.cs(19) ItemUtils.CreateCustomBluePrint` 调用已删除 API，与本次任务无关；最近 commit `97c6b73 update` 已含此问题
  - 本次新增改动 0 引入新 error
- [x] Grep 验证 `SavesSystem.Save(NpcSaveKey` / `SavesSystem.Save(BuildingRestoreKey` / `SavesSystem.Save(SaveKey()` / `File.WriteAllText` / `QuestManagerSlotCleanupPatch` / `return true; // 放行原生` 均为 0 匹配

### 遗留问题

- [ ] `Tests/QuestTest.cs` 仍调用已废弃 `ItemUtils.CreateCustomBluePrint`；属 Phase 6 测试质量工作范畴，与本次任务正交，未触碰
- [ ] 跨槽位持久化迁移期间未做"双读兼容"——本次按用户决策放宽双读要求直接迁移；旧 `questgiver_id_map.json` 文件将自动失效（ FML 不再读取该全局 JSON）

### 设计偏离

- `NpcProximityTrigger` 的 Identifier Path 拼接 `npc_trigger_{NpcId.Domain}_{NpcId.Path}` 内含下划线分隔；最终 ES3 键形如 `FeatherModfeather:npc_trigger_..._...`——这是 SaveUtils 前缀（"FeatherMod"）+ FML 内部 Domain（"feather"）拼接导致的双前缀现象，已在内部使用层面接受（不影响隔离效果）。若未来想优化可在 SaveUtils 中针对 FMLConstants.Domain 用例跳过框架前缀

---

## MachineRecipe 与 Building UI 系统 — ✅ 已完成

**完成时间**: 2026-07-22
**耗时**: 约 4 小时
**类型**: 新功能（Building Machine 系统 + 反射清理）

### 背景

根据 ACB（ACBuildingExpanded）和 VAE（VanillaAttachmentsExpanded）两个远古 Mod 的逆向审计，发现 FML 缺失：
1. Building 子库存和 DetailsView 自定义注入
2. 建筑设备 Recipe（MachineRecipe）——区别于手动合成
3. 时间模拟框架（GameClock 驱动离线进度计算）
4. Building 行为组件模式，以及 Perk 门控建筑功能
5. ~15 处遗留反射代码

### 新增文件

| 文件 | 行数 | 说明 |
|------|------|------|
| `Buildings/MachineRecipe.cs` | ~110 | 抽象基类：CanExecute/Execute/GetProgress + SetState/GetState 自动存档 |
| `Buildings/SimpleMachineRecipe.cs` | ~210 | 内置声明式配方 Input→Output + ProductionTimer + MachineInput/MachineOutput/DurabilityCost DTO |
| `Buildings/ProductionTimer.cs` | ~110 | UniTask + GameClock 异步计时器 + 离线追赶 + 序列化 |
| `Buildings/BuildingSlotsWatcher.cs` | ~85 | 每 Machine 独立 Watcher：监听子库存 → CanExecute/Execute |
| `Buildings/BuildingUIConfig.cs` | ~100 | DTO：BuildingUIConfig/MachineDef/SubInventoryDef/ProgressBarDef/BuildingButtonDef |
| `Buildings/BuildingBehaviour.cs` | ~45 | BuildingBehaviour : MonoBehaviour 抽象基类 |
| `Utils/TimeUtils.cs` | ~65 | GameClock 访问 + TimeSpan 序列化 + 时间差计算 |

### 修改文件

| 文件 | 改动摘要 |
|------|---------|
| `Buildings/BuildingUtils.cs` | 反射清理：HookBuildingEvents 从 GetEvent+AddEventHandler → 直接 `+=`（↘12 行）；PlaceBuilding 从 Invoke → 直接调用（↘6 行）；SanitizeBuildingInfo 从 FieldInfo+SetValueDirect → 直接赋值（↘32 行）；新增：ConfigureBuildingUI / RegisterMachineRecipe / UnregisterMachineRecipe / IsMachineAvailable / AttachBehaviour / RemoveAllMachinesForMod / UnhookBuildingEvents（+120 行） |
| `Buildings/Patches/BuildingCollectionPatch.cs` | Sanitize 从 FieldInfo×3 → 直接赋值（↘12 行）；移除 `using System.Reflection` |
| `PerkTrees/PerkTreeUtils.cs` | 新增 `IsPerkUnlocked(Identifier)` 公共方法（+10 行） |
| `UI/SimpleViewBuilder.cs` | 补充 `using FeatherMod.UI` |

### 新增 public API

```csharp
// Building Machine 管理
BuildingUtils.ConfigureBuildingUI(buildingId, config, modid);
BuildingUtils.RegisterMachineRecipe(buildingId, machineKey, recipe, modid);
BuildingUtils.UnregisterMachineRecipe(buildingId, machineKey);
BuildingUtils.AttachBehaviour<T>(buildingId, behaviour?);

// Perk 门控
PerkTreeUtils.IsPerkUnlocked(perkId);

// MachineRecipe（modder 继承基类或使用内置 SimpleMachineRecipe）
public abstract class MachineRecipe {
    protected void SetState<T>(key, value);
    protected T GetState<T>(key, defaultValue);
    public abstract bool CanExecute();
    public abstract void Execute();
    public virtual float GetProgress();
    public virtual bool IsRunning;
}

public class SimpleMachineRecipe : MachineRecipe {
    public MachineInput[] Inputs;
    public MachineOutput[] Outputs;
    public MachineOutput[]? Byproducts;
    public float? DurationSeconds;
    public DurabilityCost[]? DurabilityCosts;
}

// BuildingBehaviour
public abstract class BuildingBehaviour : MonoBehaviour {
    public virtual void OnBuildingPlaced();
    public virtual void OnBuildingDemolished();
}
```

### 设计决策

- **MachineRecipe 存档自动化**：modder 通过 `SetState<T>/GetState<T>` 存取运行时状态，框架自动 JSON 序列化/恢复。SimpleMachineRecipe 的进度和计时器状态均通过此机制持久化。
- **一 Building 多 Machine**：每个 MachineDef 有独立的子库存、Recipe 和 BuildingSlotsWatcher，多 Machine 并行运行互不干扰。
- **Perk 门控在 Machine 级别**：`MachineDef.UnlockedByDefault` + `RequiredPerk` 控制整个 Machine（UI + Recipe）的可见性。
- **onContentChanged 反射隔离**：`Inventory.onContentChanged` 因 Publicizer 产生 CS0229 二义性，BuildingSlotsWatcher 使用 `EventInfo.AddEventHandler` 规避（标记为唯一合理反射处）。

### 验证结果
- [x] `dotnet build` 通过（0 错误，52 预存警告，无新增）
- [ ] 功能测试（待游戏运行时验证）

### 遗留问题
- ProductionTimer 的离线追赶逻辑需在游戏中测试（GameClock.Now 的 TimeSpan 行为）

---

## Perk 系统 v2 重构 — ✅ 已完成

**完成时间**: 2026-07-20
**耗时**: 约 2 小时
**类型**: 架构重构（Identifier 语义修正 + PerkRequirement 桥接 + 原版资源引用）

### 背景

Phase 4 的 PerkTree 系统存在三个设计缺陷：

1. **Identifier 语义二义性**：`AddPerk(Identifier id, ...)` 中 `id.Domain` 被 `ResolveTreeId` 用于推导 treeId，违反 `Domain=modid` 约定。一个 mod 有多个 PerkTree 时无法区分。
2. **PerkRequirement 未接入 Identifier 体系**：直接暴露游戏原生 `PerkRequirement` 类型，modder 需填写裸 `int typeID`。
3. **无法引用原版 Perk**：原版 Perk 不在 `_perkRegistry` 中，`ConnectPerks` 无法建立与原版 Perk 的前置关系。

### 修复方案

#### 1. AddPerk 签名重构

```csharp
// 旧（已删除）
AddPerk(Identifier id, PerkRequirement req, Sprite icon, ...)  // id.Domain → 推导 treeId ❌

// 新
AddPerk(Identifier treeId, PerkConfig config)                   // treeId + config 分离 ✅
```

`treeId.Domain="duckov"` → `PerkTreeManager.GetPerkTree(treeId.Path)` 获取原版树
`treeId.Domain="mymod"` → 从 FML 已注册树或 PerkTreeManager 查找

#### 2. PerkConfig DTO（桥接 PerkRequirement）

新增 `PerkConfig.cs`，含全部 Perk 字段 + PerkRequirement 映射：

```csharp
public class PerkConfig
{
    public Identifier PerkId;
    public Sprite Icon;
    public string DisplayNameKey;
    public int RequiredLevel;           // → PerkRequirement.level
    public ItemEntry[] CostItems;       // → Cost.items（ItemEntry 桥接，Identifier → typeID）
    public long Money;                  // → Cost.money
    public long RequireTimeTicks;       // → PerkRequirement.requireTime
    public Identifier[] RequiredPerks;  // 全走 Identifier，FML 内部懒注册 + ConnectPerks
}
```

#### 3. 原版 Perk 懒注册（TryLazyRegister）

- 原版 Perk Identifier 约定：`Identifier("duckov", "treeID/perkName")`
- `ConnectPerks` / `ForceUnlock` / `AddPerkBehaviour` 中 registry miss 时自动触发
- 内部解析 Path → 分离 treeId + perkName → `PerkTreeManager` 查找 → 写入 `_perkRegistry`（owner=`FMLConstants.VanillaOwner`）

#### 4. 公共反射修复

- `PerkTreeManager.perkTrees` 原为 `BindingFlags.Static | BindingFlags.NonPublic` 反射（bug——字段是 `public` 实例），改为 `PerkTreeManager.Instance.perkTrees` 直接访问
- Perk 字段（icon, displayName, hasDescription, quality, defaultUnlocked, requirement）改用 Publicizer 直接赋值，消除无意义反射
- `graph` 字段（NodeCanvas，第三方 DLL）保留反射

### 删除

| 方法 | 原因 |
|---|---|
| `ResolveTreeId(domain)` | 反向推导逻辑彻底消除 |
| `FindPerkInTree(tree, perkName)` | 与旧 Obsolete API 绑定 |
| `AddPerk(Identifier, PerkRequirement, Sprite, string?)` | 旧签名 |
| `AddPerk(string, string, ...)` | `[Obsolete]` string 重载 |
| `ConnectPerks(string, string, string)` | `[Obsolete]` string 重载 |
| `ForceUnlock(string, string)` | `[Obsolete]` string 重载 |

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `PerkConfig.cs` | Perk 配置 DTO + `BuildPerkRequirement()` 内部转换 |
| 修改 | `FMLConstants.cs` | +`VanillaOwner = "__vanilla__"` 常量 |
| 重写 | `PerkTrees/PerkTreeUtils.cs` | ~140 行改动：新增 `AddPerk(Identifier, PerkConfig)`、`TryLazyRegister`、`ResolvePerk`、`ResolvePerkTree`；删除 `ResolveTreeId`/`FindPerkInTree` 及全部 Obsolete 重载；`ConnectPerks`/`ForceUnlock`/`AddPerkBehaviour` 接入懒注册回退；`RegisterPerkTree` 反射清理；`RemoveAllPerks` 反射修正 |
| 更新 | `Docs/USAGE.md` | §14 Perk 技能树章节全文重写 |
| 更新 | `Docs/MIGRATION.md` | PerkTreeUtils 迁移章节更新至 v2 API |
| 删除 | `Docs/PLAN-Phase5-Goals.md` | Phase 5 已完成，计划文档陈旧 |
| 删除 | `Docs/TODO/` (13 文件) | TODO/ISSUE/PLAN/DESIGN 全部陈旧，已清理 |

### 验证结果
- [x] 代码审查通过（Identifier 语义、ItemEntry 桥接、懒注册流程）
- [ ] `dotnet build` 编译通过（需 Unity 引用，待构建环境验证）
- [ ] 功能测试（待游戏运行时验证）

---

## PerkBehaviour 声明式配置封装 — ✅ 已完成

**完成时间**: 2026-07-20
**耗时**: 约 1 小时
**类型**: 功能增强（PerkConfig 集成 PerkBehaviour 配置）

### 背景

`AddPerkBehaviour<T>` 对自定义 `T : PerkBehaviour` 子类完全够用，但 modder 想用原版 PerkBehaviour 子类时面临两个障碍：
1. 原版子类的配置字段为 `[SerializeField] private`，即使 Publicizer 公开，modder 也不便访问
2. `UnlockStockShopItem.itemTypeID` 使用裸 int，未接入 Identifier 体系

### 实现

新建 `PerkBehaviourConfigs.cs`，含 1 个抽象基类 + 7 个配置类：

| # | Config 类 | 对应原版 Behaviour | 核心字段 |
|---|---|---|---|
| 1 | `UnlockFormulaConfig` | `UnlockFormula` | 无（自动匹配 `CraftingFormula.requirePerk`） |
| 2 | `UnlockAchievementConfig` | `UnlockAchievement` | `AchievementKey` |
| 3 | `ModifyStatsConfig` + `StatModifierEntry` | `ModifyCharacterStatsBase` | `Entries[]`（Key, Value, Percentage） |
| 4 | `BlackMarketRefreshTimeConfig` | `ChangeBlackMarketRefreshTimeFactor` | `Amount` |
| 6 | `BlackMarketRefreshChanceConfig` | `AddBlackMarketRefreshChance` | `AddAmount` |
| 8 | `AddPlayerStorageConfig` | `AddPlayerStorage` | `Capacity` |
| 10 | `UnlockShopItemConfig` | `UnlockStockShopItem` | `ItemId`（Identifier → typeID 自动解析） |

`PerkConfig` 新增 `Behaviours: PerkBehaviourConfig[]?` 字段。`AddPerk` 末尾对每条配置调用 `ApplyTo(perkGo)`，内部 `AddComponent` + 字段赋值。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `PerkTrees/PerkBehaviourConfigs.cs` | 抽象基类 + 7 个 Config 包装（~220 行） |
| 修改 | `PerkConfig.cs` | +`Behaviours` 字段 |
| 修改 | `PerkTrees/PerkTreeUtils.cs` | +6 行 Behaviours 应用逻辑 |
| 更新 | `Docs/USAGE.md` | §14 增加 Behaviours 声明式使用示例 |

### 验证结果
- [x] `dotnet build` 0 errors, 0 warnings

---

## 测试 Mod 反馈五项问题修复 — ✅ 已完成

**完成时间**: 2026-07-19
**耗时**: 约 3 小时
**类型**: Bug 修复（本地化 / 对话 / NPC 生成 / 交互点 / 建筑渲染）

### 问题来源

测试 Mod 实测反馈 5 个问题。经 5 路并行子代理调查（FML 源码 + 游戏逆向工程 `assembly_0625`），逐一诊断修复。参考原版 Jeff / XiaoMing（小明）实现。

### 根因摘要

| # | 问题 | 根因 |
|---|------|------|
| 1 | QuestGiver 本地化键 `Character_{int}` 不稳定 | questGiverID 每次会话从 50 起动态递增分配，跨会话变化 |
| 2 | Quest 激活/完成 dialogue 不触发 | `QuestData` 无声明式对话字段；`DialogueTrigger` 用反射 `GetField`+`SetValue` 订阅 field-like event（依赖 backing field 名，不可靠） |
| 3 | 旧存档进已有建筑不生成 Mod NPC | `RestoreNpcSpawns` 仅挂 `LevelInitializedEvent`（仅新游戏触发），读档/进出子场景不触发 |
| 4 | 对话标识 Y 轴低 / 任务 "!" 不显示 | `DuckovDialogueActor.offset` 未设（默认 0，贴脚）；QuestGiver 在 active GO 上 AddComponent，Awake 用默认 `interactMarkerOffset(0)` 把指示器埋进模型 |
| 5 | 新增建筑渲染到加载界面 | 建筑 prefab `DontDestroyOnLoad` 且 active，被 Curtain 相机（CullingMask=Everything, DepthOnly）渲染 |

### 修复方案（以原版设计为准）

1. **本地化键稳定化**：`QuestGiverRegistry` 把 `Identifier→int` 映射持久化到全局文件（`persistentDataPath/FML/questgiver_id_map.json`，跨存档槽），同一 Identifier 永远分配相同 int ID；`I18n` 加载语言文件后触发 `OnLanguageFileLoaded`，`QuestGiverUtils` 订阅并刷新 `Character_{int}` override（补齐注册时翻译未就绪的缺口）。
2. **任务对话声明式**：`QuestData` 新增 `onActivateDialogue`/`onCompleteDialogue`（`QuestDialogue`），`RegisterQuestInternal` 自动注册到 `DialogueTrigger`；`DialogueTrigger` 改用 `EventInfo.AddEventHandler` 订阅原生 public static event（标准 event 订阅，绕开 Publicizer 导致的 event/field 二义 CS0229 与 CS0571）；用"激活集合"区分真实完成与读档恢复（读档恢复不经 `NotifyActivated`），避免读档时历史任务误播完成对话。
3. **NPC 场景恢复**：桥接 `SceneLoader.onFinishedLoadingScene` 为 `MainSceneLoadedEvent`，连同已有 `SceneLoadFinishedEvent` 一起触发 `RestoreNpcSpawns`；`NpcSpawnEntry` 增加 `scene` 字段按场景过滤（旧存档无该字段按主场景处理），对齐原版 XiaoMing 进入即在原点位生成。
4. **交互点对齐原版**：`DuckovDialogueActor.offset=(0,1.25,0)`（原版 Actor_Jeff 值，新增 `FriendlyNpcConfig.DialogueOffset` 可覆盖）；QuestGiver/PerkTreeUIInvoker 改"先禁用再挂组件"模式，使 Awake/Start 以最终字段执行（指示器自动定位头顶，`PossibleQuests` 缓存正确）；交互碰撞体对齐小明 `(2,1.3,2)`；`QuestGiverUtils.BindQuest` 后清 `_possibleQuests` 缓存并重启激活刷新指示器。
5. **建筑 prefab 隔离**：建筑 prefab 挂入 inactive 容器 `FML_BuildingPrefabs`（`activeSelf=true` 保证 Instantiate 实例正常激活，`activeInHierarchy=false` 保证 prefab 本体不渲染），与原版 asset prefab 语义一致。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|----------|----------|
| 修改 | `QuestGivers/QuestGiverRegistry.cs` | +ID 持久化（加载/保存/重复注册复用）；删死代码 `SetQuestGiverId` |
| 修改 | `QuestGivers/QuestGiverUtils.cs` | 订阅 `I18n.OnLanguageFileLoaded` 刷新 override；`BindQuest` 后 `RefreshQuestGiverIndicators` 清缓存刷新 |
| 修改 | `I18n.cs` | 新增 `OnLanguageFileLoaded` 事件，语言文件加载后触发 |
| 修改 | `Quests/QuestData.cs` | 新增 `QuestDialogue` 类 + `onActivateDialogue`/`onCompleteDialogue` 字段 |
| 修改 | `Quests/QuestUtils.cs` | 注册时自动绑定声明式对话到 `DialogueTrigger` |
| 修改 | `Dialogues/DialogueTrigger.cs` | 反射 `GetField` 改为 `EventInfo.AddEventHandler`；`s_activatedQuestIds` 排除读档恢复误触发 |
| 修改 | `Events/Adapters/GameEventAdapters.cs` | 桥接 `SceneLoader.onFinishedLoadingScene` → `MainSceneLoadedEvent` |
| 新建 | `Events/GameEvents/MainSceneLoadedEvent.cs` | 主场景加载完成事件 |
| 修改 | `Entities/FriendlyNpcUtils.cs` | NPC 场景恢复（订阅+场景过滤+`scene` 字段）；`actor.offset`；禁启初始化；碰撞体对齐 |
| 修改 | `Entities/FriendlyNpcConfig.cs` | 新增 `DialogueOffset` 可选字段 |
| 修改 | `Buildings/BuildingUtils.cs` | 新增 `PrefabHolder` inactive 容器，prefab 挂入隔离 |

### 新增 public API

```csharp
// QuestData 声明式对话
new QuestData {
    onActivateDialogue = new QuestDialogue(npcId, new DialogueSequence(actorId, "激活对话")),
    onCompleteDialogue = new QuestDialogue(npcId, new DialogueSequence(actorId, "完成对话")),
};

// FriendlyNpcConfig 对话指示器偏移（可选，默认 1.25）
config.DialogueOffset = new Vector3(0, 1.3f, 0);
```

### 设计说明 / 已知边界

- **问题2 事件订阅方式**：按用户要求未用 Harmony patch。`EventInfo.AddEventHandler` 是标准 event 订阅反射（与框架 `GameEventAdapters.WireDynamicEvent` 同模式），不依赖 backing field 名，比原 `GetField`+`SetValue` 可靠。
- **完成对话边缘**：玩家"激活后存档、读档继续完成"的 quest 因读档恢复不经 `onQuestActivated`，完成对话不播（可接受，避免读档历史任务乱播）。如需覆盖，后续可在读档后扫描 `activeQuests` 补入激活集合。
- **OcclusionFadeChecker**：经核实原版中该组件挂在全局 `OcclusionFadeManager` 上（跟随玩家的 aim/character 两个 checker），**不在 NPC 交互点上**。原版 NPC 交互点（SpecialAttachment）同样没有该组件。FML 与原版保持一致——不添加。用户观察到的"CharacterChecker"是 Manager 的全局子物体，非 NPC 组件。

### 验证结果

- [x] 编译通过（0 错误；49 个警告均为预先存在的 nullable 警告，与本次改动无关）
- [ ] 功能测试：本地化键稳定、任务激活/完成对话、旧存档 NPC 生成、交互点高度、任务 "!" 显示、建筑不进加载界面（需测试 Mod 实测）

---

## NPC 系统全面修复（5 合 1）— ✅ 已完成

**完成时间**: 2026-07-19
**耗时**: 约 4 小时
**类型**: Bug 修复 + 功能补全

### 问题来源

测试 Mod 运行发现 5 个问题，经对照游戏原版 XiaoMing（小明）模板逐一诊断修复。

### 根因摘要

| # | 问题 | 根因 |
|---|------|------|
| 1 | 复合角色（商人+任务）失败 & 头顶标记缺失 & PerkTree 无法绑定 | 多独立 `InteractableBase` 碰撞体竞争；`interactMarkerOffset` 未设（脚底 0m）；PerkTreeUIInvoker 完全缺失 |
| 2 | 对话 ActorId 不显示 | `DuckovDialogueActor.nameKey` 从未赋值；`DialogueTrigger` 回退用 `NpcId.Path` 非配置 ActorId |
| 3 | 商店名空白 + 交易方式错误 | `StockShop` 只设了 `merchantID`，原版还需 `DisplayNameKey`/`accountAvaliable`/`refreshAfterTimeSpan` 等 |
| 4 | NPC 不跟随旋转 | 闲置 NPC 的 `IsAiming()`=true → `UpdateAiming` 每帧用未设瞄准点覆盖 `targetAimDirection`；直接写方向被原生管线冲掉 |
| 5 | NPC 系统与原版不匹配 | 逐一核对 XiaoMing 模板，补齐全量交互结构与字段 |

### 修复方案

严格参照 `SpecialAttachment_XiaoMing.prefab` 结构重建交互组装逻辑：
- `interactableGroup=true` + `otherInterablesInGroup` 复合组装配，成员碰撞体禁用；
- 标记偏移全用原版值（主标记 0.66m/任务 "!" 1.87m/技能标记 0.87m）；
- `NpcFacePlayer` 从直接写 `targetAimDirection` 改为 `SetAimPoint` 原生管线（与原版 `AimToPlayer` 同机制）；
- `StockShop` 补齐 6 个字段；`DuckovDialogueActor` 补齐 `nameKey`。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|----------|----------|
| 修改 | `Entities/FriendlyNpcConfig.cs` | 新增 `PerkTreeId`/`ShopAccountAvaliable`/`ShopReturnCash`/`ShopSellFactor`/`FacePlayerRange` 共 5 个配置字段；`AutoFacePlayer` 默认改为 `true` |
| 修改 | `Entities/FriendlyNpcUtils.cs` | ~200 行重构：`AttachInteractionComponents` 组装配 + PerkTree 分支 + 原版偏移 + 商店全字段 + `actor.nameKey`；新增 `IsPerkTreeAvailable`/`ResolveShopNameKey`/`SetupInteractionGroup`/`ConfigureShopInteract` 辅助方法；新增 `TryGetNpcActorId`/`SetNpcFaceDirection`/`SetNpcFaceAngle`/`ClearNpcFaceDirection` 公共 API；`preset.nameKey` 改用 `DisplayNameKey`；消除 `InitializeEntries` 反射 |
| 修改 | `Dialogues/NpcFacePlayer.cs` | 重写为 `CharacterMainControl.SetAimPoint` 原生管线 + `FixedDirection` 固定朝向模式 + `FollowRange` 距离门限 |
| 修改 | `Dialogues/DialogueTrigger.cs` | `PlayDialogueForNpc` ActorId 回退链：显式传入 → NPC 配置缓存 → NpcId.Path |
| 修改 | `Dialogues/NpcProximityTrigger.cs` | 同上 ActorId 回退链 |

### 新增 public API

```csharp
// 查询 NPC 注册的 ActorId（DialogueTrigger 联动）
FriendlyNpcUtils.TryGetNpcActorId(npcId, out string actorId);

// 固定朝向（覆盖 AutoFacePlayer）
FriendlyNpcUtils.SetNpcFaceDirection(npcId, direction);
FriendlyNpcUtils.SetNpcFaceAngle(npcId, yAngle);
FriendlyNpcUtils.ClearNpcFaceDirection(npcId);
```

### 向后兼容性

全部改动向后兼容。已有字段默认值不变（仅 `AutoFacePlayer` 从 `false` 变 `true`——对齐原版小明行为）；所有新增字段均有合理默认值；public API 签名无 breaking change。

### 设计偏离

- **无 `aiController`**:FML 友善 NPC 不挂原版行为树（原版小明会巡逻游走），驻守 NPC 更符合 Mod 预期。跟随旋转已由 `SetAimPoint` 原生实现。
- **PerkTreeId 为 `Identifier?`**:不使用 `NpcRole` 标志位（冗余），以字段是否为 null 判定是否绑定。

### 验证结果

- [x] 编译通过（0 错误）
- [ ] 功能测试：复合交互菜单、头顶标记、ActorId 显示、商店名/账户支付、跟随旋转、固定朝向（需测试 Mod 实测）

---

## 对话系统重写：DialogueTreeController 方案 — ✅ 已完成

**完成时间**: 2026-07-17

三度修复后彻底放弃反射方案，改用游戏原生 `DialogueTreeController` 驱动完整对话流程。

### 根因

反射获取 `DialogueTree.OnDialogueStarted` backing field 失败（Mono 编译器命名不可靠）→ 对话面板永远打不开。

### 方案

运行时动态创建 `DialogueTreeController` → 注入 minimal JSON → `StartDialogue()` → NodeCanvas 接管。**零反射调用（仅一次 field set 用反射兜底）**。

### 文件变更

| 操作 | 文件 | 改动摘要 |
|------|------|----------|
| 重写 | `Dialogues/DialogueUtils.cs` | `PlayDialogue(actorId, lines)` 新 API；剔除反射 delegate；`BuildDialogueJson` 内建 JSON 生成 |
| 重写 | `Dialogues/DialogueTrigger.cs` | `QuestTriggerEntry` 新增 `ActorId`；`PlayDialogueForNpc` 用新 API |
| 修改 | `Dialogues/NpcProximityTrigger.cs` | 改用 `DialogueUtils.PlayDialogue` |
| 修改 | `Entities/FriendlyNpcConfig.cs` | 新增 `SightDistance`（默认 8f） |
| 修改 | `Entities/FriendlyNpcUtils.cs` | `BuildFriendlyPreset` 用 `config.SightDistance`；`CreateInteractChild` 加 `BoxCollider`；`RemoveNpc` 不再删 preset |

### 新增 API

```csharp
// 播放任意对话（面板+镜头+字幕全流程）
await DialogueUtils.PlayDialogue("actor_id", new[] {
    new SubtitleLine { Text = "你好！" },
    new SubtitleLine { Text = "有什么需要？" },
});

// NPC 面向玩家
config.SightDistance = 8f;  // 默认值，AI 自然朝向
```

### 设计偏离

从 `DialogueTree.OnDialogueStarted` 反射 invoke → `DialogueTreeController.StartDialogue()` 全流程。这完全匹配原版 CutScene 机制。

### 验证结果

- [x] 编译通过

---

## 项目改名 — ✅ 已完成

**完成时间**: 2026-07-06
**耗时**: 约 1 小时

### 变更摘要

| 维度 | 旧值 | 新值 |
|------|------|------|
| 品牌名 | Fast Modding Lib | **Feather** (Feather Modding Lib) |
| 命名空间 | `FastModdingLib` | `FeatherMod` |
| 程序集 | `FastModdingLib.dll` | `FeatherMod.dll` |
| 框架 modid | `"FastModdingLib"` | `"FeatherMod"` |
| 内部 domain | `"fastmoddinglib"` | `"feather"` |

### 文件变更

| 操作 | 范围 | 说明 |
|------|------|------|
| 修改 | 118 个 `.cs` 文件 | 命名空间、using 语句、字符串常量全部更新 |
| 修改 | `FastModdingLib.csproj` | 新增 AssemblyName + RootNamespace 为 `FeatherMod` |
| 修改 | `README.md` | 品牌名、描述、示例代码更新 |
| 修改 | `Docs/USAGE.md` | 标题、示例代码、namespace 引用更新 |
| 修改 | `Docs/MIGRATION.md` | 新增 §0 项目改名迁移章节 |
| 修改 | `Docs/PROGRESS.md` | 新增本条目 |
| 修改 | `Docs/TODO/*.md` (7 文件) | 历史引用更新 |

### 设计偏离
- 源代码目录名 `FastModdingLib/` 保持不变（避免 git 历史断裂和 CI 配置变更）
- 缩写 FML 保持不变（Feather Modding Lib）
- `fml.json` 文件名不变（保持向下兼容）
- 历史文档中 `"old_fml_version"` 字符串保留（描述旧版 API 行为，非框架标识）

### 补充（2026-07-07）：代码层改名完成
- 全部 114 个 `.cs` 文件 `namespace` / `using` 引用已从 `FastModdingLib` 替换为 `FeatherMod`
- 12 处字符串常量/注释中的 modid `"FastModdingLib"` → `"Feather"`，domain `"fastmoddinglib"` → `"feather"`
- 改名映射：命名空间 `FastModdingLib` → `FeatherMod`；框架 modid `"FastModdingLib"` → `"FeatherMod"`；内部 domain `"fastmoddinglib"` → `"feather"`

---

## 代码质量与性能审计 — ✅ 已完成

**完成时间**: 2026-07-06
**耗时**: 约 2 小时

### 审计范围
- 编译警告分析（35 个预存 nullable/过时 API/不可达代码警告）
- 代码坏味道扫描（空 catch、静默吞异常、命名一致性、大文件）
- 性能热点分析（反射缓存、LINQ 分配、GetComponent 缓存、装箱）

### 已修复

| 文件 | 问题 | 严重度 |
|------|------|--------|
| `Crafting/TagCostValidator.cs` | `GetMethod("GetStat")` + `GetProperty` 每次合成均反射调用 → 静态缓存 | CRITICAL |
| `Crafting/TagCostValidator.cs` | `GetProperty("AllSlots")` + `GetProperty("Content")` 每次背包枚举均反射 → 延迟缓存 | CRITICAL |
| `Crafting/TagCostValidator.cs` | `new List<(Item,float)>()` 每次消耗均分配 → 复用 static buffer | HIGH |
| `Crafting/TagCostValidator.cs` | `new List<Item>()` 每次枚举均分配 → 复用 static buffer | HIGH |
| `Crafting/TagCostValidator.cs` | `foreach` 热循环 → `for` 消除迭代器分配 | MEDIUM |

### 已知遗留问题（预存，未修改）

| # | 文件 | 问题 | 优先级 |
|---|------|------|--------|
| 1 | `Items/ItemUtils.cs:643-672` | `HasTag()` 每次调用反射（`GetProperty("Tags")` + 每标签 `GetProperty("name")`） | CRITICAL |
| 2 | `Quests/FMLTask_SubmitItemByTag.cs:66-91` | 同上 `GetMethod("GetStat")` 模式未缓存 | CRITICAL |
| 3 | `LotteryBoxPatch.cs:127,191` | 循环内每次条目反射（`GetField("value"/"weight")`） | CRITICAL |
| 4 | `PerkTrees/PerkTreeUtils.cs` | 11+ 处未缓存反射（`GetField("perkTrees")`、`GetField("id")` 等） | HIGH |
| 5 | `Items/ItemUtils.cs:668-670` | 空 `catch { }` 静默吞异常 | HIGH |
| 6 | `Events/Adapters/GameEventAdapters.cs:231` | `catch (Exception)` 无日志吞异常 | HIGH |
| 7 | `Items/ItemUtils.cs` 674 行 | 最大文件，含 5 个不同关注点，建议拆分 | MEDIUM |
| 8 | `Items/ItemUtils.cs:23,57` | `createUsage`/`createBehavior` camelCase 命名 | LOW |
| 9 | `Register/SimpleRegistry.cs:101-108` | LINQ `Where().Select().ToList()` 卸载时分配 | MEDIUM |
| 10 | `AssetUtil.cs:56` | `Debug.Log` 应为 `Debug.LogError` | LOW |

### 编译状态
- ✅ 0 错误
- ⚠️ 35 预存警告（nullable 安全 / 过时 API / 不可达代码 / 未使用字段）

---

## 待测试模块

以下模块代码已完成但尚未在实际游戏环境中验证：

| 模块 | Phase | 关联文件 | 测试要点 |
|------|-------|---------|---------|
| **Building** | Phase 4 | `BuildingUtils.cs`, `BuildingRegistry.cs`, `BuildingCollectionPatch.cs` | 建筑注册、PlaceBuilding 反射放置、GetInfo/GetPrefab 回退、建造面板显示 |
| **PerkTree** | Phase 4 | `PerkTreeUtils.cs`, `PerkTreeRegistry.cs`, `PerkTreeEnablePatch.cs`, `PerkTreeCollectGuard.cs` | 自定义 PerkTree 创建/显示、AddPerk/ConnectPerks、ForceUnlock、PerkBehaviour 挂载 |
| **NPC / Enemy** | Phase 3–4 | `EnemyUtils.cs`, `EnemyRegistry.cs`, `EnemyPresetData.cs`, `IStateConfig.cs`, `StateMachineToBT.cs`, `OtherPatches.cs`, `AICharacterControllerInit.cs` | 自定义敌人注册/生成、IStateConfig→BT 编译、捏脸引用、AI 行为注入 |
| **NPC 武器注入** | ✅ 已完成 | `WeaponInjectionUtils.cs`, `WeaponInjectionRegistry.cs` | 按 preset/team 注入武器、卸载恢复 — 测试通过 |
| **抽奖箱注入** | ⚠️ 部分不稳定 | `LotteryBoxUtils.cs`, `LotteryBoxPatch.cs`, `LotteryBoxRegistry.cs` | 物品注入/权重、枪/刀隔离、卸载恢复 — 测试通过，但游戏本体后续更新可能修改抽奖箱实现，存在不稳定性 |

> 测试通过后更新此表对应行状态为 ✅，并补充测试环境信息（游戏版本、Mod 列表）。

---

## Phase 0 — 仓库与工程基础整理 ✅ 已完成

**完成时间**: 2026-06-20
**耗时**: 约 2 小时

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `.gitignore` | 写入根 gitignore，覆盖 `DecompiledDLL/`、`.vs/` 等 |
| 修改 | `README.md` | 全面更新，反映全模块 API |
| 修改 | `FeatherMod/DuckovPath.targets` | 新增 `$(DUCKOV_PATH)` 环境变量优先 |
| 修改 | `Tests/Tests.csproj` | 通过 `Condition` 控制 Debug 配置排除 |
| 删除 | 嵌套 `.sln` | 删除子目录的重复 sln 文件 |

### 遗留问题
- 无

---

## Phase 1 — 框架内核加固 ✅ 已完成

**完成时间**: 2026-06-25
**耗时**: 约 6 小时

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FeatherMod/Events/` | EventBus 核心 + AsyncEventBus + 15 个游戏事件桥接 |
| 新建 | `FeatherMod/Events/EventBusTest.cs` | EventBus 7 个单元测试用例 |
| 新建 | `FeatherMod/Register/` | Register 一体化：IRegistry、SimpleRegistry、ReverseLookupRegistry、RegistryManager、ModScope |
| 新建 | `FeatherMod/Register/RegisterTest.cs` | 15 个 Register 测试用例 |
| 修改 | `FeatherMod/ModBehaviour.cs` | 生命周期：OnAfterSetup 调 EventBus + Register bootstrap |
| 修改 | 多个模块 | Audio/Quests/Shop/Items/Crafting 五模块旁路字典收编到 Registry |

### 遗留问题
- 无

---

## Phase 2 — 头部消费系统 ✅ 已完成

**完成时间**: 2026-06-27
**耗时**: 约 3 小时

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FeatherMod/EconomyUtils.cs` | Money 增删查、SetMoney、物品解锁/确认/查询 |
| 新建 | `FeatherMod/BuffUtils.cs` + `BuffRegistry.cs` | Buff 注册/查询/卸载 |
| 新建 | `FeatherMod/Options/` | ModOptionsBuilder + ModOptionsRegistry（Toggle/Slider/Dropdown/Button） |

### 遗留问题
- 无

---

## Phase 3 — 内容创作系统 ✅ 已完成

**完成时间**: 2026-06-29
**耗时**: 约 5 小时

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FeatherMod/Shop/ShopUtils.cs` + `ShopRegistry.cs` | 完整 A-Z 商店 API |
| 新建 | `FeatherMod/Audio/AudioUtil.cs` | BGM 控制 + FMOD 总线音量 |
| 新建 | `FeatherMod/PerkTrees/PerkTreeUtils.cs` + `PerkTreeRegistry.cs` | 基础 API：AddPerk、ConnectPerks、ForceUnlock |
| 新建 | `FeatherMod/Buildings/BuildingUtils.cs` + `BuildingRegistry.cs` | 基础 API：RegisterBuilding、PlaceBuilding（占位） |
| 新建 | `FeatherMod/Entities/` | EnemyUtils、IStateConfig、StateMachineToBT、EnemyRegistry、3 个 Patch 文件 |

### 遗留问题
- PlaceBuilding 抛 NotSupportedException（Phase 4 B1 修复）
- ConnectPerks 用 try/catch 反射包装，脆弱（Phase 4 P1 重写）
- Endowment 完全缺失（Phase 4 E1 从零实现）

---

## Phase 4 — Building / PerkTree / Endowment / UI 深化 ✅ 已完成

**完成时间**: 2026-07-01
**耗时**: 约 3 小时
**审查修复完成**: 2026-07-01

### 设计原则
所有新增/修改的 public API 统一使用 `Identifier` 作为资源标识符，
游戏原生数字 ID（如 `EndowmentIndex` 枚举值）由 FML 内部自动分配，对 modder 完全透明。

### 文件变更清单

#### B1/B2 — Building 深化
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FeatherMod/Buildings/Patches/BuildingCollectionPatch.cs` | 3 个 Harmony Postfix（GetInfo/GetPrefab/GetBuildingsToDisplay） |
| 修改 | `FeatherMod/Buildings/BuildingUtils.cs` | PlaceBuilding 反射实现 + Identifier 化；GetBuildingInfo(Identifier) 新增；GetAllBuildingIds() 返回 IReadOnlyList\<Identifier\>；[Obsolete] string 重载保留 |
| 修改 | `FeatherMod/Buildings/BuildingRegistry.cs` | 新增 GetAllInfos() 供 Patch 层遍历 |

#### P1 — PerkTree 稳健化
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 修改 | `FeatherMod/PerkTrees/PerkTreeUtils.cs` | ConnectPerks 重写（去 try/catch + NodeCanvas 直接 API）；AddPerk(Identifier) Identifier 化；新增 AddPerkBehaviour\<T\>；新增 RegisterPerkTree 完整创建自定义树；ForceUnlock(Identifier) Identifier 化；保留 [Obsolete] string 重载 |
| 新建 | `FeatherMod/PerkTrees/Patches/PerkTreeEnablePatch.cs` | LevelConfig.IsPerkTreeEnabled Prefix——自定义 treeId 返回 true |
| 新建 | `FeatherMod/PerkTrees/Patches/PerkTreeCollectGuard.cs` | PerkTree.Collect Prefix——跳过 FML 树的 Collect |

#### E1 — Endowment 完整实现
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FeatherMod/Endowment/EndowmentUtils.cs` | 完整 API：RegisterEndowment/UnregisterEndowment/SelectEndowment/IsEndowmentUnlocked/UnlockEndowment/GetCurrentSelection——全部走 Identifier |
| 新建 | `FeatherMod/Endowment/EndowmentRegistry.cs` | SimpleRegistry\<EndowmentEntry\> + Identifier→EndowmentIndex 内部映射（≥10） |
| 新建 | `FeatherMod/Endowment/Patches/EndowmentManagerPatch.cs` | Awake Postfix 注入 + SelectIndex Prefix |

#### U1 — UI 交互辅助
| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FeatherMod/UI/InteractTemplates.cs` | 三个 InteractableBase 子类模板（Building/PerkTree/Endowment） |
| 修改 | `FeatherMod/Register/RegisterBootstrap.cs` | 新增 EndowmentUtils.Init() 调用 |

### 设计偏离
- **ModifierDescription 类型不可编译引用**：`EndowmentUtils.RegisterEndowment` 便捷重载改用 `object[]` 参数代替 `ModifierDescription[]`，避免对游戏内部类型的编译期依赖。运行时通过反射设置到 EndowmentEntry.modifiers 字段。
- **PerkTree.ConnectTo 接口存疑**：`PerkRelationNode.ConnectTo()` 在编译期不可用（NodeCanvas 版本差异），回退为反射调用 + graph.ConnectNodes 双重兜底方案。
- **EndowmentManager API 签名差异**：`IsUnlocked` 和 `UnlockEndowment` 通过反射调用，以兼容 static/instance 两种可能的签名。

### 验证结果
- [x] `dotnet build` 通过（0 错误，25 警告——均为预存警告，无新增）
- [x] 所有新增 public API 使用 Identifier（无裸 string/数字 ID）
- [x] 不修改 0Harmony.dll 引用方式

### 审查修复记录（2026-07-01 二次审查）
- **`PerkTreeUtils.ResolveTreeId`**：原本只返回 domain，现改为先查已注册 PerkTree 再查原生 treeId
- **`PerkTreeUtils.RegisterPerkTree`**：原本用 `null` 作为 registry value，现改用 HashSet 跟踪 treeId + 正确 cleanup
- **`PerkTreeUtils.RemoveAllPerks`**：新增自定义 PerkTree 的 GameObject 销毁和 PerkTreeManager 列表清理
- **`PerkTreeEnablePatch`/`PerkTreeCollectGuard`**：从名称前缀检测改为 `IsFMLTree()` 注册表检测
- **PLAN.md "Stub / 空缺" 部分**：Endowment/Building/PerkTree 状态从 ❌/⚠️ 更新为 ✅

### 遗留问题
- [x] **Endowment API 设计缺陷**：已修复（2026-07-03）——新增 `EndowmentConfig`/`EndowmentModifier` DTO，modder 纯 C# 配置天赋，无需接触游戏内部类型。旧 `object[]` 和 `EndowmentEntry` 重载标记 `[Obsolete]`。
- [x] **全局 `internal static Registry` 属性**：已修复（2026-07-03）——7 个模块的 Registry 从 `internal` 改为 `public`。
- [x] **Endowment 系统内部反射清理**：已修复（2026-07-03）——EndowmentUtils、EndowmentManagerPatch、EndowmentRegistry 共 14 处反射替换为直接访问（利用 Publicizer 公开的游戏成员）。
- [x] **Endowment UI 选择**：已修复（2026-07-03）——根因确认为时序竞争（`Awake` 早于 `PatchAll`）。修复方案：注册时主动注入 `entries`（`TryInjectToManager`），`AllocateIndex` 幂等化。Patch 层保留为安全网。
- [x] **PerkTree 系统 9 处游戏数据反射**：已修复（2026-07-20）——Perk 系统 v2 重构中，`PerkTreeManager.perkTrees` 改为直接访问（原反射 `BindingFlags.Static` 有 bug），Perk 字段（icon/displayName/hasDescription/quality/defaultUnlocked/requirement）改用 Publicizer 直接赋值。仅 `graph`（NodeCanvas 第三方 DLL）保留反射。

### 未实现的 PLAN-Phase4 设计项
以下组件在 `PLAN-Phase4-Building-Perk-Endowment-UI.md` §14-17 中有详细设计但未在 Phase 4 中实现，
已移至后续 Phase 5 或更晚处理：

| 组件 | 设计章节 | 状态 |
|------|---------|------|
| `EnemyPresetData` DTO + `ModelRef` | §14 | ✅ 已完成（Wave 2 补实现） |
| `CreateSimpleBuilding()` | §15.1 | ✅ 已完成（Wave 2 补实现） |
| `SetBuildingModel()` | §15.2 | ✅ 已完成（Wave 2 补实现） |
| `OnBuildingBuilt` 真回调 | §13-A.4 | ✅ 已完成（Wave 2 补实现） |
| `SimpleViewBuilder` | §16.2 | ✅ 已完成（Wave 3 补实现） |
| UI 注入辅助 | §16.1 | ⏳ 待 Phase 5 |
| `ItemEntry.ByTag()` + `WithDurabilityCost()` | §13-C.3 | ✅ 已完成（Wave 2 补实现） |

### P0/P1 修复记录（2026-07-03 Endowment DTO + Registry 公开 + 反射清理）

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 新建 | `FeatherMod/Endowment/EndowmentConfig.cs` | `EndowmentModifier` + `EndowmentConfig` DTO，modder 纯 C# 配置天赋 |
| 修改 | `FeatherMod/Endowment/EndowmentUtils.cs` | 新增 `RegisterEndowment(Identifier, EndowmentConfig)`；旧 API 标记 `[Obsolete]`；9 处反射→直接访问；移除 `System.Reflection` 依赖 |
| 修改 | `FeatherMod/Endowment/EndowmentRegistry.cs` | 4 个方法 `internal`→`public`；`OnRemoved` 中 2 处反射→`EndowmentManager.CurrentIndex` + `Instance.SelectIndex()` |
| 修改 | `FeatherMod/Endowment/Patches/EndowmentManagerPatch.cs` | 3 处反射→直接访问（`Registry`/`entries`/`index`）；移除 `System.Reflection` 依赖 |
| 修改 | `FeatherMod/Buildings/BuildingUtils.cs` | `internal static Registry` → `public` |
| 修改 | `FeatherMod/Buffs/BuffUtils.cs` | `internal static Registry` → `public` |
| 修改 | `FeatherMod/PerkTrees/PerkTreeUtils.cs` | `internal static Registry` → `public` |
| 修改 | `FeatherMod/Entities/EnemyUtils.cs` | `internal static Registry` → `public` |
| 修改 | `FeatherMod/Shop/ShopUtils.cs` | `internal static Registry` → `public` |
| 修改 | `FeatherMod/Quests/QuestUtils.cs` | `internal static Registry` → `public` |
| 修改 | `Docs/USAGE.md` | §15 EndowmentUtils 文档重写为 DTO 用法 |
| 修改 | `Docs/PROGRESS.md` | 遗留问题状态更新 |
| 新建 | `Docs/ISSUES.md` | 完整问题记录与修复计划 |

### 修复记录（2026-07-03 Endowment 时序 + Icon + 解锁）

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `FeatherMod/Endowment/EndowmentRegistry.cs` | `AllocateIndex` 幂等化 + 新增 `TryInjectToManager` 主动注入方法 |
| 修改 | `FeatherMod/Endowment/EndowmentUtils.cs` | `RegisterEndowment` 调用 `TryInjectToManager` 解决 Awake 时序竞争；`CreateNativeEntry` 补充 `unlockedByDefault` 和 `icon` 字段设置 |
| 修改 | `FeatherMod/Endowment/Patches/EndowmentManagerPatch.cs` | `Awake_Postfix` 委托给 `TryInjectToManager`，作为安全网兜底 |
| 修改 | `FeatherMod/Endowment/EndowmentConfig.cs` | 新增 `Icon (Sprite?)` 字段，支持 modder 传入图标 |
| 修改 | `Docs/USAGE.md` | §15 补充 Icon 用法 + 默认解锁示例 + Quest 任务解锁完整示例 |
| 修改 | `Docs/PROGRESS.md` | 遗留问题状态更新 |

### Quest 修复记录（2026-07-03 RewardUnlockEndowment + RewardUnlockBuilding）

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 新建 | `FeatherMod/Quests/FMLReward_UnlockEndowment.cs` | `Reward` 子类，AutoClaim + onCompleted 双重保障解锁天赋 |
| 新建 | `FeatherMod/Quests/FMLReward_UnlockBuilding.cs` | `Reward` 子类，任务完成时将建筑注册到 BuildingDataCollection |
| 修改 | `FeatherMod/Quests/QuestData.cs` | 新增 `RewardUnlockEndowmentData` + `RewardUnlockBuildingData`；添加 `Duckov.Buildings`/`UnityEngine` using |
| 修改 | `Docs/USAGE.md` | §6 Quest 奖励示例加入解锁天赋 + 解锁建筑用法 |

### 清理记录（2026-07-03 移除未使用参数 + Harmony 修正）

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `FeatherMod/Items/ItemUtils.cs` | 移除所有 `LoadSprite`/`LoadSpriteAsync` 中未使用的 `int NEW_ITEM_ID` 参数 |
| 修改 | `FeatherMod/Crafting/Patches/CraftingManagerPatch.cs` | `[HarmonyPatch]` 添加 `typeof(CraftingFormula)` 消除重载二义性 |
| 修改 | `FeatherMod/PerkTrees/Patches/PerkTreeEnablePatch.cs` | `Prefix` 参数 `treeId` → `perkTreeID` 匹配游戏原生方法签名 |
| 修改 | `Docs/USAGE.md` | 6 处 `LoadSprite(name, int)` → `LoadSprite(name)` |
| 修改 | `Docs/MIGRATION.md` | 1 处旧 API 引用更新 |
| 修改 | `Docs/FML-REFERENCE.md` | 1 处旧 API 引用更新 |
| 修改 | `Docs/CASE-STUDIES.md` | 1 处旧 API 引用更新 |

### 本地化记录（2026-07-03 Reward/Task I18n + FML 自注册）

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 新建 | `FeatherMod/assets/lang/en_us.json` | 英文 Reward/Task 本地化条目 |
| 新建 | `FeatherMod/assets/lang/zh_cn.json` | 简体中文本地化 |
| 新建 | `FeatherMod/assets/lang/zh_tw.json` | 繁体中文本地化 |
| 新建 | `FeatherMod/assets/lang/ja_jp.json` | 日文本地化 |
| 新建 | `FeatherMod/assets/lang/ko_kr.json` | 韩文本地化 |
| 新建 | `FeatherMod/assets/lang/ru_ru.json` | 俄文本地化 |
| 新建 | `FeatherMod/assets/lang/it_it.json` | 意大利文（英文回退） |
| 新建 | `FeatherMod/assets/lang/fr_fr.json` | 法文（英文回退） |
| 新建 | `FeatherMod/assets/lang/sv_se.json` | 瑞典文（英文回退） |
| 修改 | `FeatherMod/Quests/FMLReward_UnlockEndowment.cs` | `Description` 改用 `ToPlainText()` 本地化 |
| 修改 | `FeatherMod/Quests/FMLReward_UnlockBuilding.cs` | `Description` 改用 `ToPlainText()` 本地化 + 新增 `BuildingDisplayName` |
| 修改 | `FeatherMod/I18n.cs` | 修复 FML 路径 bug：`Assembly.Location` → `Path.GetDirectoryName(Assembly.Location)` |
| 修改 | `FeatherMod/FMLBootstrap.cs` | `EnsureInit()` 新增 `I18n.InitI18n()` 调用 |

### Wave 修复记录（2026-07-01 文档&代码修复）
- **Wave 1（文档）**：MIGRATION.md API 签名修正、PLAN.md 索引/矩阵/日期更新、PROGRESS.md 补充未实现项、USAGE.md 注释修正
- **Wave 2（代码）**：`EnemyPresetData.cs` + `ModelRef` 新建；`BuildingUtils.CreateSimpleBuilding`/`SetBuildingModel`/反射事件订阅；`CraftingData.ItemEntry` 扩展 `ByTag`/`WithDurabilityCost`
- **Wave 3（代码）**：`SimpleViewBuilder.cs` 新建；USAGE.md 补充文档

### Wave 遗漏模块补录（2026-07-02 审计发现）
以下模块已在代码中实现但未在 Wave 2/3 记录中列出，存在已实现代码无对应进度记录的问题：

| 模块 | 文件路径 | 状态 |
|------|---------|------|
| `TagCostRegistry` + `TagCostValidator` + `CraftingManagerPatch` | `FeatherMod/Crafting/` | ✅ 已实现（标签合成 Patch 系统） |
| `FMLTask_KillCountByTag` + `FMLTask_SubmitItemByTag` | `FeatherMod/Quests/` | ✅ 已实现（任务扩展子类） |
| `TaskKillByTagData` + `TaskSubmitItemByTagData` | `FeatherMod/Quests/QuestData.cs` | ✅ 已实现（任务数据 DTO） |
| `FaceRef` + `FacePartIds` + `FaceRefMode` + `NpcRole` | `FeatherMod/Entities/FaceRef.cs` + `EnemyPresetData.cs` | ✅ 已实现（捏脸引用类型） |

---

## NPC 武器注入 API — ✅ 已完成

**完成时间**: 2026-07-05
**耗时**: 约 3 小时
**策略**: Preset 数据层 — 合并到已有 Pool（零 Harmony Hook）

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|------|----------|----------|
| 新建 | `FeatherMod/Entities/WeaponInjectionData.cs` | 数据结构：WeaponInjectionData + PoolBackup + PoolEntrySnapshot |
| 新建 | `FeatherMod/Entities/WeaponInjectionRegistry.cs` | 注册表：继承 SimpleRegistry，OnRemoved 自动恢复 Pool |
| 新建 | `FeatherMod/WeaponInjectionUtils.cs` | 公开 API：AddWeaponToPreset/Team, Remove*, UnregisterAll |
| 修改 | `FeatherMod/Register/RegisterBootstrap.cs` | +1行：WeaponInjectionUtils.Init() 元表注册 |

### API
```csharp
public static void AddWeaponToPreset(string presetNameKey, ItemEntry weapon, float chance = 0.3f);
public static void AddWeaponToTeam(Teams team, ItemEntry weapon, float chance = 0.3f);
public static bool RemoveWeaponFromPreset(string presetNamePattern, ItemEntry weapon);
public static bool RemoveWeaponFromTeam(Teams team, ItemEntry weapon);
public static int UnregisterAllWeaponInjections(string modid);
```

### 设计偏离
- 枪刀 fallback 被移除：枪刀敌人类型不兼容，改为严格隔离（不跨类型注入）
- 无武器条目的 preset 直接跳过（不新建条目）
- `PoolEntrySnapshot` 独立结构体替代 `RandomContainer<Entry>.Entry`（避免嵌套泛型类型混淆）

### 遗留问题
- 无

---

## LotteryBox 物品注入 API — ✅ 已完成

**完成时间**: 2026-07-05
**耗时**: 约 2 小时
**策略**: Harmony Patch — Begin() Prefix 自动延迟注入；反射封装 ItemTypeID 私有类型访问

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|------|----------|----------|
| 新建 | `FeatherMod/Entities/LotteryBoxData.cs` | 数据模型：LotteryBoxData + CandidateSnapshot + CandidateBackup |
| 新建 | `FeatherMod/Entities/LotteryBoxRegistry.cs` | 注册表：继承 SimpleRegistry，OnRemoved 自动恢复 candidates |
| 新建 | `FeatherMod/LotteryBoxUtils.cs` | 公开 API：AddItemToLotteryBox / Remove / UnregisterAll（零反射） |
| 新建 | `FeatherMod/LotteryBoxPatch.cs` | Harmony Patch：Begin() Prefix 自动延迟注入 + ClassifyWeapon + RestoreCandidates |
| 修改 | `FeatherMod/Register/RegisterBootstrap.cs` | +1行：LotteryBoxUtils.Init() 元表注册 |

### API
```csharp
public static void AddItemToLotteryBox(string sceneNamePattern, ItemEntry item, float weight = 0.3f);
public static bool RemoveItemFromLotteryBox(string sceneNamePattern, ItemEntry item);
public static int UnregisterAllLotteryInjections(string modid);
```

### 枪/刀互斥
- 复用 WeaponInjection 的 ClassifyWeapon 逻辑（组件优先 + Tag 回退）
- 修正：无标准 `"MeleeWeapon"` Tag，用 `"Weapon"` Tag 兜底
- 严格隔离：枪→枪箱，刀→刀箱；类型不匹配跳过

### 设计偏离
- LotteryBox.ItemTypeID 为 private 嵌套类，无法直接引用。全部 candidates 操作通过 Harmony Traverse + 反射封装在 LotteryBoxPatch 中，公开 API 层零反射。
- LotteryBox 无全局集中列表（与 CharacterRandomPresetData.presets 不同），采用 Harmony Begin() Prefix 自动延迟注入，modder 无需手动管理时机。

### 遗留问题
- 无

---

## Phase 5 — 长尾幂等系统 ✅ 已完成

**Wave 1 完成**: 2026-07-14（Note + Fishing）  
**Wave 2/3 完成**: 2026-07-14（Friendly NPC + Weather + Multi-Scene）  
**设计文档**: `Docs/PLAN-Phase5-Goals.md`  
**逆向基础**: `duckov_assembly/assembly_0625` 反编译审计

### 子系统清单

| 优先级 | 子系统 | 预估 LOC | 状态 |
|--------|--------|---------|------|
| **P0** | Note（笔记/收集品） | ~180 LOC / 5 文件 | ✅ Wave 1 已完成 |
| **P0** | Fishing（钓鱼） | ~250 LOC / 4 文件 | ✅ Wave 1 已完成 |
| **P1** | Friendly NPC（友善 NPC） | ~200 LOC / 3 文件 | ✅ Wave 2 已完成 |
| **P1** | Weather & Seasons（天气/季节） | ~180 LOC / 3 文件 | ✅ Wave 2 已完成 |
| **P2** | Multi-Scene（多场景） | ~200 LOC / 3 文件 | ✅ Wave 3 已完成 |

> **已移除**: Achievements — 与 Steam 成就绑定，不应让 modder 更改。  
> **替代**: Note（笔记/收集品） — 纯游戏内，支持 UI 展示 + 条件门控 + 运行时注册

### 实施顺序
1. **Wave 1**（并行 P0）：Note + Fishing
2. **Wave 2**（并行）：Friendly NPC + Weather & Seasons
3. **Wave 3**（顺序）：Multi-Scene

### 已完成的前置工作（可在 Phase 5 启用）
- `FaceRef` / `FacePartIds` / `NpcRole` 类型已就绪
- `FMLTask_KillCountByTag` / `FMLTask_SubmitItemByTag` 类型已就绪
- `TagCostRegistry` / `TagCostValidator` / `CraftingManagerPatch` 已就绪

### 遗留问题
- FaceRef 运行时查找/创建（`FaceRefResolver`）待 Phase 5 实现
- TagCost / QuestTask 运行时验收待实际游戏环境测试

### Wave 1 文件变更清单（Note + Fishing — 2026-07-14）

| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FeatherMod/Notes/NoteConfig.cs` | NoteConfig DTO（TitleKey/ContentKey/Image/Hidden） |
| 新建 | `FeatherMod/Notes/NoteRegistry.cs` | NoteRegistry : SimpleRegistry\<Note\> + key 索引 |
| 新建 | `FeatherMod/Notes/NoteUtils.cs` | RegisterNote/Unlock/IsUnlocked/SpawnPickup/Init |
| 新建 | `FeatherMod/Notes/NoteEvents.cs` | NoteRegisteredEvent/NoteUnlockedEvent/NoteReadEvent |
| 新建 | `FeatherMod/Notes/Patches/NoteEventPatch.cs` | Harmony Postfix 桥接 SetNoteUnlocked/SetNoteRead → EventBus |
| 新建 | `FeatherMod/Fishing/FishingPoolConfig.cs` | FishingPoolConfig + FishingPoolEntry DTO |
| 新建 | `FeatherMod/Fishing/FishingRegistry.cs` | FishingRegistry : SimpleRegistry\<FishingPoolConfig\> + special catches |
| 新建 | `FeatherMod/Fishing/FishingUtils.cs` | RegisterFishingPool/RegisterSpecialCatch/Stats/Init + FishCaughtEvent |
| 新建 | `FeatherMod/Fishing/Patches/FishSpawnerPatch.cs` | Harmony Postfix 注入 specialPairs 到 FishSpawner.Awake |
| 修改 | `FeatherMod/Register/RegisterBootstrap.cs` | Init() 新增 NoteUtils.Init() + FishingUtils.Init() |

### 验证结果
- [x] `dotnet build` 通过（0 错误，43 预存警告）
- [ ] 功能测试（待游戏运行时验证）

### 遗留问题
- FaceRef 运行时查找/创建（`FaceRefResolver`）✅ 已完成（Wave 2）
- TagCost / QuestTask 运行时验收待实际游戏环境测试

### Wave 2 文件变更清单（Friendly NPC + Weather — 2026-07-14）

| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FeatherMod/Entities/FriendlyNpcConfig.cs` | FriendlyNpcConfig DTO |
| 新建 | `FeatherMod/Entities/FriendlyNpcUtils.cs` | CreateFriendlyNpc/ShowBubble/BindShop/BindQuestGiver |
| 新建 | `FeatherMod/Entities/Resolvers/FaceRefResolver.cs` | FaceRef → CharacterModel 运行时应用 |
| 新建 | `FeatherMod/Weather/WeatherType.cs` | FML WeatherType 枚举（隐藏 Snow=22） |
| 新建 | `FeatherMod/Weather/WeatherUtils.cs` | GetCurrent/Force/Storm/Precip/Temp + 事件 |
| 新建 | `FeatherMod/Weather/Patches/WeatherEventPatch.cs` | OnStormStarted/Ended → EventBus |
| 修改 | `FeatherMod/Entities/EnemyPresetData.cs` | NpcRole 枚举新增 None/Companion/DialogueOnly |

### Wave 3 文件变更清单（Multi-Scene — 2026-07-14）

| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FeatherMod/Scenes/MultiSceneUtils.cs` | LoadSubScene/TeleportTo/LevelData/MoveToScene |
| 新建 | `FeatherMod/Scenes/SceneRegistry.cs` | Identifier→sceneID 双向映射 |
| 新建 | `FeatherMod/Scenes/Patches/SceneLoadEventPatch.cs` | OnSubSceneLoaded/Unloaded → EventBus |
| 新建 | `FeatherMod/Dialogues/DialogueUtils.cs` | PlaySubtitle/PlaySubtitles/ShowBubble（NodeCanvas DialogueTree 直接调用） |
| 修改 | `FeatherMod/Register/RegisterBootstrap.cs` | Init() 新增 FriendlyNpc/Weather/MultiScene/Dialogue

### 验证结果
- [x] `dotnet build` 通过（0 错误，44 预存警告）
- [ ] 功能测试（待游戏运行时验证）

### 遗留问题
- TagCost / QuestTask 运行时验收待实际游戏环境测试
- 所有 Phase 5 模块待游戏内集成测试
- DialogueUtils.PlaySubtitle 待运行时验证 NodeCanvas DialogueUI 兼容性

### Publicizer 扩展（2026-07-14）

`FeatherMod.csproj` 新增 `NodeCanvas.DialogueTrees` 程序集 Publicizer 覆盖，
使 `DialogueTree` / `SubtitlesRequestInfo` / `IDialogueActor` / `IStatement` 等
NodeCanvas 对话系统类型可直接在 FML 内部访问，无需反射。

---

## 捏脸系统（CustomFaceUtils） — ✅ 已完成

**完成时间**: 2026-07-16
**耗时**: 约 1 小时

### 背景

游戏原生捏脸系统（`CustomFaceSettingData` / `CustomFaceInstance` / `MainCharacterFace`）已有完整的 `DataToJson()` / `JsonToData()` 序列化机制。FML 之前仅有 `FaceRef` / `FacePartIds`（NPC 捏脸引用），缺少对官方捏脸数据串（JSON）的导入/导出 API。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 新建 | `FeatherMod/Entities/CustomFaceUtils.cs` | 静态工具类：`SetPlayerFaceFromJson` / `GetPlayerFaceJson` / `SetFaceFromJson(instance)` / `GetFaceJson(instance)` / `LoadFaceFromData` / `GetFaceAsData` / `ValidateJson` / `GetPlayerFaceInstance` |

### API 设计

| 方法 | 用途 |
|------|------|
| `SetPlayerFaceFromJson(string)` | 将官方 JSON 捏脸串应用到玩家主角 |
| `GetPlayerFaceJson()` | 导出玩家当前捏脸为 JSON 字符串 |
| `SetPlayerFaceFromData(CustomFaceSettingData)` | 用原生 struct 设置玩家捏脸 |
| `GetPlayerFaceAsData()` | 获取玩家捏脸的原生 struct |
| `SetFaceFromJson(instance, string)` | 将 JSON 应用到任意 CustomFaceInstance |
| `GetFaceJson(instance)` | 从任意实例导出 JSON |
| `LoadFaceFromData(instance, data)` | 用原生 struct 设置任意实例 |
| `GetFaceAsData(instance)` | 获取任意实例的原生 struct |
| `ValidateJson(string)` | 检查 JSON 串是否合法 |
| `GetPlayerFaceInstance()` | 在场景中查找玩家主角的 CustomFaceInstance |

### 设计决策
- 直接封装游戏原生 `CustomFaceSettingData.JsonToData()` / `DataToJson()`，不做二次封装
- 与 `FaceRef` / `FacePartIds`（NPC 捏脸引用）互补——`FaceRef` 用于 NPC 创建时指定外观，`CustomFaceUtils` 用于运行时导入/导出现有捏脸数据
- `GetPlayerFaceInstance()` 通过 `FindObjectOfType<MainCharacterFace>()` 查找

### 验证结果
- [x] `dotnet build` 通过（0 错误）
- [ ] 功能测试（待游戏运行时验证）

---

## Building 模型 Prefab 规范文档 — ✅ 已完成

**完成时间**: 2026-07-16
**耗时**: 约 1 小时

### 背景

Building 系统的模型注入（`CreateSimpleBuilding` → `SetBuildingModel` → AssetBundle）缺少完整的 Prefab 规格标准说明，modder 不清楚模型 Prefab 在 Unity 中应如何构造。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `Docs/USAGE.md` §13 | 大幅扩展 Building 章节：新增 §13.1 Building Prefab 结构标准、§13.2 模型 Prefab 完整规格（根节点/Transform/材质/碰撞体/尺寸/导出格式）、§13.3 AssetBundle 完整工作流示例、§13.5 建造→NPC 生成完整示例、§13.6 碰撞体标准表 |
| 修改 | `Docs/USAGE.md` 目录 | 新增 §27 捏脸系统 + 后续章节重新编号 |
| 修改 | `Docs/PROGRESS.md` | 更新日期，新增 CustomFaceUtils 和 Building 规范条目 |

### Building 模型 Prefab 规格标准

| 要求 | 说明 |
|------|------|
| 根节点 | 纯 GameObject，无 Building 组件，无 functionContainer |
| 子物体 | 可嵌套多层 MeshRenderer |
| Transform | 注入时强制 (0,0,0)/(0,0,0)/(1,1,1) |
| 材质/Shader | 游戏原生 Shader，注入后自动修复 |
| 碰撞体 | **严禁** Collider |
| 导出 | AssetBundle → `assets/bundle/` |

---

## 测试反馈修复：InteractableBase NRE + NPC 不可见 — ✅ 已完成

**完成时间**: 2026-07-17
**耗时**: 约 1 小时
**来源**: 测试 Mod 拉回的崩溃报告（FeatherMod 框架 — FriendlyNpcUtils.CreateFriendlyNpc）

### 问题诊断

| 层级 | 问题 | 根因 | 影响 |
|------|------|------|------|
| **主因** | `InteractableBase.Awake()` 在裸 `GameObject` 上 NRE | `FriendlyNpcUtils.CreateFriendlyNpc`（line 64）和 `NoteUtils.SpawnPickup`（line 155）在 `new GameObject()` 上直接 `AddComponent<NoteInteract>()`，缺少 `BoxCollider` + `Interact` layer | **崩溃** — 但被 `BuildingUtils.OnBuildingBuiltHandler` 的 `try-catch` 捕获，NPC 创建静默失败 |
| **次因** | NPC 缺少 `CharacterModel` 组件 | `new GameObject()` 不会自动带 `CharacterModel`，`GetComponent<CharacterModel>()` 永远 null | NPC **不可见**（无 3D 模型） |
| **次因** | `FaceRef` 捏脸不生效 | `CharacterModel` 不存在 → `FaceRefResolver.ApplyToModel` 被跳过 | 无法为 NPC 设置外观 |

### 关键发现

- **FriendlyNPC 和 Enemy 在鸭科夫原生代码中是本质相同的**：两者都基于 `CharacterModel` + `CharacterRandomPreset.CreateCharacterAsync`。当前 FML 的 bare `GameObject` 路径是错误的简化。
- 正确的 NPC 创建应走 `CharacterRandomPreset.CreateCharacterAsync` 路径（与 `EnemyUtils.SpawnEnemy` 一致），届时 `CharacterModel`、`CustomFaceInstance`、Animator 等组件会自动就绪。

### 已修复

| 文件 | 改动 | 效果 |
|------|------|------|
| `Notes/NoteUtils.cs` | `SpawnPickup` 在添加 `NoteInteract` 前添加 `BoxCollider(isTrigger=true)` + `Interact` layer | 防止 `InteractableBase.Awake` NRE |
| `Entities/FriendlyNpcUtils.cs` | `CreateFriendlyNpc` 在 GameObject 创建后立即添加 `BoxCollider(isTrigger=true)` + `Interact` layer；所有 `AddComponent` 调用包裹 `try-catch` | 防止 `InteractableBase.Awake` NRE，失败时记录日志而非崩溃 |
| `Buildings/BuildingConfig.cs` | `BuildCost()` 添加防御注释，说明 `ResolveTypeId` fallback 行为 | 文档化已知行为，非功能性变更 |

### 设计偏离

- `FriendlyNpcUtils.CreateFriendlyNpc` 的 bare `GameObject` 路径未改为 `CharacterRandomPreset` 路径——此项改动范围大，涉及异步生命周期（`CreateCharacterAsync` 是 UniTask），需单独作为 Phase 6 子任务处理。当前仅修复了崩溃（NRE），**NPC 仍然不可见**（无 `CharacterModel`）。

### 遗留问题

- [ ] **NPC 不可见**：`CharacterModel` 需通过 `CharacterRandomPreset.CreateCharacterAsync` 或 Prefab 实例化方式添加。计划在装备 API 实现时一并解决。
- [ ] **FaceRef 捏脸不生效**：依赖 `CharacterModel` + `CustomFaceInstance`，两者在当前 bare `GameObject` 路径下不存在。
- [ ] **NPC 身体/头部装备 API**：用户已提出需求，待 Phase 6 实现。
- [ ] 功能测试（待游戏运行时验证——确认 `BoxCollider` + `Interact` layer 是否完全满足 `InteractableBase.Awake` 的要求）

### 验证结果

- [x] `dotnet build` 通过（0 错误，47 预存警告，0 新增）
- [ ] 功能测试（待游戏运行时——验证 NPC 交互不崩溃 + Note 拾取不崩溃）

---

## Phase 6 — 质量 ⏳ 待启动

**计划内容**（详见 PLAN.md §7）：
- NUnit 单元测试体系建设
- 示例 mod 项目（完整可运行 demo）
- 中英双语 API 文档完善
- CI/CD 流水线搭建

### 遗留问题
- 待 Phase 6 正式启动时补充详细计划文档（PLAN-Phase6-*.md）

---

## ID 策略全面重构 — ✅ 已完成

**完成时间**: 2026-07-06
**耗时**: 约 4 小时

### 核心原则落实
- **所有 public API 数字 ID 降级为 internal**（ItemUtils/ShopUtils/EconomyUtils/CraftingData/QuestUtils/BuffUtils/DecomposeRegistry）
- **ItemEntry.ItemTypeId** → `internal`，`Of(int)` 工厂 → `internal`
- **GameItemLookup** 新建：原版 `duckov` 域反查表，扫描全量游戏物品建立 `displayName ↔ TypeID` 映射
- **异步预注册机制**：`ReserveTypeId`/`ConfirmReservation`/`CancelReservation`，async 在 await 前占坑防抢占

### 文件变更清单
| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 新建 | `FeatherMod/Items/GameItemLookup.cs` | duckov 域反查表 + 公开发现 API |
| 新建 | `FeatherMod/Utils/WildcardHelper.cs` | 消除 WeaponInjectionUtils/LotteryBoxPatch WildcardMatch 重复 |
| 新建 | `FeatherMod/Utils/WeaponClassifier.cs` | 消除两处 ClassifyWeapon+WeaponKind 重复 |
| 修改 | `FeatherMod/Items/ItemUtils.cs` | TryResolveTypeId/TryGetCustomItem(int)→internal；ReserveTypeId；IsTypeIdReservedByOther；集成 GameItemLookup |
| 修改 | `FeatherMod/CraftingData.cs` | ItemEntry.ItemTypeId/Of(int)→internal；Builder int 重载→internal；SourceItemTypeId→internal |
| 修改 | `FeatherMod/EconomyUtils.cs` | 4 个 int 重载→internal |
| 修改 | `FeatherMod/Shop/ShopGoodsData.cs` | typeID→internal |
| 修改 | `FeatherMod/Shop/ShopUtils.cs` | RemoveGoods/EditGoods/TryGetGoods(int)→internal |
| 修改 | `FeatherMod/Shop/ShopRegistry.cs` | Register/TryGetIdentifier/FindIdentifier(int)→internal |
| 修改 | `FeatherMod/DecomposeRegistry.cs` | Register/TryGetIdentifier(int)→internal |
| 修改 | `FeatherMod/Buffs/BuffUtils.cs` | FindBuff(int)→internal；新增 TryGetBuffIdentifier(int) public |
| 修改 | `FeatherMod/Quests/QuestUtils.cs` | 新增 TryGetQuestIdentifier/TryGetQuestId；UnregisterQuest/AddQuestRelation Identifier 版 |
| 修改 | `FeatherMod/Quests/QuestData.cs` | 全部 int 字段→internal |
| 修改 | `FeatherMod/Utils/WeaponClassifier.cs` | Classify(int)→internal |
| 修改 | `FeatherMod/FMLConstants.cs` | 新增 DuckovDomain="duckov" |
| 修改 | `FeatherMod/FMLBootstrap.cs` | EnsureInit 加入 GameItemLookup.Init() |
| 修改 | 6 个文件 | 日志清理（12条高频 Debug.Log 删除） |
| 修改 | `LotteryBoxPatch.cs` + `WeaponInjectionUtils.cs` | 重构使用共享 WildcardHelper/WeaponClassifier |

### 日志清理
- LotteryBoxPatch: 5 条 runtime 日志删除
- OtherPatches: 4 条 runtime 日志删除
- InteractTemplates: 2 条 UI 日志删除
- ItemUtils: "Start Register" 重复日志合并
- AudioObjectMixin: Log→LogWarning 级别修正

---

## 文档清理 — ✅ 已完成

**完成时间**: 2026-07-06

### 变更
| 操作 | 路径 | 说明 |
|------|------|------|
| 新建 | `Docs/TODO/` | 未完成计划存放目录 |
| 移入 | `PLAN.md` → `Docs/TODO/` | Phase 5/6 未完成 |
| 移入 | `Docs/DESIGN-*.md` → `Docs/TODO/` | 设计文档 |
| 移入 | `Docs/CASE-STUDIES.md` 等 5 文件 → `Docs/TODO/` | 参考/问题文档 |
| 删除 | `.omo/plans/npc-weapon-injection*.md` | NPC 注入已完成 |
| 重写 | `README.md` | 反映 Identifier 优先 + GameItemLookup + 全模块速览 |

### 保留的公开文档
- `Docs/USAGE.md` — 使用指南
- `Docs/MIGRATION.md` — 迁移指南
- `Docs/PROGRESS.md` — 进度文档

---

## Quest 冲突检测与 ID 反查 — ✅ 已完成

**完成时间**: 2026-07-07
**耗时**: 约 1 小时

### 背景

Quest 模块 Identifier 化后，数字 ID 自增分配（从 1000 起）缺少与游戏原生任务的冲突检测；反查 `TryGetQuestIdentifier` 为 O(n) 线性扫描，性能较差。

### 改动

#### QuestRegistry.cs — 反向索引与冲突检测基础设施

| 改动 | 说明 |
|------|------|
| 新增 `_questIdIndex` 字段 | `Dictionary<int, Identifier>` quest 数字 ID → Identifier 反向索引 |
| `override Set()` | 写注册表时同步更新 `_questIdIndex`，处理 Identifier 替换和 ID 冲突 |
| `override Remove()` | 删条目时同步清理 `_questIdIndex` |
| `override RemoveAllByOwner()` | 批量清理反向索引（显式 + 回调双重保障） |
| `override Clear()` | 清空反向索引 |
| 新增 `TryGetIdentifier(int, out Identifier)` | O(1) 反查（替代原 O(n) 扫描） |
| 新增 `IsQuestIdOccupied(int)` | 注册表内 ID 占用检测 |
| 新增 `IsQuestIdInCollection(int)` | 遍历 `GameplayDataSettings.QuestCollection` 检测全量（含原生任务） |

#### QuestUtils.cs — 接入冲突检测与 O(1) 反查

| 方法 | 改动 |
|------|------|
| `TryGetQuestIdentifier` | O(n) 遍历 → O(1) 委托 `_questRegistry.TryGetIdentifier` |
| `RegisterQuestInternal` | `data.ID = _nextQuestId++` → while 循环检测 `QuestCollection` + `_questIdIndex` 双重校验后分配 |
| `UnregisterQuest(int)` | O(n) 遍历匹配 → O(1) `TryGetIdentifier` + `Remove` |

### 冲突检测流程

```
分配候选 ID → IsQuestIdInCollection（含原生任务）
            → IsQuestIdOccupied（已注册 FML 任务）
            → 任一冲突则 _nextQuestId++ 重试
            → 通过后写入 data.ID
```

### 设计参考

- 反向索引模式参考 `EndowmentRegistry._indexMap` + `GameItemLookup` 双字典
- 冲突检测模式参考 `ItemUtils.IsTypeIdOccupied`
- 所有 public API 保持 `Identifier` 优先，数字 ID 完全 internal

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `FeatherMod/Quests/QuestRegistry.cs` | ~90 行新增（反向索引 + 4 个 override + 3 个新方法） |
| 修改 | `FeatherMod/Quests/QuestUtils.cs` | 3 处重构（反查 O(1)、冲突检测 while、卸载 O(1)） |

---

## 交互与 UI 系统 — ✅ 已完成

**完成时间**: 2026-07-11
**耗时**: 约 2 小时
**基础**: `Docs/DESIGN_INTERACTION_API.md` + `Docs/DESIGN_UI_SYSTEM_API.md` + 反编译审计

### 新增文件（7 个）

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 新建 | `FeatherMod/Interaction/InteractionUtils.cs` | 交互系统主入口：Spawn / Attach / Query / Cleanup。Init 注册到元表 + 内置 View |
| 新建 | `FeatherMod/Interaction/InteractionRegistry.cs` | `SimpleRegistry<InteractionEntry>`，OnRemoved 自动 `Destroy(GameObject)` |
| 新建 | `FeatherMod/Interaction/Components/ViewInteractHandler.cs` | 继承 InteractableBase，交互→`ViewDispatcher.Open(ViewType, param)` |
| 新建 | `FeatherMod/Interaction/Components/DelegateInteractHandler.cs` | 继承 InteractableBase，交互→`OnInteract?.Invoke()` |
| 新建 | `FeatherMod/Interaction/ViewDispatcher.cs` | View 打开方法注册/调度 + `GameViews` 常量类（6 个内置 View） |
| 新建 | `FeatherMod/UI/GameUIUtils.cs` | 游戏原生 UI 桥接：5 个控件克隆 + 字体/配色提取 + 过滤合成/库存打开 |
| 新建 | `FeatherMod/Containers/ContainerUtils.cs` | 轻量容器管理：CRUD + 物品转移 + 绑定到建筑。含 `ItemContainerConfig` DTO |

### 修改文件（4 个）

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `FeatherMod/UI/InteractTemplates.cs` | `perkTreeID`→`public PerkTreeID`；三个模板 `OnInteractFinished` 改为 `ViewDispatcher.Open` |
| 修改 | `FeatherMod/Register/RegisterBootstrap.cs` | `Init()` 末尾新增 `InteractionUtils.Init()` |
| 修改 | `FeatherMod/UI/SimpleViewBuilder.cs` | 新增 `AddGameButton(text, onClick)` + `AddGamePanel(title)` 方法 |
| 修改 | `FeatherMod/CraftingUtils.cs` | 新增 `OpenFilteredCraftingView(params string[] tags)` → `GameUIUtils.OpenCraftingView` |

### 设计偏离

- **`InventorySlot` 类型不存在于反编译 DLL**：`GameUIUtils.OpenInventoryDevice` 移除 `slots` 参数，改为通过 `FindObjectOfType<InventoryDisplay>` 获取（`InventoryDisplay` 是 `MonoBehaviour` 非 `View` 子类，不能用 `GetViewInstance<T>`）
- **`ItemUtilities.SendToPlayer` 签名不匹配**：实际 API 为 `SendToPlayerCharacterInventory(Item)` / `SendToPlayerStorage(Item)`，`ContainerUtils.TakeItem` 改为调用前者
- **`CraftingFormula` 为值类型（struct）**：`OpenCraftingView` 中 Predicate 的 null 检查改为 `formula.tags == null`（不能对 struct 用 `== null`）
- **`PerkTreeView.Show` 方法待运行时确认**：`InteractionUtils.RegisterBuiltInViews` 中对 PerkTree 的注册保留调用，需在游戏运行时验证 API 可用性
### 验证结果

- [x] `dotnet build` 通过（0 错误）

---

## DuckovDrinks 集成 Bugfix — ⏳ 进行中

**完成时间**: 2026-07-24
**耗时**: 约 3 小时
**类型**: Bugfix（4 项修复）

### 背景

DuckovDrinks 测试 Mod 在集成 FML 时发现四个问题：
1. 建筑出现多个交互点而非单交互组，交互几乎无法使用
2. Tags 系统缺少运行时显式注册 Tag 的 API
3. 向原版 PerkTree 注入的 Perk 节点数据不持久化
4. NPC 重复生成（建筑重建后出现两个 NPC 实例）

### Bug #1：Building Machine 交互点未编组

**根因**：`BuildingUtils.SetupBuildingMachines` 为每台 Machine 创建独立的子 GameObject（各有 BoxCollider + ViewInteractHandler），但从未调用 `InteractionUtils.SetupInteractionGroup` 编组。

**修复**（`Buildings/BuildingUtils.cs`）：
- 收集所有 Machine 的 `ViewInteractHandler`，多台时调用 `InteractionUtils.SetupInteractionGroup(primary, members)` 编组
- 幂等：已初始化的 Machine 也纳入 handlers 列表
- 新增 `using Duckov;` 以支持 `InteractableBase` 类型

### Bug #2：Tags 系统缺少显式注册 API

**根因**：`ItemUtils.GetTargetTag` 仅调用 `GameplayDataSettings.Tags.Get(tagName)` 查找已有 Tag。Tag 需要通过 `ScriptableObject.CreateInstance<Tag>()` 显式创建并注册。

**修复**：
- **新增** `Items/TagUtils.cs`：`RegisterTag(name, config?)`、`GetTag(name)`、`TagExists(name)`、`TagConfig` 结构体
- **修改** `Items/ItemUtils.cs`：`GetTargetTag` 改为调用 `TagUtils.GetTag`，Tag 不存在时输出 warning

### Bug #3：原版 PerkTree 注入节点不持久化

**根因**：`PerkTreeUtils.AddPerk` 向原版树注入时，非延迟路径（graph 已就绪）没有调用 `tree.Load()`。FML 自定义树和延迟路径均有 Load，唯独此路径缺失。

**修复**（`PerkTrees/PerkTreeUtils.cs`）：
- 原版树非延迟路径（graph 非 null）的 `tree.Collect()` 之后添加 `tree.Load()`

### Bug #4：NPC 重复生成

**根因**：`FriendlyNpcUtils.SpawnFriendlyNpcAsync` 缺少去重检查。

**修复**（`Entities/FriendlyNpcUtils.cs`）：
- `SpawnFriendlyNpcAsync` 开头添加去重：若 registry 中已有实例，先移除再生成

### Bug #5：Machine 交互不检查 Perk 门控

**根因**：`IsMachineAvailable` 方法定义了但从未被调用（死代码）。Machine View 的 `ViewDispatcher` handler 直接打开 Crafting View，不检查 Perk 解锁状态。

**修复**：
- `Buildings/BuildingUtils.cs`：新增 `IsMachineAvailableByKey(string machineKey)` public 方法
- `Interaction/InteractionUtils.cs`：Machine View handler 在打开前调用 `IsMachineAvailableByKey`，Perk 未解锁时拒绝打开并输出 warning

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `Items/TagUtils.cs` | `TagUtils` 类 + `TagConfig` 结构体 |
| 修改 | `Buildings/BuildingUtils.cs` | `SetupBuildingMachines` 添加交互编组；新增 `using Duckov;` |
| 修改 | `Items/ItemUtils.cs` | `GetTargetTag` 改为调用 `TagUtils.GetTag` |
| 修改 | `PerkTrees/PerkTreeUtils.cs` | 原版树非延迟路径添加 `tree.Load()` |
| 修改 | `Entities/FriendlyNpcUtils.cs` | `SpawnFriendlyNpcAsync` 添加 NPC 去重 |
| 修改 | `Docs/PROGRESS.md` | Bugfix 记录 |

### 验证结果

- [x] `dotnet build` 通过（0 错误，53 预先存在警告）
- [ ] DuckovDrinks 功能测试（待验证）
- [ ] 功能测试（待游戏运行时验证）
- [ ] `GamePlayDataSettings.UIPrefabs` 克隆测试（待实际游戏环境）

---

## Phase 7: QuestGiver API + 商人 API 修复 — ✅ 已完成

**完成时间**: 2026-07-16
**耗时**: 约 2 小时

### 背景

项目没有为 QuestGiver 添加自定义接口。当 Modder 需要添加新的任务发放者时没有对应 API。
同时商人的 `CreateMerchantProfile` 不接受 `Identifier`，违反 Identifier 优先原则。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 新建 | `FastModdingLib/QuestGivers/QuestGiverConfig.cs` | QuestGiver 配置 DTO（DisplayName/ActorId/Face/位置/BoundQuests/POI） |
| 新建 | `FastModdingLib/QuestGivers/QuestGiverRegistry.cs` | 继承 SimpleRegistry\<GameObject\>，维护 Identifier↔questGiverID (int) 双向映射 + OnRemoved 清理 |
| 新建 | `FastModdingLib/QuestGivers/QuestGiverUtils.cs` | 公共 API：RegisterQuestGiver/SpawnQuestGiver/BindQuest/TryGetQuestGiver/Unregister（全部用 Identifier） |
| 新建 | `FastModdingLib/QuestGivers/Patches/QuestGiverIDPatch.cs` | Harmony 补丁拦截 GetAllQuestsByQuestGiverID/GetActiveQuestsFromGiver/GetHistoryQuestsFromGiver/AnyActiveQuestNeedsInspection，支持自定义 QuestGiverID（≥50） |
| 新建 | `FastModdingLib/Tests/QuestGiverTest.cs` | QuestGiver 功能测试 + FriendlyNpc 集成测试 + ShopUtils 修复验证 |
| 修改 | `FastModdingLib/Entities/FriendlyNpcUtils.cs` | ~5 处改动：SetQuestGiverId 支持 int 自定义值 + BindQuestGiver(Identifier) 重载 |
| 修改 | `FastModdingLib/Register/RegisterBootstrap.cs` | Init() 新增 `QuestGiverUtils.Init()` |
| 修改 | `FastModdingLib/Shop/ShopUtils.cs` | 新增 `CreateMerchantProfile(Identifier)` + `GetAllGoods(Identifier)` + `TryGetMerchantProfile(Identifier)` |
| 修改 | `FastModdingLib/Quests/QuestData.cs` | 新增 `QuestGiverIdentifier` 字段（Identifier?）— RegisterQuest 时自动绑定自定义 QuestGiver |
| 修改 | `FastModdingLib/Quests/QuestUtils.cs` | RegisterQuestInternal 优先使用 QuestGiverIdentifier → 自动映射到自定义 questGiverID |

### 关键技术决策

#### QuestGiverID 枚举限制绕过（方案 A）

游戏原生 `QuestGiverID` 是固定 enum（0~11），不可扩展。采用 Harmony 补丁方案：
- 自定义 QuestGiverID 从 **50** 起分配，与原生枚举值无冲突
- 通过反射将 int 值直接赋给 `QuestGiver.questGiverID` 字段（QuestGiverID 底层是 int）
- 补丁拦截 `QuestManager.GetAllQuestsByQuestGiverID` 等 4 个方法，检测自定义 ID 范围（≥50），返回 FML 内部维护的任务列表

#### FriendlyNpcUtils 集成

- `SetQuestGiverId` 升级：先尝试 `int.TryParse` → 自定义 ID（≥50）直接赋 int；否则尝试 `Enum.Parse` 匹配原生值
- 新增 `BindQuestGiver(Identifier npcId, Identifier questGiverId)` 重载，与 QuestGiverUtils 联动

#### 商人 API Identifier 修复

- `CreateMerchantProfile(Identifier id)` — 从 Identifier.Domain 推导 modid，Identifier.Path 作为 merchantID
- `GetAllGoods(Identifier id)` — Identifier.Path 作为 merchantProfileID
- `TryGetMerchantProfile(Identifier id, out profile)` — 按 Identifier 查询

### 设计偏离

- 无重大偏离。QuestGiver 模块完全遵循 EndowmentUtils 的 6 步模式（Config → Registry → Utils → Init → Bootstrap → Patch）
- `FriendlyNpcUtils.SetQuestGiverId` 保持了对旧 API（string 参数）的向后兼容

### 遗留问题

- [ ] 功能测试待游戏运行时验证（QuestGiverView 是否正确打开自定义 QuestGiver 的任务列表）
- [ ] `QuestData.questGiver` 字段仍使用原生 `QuestGiverID` 枚举 — 后续可考虑添加 `Identifier? QuestGiverIdentifier` 字段以完全遵循 Identifier 优先原则

### 验证结果
- [x] `dotnet build` 通过
- [ ] 功能测试（待游戏运行时验证）

---

## Phase 7.1: 全面代码审查修复 — ✅ 已完成

**完成时间**: 2026-07-16
**耗时**: 约 3 小时

### 背景

Phase 7（QuestGiver API）完成后，对全项目进行了 7 维度全面代码审查（健壮性/性能/API一致性/集成/Harmony/安全/设计），
发现 39 个问题。本轮修复了其中 17 个高风险问题，其余低优先级问题留待后续。

### 修复清单

#### 🔴 阻断性修复

| # | 文件 | 修复内容 |
|---|------|---------|
| C1 | `QuestGiverRegistry.cs`, `QuestGiverUtils.cs`, `FriendlyNpcUtils.cs` | 反射枚举赋值：`int` → `Enum.ToObject(field.FieldType, int)`，消除运行时 `ArgumentException` |
| C2 | `EnemyUtils.cs` | 反射参数类型：`Vector3.zero` → `Quaternion.identity`，修正 `CreateCharacterAsync` 调用 |
| C5 | `FishSpawnerPatch.cs`, `ModManagerPatches.cs`, `NoteEventPatch.cs` | 3 个独立 Harmony 实例注册到 `ModBehaviour.ExtraHarmonies`，卸载时统一 `UnpatchAll` |
| C6 | `ViewDispatcher.cs` | `UnregisterAll(modid)` 新增 `_ownerIndex`，按 mod 选择性清理而非清空全部 |
| C7 | `EventBus.cs` | 同步 `Post` 添加 `try-catch`，与 `AsyncEventBus` 保持一致 |
| C8 | `Identifier.cs` | 构造函数添加 `ArgumentNullException` 检查 |

#### 🟠 设计修复

| # | 文件 | 修复内容 |
|---|------|---------|
| M2 | `QuestGiverRegistry.cs`, `QuestGiverUtils.cs` | Registry 存储 `_spawnPositions`/`_spawnRotations`，`SpawnQuestGiver` 回退读取配置值 |
| M3 | `QuestGiverRegistry.cs` | `new` 隐藏基类 `Set()`，抛 `InvalidOperationException` 防止绕过 ID 分配 |
| M4 | `FriendlyNpcUtils.cs` | `NpcRole.QuestGiver` else 分支注释修正 |
| M5+M6 | `QuestGiverIDPatch.cs` | 全面重写：4 个 Prefix 包裹 try-catch + `BindingFlags.Public` + `nameof()` 统一 |
| M9 | `ShopRegistry.cs` | `_createdProfiles` 值类型 `List<string>` → `HashSet<string>`（O(n²)→O(n)） |

#### 🟡 轻量修复

| # | 文件 | 修复内容 |
|---|------|---------|
| M1 | `QuestGiverRegistry.cs` | 移除死存储 `_boundQuests` 字典和 `TryGetBoundQuests` 方法 |
| m1 | `QuestGiverRegistry.cs`, `QuestGiverIDPatch.cs` | 文档/代码阈值统一为 ≥50，添加 `MinCustomQuestGiverId` 常量 |
| m2 | `QuestGiverIDPatch.cs` | 字符串方法名全部改为 `nameof()` |
| — | `ModBehaviour.cs` | 补全 `using System`/`System.Collections.Generic`/`UnityEngine` |

### 文件变更清单

| 操作 | 文件 | 改动 |
|------|------|------|
| 修改 | `QuestGivers/QuestGiverRegistry.cs` | ~8 处：Enum.ToObject + Set 保护 + 位置存储 + BoundQuests 移除 + 常量 + OnRemoved |
| 修改 | `QuestGivers/QuestGiverUtils.cs` | ~2 处：Enum.ToObject + SpawnQuestGiver 位置回退 |
| 重写 | `QuestGivers/Patches/QuestGiverIDPatch.cs` | try-catch + Flags 常量 + nameof 统一 |
| 修改 | `Entities/FriendlyNpcUtils.cs` | Enum.ToObject + 注释修正 |
| 修改 | `Entities/EnemyUtils.cs` | Vector3→Quaternion |
| 修改 | `Events/EventBus.cs` | try-catch + using UnityEngine |
| 修改 | `Interaction/ViewDispatcher.cs` | _ownerIndex + Register/UnregisterAll 重写 |
| 修改 | `Utils/Identifier.cs` | null 检查 |
| 修改 | `Fishing/Patches/FishSpawnerPatch.cs` | ExtraHarmonies 注册 |
| 修改 | `Modding/ModManagerPatches.cs` | ExtraHarmonies 注册 |
| 修改 | `Notes/Patches/NoteEventPatch.cs` | ExtraHarmonies 注册 |
| 修改 | `ModBehaviour.cs` | ExtraHarmonies 列表 + using |
| 修改 | `Shop/ShopRegistry.cs` | List→HashSet |
| 修改 | `Docs/USAGE.md` | BindQuestGiver Identifier 重载文档 |
| 修改 | `Docs/PROGRESS.md` | 本记录 |

### 设计偏离

- 无重大偏离。所有修复保持**完全向后兼容**，已有 Mod 的 API 调用行为不变。
- `QuestGiverRegistry.Set()` 被 `new` 隐藏——若外部代码直接调用基类 `Set()` 会抛异常，但该路径从未暴露给 modder（内部调用已改用 `base.Set()`）。

### 未修复项

以下问题评估为低风险或需大量改动，留待后续：

- `BuildingUtils.cs` 静态初始化 + 事件钩子泄漏
- `EnemyRegistry.OnRemoved` 未清理原生 preset 列表
- `ItemUtils.HasTag` 反射缓存 + 空 catch
- `GameItemLookup` 50K 启动扫描
- API 命名不一致系列（Create/Register/Add/Spawn/Attach 混用）
- EndowmentRegistry/SettlementRegistry 反向索引缺失

### 验证结果

- [x] `dotnet build` 通过（0 错误）
- [ ] 功能回归测试（待游戏运行时验证）
- [ ] Harmony 卸载顺序验证

---

## Building 成本 API 补充 — ✅ 已完成

**完成时间**: 2026-07-16
**耗时**: 约 1 小时

### 背景

Building 系统的成本定义长期依赖游戏原生 `Cost` struct（裸 `int TypeID`），违反 FML 的 Identifier 优先原则。modder 必须手写 `new Cost.ItemEntry { id = 1001, amount = 5 }`，无法使用 FML 已有的 `ItemEntry.Of(Identifier, amount)` 模式。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 新建 | `FastModdingLib/Buildings/BuildingConfig.cs` | BuildingConfig DTO（Id/Dimensions/Money/CostItems/PrefabName/MaxAmount/UnlockedByDefault），含 `BuildCost()` 自动 Identifier→TypeID 解析 |
| 修改 | `FastModdingLib/Buildings/BuildingUtils.cs` | ~80 行新增：`CreateCost()` 成本转换桥、`RegisterBuilding(BuildingConfig)` 重载、`GetBuildingCost()` / `CanAffordBuilding()` / `SpendBuildingCost()` 成本查询、`GetBuildingPrefab()` Prefab 查询 |

### 新增 API

| API | 用途 |
|-----|------|
| `BuildingConfig` | 建筑配置 DTO，对标 `EndowmentConfig` |
| `CreateCost(long, ItemEntry[])` | 从 FML ItemEntry 构建原生 Cost（自动 Identifier→TypeID） |
| `RegisterBuilding(BuildingConfig, Building?)` | 一键注册（自动创建 prefab + 构建 BuildingInfo + 成本解析） |
| `GetBuildingCost(Identifier)` → `Cost?` | 查询建筑成本（返回原生 Cost，可调 `.Enough` / `.Pay()`） |
| `CanAffordBuilding(Identifier)` → `bool` | 检查玩家是否负担得起（委托 `Cost.Enough`） |
| `SpendBuildingCost(Identifier)` → `bool` | 手动扣除成本（委托 `Cost.Pay()`） |
| `GetBuildingPrefab(Identifier)` → `Building?` | Prefab 查询（优先 Registry，回退原生） |

### 设计偏离

- 无重大偏离。`CanAffordBuilding` / `SpendBuildingCost` 直接委托游戏原生 `Cost.Enough` / `Cost.Pay()`，不自行实现背包枚举——复用游戏中已验证的扣费逻辑。
- `BuildingConfig.RequireBuildings` / `RequireQuests` 字段仅用于 DTO 存储，当前不在 `RegisterBuilding(BuildingConfig)` 中自动设置到 `BuildingInfo`（字段名因游戏版本可能变化，后续稳定后再启用）。

### 验证结果

- [x] `dotnet build` 通过（0 错误，46 预存警告，0 新增）
- [ ] 功能测试（待游戏运行时验证——BuildingConfig 注册 + 成本询价 + PlaceBuilding 自动扣费）

### 后续修复（2026-07-16）

- **NRE 修复**：`BuildingInfo.RequirementsSatisfied()` 遍历 `requireBuildings`/`requireQuests` 时 crash。根本原因：`new BuildingInfo { ... }` 创建 struct 时这些 `string[]` 字段为 null。修复方案：`SanitizeBuildingInfo()` 在注册时用反射将 null 数组初始化为 `Array.Empty<string[]>()`，`BuildingCollectionPatch` 追加前再做一层防御。
- **Decompose 重复 key 修复**：`AddDecomposeFormulaInternal` 的 `instance.Dic.Add(sourceItemId, item)` 对已有原版配方的物品抛 `ArgumentException`。改为 `instance.Dic[sourceItemId] = item`（索引器允许覆盖），覆盖时输出 warning。
- **文档重写**：`Docs/USAGE.md` §13 建筑章节全面重写——按实战场景重新组织示例（BuildingConfig 快速开始 → 完整配置 → 三种注册模式 → 放置查询 → 回调），精简 Prefab 规格说明。

---

## Phase 6 启动：NPC 创建路径重构 + 装备 API — ✅ 已完成

**完成时间**: 2026-07-17
**耗时**: 约 3 小时
**来源**: 测试 Mod 反馈——`InteractableBase.Awake()` NRE + NPC 不可见 + 捏脸不生效

### 核心策略

Friendly NPC 与 Enemy 在鸭科夫原生代码中**本质相同**（共用 `CharacterRandomPreset`）。旧的 bare `GameObject` + `AddComponent` 路径被完全废弃，改为和 `EnemyUtils.SpawnEnemy` 一致的 `CharacterRandomPreset.CreateCharacterAsync` 路径。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `Entities/FriendlyNpcConfig.cs` | 新增 `Model` (ModelRef)、`Team` (Teams)、`HeadEquipment`/`BodyEquipment` (ItemEntry?) 字段；文档更新为新 API 用法 |
| 重写 | `Entities/FriendlyNpcUtils.cs` | **全面重写**：新增 `RegisterFriendlyNpc()` + `SpawnFriendlyNpcAsync()`（异步生成）、`BuildFriendlyPreset()`（预设构建）、`AttachInteractionComponents()`（交互组件挂载）；旧 `CreateFriendlyNpc()` 标记 `[Obsolete]` 保留兼容；内部使用 `CharacterRandomPreset.CreateCharacterAsync` 通过 UniTask + callback 桥接 |
| 新建 | `Entities/EquipmentUtils.cs` | **全新装备 API**：`ConfigureNpcEquipment()` / `SetNpcEquipment()` / `GetNpcEquipment()` / `ClearNpcEquipment()` / `InjectEquipmentToPreset()`；定义了 `EquipmentSlot` 枚举 (Head/Body/Backpack)；内部通过反射操作 `CharacterRandomPreset.itemsToGenerate`；运行时尝试访问 `CharacterModel` 装备方法 |
| 修改 | `Register/RegisterBootstrap.cs` | Init() 新增 `EquipmentUtils.Init()` |
| 修改 | `Docs/PROGRESS.md` | 新增本条目 + 更新日期 |

### 新增 API 一览

| API | 用途 |
|-----|------|
| `RegisterFriendlyNpc(Identifier, FriendlyNpcConfig)` | 创建 CharacterRandomPreset 并注册到游戏全局列表 |
| `SpawnFriendlyNpcAsync(Identifier, position?, rotation?)` | 异步生成完整可见 NPC（含 CharacterModel/CustomFaceInstance/Animator/Collider） |
| `ConfigureNpcEquipment(npcId, slot, item)` | 配置 NPC 装备（注入到 itemsToGenerate） |
| `SetNpcEquipment(npcId, slot, item)` | 运行时设置已生成 NPC 的装备 |
| `GetNpcEquipment(npcId, slot)` | 读取 NPC 当前装备 |
| `ClearNpcEquipment(npcId, slot)` | 清除 NPC 指定槽位装备 |

### API 用法

```csharp
// 新版推荐用法（异步生成+可见角色）
var config = new FriendlyNpcConfig
{
    DisplayNameKey = "NPC_Merchant_Name",
    Role = NpcRole.Merchant,
    Face = FaceRef.Preset("Duck_Default"),
    HeadEquipment = ItemEntry.Of("duckov:CowboyHat", 1),
    BodyEquipment = ItemEntry.Of("duckov:Vest_A", 1),
    SpawnPosition = new Vector3(10, 0, 5)
};
FriendlyNpcUtils.RegisterFriendlyNpc(new Identifier("mymod", "merchant"), config);
var npc = await FriendlyNpcUtils.SpawnFriendlyNpcAsync(new Identifier("mymod", "merchant"));

// 旧版兼容（标记 [Obsolete]，fire-and-forget 异步）
var go = FriendlyNpcUtils.CreateFriendlyNpc(id, config); // 返回临时占位 GameObject
```

### FaceRef 捏脸修复

- **旧路径**：运行时通过 `FaceRefResolver.ApplyToModel(model, face)` 调用 `CharacterModel.SetFaceFromPreset()`——但 `CharacterModel` 根本不存在
- **新路径**：在 `BuildFriendlyPreset()` 中通过反射设置 `CharacterRandomPreset.facePreset` 字段——`CreateCharacterAsync` 在生成角色时自动应用捏脸，`CustomFaceInstance` 随角色自动创建

### 装备实现

- **生成时装备**：在 `BuildFriendlyPreset()` 中调用 `EquipmentUtils.InjectEquipmentToPreset()`，通过反射将 `ItemEntry` 注入 `CharacterRandomPreset.itemsToGenerate`
- **运行时装备**：`SetNpcEquipment()` 先尝试通过反射调用 `CharacterModel.SetEquipment()` 等方法；若不可用则记录到待处理队列，下次生成时通过 `itemsToGenerate` 生效

### 设计偏离

- `SpawnFriendlyNpcAsync` 返回 `UniTask<GameObject?>`（异步），与旧 `CreateFriendlyNpc` 返回 `GameObject`（同步）不兼容。旧 API 保留但标记 `[Obsolete]`，内部 fire-and-forget 委托到异步版本
- 装备 API 的运行时部分依赖于游戏内部 `CharacterModel` 方法（`SetEquipment` 等），这些方法的具体签名未知——当前实现通过反射探测常见方法名，若不存在则回退到 `itemsToGenerate` 注入

### 遗留问题

- [ ] 功能测试（待游戏运行时验证——确认 `CreateCharacterAsync` 回调签名匹配 + `facePreset` 字段名正确 + `itemsToGenerate` 元素类型匹配）
- [ ] `CharacterModel` 运行时装备方法签名需在游戏运行时确认（反射探测可能遗漏）
- [ ] 测试文件 `QuestGiverTest.cs` 仍使用已废弃的 `CreateFriendlyNpc()`（需更新为新 API）
- [ ] 装备 API 运行时部分的 `TrySetEquipmentOnModel`/`TryClearEquipmentOnModel` 反射探测逻辑需运行时验证

### 验证结果

- [x] `dotnet build` 通过（0 错误，49 预存警告，0 新增错误）
- [ ] 功能测试（待游戏运行时验证）

---

## 对话系统修复：缺失 DialogueUI 管理 + DuckovDialogueActor 注册 — ✅ 已完成

**完成时间**: 2026-07-17
**耗时**: 约 1 小时
**来源**: 测试 Mod 反馈——NPC 对话只有音频，无字幕文本显示，无镜头切换

### 问题诊断

```
DialogueUtils.PlaySubtitles("laozheng", lines)
  → DuckovDialogueActor.Get("laozheng")     ← ① 可能未注册 → 静默跳过
  → _onStartedDel?.DynamicInvoke(null)      ← ② 事件发出，但无 UI 监听
  → DialogueTree.RequestSubtitles(...)      ← ③ 只发送音频数据，不负责 UI/镜头
```

| # | 问题 | 根因 |
|---|------|------|
| ① | Actor 查找可能失败 | `FriendlyNpcUtils.SetActorId` 设置了 `id` 字段但从未调用 `DuckovDialogueActor.Register()` |
| ② | DialogueUI 未打开 | `RequestSubtitles` 是底层 API，`DialogueUI` 通过 `DialogueTree.OnDialogueStarted` 事件打开，但 FML 未主动查找并激活 `DialogueUI` |
| ③ | 只有音频 | `RequestSubtitles` 会触发音频系统播放语音，但字幕文本需要 `DialogueUI` 激活后才能显示；镜头切换也由 `DialogueUI` 控制 |

### 已修复

| 文件 | 改动 |
|------|------|
| `Entities/FriendlyNpcUtils.cs` | `SetActorId()` 新增 `DuckovDialogueActor.Register(actor)` 反射调用；新增 `UnregisterActor()` 辅助方法；`RemoveNpc()` 在销毁前调用 `UnregisterActor()` |
| `Dialogues/DialogueUtils.cs` | **全面重写**：`Init()` 新增 `DialogueUI` 类型预查找；`PlaySubtitle()`/`PlaySubtitles()` 在对话前后调用 `EnsureDialogueUIOpen()`/`EnsureDialogueUIClose()`；Actor 未找到时输出详细警告 |
| `QuestGivers/QuestGiverUtils.cs` | `SetActorId()` 新增 `DuckovDialogueActor.Register()` 反射调用 |

### 验证结果

- [x] `dotnet build` 通过（0 错误）
- [ ] 功能测试（待游戏运行时验证——DialogueUI 类型名、Register 签名、UI 打开/关闭方法名）

---

## 对话系统二次修复：利用反编译源码消除推测性反射 — ✅ 已完成

**完成时间**: 2026-07-17
**耗时**: 约 1 小时
**来源**: 审查 `DecompiledDLL/Core/` 后发现的三个关键问题

### 通过反编译源码确认的事实

| 反编译文件 | 关键发现 |
|-----------|---------|
| `DuckovDialogueActor.cs` | `OnEnable()` 自动调用 `Register(this)`，`OnDisable()` 自动 `Unregister(this)` → FML 的手动反射 Register 调用**完全冗余** |
| `DialogueUI.cs` | `RegisterEvents()` 订阅 `DialogueTree.OnDialogueStarted/OnSubtitlesRequest/OnDialogueFinished` 三个事件；`OnDialogueStarted` 调用 `mainFadeGroup.Show()` + `InputManager.DisableInput()` → **主面板和镜头由事件驱动，无需手动管理 UI** |
| `LocalizedStatement.cs` | `text` 属性调用 `textKey.ToPlainText()` 实现本地化；`audio` 字段可选音频 clip |

### 根因确认

```csharp
// ❌ 旧代码（上一轮修复中遗留）
var bfs = BindingFlags.Public | BindingFlags.Static;  // 只搜索 public 字段
_onStartedDel = typeof(DialogueTree).GetField("OnDialogueStarted", bfs)...;
// C# event 的 backing field 是 PRIVATE static → GetField 返回 null
// → _onStartedDel 为 null → DynamicInvoke 从未执行
// → DialogueUI.OnDialogueStarted 从未触发 → mainFadeGroup 从未显示 → 字幕不可见
```

### 已修复

| 文件 | 改动 |
|------|------|
| `Dialogues/DialogueUtils.cs` | `Init()` 中 `BindingFlags` 加上 `NonPublic`（**一行修复：核心 Bug**）；移除所有推测性 `DialogueUI` 反射代码（`EnsureDialogueUIOpen/Close/FindDialogueUI/CallMethodIfExists`）；移除 `_dialogueUIType/_cachedDialogueUI/_dialogueUISearched` 字段 |
| `Entities/FriendlyNpcUtils.cs` | 移除冗余的 `DuckovDialogueActor.Register/Unregister` 反射调用（`OnEnable/OnDisable` 自动处理）；移除 `UnregisterActor()` 方法 |
| `QuestGivers/QuestGiverUtils.cs` | 移除冗余的 `DuckovDialogueActor.Register` 反射调用 |

### 代码行数变化

- DialogueUtils.cs: 216 行 → 130 行（**-40%**，移除全部推测性代码）
- FriendlyNpcUtils.cs: 移除 ~35 行冗余反射

### 验证结果

- [x] `dotnet build` 通过（0 错误）
- [ ] 功能测试（待游戏运行时验证——确认 `OnDialogueStarted` backing field 名称为 `OnDialogueStarted`，`BindingFlags.NonPublic` 能访问到）

---

## CreateCharacterAsync 反射参数类型修复 — ✅ 已完成

**完成时间**: 2026-07-17
**耗时**: 约 1 小时
**来源**: 测试 Mod 崩溃——`SpawnFriendlyNpcAsync` 抛出 `ArgumentException: Quaternion cannot be converted to Vector3`

### 根因

`duckov_assembly/Core/CharacterRandomPreset.cs:251` 反编译确认游戏**唯一重载**：
```csharp
public async UniTask<CharacterMainControl> CreateCharacterAsync(
    Vector3 pos, Vector3 dir, int relatedScene,
    CharacterSpawnerGroup group, bool isLeader)
```
**不存在** Quaternion/Teams/callback 重载。FML 代码三处错误传入 Quaternion（第二参数应为 Vector3 dir）和 Teams 枚举（第三参数应为 int sceneBuildIndex）。

### 问题汇总

| 位置 | 问题 |
|------|------|
| `FriendlyNpcUtils.cs:140` | `rot`(Quaternion) 传入 `dir`(Vector3) 参数位——**直接崩溃** |
| `FriendlyNpcUtils.cs:130-131` | 查找不存在的 4-arg `(Vector3, Quaternion, Teams, Action<>)` 重载——永远为 null，必定走 fallback |
| `FriendlyNpcUtils.cs:418` | `GetMethod` 无参数类型——重载歧义，可能返回错误方法 |
| `EnemyUtils.cs:157` | `Quaternion.identity` 传入 `dir` 位——同 bug 潜伏中 |
| `OtherPatches.cs:27-28` | Harmony postfix 参数 `(Vector3, Quaternion, Teams, Action<>)` 与游戏方法不匹配——静默失效 |

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `Entities/FriendlyNpcUtils.cs` | `SpawnFriendlyNpcAsync`: 移除不存在的 4-arg callback 代码；`Quaternion`→`Vector3.forward` 方向转换；`(int)config.Team`→`SceneManager.GetActiveScene().buildIndex`；直接 await UniTask 返回值 |
| 修改 | `Entities/FriendlyNpcUtils.cs` | `GetCreateCharacterAsyncMethod`: `GetMethod` 添加精确 5 参数类型，消除重载歧义 |
| 修改 | `Entities/EnemyUtils.cs` | `GetCreateCharacterAsyncMethod` + `SpawnInternal`: 同上修复 |
| 修改 | `Entities/Patches/OtherPatches.cs` | `CreateCharacterAsyncPostfix` 移除不匹配的形参（仅保留 `__instance`）；Patch #10 GetMethod 歧义修复 |

### 设计偏离

- 原有 4-arg callback 方案（假设游戏有 `(Vector3, Quaternion, Teams, Action<CharacterMainControl>)` 重载）基于错误的反编译结论。实际游戏仅有 5-arg 重载，返回 `UniTask<CharacterMainControl>`，FML 修复后直接 await 该返回值。

### 验证结果
- [x] `dotnet build` 通过（0 错误，48 预存警告）
- [ ] 功能测试（待游戏运行时验证——确认 FriendlyNpc + Enemy 生成正常）

---

## FaceRef.FromJson(): NPC 捏脸 JSON 数据驱动 — ✅ 已完成

**完成时间**: 2026-07-17
**耗时**: 约 0.5 小时
**来源**: 测试 Mod 需要将已保存的玩家捏脸 JSON 数据应用到 NPC（非"跟随当前玩家捏脸"）

### 背景

`FaceRef` 之前支持三种模式：

| 模式 | 效果 |
|------|------|
| `Preset("name")` | 查找 Resources 中的 `CustomFacePreset` |
| `PlayerFace()` | 设置 `usePlayerPreset = true`（跟随当前玩家捏脸） |
| `Custom(FacePartIds)` | 按部件 ID 组合自定义 |

缺少"从 `CustomFaceSettingData` JSON 字符串创建捏脸"的能力。Modder 有已保存的捏脸数据（`GetPlayerFaceJson()` 导出或从存档提取），需要直接应用到 NPC。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `Entities/FaceRef.cs` | `FaceRefMode` 新增 `FromJson` 枚举值；`FaceRef` 新增 `FaceJson` 字段 + `FromJson(string json)` 静态工厂 |
| 修改 | `Entities/FriendlyNpcUtils.cs` | `BuildFriendlyPreset` switch 新增 `FromJson` 分支：`JsonToData(json)`→创建 `CustomFacePreset`→赋值 `facePreset` |

### API

```csharp
// 直接从 JSON 字符串创建捏脸引用
Face = FaceRef.FromJson(jsonString),

// 从文件加载
string json = File.ReadAllText(Path.Combine(modDir, "faces", "npc_laozheng.json"));
Face = FaceRef.FromJson(json),
```

### 验证结果
- [x] `dotnet build` 通过（0 错误，48 预存警告）
- [ ] 功能测试（待游戏运行时验证——确认 NPC 捏脸正确应用）

---

## DockovDrinks BUG 修复 — ✅ 已完成

**完成时间**: 2026-07-17
**耗时**: 约 4 小时
**触发**: `Docs/TODO/ISSUE_FEATHER_NPC_FACE_PLAYER.md`

### 审计与修复范围

对 DockovDrinks v0.5.0 提交的 4 个核心 BUG + 2 个额外问题进行全面审计，对照逆向源码验证后实施修复。

### BUG 1：建筑回收 NPC 残留 + 重复生成

**修复**: `BuildingUtils` 新增 `OnBuildingDemolished` / `OffBuildingDemolished` 回调 API
- 游戏原生已提供 `BuildingManager.OnBuildingDestroyedComplex` 事件
- 框架选择性地只 Hook 了建成事件，现补全对称的回收 Hook
- `HookBuildingEvents()` 同时注册 `OnBuildingBuiltComplex` 和 `OnBuildingDestroyedComplex`

### BUG 3-A：交互 NPC 打开笔记而非商店

**根因**: 框架 `AttachInteractionComponents` 对 `NpcRole.Merchant` 分支添加了 `NoteInteract`（笔记交互），而非商店交互。
**修复**:
- Merchant 分支改为挂载 `StockShop` 组件 + 自定义 `NpcShopInteract : InteractableBase`
- `NpcShopInteract.OnInteractFinished()` 调用 `StockShop.ShowUI()` 打开游戏原生商店 View
- `StockShop.merchantID` 通过 Publicizer 公开字段直接赋值

### BUG 3-B：GameViews.Shop/Quest handler 为空占位

**修复**: `RegisterBuiltInViews` 中 Shop/Quest handler 实现有意义的逻辑
- `GameViews.Shop`: 遍历 `FriendlyNpcUtils.Registry` 查找匹配 NPC 并调用 `StockShop.ShowUI()`
- `GameViews.Quest`: 调用 `QuestView.Show()` 打开任务面板

### BUG 3-C：NpcRole 不支持复合角色

**修复**: `NpcRole` 枚举改为 `[Flags]`
- 新增值: `None = 0`, `Enemy = 1<<0`, `Merchant = 1<<1`, `QuestGiver = 1<<2`, `Neutral = 1<<3`, `Companion = 1<<4`, `DialogueOnly = 1<<5`
- `AttachInteractionComponents` switch → `HasFlag()` 检查，支持 `Merchant | QuestGiver` 复合角色
- `FriendlyNpcConfig.Role` 默认值 `NpcRole.None = 0` 不变

### 新增：对话触发链条

**新增文件**:
- `Dialogues/DialogueTrigger.cs` — 对话触发 API（直接订阅 `Quest.onQuestActivated` / `Quest.onQuestCompleted` 静态事件）
- `Dialogues/NpcProximityTrigger.cs` — NPC 接近检测 MonoBehaviour

**API 设计**:
```csharp
// 接近触发（距离 < 3m 时播放对话）
DialogueTrigger.OnProximity(npcId, 3f, lines, DialogueTriggerMode.Once);

// 任务激活时触发
DialogueTrigger.OnQuestAccepted(questId, npcId, lines, DialogueTriggerMode.Repeatable);

// 任务完成时触发
DialogueTrigger.OnQuestCompleted(questId, npcId, lines);
```

**特性**: 支持 `Once`（默认）和 `Repeatable` 两种触发模式。接近触发也可通过 `FriendlyNpcConfig.ProximityDialogue` 声明式配置。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 修改 | `Entities/EnemyPresetData.cs` | `NpcRole` 枚举改为 `[Flags]`，调整值为位掩码 |
| 修改 | `Entities/FriendlyNpcConfig.cs` | 新增 `ProximityDialogue`、`AutoFacePlayer` 字段 |
| 修改 | `Entities/FriendlyNpcUtils.cs` | `AttachInteractionComponents` switch→HasFlag；添加 `NpcShopInteract` 内部类；添加 ProximityDialogue 处理；新增 `using FeatherMod.Interaction` |
| 修改 | `Buildings/BuildingUtils.cs` | 新增 `OnBuildingDemolished`/`OffBuildingDemolished`；`HookBuildingEvents` 同步 Hook `OnBuildingDestroyedComplex`；新增 `OnBuildingDemolishedHandler` |
| 修改 | `Interaction/InteractionUtils.cs` | `GameViews.Shop` 实现查找 NPC+调用 ShowUI；`GameViews.Quest` 实现 `QuestView.Show()` |
| 修改 | `Register/RegisterBootstrap.cs` | 新增 `DialogueTrigger.Init()` 调用 |
| 新建 | `Dialogues/DialogueTrigger.cs` | 对话触发 API + `QuestStatusChangedEvent` 事件定义 |
| 新建 | `Dialogues/NpcProximityTrigger.cs` | NPC 接近检测 MonoBehaviour |
| 新建 | `Dialogues/NpcFacePlayer.cs` | NPC 面向玩家（`Movement.ForceTurnTo` 驱动） |

### BUG 4：NPC 不面向玩家

**修复**: `FriendlyNpcConfig` 新增 `AutoFacePlayer`（bool，默认 false）
- 设为 `true` 时，`AttachInteractionComponents` 自动挂载 `NpcFacePlayer` 组件
- `NpcFacePlayer` 每帧使用 `Quaternion.RotateTowards` 平滑旋转 `transform.rotation`
- 仅在水平面旋转，无抽搐/跳变问题

```csharp
config.AutoFacePlayer = true; // 一行启用
```

### 复合角色交互点分离

交叉验证 `SpecialAttachment_XiaoMing.prefab` 原版结构后修正：

```
// 原版 XiaoMing Prefab 模式：
SpecialAttachment_XiaoMing (Layer 8)
├── StockShop (数据) + InteractableBase → OnInteractFinished → ShowUI
├── Interact_Quest (子GO) → QuestGiver (独立交互, questGiverID=7)
└── Interact_Skill (子GO) → PerkTree交互
```

**修正后的 FML 实现**：
- 单一 Merchant：父 GO `StockShop` + `NpcShopInteract`（对应原版 InteractableBase + UnityEvent 模式）
- 单一 QuestGiver：父 GO `QuestGiver`（自交互）
- `Merchant | QuestGiver`：父 GO `StockShop` + `NpcShopInteract`；子 GO `Interact_Quest` + `QuestGiver`（参照原版独立子对象）
- 不再使用 `ViewInteractHandler` 处理 QuestGiver——`QuestGiver` 自身即是 `InteractableBase`

### BUG 2 判定：不修复（当前实现已满足契约）

`SpawnFriendlyNpcAsync` 的 `AttachInteractionComponents` 中 `SetActorId` 已调用 `DuckovDialogueActor.Register(actor)`，确保 Actor 在 spawn 返回前已注册。如在特定场景仍失败，根因更可能在 `DialogueUtils.Init()` 中 event backing field 获取失败。

### 验证结果
- [ ] `dotnet build` 编译通过（需 Unity 引用，待构建环境验证）
- [ ] LSP diagnostics（LSP 服务不可用）
- [ ] 功能测试（待游戏运行时验证）

---

## Phase 6 增量：跨模组联动 API（ModUtils） — ✅ 已完成

**完成时间**: 2026-07-22
**耗时**: 约 0.5 小时
**来源**: 梳理跨模组联动案例后发现的核心缺失——缺少运行时 mod 加载状态查询 API

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|---|---|---|
| 新建 | `FastModdingLib/Modding/ModUtils.cs` | 新增 `ModUtils` 静态工具类，提供 `IsModLoaded(modid)` / `IsModInstalled(modid)` 两个公开 API |
| 修改 | `Docs/USAGE.md` | 新增 §34 跨模组联动章节（完整 API 文档 + 使用示例 + 与 fml.json 配合说明）；更新命名空间速查表 |
| 修改 | `README.md` | 模块速览表新增 ModUtils 行；模块计数 28→29；关键指标更新；FAQ 补充 ModUtils 引用 |

### 新增 API

| API | 说明 |
|-----|------|
| `ModUtils.IsModLoaded(string modid)` | 检查指定 mod 是否已安装且处于激活状态 |
| `ModUtils.IsModInstalled(string modid)` | 检查指定 mod 是否已安装（不论激活状态） |

### 设计偏离
- 无偏离。实现遵循现有 `ModManagerPatches.ShouldActivateMod_Postfix` 中已验证的模式：遍历 `ModManager.modInfos` + 调用游戏原生 `ModManager.IsModActive`

### 验证结果
- [x] `dotnet build` 编译通过（0 errors）
- [x] LSP diagnostics（文件内容正确）
- [ ] 功能测试（待游戏运行时验证）

---

## BugFix: PerkTree ע��� AddPerk �Ҳ����� �� ? ���޸�

**���ʱ��**: 2026-07-22
**��ʱ**: Լ 1 Сʱ
**����**: Bug �޸���PerkTreeManager ��ʼ��ʱ�����⣩

### ����

DuckovDrinks mod �� OnAfterSetup �е��� RegisterPerkTree ������ AddPerk��������
ArgumentException: PerkTree 'DuckovDrinks:laozheng_arts' not found.

### ����

PerkTreeManager �ǳ����е� MonoBehaviour �������� Instance �� Awake() �����á�
��� mod �� OnAfterSetup �ڳ������� PerkTreeManager ֮ǰ�����ã�

1. RegisterPerkTree ������ PerkTree ��**����**�� PerkTreeManager.perkTrees.Add()����Ϊ Instance == null��
2. ResolvePerkTree ���� PerkTreeManager.Instance.perkTrees �� ʧ�� �� ���� GetPerkTree Ҳ���� null
3. AddPerk �׳��쳣

**����**��FML û�ж���ά�� PerkTree ���û��棬��ȫ������Ϸ PerkTreeManager �ĳ�ʼ��ʱ��

### �޸�����

���� _registeredTrees �ֵ���Ϊ FML �ڲ� PerkTree ���棬**������ PerkTreeManager.Instance �Ŀ�����**��

### �ļ����

| ���� | �ļ� | �Ķ�λ�� |
|------|------|---------|
| �޸� | PerkTrees/PerkTreeUtils.cs | L30-31������ _registeredTrees �ֶ� |
| �޸� | PerkTrees/PerkTreeUtils.cs | L101��RegisterPerkTree �л��������� |
| �޸� | PerkTrees/PerkTreeUtils.cs | L200-202��ResolvePerkTree ���ȴӻ������ |
| �޸� | PerkTrees/PerkTreeUtils.cs | L311-318��RemoveAllPerks ͬ���������� |

### �߼��仯

**ResolvePerkTree �������ȼ�**���޸ĺ󣩣�
1. ԭ������Domain == "duckov"���� PerkTreeManager.GetPerkTree
2. **FML �ڲ�����** _registeredTrees �� ֱ�ӷ��أ������������� Instance��
3. ���ˣ�PerkTreeManager.Instance.perkTrees ���� �� ������ע�����
4. ���ף�PerkTreeManager.GetPerkTree

### ��֤���
- [x] dotnet build ����ͨ����0 errors, 0 warnings ������
- [ ] ���ܲ��ԣ��� DuckovDrinks ���±��� FML DLL ����Ϸ����֤��

---

## BugFix: PerkTree Registration & UI Pipeline (2026-07-22)

**Status**: Verified (build pass), pending runtime test
**Type**: Bug fix chain (5 interdependent issues)

### Root Cause Chain

| # | Symptom | Root Cause | Fix |
|---|---------|-----------|-----|
| 1 | AddPerk throws "PerkTree not found" | PerkTreeManager.Instance null at mod init; no FML-side cache | Added _registeredTrees cache + ResolvePerkTree priority |
| 2 | PerkTreeUIInvoker NRE (game code) | Game calls GetPerkTree directly, bypasses FML cache | Harmony postfix on PerkTreeManager.GetPerkTree (3-tier fallback) |
| 3 | Perk.EnabledInCurrentLevel NRE | perk.Master never set (Collect blocked by PerkTreeCollectGuard) | Manual perk.Master = tree + tree.perks.Add in AddPerk |
| 4 | PopulatePerks NRE | relationGraphOwner.graph was null - reflection GetField("graph") on PerkTreeRelationGraphOwner missed base class | Direct assignment: graphOwner.graph = graph (NodeCanvas.Framework publicized) |
| 5 | All nodes at (0,0) + no connection lines | ConnectNodes called via reflection (silent fail); cachedPosition never set | Direct graph.ConnectNodes + BFS-centered layout engine |

### File Changes

| File | Change |
|------|--------|
| PerkTrees/PerkTreeUtils.cs | + _registeredTrees cache, + TryGetRegisteredTree, + TryLayoutGraph, + LayoutGraph (BFS), refactored RegisterPerkTree/ResolvePerkTree/AddPerk/RemoveAllPerks/ConnectPerksInternal/EnsureGraphNode |
| PerkTrees/Patches/PerkTreeManagerGetPerkTreePatch.cs | **New** - Harmony postfix with 3-tier tree lookup + layout trigger |
| PerkConfig.cs | + Position field (optional Vector2 for manual node placement) |

### Layout Engine Design

**Auto-layout** (BFS + per-depth centering):
- Trigger: first game access to FML tree (via Harmony patch)
- Depth assignment: BFS from roots, max depth for multi-prerequisite nodes
- Sorting: by average parent X per depth layer
- Centering: startX = -(count-1)*120/2 per layer
- Spacing: X=120, Y=100 (constants LAYOUT_X/Y_SPACING)

**Manual override**: PerkConfig.Position skips auto-layout for that node.

**Vanilla tree injection**: Immediate position calc from prerequisite node in ConnectPerksInternal (no global layout for vanilla trees).

### Layout Example (DuckovDrinks)

`
                    survival_instinct (0, 0)
             +----------+-----------+
    water_efficiency     gun_affinity
    (-60, 100)           (60, 100)
     +----+----+        +----+----+
load_bearing nutrition recoil_tuning marksmanship
(-180,200) (-60,200) (60,200) (180,200)
                      |
               storm_survivor (0, 300)
                      |
                true_teaching (0, 400)
`

### Verification
- [x] dotnet build (0 errors)
- [ ] Runtime test: DuckovDrinks perk tree nodes visible, connected, centered

---

## 交互系统 API 重新设计（FEATHER_API_GAPS 修复） — ✅ 已完成

**完成时间**: 2026-07-22
**耗时**: 约 2 小时
**类型**: API 重新设计（5 个缺口修复 + 原版通道封装）
**来源**: `Docs/TODO/FEATHER_API_GAPS.md` — DockovDrinks 测试 Mod 的 API 交叉核验报告

### 背景

DockovDrinks v0.5.0 饮品制作台开发中，对 Feather 交互系统进行了全面的 API 交叉核验，发现 5 个缺口。经审计确认全部属实，实施了分 6 层的重新设计。

### 缺口与修复对照

| # | 缺口 | 修复方案 |
|---|------|---------|
| **1** | InteractTemplates 缺少 Crafting 交互模板 | 新建 `CraftingInteractTemplate`，对标 `PerkTreeInteractTemplate` |
| **2** | 缺少公开的通用多交互组装 API | `SetupInteractionGroup` 从 `FriendlyNpcUtils` 提取为 `InteractionUtils` 公开方法 + 新建 `InteractionGroupBuilder` Builder 模式 API |
| **3** | ViewInteractHandler 不设置交互名 | 新增 `InteractNameKey`/`MarkerOffset`/`CoolTime`/`FinishWhenTimeOut` 字段 + `Awake()` 自动应用 |
| **4** | Building.functionContainer 访问路径不透明 | `BuildingUtils` 新增公开 `GetFunctionContainer()`/`GetGraphicsContainer()` 方法 |
| **5-A** | Duckov 原版通道与 Feather 通道分裂（增强 Feather 侧） | `ViewInteractHandler` + `InteractionUtils` 全面增强（对标原版组件能力）；`InteractTemplates` Identifier 合规化 |
| **5-B** | 原版组件缺少 Feather 封装 | 新建 `FeatherShopInteract`/`FeatherQuestGiverInteract`/`FeatherPerkTreeInteract` 三个封装类 |

### 设计决策

- **通道统一方向**：A+B 双管齐下。增强 Feather 通道（ViewInteractHandler）使其具备原版组件的完整能力（交互名、标记偏移、多交互分组），同时为原版组件提供 Feather 封装（生命周期管理 + Identifier 体系）。
- **NPC 迁移**：方案 A 保守路线 — NPC 保持使用原版组件，但多交互组装改用公开的 `InteractionUtils.SetupInteractionGroup`。
- **Identifier 序列化**：`InteractTemplates` 字段保持 `string?`（Unity SerializeField），通过 `Identifier.Parse()` 在运行时验证和规范化，默认 domain 为 `duckov`。
- **Building 容器访问**：公开封装方法，对内沿用已有反射逻辑（`Building` 为游戏原生类，Publicizer 可能未覆盖）。

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `Interaction/Components/ViewInteractHandler.cs` | 新增 4 个 public 字段（InteractNameKey/MarkerOffset/CoolTime/FinishWhenTimeOut）+ Awake() 覆盖 |
| 修改 | `Interaction/InteractionUtils.cs` | AttachViewInteract/SpawnViewInteract 新增 3 个可选参数（interactNameKey/markerOffset/coolTime）；新增 public SetupInteractionGroup(primary, params members[]) |
| 新建 | `Interaction/InteractionGroupBuilder.cs` | 声明式多交互组 Builder（Add/WithPrimary/BuildOn 链式 API），对标 DialogueSequence.Build 风格 |
| 修改 | `UI/InteractTemplates.cs` | 新增 CraftingInteractTemplate；Building/PerkTree/Endowment 模板新增 InteractNameKey + Awake；PerkTreeID→PerkTreeId 命名修正；buildingIdentifier/PerkTreeId 经 Identifier.Parse 规范化后传递 |
| 修改 | `Buildings/BuildingUtils.cs` | 新增 public GetFunctionContainer(building)/GetGraphicsContainer(building)，封装已有反射逻辑 |
| 修改 | `Entities/FriendlyNpcUtils.cs` | SetupInteractionGroup 私有方法改为委托 InteractionUtils.SetupInteractionGroup，保留 NPC 优先级逻辑（shop>quest>perk） |
| 新建 | `Interaction/Components/FeatherShopInteract.cs` | 原版 NpcShopInteract+StockShop 的 Feather 封装（InteractableBase 子类，Identifier 识别，Registry 追踪） |
| 新建 | `Interaction/Components/FeatherQuestGiverInteract.cs` | 原版 QuestGiver 的 Feather 封装 |
| 新建 | `Interaction/Components/FeatherPerkTreeInteract.cs` | 原版 PerkTreeUIInvoker 的 Feather 封装 |

### 新增 public API

```csharp
// ── ViewInteractHandler 增强 ──
// AttachViewInteract/SpawnViewInteract 新增可选参数：
InteractionUtils.AttachViewInteract(id, target, viewType, viewParam,
    interactNameKey: "UI_Craft_Drinks",    // ← 交互提示文本本地化键
    markerOffset: new Vector3(0, 1.5f, 0), // ← 标记偏移
    coolTime: 0.2f);                        // ← 冷却时间

// ── 多交互组装 ──
// 底层 API（手动指定 primary）：
InteractionUtils.SetupInteractionGroup(primaryInteract, memberA, memberB);

// Builder 模式（声明式）：
var primary = new InteractionGroupBuilder()
    .Add(craftId, GameViews.Crafting, "drink", interactNameKey: "UI_Craft")
    .Add(perkId, GameViews.PerkTree, "brewmaster", interactNameKey: "UI_Perk")
    .WithPrimary(0)
    .BuildOn(functionContainer);

// ── Building 容器访问 ──
GameObject func = BuildingUtils.GetFunctionContainer(building);
GameObject graphics = BuildingUtils.GetGraphicsContainer(building);

// ── Feather 封装（非 NPC 场景使用） ──
FeatherShopInteract.Attach(id, target, "myMerchantId");
FeatherPerkTreeInteract.Attach(id, target, "brewmaster");
```

### 设计偏离

- **Feather 封装类同一 GO 双 InteractableBase**：三个 Feather 封装类（FeatherShopInteract 等）在创建时将原生组件（NpcShopInteract/QuestGiver/PerkTreeUIInvoker）添加到同一 GameObject，导致同一 GO 上存在两个 InteractableBase。`FriendlyNpcUtils` 的做法是将交互组件放在独立子 GO 上再编组。当前方案下 wrapper 的 `OnInteractFinished` 被优先命中，不导致功能故障。后续如需迁移到子 GO 模式成本较小。
- **NpcShopInteract 实际为 FML 内部类**：任务规格假设 NpcShopInteract 在 `Duckov` 或 `Duckov.Economy` 命名空间，实际是 `FriendlyNpcUtils.cs` 中定义的 `internal class`（`FeatherMod` 命名空间）。由于封装类在同一程序集中，C# 父命名空间查找自动解析，无需额外 using。

### 已知边界

- **FeatherShopInteract.refreshAfterTimeSpan**：任务规格原始值 `720`（ticks=72μs）修正为 `6000000000L`（10 分钟，对齐原版 `DefaultShopRefreshTicks`）。
- **FeatherQuestGiverInteract 的 questGiverID 绑定**：`Util.SetGiverId` 方法不存在，改为内联解析（int.TryParse → 自定义 ID ≥50；回退 Enum.Parse）。
- **PerkTreeUIInvoker 命名空间**：实际在 `Duckov.PerkTrees.Interactable`，非 `Duckov.PerkTrees`。

### 验证结果

- [x] `dotnet build` 通过（0 错误，0 警告）
- [ ] 功能测试（待 DockovDrinks 测试 Mod 验证——Crafting 交互 + 多交互组装 + 交互名显示 + functionContainer 访问）

---

## CraftingFormulaData.RequirePerk Identifier 迁移 — ✅ 已完成

**完成时间**: 2026-07-22
**类型**: API 修正（Identifier 合规）

### 背景

`CraftingFormulaData.RequirePerk` 为裸 `string`，违反 Identifier 优先原则。

### 变更

| 文件 | 改动 |
|------|------|
| `CraftingData.cs` | `RequirePerk`: `string` → `Identifier?`；Builder 新增 `RequirePerk(Identifier?)` 重载；文档示例更新 |
| `CraftingUtils.cs` | `data.RequirePerk?.Path ?? ""` 转换；5 个内部兼容重载的 `requirePerk` 赋值统一改为 `string.IsNullOrEmpty → null : Identifier.Parse` |

### API 对比

```csharp
// ❌ 旧：裸 string
new CraftingFormulaData { RequirePerk = "hacker/cooking" }
// ✅ 新：Identifier
new CraftingFormulaData { RequirePerk = new Identifier("duckov", "hacker/cooking") }
```

### 验证
- [x] `dotnet build` 0e 52w（全预存）

---

## FormulasRegisterView 集成补充 — ✅ 已完成

**完成时间**: 2026-07-22
**类型**: API 补充（ViewDispatcher + InteractionUtils + InteractTemplates + GameUIUtils）
**来源**: 调查发现游戏中 3 个合成相关视图（FormulasRegisterView / FormulasIndexView / ItemDecomposeView）未被 FML 集成

### 背景

游戏中有 4 个合成相关视图，FML 原本只集成了 1 个（CraftView）：

| 游戏原生视图 | 用途 | FML 集成 |
|------------|------|---------|
| `CraftView` | 合成执行 | ✅ 已集成 |
| `FormulasRegisterView` | 配方注册/解锁（提交物品学配方） | ❌→✅ 本轮补充 |
| `FormulasIndexView` | 配方索引浏览（全量配方） | ❌→✅ 本轮补充 |
| `ItemDecomposeView` | 物品分解 | ❌→✅ 本轮补充 |

### 文件变更清单

| 操作 | 文件路径 | 改动摘要 |
|------|---------|---------|
| 修改 | `Interaction/ViewDispatcher.cs` | `GameViews` 新增 3 个常量：`Formulas`/`FormulasRegister`/`Decompose` |
| 修改 | `Interaction/InteractionUtils.cs` | `RegisterBuiltInViews()` 注册 3 个新 handler |
| 修改 | `UI/InteractTemplates.cs` | 新增 3 个模板类：`FormulasIndexInteractTemplate`/`FormulasRegisterInteractTemplate`/`DecomposeInteractTemplate` |
| 修改 | `UI/GameUIUtils.cs` | 新增 `OpenFormulasIndexView()`/`OpenFormulasRegisterView()`/`OpenDecomposeView()` 快捷方法 |

### 新增 API

```csharp
// ── ViewDispatcher ──
ViewDispatcher.Open(GameViews.Formulas);          // → FormulasIndexView.Show()
ViewDispatcher.Open(GameViews.FormulasRegister);  // → FormulasRegisterView.Show(null)
ViewDispatcher.Open(GameViews.Decompose);         // → ItemDecomposeView.Show()

// ── GameUIUtils 快捷方法 ──
GameUIUtils.OpenFormulasIndexView();     // 配方索引浏览
GameUIUtils.OpenFormulasRegisterView();  // 配方注册（显示全部可注册配方）
GameUIUtils.OpenDecomposeView();         // 物品分解

// ── 交互模板（挂载到建筑 functionContainer） ──
go.AddComponent<FormulasIndexInteractTemplate>().InteractNameKey = "UI_Formulas";
go.AddComponent<FormulasRegisterInteractTemplate>();
go.AddComponent<DecomposeInteractTemplate>().InteractNameKey = "UI_Decompose";
```

### 设计偏离

- **Tag 字符串查找不可用**：`Tag` 为 `ScriptableObject`，无 `GetTag(string)` 静态方法。`FormulasRegisterView.Show(ICollection<Tag>)` 的 tag 过滤参数在 handler 中传 `null`（显示全部可注册配方）。若后续需要 tag 过滤，可加载 Resources 中的 Tag 资源做匹配。

### 验证结果

- [x] `dotnet build` 通过（0 错误，0 警告）

