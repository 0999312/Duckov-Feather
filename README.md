# Feather · Feather Modding Lib

> *为《逃离鸭科夫》(Escape From Duckov) 量身打造的声明式 Mod 开发框架——轻如羽毛，快 10 倍。*

<p align="center">
  <img src="https://img.shields.io/badge/framework-.NET%20Standard%202.1-512BD4?style=flat-square&logo=.net" alt=".NET Standard 2.1">
  <img src="https://img.shields.io/badge/Harmony-2.4.1.0-008080?style=flat-square" alt="Harmony 2.4.1.0">
  <img src="https://img.shields.io/badge/status-活跃开发中-brightgreen?style=flat-square" alt="Status">
  <img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License">
  <img src="https://img.shields.io/badge/Patch-22%20个-blueviolet?style=flat-square" alt="Patches">
  <img src="https://img.shields.io/badge/module-29%20个-orange?style=flat-square" alt="Modules">
</p>

---

## 简介

**Feather**（Feather Modding Lib，简称 FML）将《逃离鸭科夫》Mod 开发中常见的底层复杂性——数字 ID 映射、Harmony 补丁冲突、资源生命周期追踪、事件桥接——全部封装到框架内部。模组作者只需写**纯 C# 代码**，即可完成从物品、合成、任务到天赋、技能树、自定义敌人的全部开发。

**核心价值**：用 `Identifier("mymod", "coffee")` 替代游戏原生的神秘数字 ID；用声明式 API 注册替代冗长的样板代码；用自动卸载替代手动的资源清理。

---

## 📖 目录

- [核心原则](#核心原则)
- [快速开始](#快速开始)
- [模块速览](#模块速览)
- [架构概览](#架构概览)
- [技术栈](#技术栈)
- [工程配置](#工程配置)
- [文档](#文档)
- [常见问题 (FAQ)](#常见问题-faq)
- [贡献指南](#贡献指南)
- [使用 FML 的模组](#使用-fml-的模组)
- [社区 / 讨论](#社区--讨论)
- [鸣谢 / 致谢](#鸣谢--致谢)
- [项目状态](#项目状态)

---

## 核心原则

### Identifier 优先

所有 FML public API 统一使用 `Identifier("domain", "path")` 作为资源标识符。游戏原生的数字 ID（TypeID、EndowmentIndex 等）由 FML 内部自动分配、映射和冲突检测，对模组作者完全透明。

```csharp
// ✅ 正确
ItemUtils.CreateCustomItem(new Identifier("mymod", "coffee"), config);

// ❌ 禁止：不再公开数字 ID API
ItemUtils.CreateCustomItem(50001, config);
```

### 引用原版内容

通过 `duckov` 域引用游戏原版物品，支持标签浏览和数字 ID 反查：

```csharp
// 已知物品名称
ItemEntry.Of(Identifier("duckov", "Waterbottle"), 1)

// 只知道数字 TypeID → 反查
GameItemLookup.TryGetIdentifier(1001, out var id);

// 按标签浏览
GameItemLookup.TryFindByTag("Gun", out var guns);
```

### 声明式注册

调用一个方法即完成注册、映射、持久化。不需要手动管理字典、生命周期或卸载逻辑。所有通过 FML API 注册的资源自动绑定到 modid，模组卸载时框架自动清理。

---

## 快速开始

```csharp
public class MyMod : Duckov.Modding.ModBehaviour, IHasModid
{
    public string GetModid() => "MyMod";

    protected override void OnAfterSetup()
    {
        base.OnAfterSetup();

        // 注册自定义物品
        ItemUtils.CreateCustomItem(
            new Identifier("MyMod", "coffee"),
            new ItemData { itemId = 50001, maxStackCount = 10, ... });

        // 引用原版物品作为合成材料
        CraftingUtils.AddCraftingFormula(new CraftingFormulaData
        {
            Id = new Identifier("MyMod", "brew_coffee"),
            Money = 100,
            CostItems = new[] { ItemEntry.Of("duckov:Waterbottle", 1) },
            Result = ItemEntry.Of("MyMod:coffee", 1)
        });
    }
}
```

---

## 模块速览

FML 提供 **29 个功能模块**，覆盖 Mod 开发的大部分场景：

| 模块 | 入口类 | 示例 API |
|------|--------|---------|
| **物品** | `ItemUtils` | `CreateCustomItem(id, config)` |
| **合成** | `CraftingUtils` | `AddCraftingFormula(data)` |
| **任务** | `QuestUtils` | `RegisterQuest(id, data)` |
| **商店** | `ShopUtils` | `AddGoods(data)` |
| **音频** | `AudioUtil` | `RegisterAudio(id, data)` |
| **本地化** | `I18n` | `InitI18n()` |
| **事件总线** | `EventBusManager` | `Sync.Register<T>(handler)` |
| **跨模组联动** | `ModUtils` | `IsModLoaded(modid)` / `IsModInstalled(modid)` |
| **经济** | `EconomyUtils` | `UnlockItem(id)` |
| **Buff** | `BuffUtils` | `RegisterBuff(id, prefab)` |
| **建筑** | `BuildingUtils` | `RegisterBuilding(id, info, prefab)` |
| **Perk 技能树** | `PerkTreeUtils` | `AddPerk(id, req, icon)` |
| **天赋** | `EndowmentUtils` | `RegisterEndowment(id, config)` |
| **敌人** | `EnemyUtils` | `RegisterEnemy(id, aiConfig, preset)` |
| **NPC 武器注入** | `WeaponInjectionUtils` | `AddWeaponToPreset(pattern, item, chance)` |
| **抽奖箱注入** | `LotteryBoxUtils` | `AddItemToLotteryBox(pattern, item)` |
| **交互系统** | `InteractionUtils` | `SpawnViewInteract(id, pos, view)` |
| **View 调度** | `ViewDispatcher` | `Register(viewType, handler, modid)` |
| **UI 桥接** | `GameUIUtils` | `CloneButton(parent, label, onClick)` |
| **UI 构建器** | `SimpleViewBuilder` | `Create(name).AddGameButton().Build()` |
| **物品容器** | `ContainerUtils` | `CreateContainer(id, slots, modid)` |
| **设置面板** | `ModOptionsRegistry` | `RegisterPanel(modId, name, builder)` |
| **笔记** | `NoteUtils` | `RegisterNote(id, config)` |
| **钓鱼** | `FishingUtils` | `RegisterFishingPool(id, config)` |
| **友善 NPC** | `FriendlyNpcUtils` | `CreateFriendlyNpc(id, config)` |
| **天气** | `WeatherUtils` | `GetCurrentWeather()` |
| **多场景** | `MultiSceneUtils` | `TeleportTo(sceneId, location)` |
| **对话** | `DialogueUtils` | `PlaySubtitles(actorId, lines)` |
| **AssetBundle** | `AssetUtil` | `LoadBundle("weapons")` |

**原版内容反查**：

| 模块 | 反查 API |
|------|---------|
| Item | `GameItemLookup.TryGetIdentifier(int, out Identifier)` |
| Buff | `BuffUtils.TryGetBuffIdentifier(int, out Identifier)` |
| Quest | `QuestUtils.TryGetQuestIdentifier(int, out Identifier)` |
| Endowment | `EndowmentRegistry.TryGetIdentifier(EndowmentIndex, out Identifier)` |

---

## 架构概览

### 四层架构

```
┌──────────────────────────────────────────────────┐
│  模组层 (Modder Code)                              │
│  MyMod : ModBehaviour + IHasModid                 │
│  → ItemUtils / CraftingUtils / QuestUtils / ...   │
├──────────────────────────────────────────────────┤
│  框架层 (FML Core)                                 │
│  RegistryManager ─── EventBusManager               │
│  SimpleRegistry<T>   EventBus / AsyncEventBus      │
│  ModMetaCache        GameEventAdapters (15+)       │
├──────────────────────────────────────────────────┤
│  桥接层 (Harmony Patches)                          │
│  22 个补丁点，覆盖 Building / Perk / Endowment     │
│  / Crafting / Enemy / LotteryBox / ModManager      │
├──────────────────────────────────────────────────┤
│  游戏层 (Duckov Native)                            │
│  ItemAssetsCollection / CraftingManager            │
│  EndowmentManager / ModManager / ...               │
└──────────────────────────────────────────────────┘
```

### 生命周期

**启动**：游戏加载 FML → Harmony PatchAll → ModManagerPatches（排序 + 自激活）→ 各模块 Init（幂等）→ EventBus 桥接 15 个原生游戏事件

**运行时**：模组调用 API → Registry 追踪 owner → EventBus 派发事件

**卸载**：`GameEventAdapters.TearDown()` 解除原生事件 → `EventBusManager.Clear()` 清空 handler → `RegistryManager.RemoveAllByOwner()` 批量卸载全部资源（三步自动执行，无需手动处理）

### 关键指标

- **22 个** Harmony 补丁点（全部独立 try/catch，失败不崩溃）
- **15 个** 原生游戏事件桥接
- **29 个** 功能模块
- **0 反射**（模组侧——Publicizer 使 FML 内部直接访问游戏私有成员，对模组透明）

---

## 技术栈

| 组件 | 版本/说明 |
|------|----------|
| 目标框架 | .NET Standard 2.1 |
| Harmony | 2.4.1.0（vendored） |
| Publicizer | Krafs.Publicizer（内部使用，模组无感） |
| 异步 | UniTask |
| Mod 排序 | 拓扑排序 + `fml.json` 声明式依赖 |

---

## 工程配置

1. 准备《逃离鸭科夫》游戏本体
2. 创建 **.NET Standard 2.1** 类库项目
3. 添加游戏 DLL 引用：

```xml
<ItemGroup>
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\TeamSoda.*" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\ItemStatsSystem.dll" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\Unity*" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\Newtonsoft.Json.dll" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\FMODUnity.dll" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\ParadoxNotion.dll" />
  <Reference Include="$(DuckovPath)\Duckov_Data\Managed\UniTask*" />
</ItemGroup>
```

4. 继承 `Duckov.Modding.ModBehaviour` 并实现 `IHasModid`，手动导入 FML 的 DLL

---

## 文档

| 文档 | 说明 |
|------|------|
| [Docs/USAGE.md](Docs/USAGE.md) | 完整使用指南 — 快速开始、全模块 API（32 章） |
| [Docs/MIGRATION.md](Docs/MIGRATION.md) | 迁移指南 — 从旧版 FML 升级 |
| [Docs/PROGRESS.md](Docs/PROGRESS.md) | 项目进度 — Phase 完成状态与变更记录 |

---

## 常见问题 (FAQ)

<details>
<summary><b>为什么不能用数字 ID？必须走 Identifier？</b></summary>

数字 ID（TypeID）是游戏内部实现细节，各模组之间容易冲突。FML 使用 `Identifier("modid", "name")` 作为统一标识符，由框架自动分配唯一数字 ID 并检测冲突。详见 <a href="#核心原则">核心原则</a>。
</details>

<details>
<summary><b>如何从旧版 FML 迁移？</b></summary>

核心变化：继承 `ModBehaviour` + 实现 `IHasModid`，使用 `OnAfterSetup` 替代 `Awake`，所有 API 改为 Identifier 版本。详见 <a href="Docs/MIGRATION.md">迁移指南</a>。
</details>

<details>
<summary><b>CreateCustomItem 和 CreateCustomItemAsync 有什么区别？</b></summary>

`CreateCustomItem` 同步阻塞主线程加载 Sprite；`CreateCustomItemAsync` 通过 UniTask 在线程池异步加载，**推荐优先使用异步版本**，避免游戏卡顿。
</details>

<details>
<summary><b>我的 Mod 依赖另一个 Mod，怎么声明？</b></summary>

在 Mod 根目录创建 `fml.json`，声明依赖：

```json
{
    "modid": "MyMod",
    "priority": 100,
    "dependencies": [{ "name": "OtherMod" }]
}
```

FML 会自动按拓扑排序确保依赖先加载。如需**运行时条件注册**（可选联动），可使用 `ModUtils.IsModLoaded(modid)` 在代码中做分支判断。详见 `fml.json` 完整配置说明和 [USAGE.md §34 跨模组联动](Docs/USAGE.md#34-跨模组联动modutils)。
</details>

<details>
<summary><b>模组卸载时需要手动清理资源吗？</b></summary>

**不需要。** FML 自动追踪所有通过 Registry 注册的资源并绑定到 modid。卸载时执行三步清理：解除原生事件 → 清空 EventBus → 批量卸载 Registry。详见 <a href="#生命周期">生命周期</a>。
</details>

<details>
<summary><b>FML 会和其它 Mod 的 Harmony 补丁冲突吗？</b></summary>

FML 的所有 22 个补丁均使用独立的 `try/catch` 包裹，单个补丁失败不会导致游戏崩溃，也不会阻止其它补丁生效。日志会输出具体的失败原因便于排查。
</details>

---

## 贡献指南

欢迎贡献代码、报告 Bug 或提出功能建议！

- **报告 Bug**：请附上复现步骤、日志输出和 FML 版本号
- **功能建议**：建议先在 [Discussion](#社区--讨论) 中讨论可行性
- **提交代码**：遵循项目现有的代码风格，确保改动通过编译，并添加必要的注释

更多细节请参阅 [CONTRIBUTING.md](CONTRIBUTING.md)（待完善）。

---

## 使用 FML 的模组

以下是目前已知使用 FML 开发的模组：

| 模组 | 简介 | 链接 |
|------|------|------|
| **鸭科夫武器示例工程** | Feather 的示例模组之一，展示如何用 FML 快速构建武器模组 | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3610492072) |
| **PLA 武器拓展** | 添加严格遵循原版美术风格的 PLA 现役经典枪族，广受好评 | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3610492072) |
| **顶级战术武器拓展** | 为鸭科夫注入多元现代火力，广受好评的顶级武器模组 | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3610492072) |
| **粽子** | 非常简单的粽子 Mod，解决了鸭星端午节没有粽子的问题 | [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3747820901) |

> *你的模组用了 FML？欢迎提交 PR 加入此列表！*

---

## 社区 / 讨论

有关使用问题、功能建议或 Mod 开发交流，欢迎通过以下方式参与：

- 🐧 **QQ 群**：鸭星零号区饮品铺 `953260190`
- 🎮 **Steam 讨论区**：[前往讨论](https://steamcommunity.com/sharedfiles/filedetails/?id=3610491835)
- 🐛 **Bug / 功能建议**：欢迎在 [GitHub Issues](../../issues) 提交
- 💡 目前暂无 Discord，建议优先在 QQ 群、GitHub Issues 和 Steam 讨论区交流

---

## 鸣谢 / 致谢

FML 的开发离不开以下开源项目和社区的支持：

| 项目 | 用途 |
|------|------|
| [Harmony](https://github.com/pardeike/Harmony) | C# 运行时方法补丁库 — FML 与游戏桥接的基石 |
| [UniTask](https://github.com/Cysharp/UniTask) | 零分配异步/等待库 — 异步加载和分帧事件派发 |
| [Krafs.Publicizer](https://github.com/krafs/Publicizer) | free/remove internal — 框架内部安全访问游戏原生 API |
| [Newtonsoft.Json](https://www.newtonsoft.com/json) | JSON 序列化 — fml.json 解析 |
| [逃离鸭科夫](https://store.steampowered.com/app/3593260) | 游戏本体 — 感谢 **Team Soda（碳酸小队）** 工作室创造的优秀游戏 |
| [Minecraft-Style Framework](https://github.com/0999312/Minecraft-Style-Framework) | 架构参考 — FML 的 Identifier 优先 + Registry 体系设计灵感来源 |

以及所有参与测试、反馈和贡献的 Mod 开发者们 🙏

---

## 项目状态

| Phase | 名称 | 状态 |
|-------|------|------|
| Phase 0 | 仓库与工程基础整理 | ✅ 已完成 |
| Phase 1 | 框架内核加固（EventBus + Registry） | ✅ 已完成 |
| Phase 2 | 头部消费系统（Economy / Buff / Options） | ✅ 已完成 |
| Phase 3 | 内容创作系统（Shop / Audio / Perk / Building / Enemy） | ✅ 已完成 |
| Phase 4 | Building / Perk / Endowment / UI 深化 | ✅ 已完成 |
| Phase 5 | 长尾幂等系统（Note / Fishing / NPC / Weather / Multi-Scene） | ✅ 已完成 |
| Phase 6 | 质量（测试 / 示例 / CI/CD） | ⏳ 待启动 |

---

## License

见 [LICENSE](LICENSE) 文件。
