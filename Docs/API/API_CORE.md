# API Reference — Core / 核心 API

> **模块**：标识符、注册表、事件总线、本地化、跨模组联动、AssetBundle、模组声明
> **教程**：[USAGE.md 核心概念](../USAGE.md#2-核心概念--core-concepts)

---

## 目录

- [ModBehaviour / IHasModid — 模组主类](#modbehaviour--ihasmodid)
- [Identifier — 标识符](#identifier)
- [ModPathResolver — 路径解析](#modpathresolver)
- [I18n — 本地化](#i18n)
- [Registry — 注册表系统](#registry)
- [EventBus — 事件总线](#eventbus)
- [ModUtils — 跨模组联动](#modutils)
- [AssetUtil — AssetBundle](#assetutil)
- [fml.json — 声明式模组配置](#fmljson--modmeta)
- [SaveUtils — 存档](#saveutils)

---

## ModBehaviour / IHasModid

**命名空间**：`FeatherMod` | **源码**：`ModBehaviour.cs`, `IHasModid.cs`

外部模组**不继承** `FeatherMod.ModBehaviour`（FML 自身入口），而是继承游戏基类 `Duckov.Modding.ModBehaviour` 并实现 `IHasModid`：

```csharp
public class MyMod : Duckov.Modding.ModBehaviour, IHasModid
{
    public string GetModid() => "MyModId";
    protected override void OnAfterSetup() { /* 注册内容 */ }
}
```

| 成员 | 类型 | 说明 |
|------|------|------|
| `FrameworkName` | `const string` | `"FeatherMod"`，FML 固定 modid |
| `GetModid()` | `string` | IHasModid 接口：返回 mod 唯一标识 |

**生命周期**：

| 阶段 | 方法 | 说明 |
|------|------|------|
| 游戏启动 | `Awake()` | 游戏引擎调用 |
| 初始化就绪 | `OnAfterSetup()` | 注册路径 → Harmony.PatchAll → 调用 FML 工具方法 |
| 模组卸载 | `OnBeforeDeactivate()` | FML 自动清理，一般无需覆写 |

---

## Identifier

**命名空间**：`FeatherMod.Utils` | **源码**：`Utils/Identifier.cs`

统一资源标识符，格式 `domain:path`（类 Minecraft ResourceLocation）。

| 成员 | 签名 | 说明 |
|------|------|------|
| 构造 | `Identifier(string domain, string path)` | 双段构造，校验非法字符 |
| 构造 | `Identifier(string raw)` | 从 `"domain:path"` 解析 |
| 静态 | `static Identifier Parse(string raw)` | 解析，非法输入抛异常 |
| 静态 | `static bool TryParse(string raw, out Identifier? result)` | 安全解析 |
| 属性 | `string Domain { get; }` | 域（通常 = modid 或 `"duckov"`） |
| 属性 | `string Path { get; }` | 路径（可用 `/` 分层，如 `items/weapons/rifle`） |
| 覆写 | `Equals` / `GetHashCode` / `ToString()` | `ToString()` → `"domain:path"` |

**校验规则**：禁止 `:`、`\`、`..`、空串；`domain` 禁止 `/`；`path` 允许 `/`。

---

## ModPathResolver

**命名空间**：`FeatherMod.Utils` | **源码**：`Utils/ModPathResolver.cs`

mod 目录解析器。`I18n`、`ItemUtils.LoadSprite`、`AssetUtil.LoadBundle` 的便捷重载依赖它。

| 方法 | 签名 | 说明 |
|------|------|------|
| `Register` | `static void Register(string modid, string dllPath)` | 注册 mod 路径（幂等），`OnAfterSetup` 中调用 |
| `Resolve` | `static string? Resolve(string modid)` | 解析 mod DLL 路径 |
| `ResolveDirectory` | `static string? ResolveDirectory(string modid)` | 解析 mod 目录（未注册返回 null） |

---

## I18n

**命名空间**：`FeatherMod` | **源码**：`I18n.cs`

语言文件加载与语言切换监听。

| 成员 | 签名 | 说明 |
|------|------|------|
| 事件 | `static event Action? OnLanguageFileLoaded` | 语言文件加载完成 |
| 字段 | `static Dictionary<SystemLanguage, string> localizedNames` | 9 种语言 → JSON 文件名映射 |
| `InitI18n` | `static void InitI18n(string modid = ModBehaviour.FrameworkName)` | 初始化（需先 `ModPathResolver.Register`） |
| `GetLangCode` | `static string GetLangCode(SystemLanguage lang)` | 语言 → 语言码（如 `zh_cn`） |
| `LoadLanguageFile` | `static void LoadLanguageFile(string loc)` | 手动加载语言文件 |

**语言文件**：mod 目录 `assets/lang/{lang_code}.json`（`en_us` / `zh_cn` / `zh_tw` / `ja_jp` / `ru_ru` / `ko_kr` / `it_it` / `fr_fr` / `sv_se`）。

---

## Registry

**命名空间**：`FeatherMod.Register` | **源码**：`Register/`

所有模块数据的注册表基座，支持 owner 追踪与按 modid 批量卸载。

### RegistryManager

| 成员 | 签名 | 说明 |
|------|------|------|
| 单例 | `static RegistryManager Instance` | 元注册表 |
| 属性 | `static string CurrentModid` | 当前 mod 作用域 |
| 静态方法 | `static IDisposable EnterModScope(string modid)` | 进入 mod 作用域（using 块） |
| 字段 | `readonly NonAlterableSimpleRegistry<ERegistry> Registry` | 元注册表（模块注册表 → 名称映射） |
| 字段 | `readonly ReverseLookupRegistry<int, int> ItemID` | TypeID 反查 |
| 方法 | `void RemoveAllByOwner(string modid)` | 按 modid 批量卸载全部注册表 |

### 接口与实现

| 类型 | 说明 |
|------|------|
| `IRegistry<T>` | 核心接口：`this[Identifier]`、`TryGet`、`Get`、`Set`、`Remove`、`Clear`、`Set(id, value, modid)`、`TryGetOwner`、`GetAllByOwner`、`RemoveAllByOwner`、可枚举 |
| `ERegistry` | 标记接口：`int RemoveAllByOwner(string modid)` |
| `SimpleRegistry<T>` | 默认实现。protected `OnRemoved(Identifier, T, string?)` 可覆写做资源清理 |
| `NonAlterableSimpleRegistry<T>` | 写入后不可覆盖；`bool SetIfAbsent(id, value, modid)` |
| `ReverseLookupRegistry<T, TKey>` | 按 native key 反查 Identifier；构造 `(Func<T, TKey> nativeKeySelector)`；`Register(TKey, Identifier, T, string)`、`TryGetIdentifier(TKey, out Identifier?)` |

---

## EventBus

**命名空间**：`FeatherMod.Events` | **源码**：`Events/`

统一事件总线，同步 + 异步双总线，自动桥接游戏原生事件。

### EventBusManager

| 成员 | 签名 | 说明 |
|------|------|------|
| 单例 | `static EventBusManager Instance` | |
| 属性 | `EventBus Sync { get; }` | 同步总线 |
| 属性 | `AsyncEventBus Async { get; }` | 异步总线（handler 为 `Func<T, UniTask>`） |
| 方法 | `void Clear()` | 清空全部 handler |

### EventBus（同步）

| 方法 | 签名 |
|------|------|
| `Register<T>` | `void Register<T>(Action<T> handler) where T : Event` |
| | `void Register<T>(Action<T> handler, int priority)` |
| | `void Register<T>(Action<T> handler, int priority, object? ownerMod)` |
| `Unregister<T>` | `bool Unregister<T>(Action<T> handler)` |
| `UnregisterAll` | `int UnregisterAll(object ownerMod)` |
| `Post` | `bool Post(Event evt)` |
| `Clear` | `void Clear()` |

### AsyncEventBus

| 方法 | 签名 |
|------|------|
| `Register<T>` | `void Register<T>(Func<T, UniTask> handler)` / `(handler, priority, ownerMod)` |
| `Unregister<T>` | `bool Unregister<T>(Func<T, UniTask> handler)` |
| `UnregisterAll` | `int UnregisterAll(object ownerMod)` |
| `Post` | `async UniTask Post(Event evt)` |
| `Clear` | `void Clear()` |

### Event 基类

| 成员 | 说明 |
|------|------|
| `abstract class Event` | 事件基类 |
| `bool Cancelled { get; }` | 是否已被取消 |
| `bool IsCancelable()` | 是否带 `[Cancelable]` 特性 |
| `void SetCancelled(bool)` | 取消事件（不可取消时抛 `NotSupportedException`） |
| `[Cancelable]` | 特性：声明事件可被 handler 取消（目前 `HurtEvent` 使用） |

### 游戏桥接事件（Events.GameEvents）

| 事件 | 触发时机 | 可取消 |
|------|----------|:------:|
| `HurtEvent` | 角色受伤 | ✅ |
| `EntityDeathEvent` | 角色死亡 | |
| `LevelInitializedEvent` | 关卡初始化完成 | |
| `MoneyChangedEvent` | 金钱变化 | |
| `LanguageChangedEvent` | 语言切换 | |
| `PlayerHearSoundEvent` | 玩家听到声音 | |
| `SoundSpawnedEvent` | 声音产生 | |
| `PlayerDeathEvent` | 玩家死亡 | |
| `ControllingCharacterChangedEvent` | 切换控制角色 | |
| `ItemUnlockStateChangedEvent` | 物品解锁状态变化 | |
| `ItemCraftedEvent` | 物品制作成功 | |
| `FormulaUnlockedEvent` | 配方解锁 | |
| `QuestTaskFinishedEvent` | 任务目标完成 | |
| `CollectSaveDataEvent` | 收集存档数据 | |
| `KillCountChangedEvent` | 击杀数变化 | |
| `MainSceneLoadedEvent` | 主场景加载完成 | |
| `SaveDeletedEvent` | 存档删除 | |

> 其它自定义事件（非游戏桥接）：`WeatherChangedEvent`/`StormStartedEvent`/`StormEndedEvent`、`NoteUnlockedEvent`/`NoteReadEvent`/`NoteRegisteredEvent`、`NpcCreatedEvent`/`NpcShopBoundEvent`、`FishCaughtEvent`、`SceneLoadStartedEvent`/`SceneLoadFinishedEvent`/`SubSceneChangedEvent` 等，见各模块文档。

---

## ModUtils

**命名空间**：`FeatherMod.Modding` | **源码**：`Modding/ModUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `IsModLoaded` | `static bool IsModLoaded(string modid)` | 已安装且激活（`ModManager` 存在 + `IsModActive`） |
| `IsModInstalled` | `static bool IsModInstalled(string modid)` | 仅检查已安装（不论是否启用） |

> 调用时机：`OnAfterSetup` 及之后（`Awake` 阶段 mod 列表未就绪）。

### ModMetaCache / ModDependencyResolver / ModMeta / ModDependency

| 类型 | 说明 |
|------|------|
| `ModMetaCache` | `Get(string)` / `TryGet(string, out ModMeta)` / `LoadAll(List<ModInfo>)` / `Clear()` |
| `ModDependencyResolver` | `Sort(List<ModInfo>)`（priority + 拓扑）/ `SortByDependencyOnly(List<ModInfo>)` |
| `ModMeta` | `ModId` / `Priority` / `Dependencies` / `LoadAfter` / `LoadBefore` / `Loaded` |
| `ModDependency` | `Name` / `WorkshopId` / `Matches(ModInfo)` |

---

## AssetUtil

**命名空间**：`FeatherMod` | **源码**：`AssetUtil.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `LoadBundle` | `static AssetBundle? LoadBundle(Identifier id)` | 从 mod 目录 `assets/bundle/{path}` 加载（缓存） |
| | `static AssetBundle? LoadBundle(string bundleName)` | 便捷重载（需已注册路径） |
| `LoadBundleFromDir` | `static AssetBundle? LoadBundleFromDir(string modDirectory, string bundleName)` | 指定目录 |
| `UnloadBundle` | `static void UnloadBundle(string modDirectory, string bundleName, bool unloadAllLoadedObjects = true)` | 卸载单个 |
| `UnloadAllBundles` | `static void UnloadAllBundles(bool unloadAllLoadedObjects = true)` | 卸载全部缓存（`OnBeforeDeactivate` 调用） |

> 已加载 Bundle 缓存复用，重复调用返回同一实例。

---

## fml.json / ModMeta

模组根目录声明文件，游戏 Rescan 时自动加载。

| 字段 | 类型 | 必填 | 默认 | 说明 |
|------|------|:----:|------|------|
| `modid` | string | ✅ | — | 必须与 `info.ini` 的 `name` 一致 |
| `priority` | int | | `int.MaxValue` | 越小越先加载 |
| `dependencies` | string[] | | `[]` | 硬依赖：目标必须存在且激活 |
| `loadAfter` | string[] | | `[]` | 软依赖：仅保证排在其后 |

**机制**：priority 升序 + 拓扑排序满足依赖约束；循环依赖输出参与 mod 名并回退仅按 priority 排序。

```jsonc
{
    "modid": "MyOverhaul",
    "priority": 200,
    "dependencies": ["FeatherMod"],
    "loadAfter": ["MyWeaponPack", "MyQuestPack"]
}
```

---

## SaveUtils

**命名空间**：`FeatherMod.Saves` | **源码**：`Saves/SaveUtils.cs`

| 方法 | 签名 | 说明 |
|------|------|------|
| `MakeKey` | `static string MakeKey(Identifier identifierKey)` | 生成 ES3 存档 key |
| `KeyExists` | `static bool KeyExists(Identifier identifierKey)` | |
| `Load<T>` | `static T? Load<T>(Identifier identifierKey)` | |
| | `static T Load<T>(Identifier identifierKey, T defaultValue)` | |
| `Save<T>` | `static void Save<T>(Identifier identifierKey, T? value)` | |
| `Delete<T>` | `static void Delete<T>(Identifier identifierKey)` | |

> `ES3Validator.CanBeSerializedByES3<T>()` 可预检类型可序列化性。

---

## 废弃 API / Obsolete

| 成员 | 替代 |
|------|------|
| （本模块暂无） | |

_已废弃 API 从新代码中禁用，详见各模块文档的废弃表。_
