# Feather API Reference / FML API 参考

> **本文档是 FML（Feather Modding Lib）的 API 参考手册**——面向"有什么可用"。
> 教程与场景化示例请见 [Docs/USAGE.md](../USAGE.md)。
>
> **Audience / 受众**：AI Agent（检索 API 签名）与人类开发者（速查）。
>
> **Maintenance / 维护约定**：修改任何 public API 时必须同步更新本目录对应文件。详见 [AGENTS.md 文档同步约束](../../AGENTS.md)。

---

## 文档体系 / Document Tree

```
Docs/
├── USAGE.md          # 使用指南（教程式：怎么用）
└── API/              # API 参考（参考式：有什么）← 你在这里
    ├── API.md               # 索引（本文件）
    ├── API_CORE.md          # 核心：Identifier / Registry / EventBus / I18n / ModUtils / AssetUtil / fml.json
    ├── API_ITEMS.md         # 物品：ItemUtils / ItemData / GameItemLookup / TagUtils / ItemGraphic / ModelUtils
    ├── API_CRAFTING.md      # 合成：CraftingUtils / ItemEntry / CraftingFormulaData / DecomposeFormulaData
    ├── API_QUESTS.md        # 任务：QuestUtils / QuestData / TaskData / RewardData / QuestGiverUtils
    ├── API_BUILDING.md      # 建筑：BuildingUtils / BuildingConfig / MachineRecipe / BuildingBehaviour / TimeUtils
    ├── API_PERK_ENDOWMENT.md # Perk 与天赋：PerkTreeUtils / PerkConfig / EndowmentUtils / EndowmentConfig
    ├── API_ENTITIES.md      # 实体：EnemyUtils / FriendlyNpcUtils / EquipmentUtils / CustomFaceUtils / 注入器
    ├── API_INTERACTION_UI.md # 交互与 UI：InteractionUtils / ViewDispatcher / GameUIUtils / SimpleViewBuilder / ModOptions
    └── API_SYSTEM.md        # 系统：Audio / Economy / Buff / Container / Note / Fishing / Weather / Scene / Dialogue / Save
```

---

## 模块地图 / Module Map

| 模块 | 入口类 | 文档 |
|------|--------|------|
| 物品 Items | `ItemUtils` | [API_ITEMS.md](API_ITEMS.md) |
| 合成 Crafting | `CraftingUtils` | [API_CRAFTING.md](API_CRAFTING.md) |
| 任务 Quests | `QuestUtils` | [API_QUESTS.md](API_QUESTS.md) |
| 商店 Shop | `ShopUtils` | [API_SYSTEM.md](API_SYSTEM.md#shoputils--shopgoodsdata) |
| 音频 Audio | `AudioUtil` | [API_SYSTEM.md](API_SYSTEM.md#audioutil--audiodata) |
| 经济 Economy | `EconomyUtils` | [API_SYSTEM.md](API_SYSTEM.md#economyutils) |
| Buff 状态 | `BuffUtils` | [API_SYSTEM.md](API_SYSTEM.md#buffutils) |
| 建筑 Building | `BuildingUtils` | [API_BUILDING.md](API_BUILDING.md) |
| Perk 技能树 | `PerkTreeUtils` | [API_PERK_ENDOWMENT.md](API_PERK_ENDOWMENT.md#perktreeutils) |
| 天赋 Endowment | `EndowmentUtils` | [API_PERK_ENDOWMENT.md](API_PERK_ENDOWMENT.md#endowmentutils) |
| 敌人 Enemy | `EnemyUtils` | [API_ENTITIES.md](API_ENTITIES.md#enemyutils) |
| 友善 NPC | `FriendlyNpcUtils` | [API_ENTITIES.md](API_ENTITIES.md#friendlynpcutils) |
| 捏脸 Custom Face | `CustomFaceUtils` | [API_ENTITIES.md](API_ENTITIES.md#customfaceutils) |
| 装备 Equipment | `EquipmentUtils` | [API_ENTITIES.md](API_ENTITIES.md#equipmentutils) |
| 武器注入 | `WeaponInjectionUtils` | [API_ENTITIES.md](API_ENTITIES.md#weaponinjectionutils) |
| 抽奖箱注入 | `LotteryBoxUtils` | [API_ENTITIES.md](API_ENTITIES.md#lotteryboxutils) |
| 交互 Interaction | `InteractionUtils` / `ViewDispatcher` | [API_INTERACTION_UI.md](API_INTERACTION_UI.md) |
| UI 桥接 | `GameUIUtils` / `SimpleViewBuilder` | [API_INTERACTION_UI.md](API_INTERACTION_UI.md) |
| 设置面板 Options | `ModOptionsRegistry` | [API_INTERACTION_UI.md](API_INTERACTION_UI.md#modoptionsregistry) |
| 容器 Container | `ContainerUtils` | [API_SYSTEM.md](API_SYSTEM.md#containerutils) |
| 笔记 Note | `NoteUtils` | [API_SYSTEM.md](API_SYSTEM.md#noteutils) |
| 钓鱼 Fishing | `FishingUtils` | [API_SYSTEM.md](API_SYSTEM.md#fishingutils) |
| 天气 Weather | `WeatherUtils` | [API_SYSTEM.md](API_SYSTEM.md#weatherutils) |
| 多场景 Multi-Scene | `MultiSceneUtils` | [API_SYSTEM.md](API_SYSTEM.md#multisceneutils) |
| 对话 Dialogue | `DialogueManager` / `DialogueTrigger` | [API_SYSTEM.md](API_SYSTEM.md#dialoguemanager) |
| 本地化 I18n | `I18n` | [API_CORE.md](API_CORE.md#i18n) |
| 事件总线 EventBus | `EventBusManager` | [API_CORE.md](API_CORE.md#eventbus) |
| 注册表 Registry | `RegistryManager` | [API_CORE.md](API_CORE.md#registry) |
| 跨模组联动 | `ModUtils` | [API_CORE.md](API_CORE.md#modutils) |
| AssetBundle | `AssetUtil` | [API_CORE.md](API_CORE.md#assetutil) |
| 存档 Save | `SaveUtils` | [API_SYSTEM.md](API_SYSTEM.md#saveutils) |

---

## 命名空间速查 / Namespace Quick Reference

| 命名空间 | 包含 |
|----------|------|
| `FeatherMod` | `ItemUtils`, `CraftingUtils`, `QuestUtils`, `ShopUtils`, `EconomyUtils`, `BuffUtils`, `BuildingUtils`, `PerkTreeUtils`, `EndowmentUtils`, `EnemyUtils`, `AssetUtil`, `I18n`, `ContainerUtils`, `NoteUtils`, `FishingUtils`, `FriendlyNpcUtils`, `CustomFaceUtils`, `WeatherUtils`, `MultiSceneUtils`, `DialogueManager`, `DialogueTrigger`, `ModBehaviour`, `LotteryBoxUtils`, `WeaponInjectionUtils`, `EquipmentUtils`, `QuestGiverUtils`, `TimeUtils`, `ItemGraphicUtils`, `ModelUtils`, `MachineRecipe`, `BuildingBehaviour`, `DialogueLine`, `DialogueSequence`, `ItemData`, `BulletData`, `BlueprintData`, `UsageData`, `ModifierData`, `SlotData`, `SlotKeys` |
| `FeatherMod.Modding` | `ModUtils`, `ModMetaCache`, `ModMeta`, `ModDependency`, `ModDependencyResolver`, `ModManagerPatches` |
| `FeatherMod.Utils` | `Identifier`, `Singleton<T>`, `ModPathResolver`, `TimeUtils`, `CameraUtils`, `ShaderReplacer`, `WildcardHelper`, `WeaponClassifier` |
| `FeatherMod.Items` | `GameItemLookup`, `TagUtils`, `TagConfig` |
| `FeatherMod.Crafting` | `CraftingFormulaData`, `DecomposeFormulaData`, `ItemEntry`, `TagCostEntry`, `TagItemCost` |
| `FeatherMod.Quests` | `QuestData`, `QuestDialogue`, `TaskData` 及子类, `RewardData` 及子类, `FMLTask_KillCountByTag`, `FMLTask_SubmitItemByTag` |
| `FeatherMod.Entities` | `IStateConfig`, `Transition`, `StateMachineToBT`, `EnemyPresetData`, `FriendlyNpcConfig`, `FaceRef`, `NpcRole`, `ModelRef`, `EnemyRegistry` |
| `FeatherMod.Interaction` | `InteractionUtils`, `ViewDispatcher`, `GameViews`, `InteractionRegistry`, `InteractionGroupBuilder`, `InteractionEntry` |
| `FeatherMod.Interaction.Components` | `ViewInteractHandler`, `DelegateInteractHandler`, `FeatherShopInteract`, `FeatherPerkTreeInteract`, `FeatherQuestGiverInteract`, `FeatherFormulasRegisterInteract` |
| `FeatherMod.UI` | `GameUIUtils`, `GameUIColorPalette`, `SimpleViewBuilder`, `InteractTemplates` |
| `FeatherMod.Register` | `IRegistry<T>`, `SimpleRegistry<T>`, `NonAlterableSimpleRegistry<T>`, `ReverseLookupRegistry<T,TKey>`, `RegistryManager`, `ERegistry` |
| `FeatherMod.Events` | `EventBusManager`, `EventBus`, `AsyncEventBus`, `Event`, `CancelableAttribute` |
| `FeatherMod.Events.GameEvents` | 18 个游戏桥接事件（`HurtEvent`, `MoneyChangedEvent` 等） |
| `FeatherMod.Audio` | `AudioUtil`, `AudioData` |
| `FeatherMod.Options` | `ModOptionsRegistry`, `ModOptionsBuilder` |
| `FeatherMod.Saves` | `SaveUtils`, `ES3Validator` |
| `FeatherMod.Minigame` | `MinigameUtil` |

---

## 全局约定 / Global Conventions

1. **Identifier 优先**：所有 public API 使用 `Identifier("domain", "path")`。游戏原生数字 ID 由 FML 内部分配，modder 不接触。
2. **duckov 域**：引用游戏原版内容使用 `Identifier("duckov", "Name")`。
3. **UniTask 异步**：异步 API 一律返回 `UniTask` / `UniTask<T>`，禁止 `async Task`。
4. **自动卸载**：所有注册资源绑定 modid，卸载时自动清理（见 [USAGE § 生命周期](../USAGE.md)）。
5. **`[Obsolete]` 标注**：已废弃 API 仍可用，但新代码禁用，文档中集中列出。
6. **modid 推导**：多数 API 的 `modid` 参数可省略，默认从 `Identifier.Domain` 推导。

---

## 版本记录 / Change Log

| 日期 | 变更 |
|------|------|
| 2026-08-07 | 首次建立 API 文档体系（按模块拆分 9 个文件 + 索引） |
