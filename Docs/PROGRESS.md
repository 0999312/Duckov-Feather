# 项目进度文档 (PROGRESS.md)

> 最后更新：2026-07-17

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
- [ ] **PerkTree 系统 9 处游戏数据反射**：待后续修复——`PerkTreeUtils.cs` 中通过反射访问 `PerkTreeManager.perkTrees` 等字段，Publicizer 已覆盖但未清理。

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
- [x] `dotnet build` 通过（0 错误，0 警告）
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
