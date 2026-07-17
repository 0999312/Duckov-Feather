# Phase 5 — 长尾幂等系统：目标与 API 设计

> **状态**: ✅ 已完成  
> **创建日期**: 2026-07-14  
> **完成日期**: 2026-07-14  
> **基础**: PLAN.md §Phase 5 + `duckov_assembly/assembly_0625` 反编译审计  
> **前置**: Phase 0–4 全部完成

---

## 概述

Phase 5 覆盖五个子系统，均遵循 FML 的 Identifier-first / DTO 驱动的设计原则。五个系统按优先级分两个波次：

| 优先级 | 子系统 | 游戏侧复杂度 | FML 工作量 | 前置依赖 |
|--------|--------|------------|-----------|---------|
| **P0** | Note（笔记/收集品） | 低（字符串 key + 完整 UI + 运行时注册） | ~150 LOC | 无 |
| **P0** | Fishing | 中（FishSpawner 扩展点 + 统计属性） | ~200 LOC | 无 |
| **P1** | Friendly NPC | 中（InteractableBase 体系 + Dialogue/Shop/Quest） | ~250 LOC | InteractionUtils |
| **P1** | Weather & Seasons | 低（读取 API 完善；写入需覆盖机制） | ~150 LOC | 无 |
| **P2** | Multi-Scene | 高（MultiSceneCore 添加剂载 + Location 系统） | ~200 LOC | SceneInfoCollection 审计 |

> **已移除**: Achievements — 与 Steam 成就绑定，不应让 modder 更改。  
> **替代**: Note（笔记）系统 — 纯游戏内收集品，支持 UI 展示 + 条件门控 + 运行时注册

**已完成的前置工作（Phase 4）**：
- `FaceRef` / `FacePartIds` / `NpcRole` 类型已就绪（`FeatherMod/Entities/FaceRef.cs`）
- `FMLTask_KillCountByTag` / `FMLTask_SubmitItemByTag` 已就绪（`FeatherMod/Quests/`）
- `TagCostRegistry` / `TagCostValidator` / `CraftingManagerPatch` 已就绪（`FeatherMod/Crafting/`）
- `ViewDispatcher` + `GameUIUtils` + `InteractionUtils` Phase 4 补实现已完成

---

## 1. Note（笔记/收集品） — P0

### 1.1 游戏侧架构

```
Note (数据模型, Serializable)
  └─ key: string                    // 标识符 (e.g. "note_01")
  └─ image: Sprite | null           // 可选插图
  └─ hide: bool                     // 隐藏则不计入总数
  └─ titleKey → "Note_{key}_Title"   // 自动生成本地化键
  └─ contentKey → "Note_{key}_Content"
  └─ Title / Content → ToPlainText() 解析

NoteIndex (MonoBehaviour 单例，通过 GameManager.NoteIndex 访问)
  └─ notes: List<Note>              // 所有已定义笔记（场景级序列化）
  └─ unlockedNotes: HashSet<string>  // 已解锁 key
  └─ readNotes: HashSet<string>      // 已阅读 key
  └─ onNoteStatusChanged: Action<string>  // 解锁/已读时触发
  └─ SetNoteDynamic(Note)            // 🔑 运行时注册新笔记
  └─ SetNoteUnlocked(key) / SetNoteRead(key)
  └─ GetNote(key) / GetNoteUnlocked(key) / GetNoteRead(key)
  └─ 持久化: SavesSystem.Save("NoteIndexData", SaveData)

NoteInteract (InteractableBase 子类)
  └─ 世界空间可拾取物品 → 交互 = 解锁 + 打开 UI

NoteIndexProxy (MonoBehaviour)
  └─ UnityEvent 桥接: UnlockNote(key) / UnlockAndShowNote(key)

NoteIndexView (View 子类)
  └─ 全屏笔记本 UI: 左侧列表 + 右侧详情
  └─ ShowNote(key) → 打开视图并滚动到指定笔记

RequireNoteIndexUnlocked (Condition 子类)
  └─ 任务条件: 检查指定笔记是否已解锁 → 门控
```

### 1.2 FML API 目标

```csharp
// 笔记注册
NoteUtils.RegisterNote(Identifier id, NoteConfig config);

public class NoteConfig
{
    public string TitleKey { get; set; }       // (默认 "Note_{id.Path}_Title")
    public string ContentKey { get; set; }     // (默认 "Note_{id.Path}_Content")
    public Sprite? Image { get; set; }
    public bool Hidden { get; set; }           // 不计入总数
}

// 状态查询
NoteUtils.IsUnlocked(Identifier id);           // → NoteIndex.GetNoteUnlocked(key)
NoteUtils.IsRead(Identifier id);               // → NoteIndex.GetNoteRead(key)

// 解锁
NoteUtils.Unlock(Identifier id);               // → SetNoteUnlocked
NoteUtils.UnlockAndShow(Identifier id);        // → Unlock + NoteIndexView.ShowNote

// 统计
NoteUtils.GetTotalCount();                     // → GetTotalNoteCount()
NoteUtils.GetUnlockedCount();

// 世界空间拾取物
NoteUtils.SpawnPickup(Identifier id, Vector3 position, string? sceneId = null);

// 事件桥接 (EventBus)
// → NoteUnlockedEvent { Identifier NoteId }
// → NoteReadEvent { Identifier NoteId }
```

### 1.3 关键设计决策

- **运行时注册优先**：游戏已支持 `SetNoteDynamic(Note)`，FML 直接利用，无需 Patch
- **本地化自动生成**：默认遵循 `Note_{key}_Title` 规则，与游戏原生一致
- **拾取物复用 NoteInteract**：`SpawnPickup` 通过 InteractableBase 体系互操作
- **纯游戏内系统**：不依赖 Steam/外部平台

### 1.4 文件布局预估

```
FeatherMod/Notes/
├── NoteConfig.cs               (~30 LOC)  DTO
├── NoteUtils.cs                (~100 LOC) Register/Unlock/Query/Spawn + 事件桥接
├── NoteRegistry.cs             (~40 LOC)  Identifier→key 映射
└── Patches/
    └── NoteEventPatch.cs       (~20 LOC)  onNoteStatusChanged → EventBus
```

### 1.5 验收标准

- [ ] `RegisterNote(Identifier, NoteConfig)` → `NoteIndex.notes` 包含该笔记
- [ ] `Unlock(id)` → `GetNoteUnlocked(key) == true`
- [ ] `UnlockAndShow(id)` → 笔记解锁 + NoteIndexView 打开
- [ ] `SpawnPickup(id, pos)` → 世界空间出现可拾取笔记
- [ ] FML EventBus 收到 `NoteUnlockedEvent`
- [ ] 卸载 mod 时 RemoveAllByOwner 清理
- [ ] `dotnet build` 0 错误

---

## 2. Fishing — P0

### 2.1 游戏侧架构

```
Action_FishingV2 (主活动，V2 为当前版本)
  └─ 状态机: non → throwing → waiting → ring → successBack/failBack
  └─ ring 缩小机制: scaleRange (3.0→0.5), successRange (0.75-1.1)
  └─ 时机 = player.FishingTime * 1.25 / fish.FishingDifficulty

FishingRod (MonoBehaviour)
  └─ Bait Slot: 鱼饵物品槽
  └─ Bait 属性 (Item), UseBait() 方法

FishingPoint (InteractableBase)
  └─ 世界空间钓鱼点，传送玩家 + 启动 Action_FishingV2

FishSpawner (MonoBehaviour) — 核心扩展点
  └─ Spawn(baitID, luck):
    路径1: specialPairs[] (baitID→fishID 精确映射 + chance + 日夜/天气检查)
    路径2: 基于标签的随机 (RandomContainer<Tag> × RandomContainer<int> × ItemFilter)
  └─ 时间/天气标签: Fish_OnlyDay, Fish_OnlyNight, Fish_OnlySunDay,
    Fish_OnlyRainDay, Fish_OnlyStorm

钓鱼统计属性 (通过 item.GetStatValue(hash) 读取)
  └─ "FishingTime"           (玩家属性，控制 ring 持续时间)
  └─ "FishingDifficulty"     (鱼属性，控制 ring 缩小速度)
  └─ "FishingQualityFactor"   (玩家属性，影响上钩品质)

Bait 标签: GameplayDataSettings.Tags.Bait
```

### 2.2 FML API 目标

```csharp
// 钓鱼池注册（注入到 FishSpawner 的特殊配对表或标签随机池）
FishingUtils.RegisterFishingPool(Identifier waterId, FishingPoolConfig config);

// 钓鱼池配置 DTO
public class FishingPoolConfig
{
    public Identifier WaterId;                      // 水域标识
    public FishingPoolEntry[] Entries;               // 鱼种 + 权重
    public Tag[] RequiredWeatherTags;               // 仅特定天气可用
    public float MinLuck, MaxLuck;                  // 运气范围
}

public struct FishingPoolEntry
{
    public Identifier FishId;                       // 鱼物品 Identifier
    public float Weight;                            // 权重
    public int? MinQuality;                         // 最低品质
    public Tag[] Tags;                             // 鱼的标签（用于 FishSpawner tag-based random）
}

// 特殊配对（精确 baitID→fishID）
FishingUtils.RegisterSpecialCatch(Identifier baitId, Identifier fishId, float chance);

// 钓鱼属性查询（类型安全的统计属性包装）
FishingUtils.GetFishingTime(CharacterMainControl character);      // → "FishingTime" stat
FishingUtils.GetFishingDifficulty(Item fish);                     // → "FishingDifficulty" stat
FishingUtils.GetFishingQualityFactor(CharacterMainControl character); // → "FishingQualityFactor" stat

// 鱼竿/鱼饵操作
FishingUtils.GetCurrentBait(CharacterMainControl character);      // → 当前鱼饵 Item
FishingUtils.SetBait(FishingRod rod, Item bait);                  // → 设置鱼饵
FishingUtils.HasBait(FishingRod rod);                             // → 是否有鱼饵

// 事件桥接
// → FishCaughtEvent { Identifier FishId, Item FishItem, CharacterMainControl Player }
```

### 2.3 关键设计决策

- **FishSpawner 注入策略**：通过 Harmony Prefix 拦截 `Spawn()`，在原生逻辑前尝试 FML 注册的标签/配对。无需修改原生 FishSpawner 配置。
- **Fish 物品本身用 `ItemUtils.CreateCustomItem`** 创建——FishingUtils 只管理"什么鱼可以从哪里钓到"，不负责鱼物品的创建。
- **标签驱动**：复用 `GameplayDataSettings.Tags` 体系。`Fish_*` 标签由游戏原生定义，FML 只注册与这些标签匹配的鱼物品。
- **Aquarium（鱼缸）不纳入**：`DummyFish` / `IAquariumContent` 是纯视觉系统，modder 可直接通过创建带有 `Tag("Fish")` 的物品来 DIY，不需要 FML 封装。

### 2.4 文件布局预估

```
FeatherMod/Fishing/
├── FishingPoolConfig.cs       (~40 LOC)  DTO: FishingPoolConfig + FishingPoolEntry
├── FishingUtils.cs            (~100 LOC) 主 API + Harmony Patch
├── FishingRegistry.cs         (~50 LOC)  SimpleRegistry<FishingPoolConfig>
└── Patches/
    └── FishSpawnerPatch.cs    (~40 LOC)  Harmony Prefix 拦截 Spawn()
```

### 2.5 验收标准

- [ ] `RegisterFishingPool(waterId, config)` → FishSpawner 可识别并返回注册的鱼
- [ ] `RegisterSpecialCatch(baitId, fishId, chance)` → 指定鱼饵可钓到指定鱼
- [ ] `GetFishingTime(character)` → 返回正确的 FishingTime 属性值
- [ ] `GetFishingDifficulty(fish)` → 返回正确的 FishingDifficulty 属性值
- [ ] FML EventBus 收到 `FishCaughtEvent`
- [ ] 卸载 mod 时 `RemoveAllByOwner` 清理所有钓鱼池
- [ ] `dotnet build` 0 错误

---

## 3. Friendly NPC Interaction — P1

### 3.1 游戏侧架构

```
InteractableBase (交互基类) — 完整的虚拟生命周期
  ├─ StartInteract / UpdateInteract / FinishInteract / StopInteract
  ├─ interactTime, coolTime, disableOnFinish, requireItem/requireItemId
  ├─ UnityEvent 回调: OnInteractStartEvent/TimeoutEvent/FinishedEvent
  └─ interactableGroup: 多交互体编组

InteractablePMC (友善 NPC 子类)
  └─ 设定 AI 领导关系: aiCharacterController.leader = player
  └─ 触发拔武器动作

DuckovDialogueActor (对话角色)
  └─ id (字符串), portraitSprite, nameKey
  └─ 静态注册: Register/Unregister/Get(id)

DialogueUI (全屏对话 UI)
  └─ 订阅 NodeCanvas DialogueTree 事件
  └─ Typewriter text + 多选菜单

DialogueBubblesManager (世界空间气泡)
  └─ Show(text, target, yOffset, ...)

StockShop (商人)
  └─ IMerchant + ISaveDataProvider
  └─ Buy(int typeID, int amount), Sell(Item)
  └─ Opinion 声望系统

QuestGiver (任务发放 NPC)
  └─ QuestGiverID 标识
  └─ PossibleQuests → QuestManager.GetAllQuestsByQuestGiverID
  └─ 交互: 打开 QuestGiverView
```

### 3.2 FML API 目标

```csharp
// 友善 NPC 创建
FriendlyNpcUtils.CreateFriendlyNpc(Identifier id, FriendlyNpcConfig config);

// 友善 NPC 配置 DTO
public class FriendlyNpcConfig
{
    public string DisplayNameKey;              // 本地化键
    public string ActorId;                     // DuckovDialogueActor.id → 引用已有对话角色
    public NpcRole Role;                       // Merchant / QuestGiver / Companion / None
    public FaceRef Face;                       // 捏脸（已有类型）
    public Vector3 SpawnPosition;              // 生成位置
    public string? SceneId;                    // 目标场景（null = 当前）
}

public enum NpcRole  // 已存在于 FeatherMod/Entities/
{
    None,
    Merchant,         // 打开商店 UI
    QuestGiver,       // 打开任务 UI
    Companion,        // 跟随玩家
    DialogueOnly      // 仅对话
}

// 对话气泡（简化 API）
FriendlyNpcUtils.ShowBubble(Identifier npcId, string text, float duration = 3f);

// 商店绑定（为友善 NPC 绑定商店）
FriendlyNpcUtils.BindShop(Identifier npcId, Identifier shopId);
// → 内部通过 ShopUtils 注册 + 为 NPC 的 InteractableBase 绑定 OnInteractFinished→ShopUI

// 任务绑定（为友善 NPC 绑定任务发放）
FriendlyNpcUtils.BindQuestGiver(Identifier npcId, string questGiverId);

// 事件桥接
// → NpcInteractedEvent { Identifier NpcId, CharacterMainControl Player }
// → NpcDialogueStartedEvent { Identifier NpcId, string DialogueKey }
```

### 3.3 关键设计决策

- **不新建对话系统**：游戏已使用 NodeCanvas `DialogueTree` 作为运行时引擎 + CSV 驱动的本地化。FML 提供 `ActorId` 引用现有 `DuckovDialogueActor`，不尝试替换对话运行时。
- **NpcRole 枚举扩展**：已存在的 `NpcRole` 枚举（`FeatherMod/Entities/`）补充 `Companion` 和 `DialogueOnly`。
- **FaceRef 运行时查找**：`FaceRef.Preset("name")` 的运行时查找机制在 Phase 5 中完成（详见 §6.1）。
- **商店/任务绑定**：通过 `OnInteractFinishedEvent` (UnityEvent) 连接到现有的 `ShopUtils` / `QuestUtils` API，不创建新的商店/任务子系统。

### 3.4 文件布局预估

```
FeatherMod/Entities/
├── FriendlyNpcConfig.cs       (~50 LOC)  DTO
├── FriendlyNpcUtils.cs        (~150 LOC) Create/BindShop/BindQuestGiver/Bubble
├── Patches/
│   └── FriendlyNpcSpawnPatch.cs (~40 LOC)  CharacterCreator 自定义模型注入
└── FaceRef.cs                 (已存在，补充运行时查找方法)
```

### 3.5 验收标准

- [ ] `CreateFriendlyNpc(Identifier, FriendlyNpcConfig)` → NPC 出现在目标场景
- [ ] `NpcRole.Merchant` → 交互打开商店 UI
- [ ] `NpcRole.QuestGiver` → 交互打开任务发放 UI
- [ ] `NpcRole.Companion` → NPC 跟随玩家
- [ ] `ShowBubble(npcId, text)` → 浮出对话气泡
- [ ] `FaceRef.Preset("name")` → 正确应用预设捏脸
- [ ] 卸载 mod 时 `RemoveAllByOwner` 清理 NPC + 商店 + 任务绑定
- [ ] `dotnet build` 0 错误

---

## 4. Weather & Seasons — P1

### 4.1 游戏侧架构

```
Weather 枚举: Sunny=0, Cloudy=1, Rainy=2, Snow=22, Stormy_I=3, Stormy_II=4
Seasons 枚举: spring, summer, autumn, winter

WeatherManager (单例)
  └─ GetWeather() / GetWeather(TimeSpan)
  └─ SetForceWeather(bool force, Weather value)
  └─ ForceWeather / ForceWeatherValue 属性
  └─ Season (静态，来自 LevelConfig.Season)
  └─ 基于种子 + Perlin 噪声的确定性天气（10 年周期）

Storm (循环风暴)
  └─ 三段式循环: sleep → stage1 → stage2
  └─ GetStormLevel / GetStormETA / GetStormIOverETA / GetStormIIOverETA

Precipitation (Perlin 噪声降水)
  └─ Get(TimeSpan) → 0-1
  └─ cloudyThreshold / rainyThreshold

温度系统
  └─ TimeOfDayController.coldLevel / heatLevel (static float)
  └─ CharacterMainControl.UpdateCold() → 计算寒冷伤害
  └─ 防护属性: StormProtection / ColdProtection / HeatProtection (Item stats)

事件
  └─ TimeOfDayController.OnStormStarted / OnStormEnded (静态 C# 事件)

关卡配置
  └─ LevelConfig.Season (每个关卡单独设定)
```

### 4.2 FML API 目标

```csharp
// 天气查询
WeatherUtils.GetCurrentWeather();           // → WeatherType 枚举（FML 自有，隐藏 Snow=22）
WeatherUtils.GetWeatherAt(TimeSpan time);   // → 指定时间的天气

// 天气类型（FML 自有枚举，封装游戏原生枚举的特殊值）
public enum WeatherType
{
    Sunny, Cloudy, Rainy, Snow, Stormy, SevereStormy
}

// 季节查询
WeatherUtils.GetCurrentSeason();            // → SeasonType 枚举

public enum SeasonType
{
    Spring, Summer, Autumn, Winter
}

// 天气覆盖（调试/剧情用）
WeatherUtils.ForceWeather(WeatherType type, bool force = true);
WeatherUtils.ResetWeather();                 // → SetForceWeather(false)

// 风暴信息
WeatherUtils.GetStormLevel();               // 0/1/2
WeatherUtils.GetStormETA();                 // TimeSpan 距离下次风暴
WeatherUtils.IsStormActive();

// 温度查询
WeatherUtils.GetColdLevel();                // → TimeOfDayController.coldLevel
WeatherUtils.GetHeatLevel();                // → TimeOfDayController.heatLevel

// 防护属性查询
WeatherUtils.GetStormProtection(CharacterMainControl c);
WeatherUtils.GetColdProtection(CharacterMainControl c);
WeatherUtils.GetHeatProtection(CharacterMainControl c);

// 事件桥接（通过 FML EventBus）
// → WeatherChangedEvent { WeatherType Old, WeatherType New }
// → StormStartedEvent / StormEndedEvent
// → SeasonChangedEvent { SeasonType Old, SeasonType New }

// 降水查询
WeatherUtils.GetPrecipitation();            // 0-1
WeatherUtils.IsRaining();
WeatherUtils.IsSnowing();
```

### 4.3 关键设计决策

- **FML 自有枚举**：`Snow=22` 是游戏实现的细节泄漏，FML `WeatherType` 将其归一化。
- **只读为主**：`SetForceWeather` 是覆盖模式，影响所有模组。通过 EventBus 通知所有模组天气被覆盖。
- **季节不可写**：`LevelConfig.Season` 是每个关卡的 MonoBehaviour 字段，FML 不提供运行时修改（若需修改，通过 Harmony Prefix 拦截 `LevelConfig.Awake()`）。
- **温度/防护是 Item Stats**：FML 不封装新的防护系统，通过 `ItemUtils` 现有的 stat 体系即可完成防护装备的配置。

### 4.4 文件布局预估

```
FeatherMod/Weather/
├── WeatherType.cs              (~20 LOC)  FML 自有枚举
├── WeatherUtils.cs             (~100 LOC) GetCurrent/Force/Storm/Precip/Temp/Protection + 事件桥接
└── Patches/
    └── WeatherEventPatch.cs    (~30 LOC)  Harmony 拦截 OnStormStarted/Ended → EventBus
```

### 4.5 验收标准

- [ ] `GetCurrentWeather()` → 返回正确的 FML WeatherType
- [ ] `GetCurrentSeason()` → 返回正确的 FML SeasonType
- [ ] `ForceWeather(Stormy)` → 天气立即变为 Stormy
- [ ] `GetStormLevel()` → 返回 0/1/2
- [ ] `GetColdLevel()` → 返回正确的温度值
- [ ] FML EventBus 收到 `StormStartedEvent`
- [ ] `Snow=22` 对 modder 完全透明
- [ ] `dotnet build` 0 错误

---

## 5. Multi-Scene — P2

### 5.1 游戏侧架构

```
SceneLoader (单例)
  └─ LoadScene(sceneID, MultiSceneLocation, ...) → 异步加载
  └─ 生命周期事件: onStartedLoadingScene / onFinishedLoadingScene / onAfterSceneInitialize
  └─ 加载模式: 幕布场景(Single) → 目标(Additive) → 激活 → 卸载幕布

MultiSceneCore (多场景核心)
  └─ MainScene, ActiveSubScene, ActiveSubSceneID
  └─ LoadSubScene(SceneReference) → 卸载当前 + 附加载新
  └─ LoadAndTeleport(MultiSceneLocation) → 加载 + 传送
  └─ SubScenes: List<SubSceneEntry>
  └─ inLevelData: Dictionary<int, object> (关卡内跨场景持久数据)
  └─ MoveToActiveWithScene / MoveToMainScene (物体场景归属迁移)

SubSceneEntry
  └─ sceneID (字符串)
  └─ Locations[] (场景内传送点)
  └─ TeleporterInfo[] (到其他子场景的传送器)

MultiSceneLocation (结构体)
  └─ SceneID (字符串), LocationName (字符串)
  └─ GetLocationTransform() (运行时查找)

SceneInfoCollection (ScriptableObject)
  └─ Entries: List<SceneInfoEntry>
  └─ GetSceneInfo(id) / GetSceneID(buildIndex)
  └─ SceneInfoEntry: id, SceneReference, displayName
```

### 5.2 FML API 目标

```csharp
// 场景加载
MultiSceneUtils.LoadScene(Identifier sceneId);

// 场景传送（关卡内子场景切换）
MultiSceneUtils.TeleportTo(Identifier sceneId, string locationName);
MultiSceneUtils.TeleportTo(Identifier sceneId, Vector3 position);

// 场景查询
MultiSceneUtils.GetCurrentScene();              // → Identifier
MultiSceneUtils.GetCurrentSubScene();           // → Identifier (多场景模式下)
MultiSceneUtils.GetSceneDisplayName(Identifier sceneId);
MultiSceneUtils.GetAllScenes();                 // → IReadOnlyList<Identifier>

// 关卡内持久数据（包装 MultiSceneCore.inLevelData）
MultiSceneUtils.SetLevelData(string key, object value);
MultiSceneUtils.GetLevelData<T>(string key);

// 场景归属迁移（将物体移动到目标场景）
MultiSceneUtils.MoveToScene(GameObject obj, Identifier sceneId);
MultiSceneUtils.MoveToMainScene(GameObject obj);

// 事件桥接
// → SceneLoadStartedEvent { Identifier SceneId }
// → SceneLoadFinishedEvent { Identifier SceneId }
// → SubSceneChangedEvent { Identifier FromScene, Identifier ToScene }
```

### 5.3 关键设计决策

- **场景 ID = Identifier**：`SceneInfoEntry.id` 已是字符串，FML 用 `Identifier("duckov", "Level_GroundZero_Main")` 映射。自定义场景用 modder 的 domain。
- **不暴露 SceneReference / BuildIndex**：对 modder 透明。
- **仅包装 MultiSceneCore（关卡内）**：`SceneLoader` 用于跨关卡加载（主菜单→关卡），由 `LevelManager` 管理。Phase 5 聚焦关卡内的子场景传送。
- **inLevelData 类型安全**：`SetLevelData` 接受 `object` 但 `GetLevelData<T>` 调用方负责类型安全（与 EasySave3 一致）。

### 5.4 文件布局预估

```
FeatherMod/Scenes/
├── MultiSceneUtils.cs          (~100 LOC) LoadScene/TeleportTo/Query/LevelData/事件桥接
├── SceneRegistry.cs            (~40 LOC)  Identifier→sceneID 映射
└── Patches/
    └── SceneLoadEventPatch.cs  (~40 LOC)  Harmony 拦截 SceneLoader/Loader 事件 → EventBus
```

### 5.5 验收标准

- [ ] `LoadScene(id)` → 场景正确加载
- [ ] `TeleportTo(id, location)` → 玩家正确传送到目标位置
- [ ] `GetCurrentScene()` → 返回正确 Identifier
- [ ] `SetLevelData/GetLevelData` → 数据跨子场景持久
- [ ] FML EventBus 收到 `SceneLoadFinishedEvent`
- [ ] `dotnet build` 0 错误

---

## 6. 横切关注点（Phase 5 全部子系统共享）

### 5.1 FaceRef 运行时查找（前置工作收尾）

`FaceRef` / `FacePartIds` / `FaceRefMode` / `NpcRole` 类型已在 Phase 4 实现（`FeatherMod/Entities/FaceRef.cs`），但运行时预设创建和查找待完成：

```csharp
// 补充到 FaceRef.cs 或新建 FaceRefResolver.cs：
internal static class FaceRefResolver
{
    // 按名称查找已有 CustomFacePreset
    internal static CustomFacePreset? FindPresetByName(string name);

    // 根据 FacePartIds 创建自定义捏脸预设
    internal static CustomFacePreset CreateCustomFacePreset(FacePartIds parts);

    // 应用 FaceRef 到 CharacterModel
    internal static void ApplyToModel(CharacterModel model, FaceRef face);
}
```

### 5.2 TagCost / QuestTask 运行时验收（前置工作收尾）

`TagCostRegistry` / `TagCostValidator` / `CraftingManagerPatch` 和 `FMLTask_KillCountByTag` / `FMLTask_SubmitItemByTag` 的代码已就绪，但运行时验证（合成消耗逻辑、耐久度折算、任务进度追踪）留待 Phase 5 在实际游戏环境中完成。**不涉及代码修改**，仅需验证。

### 5.3 本地化

所有 Phase 5 新增模块沿用 FML 现有的多语言本地化模式（`FeatherMod/assets/lang/*.json`）。成就名称/描述、NPC 名称、场景名称通过 `I18n.ToPlainText(key)` 处理。

### 5.4 编译约束

- 所有新增 public API 使用 `Identifier`（无裸 string / int ID）
- 所有新增 module 继承 `SimpleRegistry<T>` 并注册到 `RegisterBootstrap`
- Harmony Patch 使用独立 `try/catch`，失败不影响其他子系统
- 保持 `0Harmony.dll` 版本 2.4.1.0 的硬性锁定不变

---

## 7. 实施顺序

```
Wave 1 (并行 P0):
  ├── P0.1: Note（笔记）(~150 LOC, 4 文件)
  └── P0.2: Fishing (~200 LOC, 4 文件)

Wave 2 (并行 P1):
  ├── P1.1: Friendly NPC (~250 LOC, 4 文件)
  │   └── 依赖: FaceRefResolver (6.1)
  └── P1.2: Weather & Seasons (~150 LOC, 3 文件)

Wave 3 (P2, 顺序):
  └── P2.1: Multi-Scene (~200 LOC, 3 文件)

收尾:
  ├── FaceRefResolver 运行时查找实现
  ├── TagCost/QuestTask 运行时验收
  ├── 本地化条目补全
  └── PROGRESS.md 更新
```

---

## 8. 参考资料

| 来源 | 路径 | 内容 |
|------|------|------|
| 计划 | `Docs/TODO/PLAN.md` | Phase 5 高层规划 (§2, §7, 附录) |
| 进度 | `Docs/PROGRESS.md` | Phase 0–4 完成状态 + 遗留问题 |
| 问题 | `Docs/TODO/ISSUES.md` | 已知代码质量/设计问题 |
| 捏脸设计 | `Docs/TODO/DESIGN-FaceCustomization.md` | FaceRef/FacePartIds 完整设计 |
| 任务设计 | `Docs/TODO/DESIGN-QuestTaskExtension.md` | Task 子类完整设计 |
| 标签设计 | `Docs/TODO/DESIGN-TagCrafting.md` | TagCost 系统完整设计 |
| 逆向 | `duckov_assembly/assembly_0625/Src/` | 游戏反编译源码 |
| 逆向文档 | `duckov_assembly/assembly_0625/MODDING.md` | 游戏系统架构参考 |
| 交互UI | `Docs/DESIGN_INTERACTION_API.md` | 交互系统设计 |
| UI系统 | `Docs/DESIGN_UI_SYSTEM_API.md` | UI 系统设计 |

---

*本文档为 Phase 5 的**目标与 API 设计纲要**。各子系统进入实施前可根据实际游戏 API 差异调整细节。*
