# API Reference — System Modules / 系统模块 API

> **模块**：商店、音频、经济、Buff、容器、笔记、钓鱼、天气、多场景、对话、存档
> **教程**：[USAGE.md 各系统章节](../USAGE.md)

---

## 目录

- [ShopUtils — 商店](#shoputils)
- [AudioUtil — 音频](#audioutil)
- [EconomyUtils — 经济](#economyutils)
- [BuffUtils — Buff 状态](#buffutils)
- [ContainerUtils — 物品容器](#containerutils)
- [NoteUtils — 笔记](#noteutils)
- [FishingUtils — 钓鱼](#fishingutils)
- [WeatherUtils — 天气](#weatherutils)
- [MultiSceneUtils — 多场景](#multisceneutils)
- [DialogueManager — 对话](#dialoguemanager)
- [SaveUtils — 存档](#saveutils)

---

## ShopUtils

**命名空间**：`FeatherMod` | **源码**：`Shop/ShopUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `AddGoods` | `static void AddGoods(ShopGoodsData data, string? modid = null)` | 添加商品 |
| `UnregisterAllGoods` | `static int UnregisterAllGoods(string modid)` | 按 mod 卸载商品 |
| `RemoveGoods` | `static bool RemoveGoods(Identifier id)` | 按物品 Identifier 移除 |
| `EditGoods` | `static bool EditGoods(Identifier id, ShopGoodsData newData)` | 按物品 Identifier 编辑 |
| `CreateMerchantProfile` | `static string CreateMerchantProfile(Identifier id)` | 创建商人（Path=merchantID，已存在抛 ArgumentException） |
| | `static string CreateMerchantProfile(string name)` | 兼容重载 |
| `RemoveAllProfiles` | `static int RemoveAllProfiles(string modid)` | 卸载商人 profile |
| `RemoveAllGoods` | `static int RemoveAllGoods(string merchantProfileID)` | 移除商人下全部 FML 商品 |
| `GetAllGoods` | `static IReadOnlyList<ShopGoodsData> GetAllGoods(string merchantProfileID)` | 查询商人全部商品 |
| | `static IReadOnlyList<ShopGoodsData> GetAllGoods(Identifier id)` | Identifier 版（Path=merchantProfileID） |
| `TryGetMerchantProfile` | `static bool TryGetMerchantProfile(Identifier id, out StockShopDatabase.MerchantProfile profile)` | 查询 profile |

### ShopGoodsData

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `merchantProfileID` | `string` | `"Merchant_Normal"` | 商人 profile |
| `itemIdentifier` | `Identifier?` | — | 物品 Identifier（优先解析） |
| `typeID` | `int` | — | 回退 TypeID（itemIdentifier 未设/解析失败） |
| `maxStock` | `int` | `0` | 最大库存 |
| `forceUnlock` | `bool` | `false` | 强制解锁 |
| `priceFactor` | `float` | `1F` | 价格倍率 |
| `possibility` | `float` | `1F` | 出现概率 |

---

## AudioUtil

**命名空间**：`FeatherMod.Audio` | **源码**：`Audio/AudioUtil.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterAudio` | `void RegisterAudio(Identifier id, AudioData data)` | 注册 SFX（FMOD event） |
| `PlayBGM` | `static void PlayBGM(string name)` | 播放内置 BGM |
| `PlayCustomBGM` | `static void PlayCustomBGM(string filePath, bool loop = true)` | 播放自定义 BGM 文件 |
| `StopBGM` | `static void StopBGM()` | 停止 |
| `SwitchBGM` | `static void SwitchBGM(string name)` | 切换 |
| `IsBGMPlaying` | `static bool IsBGMPlaying()` | 播放状态 |
| `GetBusVolume` / `SetBusVolume` | `static float/void *(string busName, ...)` | 总线音量 |
| `GetMasterVolume` / `SetMasterVolume` | `static float/void *()` | 总音量 |
| `GetMusicVolume` / `SetMusicVolume` | `static float/void *()` | 音乐音量 |
| `GetSFXVolume` / `SetSFXVolume` | `static float/void *()` | SFX 音量 |
| `IsMasterMuted` / `SetMasterMute` | `static bool/void *(bool)` | 总静音 |
| `IsMusicMuted` / `SetMusicMute` | `static bool/void *(bool)` | 音乐静音 |
| `IsSFXMuted` / `SetSFXMute` | `static bool/void *(bool)` | SFX 静音 |

### AudioData

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| `Path` | `string` | — | FMOD event 路径 |
| `Eventname` | `string` | — | 事件名（反向查询） |
| `MinDistance` | `float` | `1.0F` | 最小距离 |
| `MaxDistance` | `float` | `50.0F` | 最大距离 |

---

## EconomyUtils

**命名空间**：`FeatherMod` | **源码**：`EconomyUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `GetMoney` | `static long GetMoney()` | 查询金钱 |
| `AddMoney` | `static bool AddMoney(long amount)` | 加钱 |
| `RemoveMoney` | `static bool RemoveMoney(long amount)` | 扣钱 |
| `SetMoney` | `static bool SetMoney(long amount)` | 设置 |
| `UnlockItem` | `static void UnlockItem(Identifier id, bool needConfirm = false, bool showUI = true)` | 解锁物品 |
| `ConfirmUnlockItem` | `static void ConfirmUnlockItem(Identifier id)` | 确认解锁流程 |
| `IsItemUnlocked` | `static bool IsItemUnlocked(Identifier id)` | 解锁状态 |
| `IsItemWaitingForUnlockConfirm` | `static bool IsItemWaitingForUnlockConfirm(Identifier id)` | 待确认状态 |
| `OnMoneyChanged` | `static void OnMoneyChanged(Action<MoneyChangedEvent> handler)` | 订阅金钱变化 |
| `OnItemUnlockStateChanged` | `static void OnItemUnlockStateChanged(Action<ItemUnlockStateChangedEvent> handler)` | 订阅解锁变化 |
| `RegisterMoneyChangedCallback` | `static void RegisterMoneyChangedCallback(Action<long, long> callback)` | 简化回调 `(oldMoney, nowMoney)` |

---

## BuffUtils

**命名空间**：`FeatherMod` | **源码**：`Buffs/BuffUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterBuff` | `static void RegisterBuff(Identifier id, Buff buffPrefab)` | 注册（modid 从 Domain 推导） |
| `UnregisterBuff` | `static bool UnregisterBuff(Identifier id)` | 移除 |
| `UnregisterAllBuffs` | `static int UnregisterAllBuffs(string modid)` | 批量移除 |
| `TryGetBuffIdentifier` | `static bool TryGetBuffIdentifier(int buffID, out Identifier id)` | 数字 ID → Identifier（自定义 + 内置） |
| 属性 | `static BuffRegistry Registry` | |

---

## ContainerUtils

**命名空间**：`FeatherMod` | **源码**：`Containers/ContainerUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `CreateContainer` | `static ItemContainerConfig CreateContainer(Identifier id, int slotCount, string modid)` | 创建容器 |
| `GetContainer` | `static ItemContainerConfig? GetContainer(Identifier id)` | 查询 |
| `DestroyContainer` | `static bool DestroyContainer(Identifier id)` | 销毁（不转移内部物品） |
| `PutItem` | `static bool PutItem(Identifier containerId, int slot, ItemEntry item)` | 放入 |
| `TakeItem` | `static ItemEntry? TakeItem(Identifier containerId, int slot, int amount)` | 取出（转移到玩家库存） |
| `BindDeviceToBuilding` | `static void BindDeviceToBuilding(Identifier buildingId, Identifier containerId, Identifier viewType)` | 绑定建筑（建成后挂交互） |
| `RemoveAllContainers` | `static int RemoveAllContainers(string modid)` | 批量卸载（仅清 FML 跟踪数据） |

**数据类型**：`ItemContainerConfig`（`Id` / `SlotCount` / `Modid` / `Items`）、`ItemContainerEntry`（`Item` / `Count`）。

---

## NoteUtils

**命名空间**：`FeatherMod` | **源码**：`Notes/NoteUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterNote` | `static void RegisterNote(Identifier id, NoteConfig config, string? modid = null)` | 注册（运行时注入 NoteIndex） |
| `UnregisterNote` | `static bool UnregisterNote(Identifier id)` | 移除 |
| `UnregisterAllNotes` | `static int UnregisterAllNotes(string modid)` | 批量移除 |
| `Unlock` | `static void Unlock(Identifier id)` | 解锁 |
| `UnlockAndShow` | `static void UnlockAndShow(Identifier id)` | 解锁并打开 UI |
| `IsUnlocked` | `static bool IsUnlocked(Identifier id)` | |
| `IsRead` | `static bool IsRead(Identifier id)` | |
| `GetTotalCount` / `GetUnlockedCount` | `static int *()` | 统计 |
| `GetAllNotes` | `static IReadOnlyList<Identifier> GetAllNotes(string modid)` | |
| `SpawnPickup` | `static NoteInteract? SpawnPickup(Identifier id, Vector3 position, string? sceneId = null)` | 世界空间拾取物 |

### NoteConfig

| 字段 | 类型 | 说明 |
|------|------|------|
| `TitleKey` | `string` | 标题 I18n key（`Note_{key}_Title`） |
| `ContentKey` | `string` | 内容 I18n key（`Note_{key}_Content`） |
| `Image` | `Sprite?` | 配图 |
| `Hidden` | `bool` | 不计入总数 |

**事件**：`NoteRegisteredEvent` / `NoteUnlockedEvent` / `NoteReadEvent`（均含 `NoteId`）。

---

## FishingUtils

**命名空间**：`FeatherMod` | **源码**：`Fishing/FishingUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterFishingPool` | `static void RegisterFishingPool(Identifier id, FishingPoolConfig config, string? modid = null)` | 注册钓鱼池 |
| `RegisterSpecialCatch` | `static void RegisterSpecialCatch(Identifier baitId, Identifier fishId, float chance, string? modid = null)` | 特殊配对 |
| `UnregisterFishingPool` | `static bool UnregisterFishingPool(Identifier id)` | |
| `UnregisterSpecialCatch` | `static bool UnregisterSpecialCatch(Identifier baitId, Identifier fishId)` | |
| `UnregisterAll` | `static int UnregisterAll(string modid)` | |
| `GetAllPools` | `static IReadOnlyList<FishingPoolConfig> GetAllPools()` | |
| `GetAllSpecialCatches` | `static IReadOnlyList<SpecialCatchEntry> GetAllSpecialCatches()` | |
| `GetSpecialCatchesForBait` | `static IReadOnlyList<SpecialCatchEntry> GetSpecialCatchesForBait(int baitTypeId)` | |
| `GetFishingTime` | `static float GetFishingTime(CharacterMainControl character)` | 钓鱼时间属性 |
| `GetFishingDifficulty` | `static float GetFishingDifficulty(Item fish)` | 鱼难度属性 |
| `GetFishingQualityFactor` | `static float GetFishingQualityFactor(CharacterMainControl character)` | 品质因子 |

### 数据类型

| 类型 | 字段 |
|------|------|
| `FishingPoolConfig` | `WaterId`(Identifier) / `Entries`(FishingPoolEntry[]) / `RequiredWeatherTags`(string[]) / `MinLuck`(float=0.1) / `MaxLuck`(float=1.0) |
| `FishingPoolEntry` | `FishId`(Identifier) / `Weight`(float) / `MinQuality`(int?) / `Tags`(string[]) |
| `SpecialCatchEntry` | `BaitId`(Identifier) / `FishId`(Identifier) / `Chance`(float) |

**事件**：`FishCaughtEvent(FishId, FishItem?)`。

> 鱼物品仍需 `ItemUtils.CreateCustomItem` 创建；特殊配对在 `FishSpawner.Awake` 时 Harmony Postfix 自动注入。

---

## WeatherUtils

**命名空间**：`FeatherMod` | **源码**：`Weather/WeatherUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `GetCurrentWeather` | `static WeatherType GetCurrentWeather()` | 当前天气 |
| `GetWeatherAt` | `static WeatherType GetWeatherAt(TimeSpan time)` | 指定时间天气 |
| `ForceWeather` | `static void ForceWeather(WeatherType type, bool force = true)` | 强制覆盖（调试/剧情） |
| `ResetWeather` | `static void ResetWeather()` | 取消覆盖 |
| `IsRaining` / `IsSnowing` | `static bool *()` | 便捷判断 |
| `GetCurrentSeason` | `static SeasonType GetCurrentSeason()` | 季节 |
| `GetStormLevel` | `static int GetStormLevel()` | 风暴等级（0/1/2） |
| `IsStormActive` | `static bool IsStormActive()` | |
| `GetColdLevel` / `GetHeatLevel` | `static float *()` | 冷/热等级（-10~+10） |
| `GetStormProtection` / `GetColdProtection` / `GetHeatProtection` | `static float *(CharacterMainControl character)` | 防护属性 |

**枚举**：`WeatherType`（`Sunny`/`Cloudy`/`Rainy`/`Snow`/`Stormy`/`SevereStormy`）、`SeasonType`（`Spring`/`Summer`/`Autumn`/`Winter`）。

**事件**：`WeatherChangedEvent(NewWeather)`、`StormStartedEvent`、`StormEndedEvent`。

---

## MultiSceneUtils

**命名空间**：`FeatherMod` | **源码**：`Scenes/MultiSceneUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `RegisterScene` | `static void RegisterScene(Identifier id, string sceneId, string? modid = null)` | 注册场景映射 |
| `LoadSubScene` | `static void LoadSubScene(Identifier sceneId)` | 加载子场景 |
| `TeleportTo` | `static void TeleportTo(Identifier sceneId, string locationName)` | 传送（位置点） |
| | `static void TeleportTo(Identifier sceneId, Vector3 position)` | 传送（坐标） |
| `GetCurrentSubScene` | `static Identifier? GetCurrentSubScene()` | |
| `GetSceneDisplayName` | `static string GetSceneDisplayName(Identifier sceneId)` | |
| `GetAllRegisteredScenes` | `static IReadOnlyList<Identifier> GetAllRegisteredScenes(string modid)` | |
| `SetLevelData` | `static void SetLevelData(string key, object value)` | 关卡内跨场景持久数据 |
| `GetLevelData<T>` | `static T? GetLevelData<T>(string key) where T : class` | |
| `MoveToScene` | `static void MoveToScene(GameObject obj, Identifier sceneId)` | 物体场景归属迁移 |
| `MoveToMainScene` | `static void MoveToMainScene(GameObject obj)` | |

**事件**：`SceneLoadStartedEvent` / `SceneLoadFinishedEvent`（含 `SceneId`）、`SubSceneChangedEvent`（`FromScene` / `ToScene`）。

---

## DialogueManager

**命名空间**：`FeatherMod` | **源码**：`Dialogues/DialogueManager.cs`

> 基于游戏原生 `DialogueTreeController` 驱动，自动处理面板/镜头/字幕。**新代码统一使用本类**（`DialogueUtils` 已废弃）。

| 方法 | 签名 | 说明 |
|------|------|------|
| `PlayDialogue` | `static async UniTask PlayDialogue(string actorId, DialogueLine[] lines)` | 全屏字幕对话 |
| | `static async UniTask PlayDialogue(string actorId, DialogueSequence sequence)` | 序列版 |
| `PlayBubbleDialogue` | `static async UniTask PlayBubbleDialogue(string actorId, DialogueLine[] lines)` | 气泡对话 |
| | `static async UniTask PlayBubbleDialogue(string actorId, DialogueSequence sequence)` | |
| `ShowBubbleAt` | `static void ShowBubbleAt(Vector3 pos, string text, float duration = 2f)` | 任意坐标气泡 |
| `ShowNpcBubble` | `static void ShowNpcBubble(Identifier npcId, string text, float duration = 2f)` | NPC 头顶气泡 |

### DialogueLine / DialogueSequence / SequenceBuilder

| 类型 | 成员 | 说明 |
|------|------|------|
| `DialogueLine` | `ActorId`(string) / `TextKey`(string?) / `CameraBefore`(DialogueCameraShot?) | 单行对话（`GetText()` 取文本） |
| `DialogueSequence` | `Lines` / `DefaultActorId` / `Mode`(DialogueTriggerMode) / `ProximityDistance` | 对话序列；构造 `(string actorId, params DialogueLine[])` / `(params DialogueLine[])` |
| `SequenceBuilder` | `Then(string textKey)` / `Then(string actorId, string textKey)` / `Repeatable()` / `Proximity(float)` / `WithCamera(shot)` / `CutTo(...)` / `LookAtNpc(...)` / `LookAtActor(...)` / `CutToVcam(...)` / `ResumeCamera(float)` / `Build()` | 链式构建对话 |
| `DialogueCameraShot` | `Position` / `LookAt` / `LookAtNpc` / `LookAtActorId` / `LookAtOffset` / `VcamName` / `BlendTime` | 镜头运镜；静态 `ResumeGameplay` 恢复游戏镜头 |
| `DialogueTriggerMode` | `Once` / `Repeatable` | 触发模式 |

```csharp
var seq = DialogueSequence.Build("merchant_actor")
    .Then("dialogue_hello")
    .CutTo(new Vector3(0, 2, 5), npcId)
    .Then("player", "dialogue_reply_01")
    .Then("dialogue_farewell")
    .ResumeCamera()
    .Build();
await DialogueManager.PlayDialogue(seq);
```

### DialogueTrigger

| 方法 | 签名 | 说明 |
|------|------|------|
| `OnProximity` | `static void OnProximity(Identifier npcId, float distance, DialogueLine[] lines, DialogueTriggerMode mode = Once)` | 接近触发 |
| | `static void OnProximity(Identifier npcId, DialogueSequence sequence)` | |
| `OnQuestAccepted` | `static void OnQuestAccepted(Identifier questId, Identifier npcId, DialogueLine[] lines, string? actorId = null, DialogueTriggerMode mode = Once)` | 接取触发 |
| `OnQuestCompleted` | `static void OnQuestCompleted(Identifier questId, Identifier npcId, DialogueLine[] lines, string? actorId = null, DialogueTriggerMode mode = Once)` | 完成触发 |
| `RemoveAllTriggers` | `static void RemoveAllTriggers(Identifier npcId)` | 移除触发器 |

---

## SaveUtils

**命名空间**：`FeatherMod.Saves` | **源码**：`Saves/SaveUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `MakeKey` | `static string MakeKey(Identifier identifierKey)` | 存档 key |
| `KeyExists` | `static bool KeyExists(Identifier identifierKey)` | |
| `Load<T>` | `static T? Load<T>(Identifier identifierKey)` / `(id, T defaultValue)` | 读取 |
| `Save<T>` | `static void Save<T>(Identifier identifierKey, T? value)` | 写入 |
| `Delete<T>` | `static void Delete<T>(Identifier identifierKey)` | 删除 |

> `ES3Validator.CanBeSerializedByES3<T>()` 可预检类型。

---

## 废弃 API / Obsolete

| 成员 | 替代 |
|------|------|
| `DialogueUtils`（整个类） | `DialogueManager` |
| `SubtitleLine` | `DialogueLine` |
| `ProximityDialogueConfig` | `DialogueSequence` |
