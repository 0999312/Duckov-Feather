# API Reference — Entities / 实体 API

> **模块**：敌人、友善 NPC、捏脸、装备、武器注入、抽奖箱注入
> **教程**：[USAGE.md 敌人系统](../USAGE.md#12-敌人系统--enemy)、[USAGE.md 友善 NPC](../USAGE.md#13-友善-npc--friendly-npc)、[USAGE.md 捏脸](../USAGE.md#14-捏脸系统--custom-face)、[USAGE.md NPC 注入](../USAGE.md#15-npc-注入--weapon--lotterybox-injection)

---

## 目录

- [EnemyUtils — 敌人系统](#enemyutils)
- [IStateConfig / Transition — AI 状态机](#istateconfig--transition)
- [EnemyPresetData — 敌人预设数据](#enemypresetdata)
- [FriendlyNpcUtils — 友善 NPC](#friendlynpcutils)
- [FriendlyNpcConfig — NPC 配置](#friendlynpcconfig)
- [FaceRef / NpcRole / ModelRef — 外观引用](#faceref--npcrole--modelref)
- [EquipmentUtils — 装备](#equipmentutils)
- [CustomFaceUtils — 捏脸工具](#customfaceutils)
- [WeaponInjectionUtils — 武器注入](#weaponinjectionutils)
- [LotteryBoxUtils — 抽奖箱注入](#lotteryboxutils)

---

## EnemyUtils

**命名空间**：`FeatherMod` | **源码**：`Entities/EnemyUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterEnemy` | `static void RegisterEnemy(Identifier id, IStateConfig aiConfig, CharacterRandomPreset preset)` | 注册（modid 从 Domain 推导） |
| `UnregisterEnemy` | `static bool UnregisterEnemy(Identifier id)` | 移除 |
| `UnregisterAllEnemies` | `static int UnregisterAllEnemies(string modid)` | 批量移除 |
| `SetAutoSpawn` | `static void SetAutoSpawn(Identifier id, bool auto = true)` | 自动生成开关 |
| `GetPreset` | `static CharacterRandomPreset GetPreset(string name)` | 按名查询（不存在抛 ArgumentException） |
| `TryGetEnemy` | `static bool TryGetEnemy(Identifier id, out CharacterRandomPreset preset)` | 安全查询 |
| `CompileStateMachine` | `static object? CompileStateMachine(IStateConfig config)` | 预编译状态机为 BehaviourTree |
| `SpawnEnemy` | `static CharacterMainControl? SpawnEnemy(Identifier id, Vector3 position, Action<CharacterMainControl>? onSpawned = null)` | 指定位置生成 |
| | `static CharacterMainControl? SpawnEnemy(Identifier id, CharacterSpawnerGroup group, Action<CharacterMainControl>? onSpawned = null)` | 复用原生生成点 |

---

## IStateConfig / Transition

**命名空间**：`FeatherMod.Entities` | **源码**：`Entities/IStateConfig.cs`

| 类型 | 成员 | 说明 |
|------|------|------|
| `IStateConfig` | `string GetInitialState()` | 初始状态 |
| | `void OnStateEnter(string state)` / `OnStateUpdate(string state, float deltaTime)` / `OnStateExit(string state)` | 状态钩子 |
| | `Transition[] GetTransitions(string currentState)` | 状态转移表 |
| `Transition` | `string targetState` / `Func<bool> condition` / `int priority` | 转移（构造 `(targetState, condition, priority = 0)`） |

> `StateMachineToBT.Compile(IStateConfig)` 将状态机编译为 NodeCanvas BehaviourTree（FML 内部经此实现）。

---

## EnemyPresetData

**命名空间**：`FeatherMod` | **源码**：`Entities/EnemyPresetData.cs`

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `NameKey` | `string` | — | 显示名 key |
| `NpcRole` | `NpcRole` | `Enemy` | 角色类型 |
| `Team` | `Teams` | `scav` | 阵营 |
| `Health` | `float` | `100` | 生命 |
| `Exp` | `int` | `100` | 经验 |
| `IsBoss` | `bool` | — | Boss |
| `ShowHealthBar` | `bool` | `true` | 血条 |
| `HasSoul` | `bool` | `true` | 灵魂 |
| `DefaultWeaponOut` | `bool` | `true` | 默认持枪 |
| `CanTalk` | `bool` | `true` | 可对话 |
| `CanDieIfNotRaidMap` | `bool` | — | 非突袭图可死亡 |
| `SightDistance` / `SightAngle` | `float` | `17` / `100` | 视野 |
| `ReactionTime` | `float` | `0.2` | 反应时间 |
| `HearingAbility` | `float` | `1` | 听觉 |
| `PatrolRange` / `CombatMoveRange` | `float` | `8` / `8` | 巡逻/战斗范围 |
| `CanDash` | `bool` | — | 冲刺 |
| `DamageMultiplier` / `MoveSpeedFactor` | `float` | `1` / `1` | 伤害/移速倍率 |
| `ShowName` | `bool` | — | 显示名字 |
| `ForgetTime` | `float` | `8` | 遗忘时间 |
| `Weapon` | `WeaponConfig?` | — | 武器池 |
| `Armor` / `Helmet` / `Backpack` | `EquipmentSlotConfig?` | — | 装备 |
| `Loot` | `LootConfig?` | — | 掉落 |
| `Model` | `ModelRef` | — | 模型引用 |
| `ElementFactor_*` | `float` | `1` | 物理/火/冰/毒/电/虚空/幽灵元素倍率 |
| `Face` | `FaceRef` | — | 捏脸 |
| `UsePlayerFace` | `bool` | — | 使用玩家捏脸 |
| `ShopProfile` | `string?` | — | 商店 profile |

**辅助类型**：

| 类型 | 字段 | 说明 |
|------|------|------|
| `WeaponConfig` | `WeaponPool`(ItemEntry[]) / `Chance`(float=1) / `Qualities`(QualityRange?) / `Durability`(Vector2) / `DurabilityIntegrity`(Vector2) / `WithMatchingAmmo`(bool=true) | 武器池 |
| `EquipmentSlotConfig` | `ItemPool`(ItemEntry[]) / `Chance`(float=0.5) / `Qualities`(QualityRange?) / `Durability`(Vector2) | 装备池 |
| `LootConfig` | `DropBoxOnDead`(bool=true) / `HasCashChance`(float) / `CashRange`(Vector2Int) / `ExtraLoot`(ItemEntry[]) | 掉落 |
| `QualityRange` | `Min`(int) / `Max`(int) | 品质区间 |
| `ModelRef` | `GamePrefabName` / `BundleName` / `AssetPath` | 模型引用；`GamePrefab(name)` / `FromBundle(bundle, path)` / `Default` |

---

## FriendlyNpcUtils

**命名空间**：`FeatherMod` | **源码**：`Entities/FriendlyNpcUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterFriendlyNpc` | `static CharacterRandomPreset RegisterFriendlyNpc(Identifier id, FriendlyNpcConfig config, string? modid = null)` | 注册预设（步骤 1） |
| `SpawnFriendlyNpcAsync` | `static async UniTask<GameObject?> SpawnFriendlyNpcAsync(Identifier id, Vector3? position = null, Quaternion? rotation = null)` | 异步生成（步骤 2，推荐） |
| `ShowBubble` | `static void ShowBubble(Identifier npcId, string text, float duration = 2f)` | 世界空间气泡 |
| `ShowBubbleLocalized` | `static void ShowBubbleLocalized(Identifier npcId, string key, float duration = 2f)` | 本地化气泡 |
| `BindShop` | `static void BindShop(Identifier npcId, Identifier shopId)` | 绑定商店 |
| `BindQuestGiver` | `static void BindQuestGiver(Identifier npcId, Identifier questGiverId)` | 绑定 QuestGiver |
| `TryGetNpcActorId` | `static bool TryGetNpcActorId(Identifier npcId, out string actorId)` | 查询 ActorId（对话联动） |
| `SetNpcFaceDirection` | `static void SetNpcFaceDirection(Identifier npcId, Vector3 direction)` | 固定朝向 |
| `SetNpcFaceAngle` | `static void SetNpcFaceAngle(Identifier npcId, float yAngle)` | 固定朝向（角度） |
| `ClearNpcFaceDirection` | `static void ClearNpcFaceDirection(Identifier npcId)` | 恢复跟随玩家 |
| `RemoveNpc` | `static bool RemoveNpc(Identifier id)` | 销毁 |
| `RemoveAllNpcs` | `static int RemoveAllNpcs(string modid)` | 批量销毁 |

**事件**：`NpcCreatedEvent(NpcId)`、`NpcShopBoundEvent(NpcId, ShopId)`。

---

## FriendlyNpcConfig

**命名空间**：`FeatherMod` | **源码**：`Entities/FriendlyNpcConfig.cs`

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `DisplayNameKey` | `string` | — | 显示名 I18n key |
| `ActorId` | `string` | — | `DuckovDialogueActor.id`（对话查找 + 缺省显示名 key） |
| `Role` | `NpcRole` | — | 角色（可复合） |
| `Face` | `FaceRef` | — | 捏脸 |
| `Model` | `ModelRef` | `Default` | 模型 |
| `Team` | `Teams` | `middle` | 阵营 |
| `SpawnPosition` / `SpawnRotation` | `Vector3` / `Quaternion` | — | 生成位置/朝向 |
| `SceneId` | `string?` | — | 所属场景 |
| `ShopId` | `string?` | — | 商店（Role=Merchant） |
| `ShopAccountAvaliable` | `bool` | `true` | 账户余额支付 |
| `ShopReturnCash` | `bool` | — | 卖出给现金物品 |
| `ShopSellFactor` | `float` | `0.5f` | 回收价格倍率 |
| `QuestGiverId` | `Identifier?` | — | QuestGiver（Role=QuestGiver） |
| `PerkTreeId` | `Identifier?` | — | 技能树绑定（Path=perkTreeID） |
| `HeadEquipment` / `BodyEquipment` | `ItemEntry?` | — | 初始装备 |
| `ProximityDialogue` | `DialogueSequence?` | — | 接近自动对话 |
| `AutoFacePlayer` | `bool` | `true` | 跟随玩家视线 |
| `FacePlayerRange` | `float` | `10f` | 跟随距离上限 |
| `Invincible` | `bool` | `true` | 无敌 |
| `SightDistance` | `float` | `8f` | 视野 |

---

## FaceRef / NpcRole / ModelRef

**命名空间**：`FeatherMod` | **源码**：`Entities/FaceRef.cs`, `Entities/EnemyPresetData.cs`

### FaceRef（struct）

| 静态方法 | 签名 | 效果 |
|------|------|------|
| `Preset` | `static FaceRef Preset(string name)` | 引用 Resources 中的 `CustomFacePreset` |
| `PlayerFace` | `static FaceRef PlayerFace()` | 使用玩家当前捏脸 |
| `Custom` | `static FaceRef Custom(FacePartIds parts)` | 按 8 部件 ID 自定义 |
| `FromJson` | `static FaceRef FromJson(string json)` | 从 `CustomFaceSettingData` JSON 创建 |
| `None` | `static FaceRef None` | 无 |

`FacePartIds`：`HairId` / `EyeId` / `MouthId` / `EyebrowId` / `DecorationId` / `TailId` / `FootId` / `WingId`（均可空）。

### NpcRole（[Flags]）

| 枚举值 | 位 | 行为 |
|--------|-----|------|
| `None` | `0` | 无角色 |
| `Enemy` | `1<<0` | 敌对敌人 |
| `Merchant` | `1<<1` | 交互打开商店（需 `ShopId`） |
| `QuestGiver` | `1<<2` | 交互打开任务（需 `QuestGiverId`） |
| `Neutral` | `1<<3` | 中立 |
| `Companion` | `1<<4` | 跟随玩家 |
| `DialogueOnly` | `1<<5` | 仅对话 |

> 复合：`NpcRole.Merchant | NpcRole.QuestGiver`。PerkTree 不走 Role，设 `PerkTreeId` 即可。

---

## EquipmentUtils

**命名空间**：`FeatherMod` | **源码**：`Entities/EquipmentUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `ConfigureNpcEquipment` | `static void ConfigureNpcEquipment(Identifier npcId, EquipmentSlot slot, ItemEntry item)` | 配置生成时装备 |
| `TryGetConfiguredEquipment` | `static bool TryGetConfiguredEquipment(Identifier npcId, EquipmentSlot slot, out ItemEntry item)` | 查询已配置 |
| `ClearConfiguredEquipment` | `static bool ClearConfiguredEquipment(Identifier npcId, EquipmentSlot slot)` | 清除槽位配置 |
| `ClearAllEquipment` | `static void ClearAllEquipment(Identifier npcId)` | 清除全部配置 |
| `SetNpcEquipment` | `static bool SetNpcEquipment(Identifier npcId, EquipmentSlot slot, ItemEntry item)` | 运行时设置（已生成 NPC） |
| `GetNpcEquipment` | `static ItemEntry? GetNpcEquipment(Identifier npcId, EquipmentSlot slot)` | 运行时查询 |
| `ClearNpcEquipment` | `static bool ClearNpcEquipment(Identifier npcId, EquipmentSlot slot)` | 运行时清除 |

`EquipmentSlot`：`Head` / `Body` / `Backpack`。

---

## CustomFaceUtils

**命名空间**：`FeatherMod` | **源码**：`Entities/CustomFaceUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| **玩家** | | |
| `SetPlayerFaceFromJson` | `static bool SetPlayerFaceFromJson(string jsonString)` | 从 JSON 设置玩家外观 |
| `GetPlayerFaceJson` | `static string GetPlayerFaceJson()` | 导出玩家捏脸 JSON |
| `SetPlayerFaceFromData` | `static void SetPlayerFaceFromData(CustomFaceSettingData data)` | 原生数据设置 |
| `GetPlayerFaceAsData` | `static CustomFaceSettingData GetPlayerFaceAsData()` | 原生数据导出 |
| `GetPlayerFaceInstance` | `static CustomFaceInstance? GetPlayerFaceInstance()` | 查找玩家面部实例（主菜单返回 null） |
| **任意角色** | | |
| `SetFaceFromJson` | `static bool SetFaceFromJson(CustomFaceInstance instance, string jsonString)` | 运行时改角色捏脸 |
| `GetFaceJson` | `static string GetFaceJson(CustomFaceInstance instance)` | 导出 |
| `LoadFaceFromData` | `static void LoadFaceFromData(CustomFaceInstance instance, CustomFaceSettingData data)` | |
| `GetFaceAsData` | `static CustomFaceSettingData GetFaceAsData(CustomFaceInstance instance)` | |
| `ValidateJson` | `static bool ValidateJson(string jsonString)` | 校验 JSON 合法性 |

> NPC **创建时**指定捏脸用 `FaceRef.FromJson(json)`（更早更可靠）；运行时修改用 `SetFaceFromJson`。

---

## WeaponInjectionUtils

**命名空间**：`FeatherMod` | **源码**：`WeaponInjectionUtils.cs`

零 Harmony Hook——直接修改 `CharacterRandomPreset.itemsToGenerate`。

| 方法 | 签名 | 说明 |
|------|------|------|
| `AddWeaponToPreset` | `static void AddWeaponToPreset(string presetNameKey, ItemEntry weapon, float chance = 0.3f)` | 按预设名（支持 `*` 前缀通配） |
| `AddWeaponToTeam` | `static void AddWeaponToTeam(Teams team, ItemEntry weapon, float chance = 0.3f)` | 按阵营 |
| `RemoveWeaponFromPreset` | `static bool RemoveWeaponFromPreset(string presetNamePattern, ItemEntry weapon)` | 撤销 |
| `RemoveWeaponFromTeam` | `static bool RemoveWeaponFromTeam(Teams team, ItemEntry weapon)` | 撤销 |
| `UnregisterAllWeaponInjections` | `static int UnregisterAllWeaponInjections(string modid)` | 批量撤销 |
| 属性 | `static WeaponInjectionRegistry WeaponRegistry` | |

> 自动识别枪/刀类型，仅注入兼容槽位。调用即生效（修改 ScriptableObject 数据），建议 `OnAfterSetup` 中调用。

---

## LotteryBoxUtils

**命名空间**：`FeatherMod` | **源码**：`LotteryBoxUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `AddItemToLotteryBox` | `static void AddItemToLotteryBox(string sceneNamePattern, ItemEntry item, float weight = 1.0f)` | 注册注入（支持 `*` 通配） |
| `RemoveItemFromLotteryBox` | `static bool RemoveItemFromLotteryBox(string sceneNamePattern, ItemEntry item)` | 撤销 |
| `UnregisterAllLotteryInjections` | `static int UnregisterAllLotteryInjections(string modid)` | 批量撤销 |
| 属性 | `static LotteryBoxRegistry LotteryRegistry` | |

> `weight` 为相对权重倍数：`实际权重 = weight × 原生条目平均权重`（1.0=等权）。
> Harmony `Awake` Postfix 在场景加载时自动注入，注册时机灵活。

---

## 废弃 API / Obsolete

| 成员 | 替代 |
|------|------|
| `FriendlyNpcUtils.CreateFriendlyNpc(id, config, ...)` | `RegisterFriendlyNpc(id, config)` + `SpawnFriendlyNpcAsync(id)` |
| `FriendlyNpcUtils.BindQuestGiver(npcId, string)` | `BindQuestGiver(npcId, Identifier)` |
