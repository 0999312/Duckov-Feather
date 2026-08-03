# 更新日志

## [未发布] 2026-07-28 之后

> 自 `4f0c169`（2026-07-28）以来的全部改动。规模：65 个文件，+809 / −948。
> 核心主题：Harmony 补丁静默失效修复、反射最小化清理、Mod 加载顺序与存档健壮性增强、事件 API 强类型化、兼容性修复。

### 修复

- **Harmony 类级 `[HarmonyPatch]` 特性缺失导致补丁静默失效**
  - `Entities/Patches/OtherPatches.cs`、`PerkTrees/Patches/PerkSetupSaveDataSuppressPatch.cs` 补齐类级特性——Harmony 2.4.x 的 `PatchAll` 只扫描带类级特性的类型，此前这些补丁从未生效且不报错
- **`GetAPresetByWeight` 补丁签名错配**：原生返回 `CharacterRandomPresetInfo`（struct），旧实现错误声明 `ref CharacterRandomPreset`，FML 敌人从未进入原生随机生成器
- **NPC 进图重复生成抖动**：MainSceneLoaded/SubSceneLoaded/LevelInitialized 三事件竞态导致重复 remove→respawn，新增 `_spawning` 集合拦截进行中的生成
- **PerkTree 崩溃与误报**
  - `PruneDanglingConnections`：原生 `PerkTreeView.RefreshConnections` 对悬空连接无空检查会 NRE，UI 打开前清理
  - `TryFindFallback` 结果判空后再赋值（修复 null 覆盖）
  - `PerkTreeCollectGuard` 判定统一走 `IsFMLTree` 注册表（消除双轨不一致）
  - `IsPerkTreeAvailable` 改走 `PerkTreeManager.GetPerkTree`（消除 "not found" 误报）
- **任务/奖励类静默失效**
  - `FMLReward_UnlockBuilding` / `FMLReward_UnlockEndowment`：`Quest.onCompleted` 是 internal event，旧 `GetField` 必然失败 → 改为 `GetEvent + AddEventHandler` 标准订阅
  - `FMLTask_SubmitItemByTag`：原生 `Inventory` 无 `AllSlots` 属性，旧反射恒失败 → 直接 `inv.Content` 枚举
  - `FMLTask_KillCountByTag`：`Health.OnDead` 反射订阅 → 编译期直接 `+=`/`-=`
  - `ItemUtils.HasTag`：`ItemMetaData.Tags` 不存在，旧反射恒 false → 直接遍历 `Item.Tags`
  - `DialogueManager.SetBlackboard`：字段声明在泛型基类上，`GetField` 必失败 → 直接走 public 属性
- **`FMLReward_UnlockEndowment` 兼容性防御**：`DisplayName` 访问加 try/catch，第三方 mod（如 CustomTalentFrame）的 Harmony prefix 抛异常时回退 Identifier Path，任务 UI 不再崩溃
- **Endowment index 注册即分配**：`TryInjectToManager` 将 index 分配提前到 Instance 检查之前，`entry.index` 永不残留 `None`；`Modifiers`/`RequirementTextKey` 空值兜底
- **Mod 排序与存档健壮性**
  - `RepairModsES3IfCorrupt`：`Saves/Mods.ES3` 损坏时（原生 `Load<int>` 持续报 "Failed loading mod info."、玩家手动顺序丢失）自动探测并隔离主文件与 `.bac`，随后重建干净文件
  - `EnsureFrameworkOrder`：硬性保证 FeatherMod 排在 HarmonyLoadMod 之后（不依赖 fml.json，兼容旧版包）
  - `Reorder_Postfix` 事件触发：`GetField("OnReorder")` 改 `GetField("<OnReorder>k__BackingField")`，玩家拖拽排序后事件恢复触发
  - `PersistPriorities` 改直接调用 `ModManager.RegeneratePriorities()`（Publicizer 已公开，零反射）
- **敌人系统**
  - `SpawnEnemy` 的 `onSpawned` 回调从"永远不生效"变为真正工作；`sceneBuildIndex` 由硬编码 0 改为当前活动场景
  - `ApplyWeaponConfig` 从空死代码变为真实实现（武器池注入真正生效）
  - `CreateCustomFacePreset` 修复 struct 写回（旧实现修改装箱副本，捏脸设置从未生效）
  - `FindFacePreset` 补 `Resources.Load` 回退
- **其他**
  - `AssetUtil.LoadBundle` 补 modDir 判空；`MinigameUtil.renderTexture` 判空后再赋值
  - `FriendlyNpcUtils` 创建角色判空（`character is null`）+ `_registry` 声明时初始化

### 新功能

- **`EnemyUtils.SetAutoSpawn(id, bool)`**：显式开启的敌人延迟 3 帧自动生成（替代旧的"注册即刷怪"危险行为，默认关闭）
- **`ModBehaviour.OnAfterSetup` 注册 FML 自身路径**：`AssetUtil`/`ItemUtils` 按 FeatherMod 域解析资源可用
- **`fml.json` 随构建产物分发**（csproj `CopyToOutputDirectory`），FML 自身依赖声明在发布包中真正生效

### 破坏性变更

- **移除 fml.json `autoActivate` 自激活机制**：`ShouldActivateMod_Postfix` 整体删除，`ModMeta.AutoActivate` 字段与解析同步移除，`autoActivate` 键静默忽略（向后兼容）；激活完全由玩家手动控制
- **GameEvents 字段强类型化**：13 个事件类的 `object`/`object?` 字段替换为已核验的原生强类型（`Health`、`DamageInfo`、`AISound` 等），移除 8 个 TODO 占位；订阅方需同步调整类型

### 重构（反射最小化，30+ 处）

- 大量 `GetField`/`SetValue` 反射替换为 Publicizer 直接访问：`BuildingUtils`、`ContainerUtils`、`QuestGiverIDPatch`、`AICharacterControllerInit`、`FaceRefResolver`、`FriendlyNpcUtils`、`DecomposeRegistry`、`WeaponInjectionUtils`、`FishSpawnerPatch`、`EnemyUtils`、`EnemyPresetData`（30 处）、`GameEventAdapters`
- `OtherPatches` 删除 6 个无效/占位补丁（空操作），8 个精简为 2 个
- `FriendlyNpcUtils` 删除反射缓存与死代码（`_cachedCreateAsync` 等）
- `InitLevelPostfix` 重写：仅生成显式开启 AutoSpawn 的敌人，`CreateCharacterAsync` 零反射直接调用

### 警告清理

- 构建警告从 50 个降至 **0**：CS8625/8603/8604/8600/8601/8602 nullability 标注、CS0168/0169/0414/0649 死代码清理、CS0618 过时 API 内联（`BuildingUtils.GetBuildingInfo`）

### 文档

- `USAGE.md`：fml.json 文档移除 `autoActivate`（示例、字段表、加载机制），`dependencies` 描述更新
- `MIGRATION.md`：FAQ 移除"自激活策略"措辞
- `PROGRESS.md`：新增「代码整理与占位代码处理」「运行日志交叉验证修复」两个 Phase 记录

### 已知限制

- 玩家环境存在多份不同版本 0Harmony.dll（HarmonyLoadMod 与各 mod 自带）时，FML 可能因依赖解析失败无法加载（TypeLoadException，详见 `logs/` 中 08-02/08-03 玩家日志）；建议玩家统一 0Harmony 版本
- `Mods.ES3` 损坏自愈依赖 FML 已加载（排序补丁由 FML 提供）；ES3 损坏 + FeatherMod 排在 HarmonyLoadMod 之前时需玩家手动重排
