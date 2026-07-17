# Feather 使用文档 / Usage Guide

_面向全新模组项目的完整使用指南。如果你是第一次使用 FML 开发《逃离鸭科夫》模组，请从此处开始。_

---

## 目录

1. [快速开始](#1-快速开始)
2. [模组主类（ModBehaviour）](#2-模组主类)
3. [Identifier 标识符系统](#3-identifier-标识符系统)
4. [物品系统（ItemUtils）](#4-物品系统itemutils)
5. [合成配方（CraftingUtils）](#5-合成配方craftingutils)
6. [任务系统（QuestUtils）](#6-任务系统questutils)
7. [商店系统（ShopUtils）](#7-商店系统shoputils)
8. [音频系统（AudioUtil）](#8-音频系统audioutil)
9. [本地化（I18n）](#9-本地化i18n)
10. [事件总线（EventBus）](#10-事件总线eventbus)
11. [经济系统（EconomyUtils）](#11-经济系统economyutils)
12. [Buff 状态效果（BuffUtils）](#12-buff-状态效果buffutils)
13. [建筑系统（BuildingUtils）](#13-建筑系统buildingutils)
14. [Perk 技能树（PerkTreeUtils）](#14-perk-技能树perktreeutils)
15. [天赋系统（EndowmentUtils）](#15-天赋系统endowmentutils)
16. [敌人系统（EnemyUtils）](#16-敌人系统enemyutils)
17. [NPC 武器注入（WeaponInjectionUtils）](#17-npc-武器注入weaponinjectionutils)
18. [抽奖箱注入（LotteryBoxUtils）](#18-抽奖箱注入lotteryboxutils)
19. [交互系统（InteractionUtils）](#19-交互系统interactionutils)
20. [UI 系统与控件桥接（GameUIUtils）](#20-ui-系统与控件桥接gameuiutils)
21. [物品容器（ContainerUtils）](#21-物品容器containerutils)
22. [自定义设置面板（ModOptionsRegistry）](#22-自定义设置面板modoptionsregistry)
23. [AssetBundle 加载（AssetUtil）](#23-assetbundle-加载assetutil)
24. [笔记系统（NoteUtils）](#24-笔记系统noteutils)
25. [钓鱼系统（FishingUtils）](#25-钓鱼系统fishingutils)
26. [友善 NPC（FriendlyNpcUtils）](#26-友善-npcfriendlynpcutils)
27. [捏脸系统（CustomFaceUtils）](#27-捏脸系统customfaceutils)
28. [天气系统（WeatherUtils）](#28-天气系统weatherutils)
29. [多场景（MultiSceneUtils）](#29-多场景multisceneutils)
30. [对话系统（DialogueUtils）](#30-对话系统dialogueutils)
31. [注册表系统（Registry）](#31-注册表系统registry)
32. [模组卸载生命周期](#32-模组卸载生命周期)
33. [NPC 装备系统（EquipmentUtils）](#33-npc-装备系统equipmentutils)
34. [附录：项目结构参考](#34-附录项目结构参考)

---

## 1. 快速开始

### 1.1 创建工程

1. 通过 Visual Studio 创建一个 **.NET 类库**（Class Library）。
2. 目标框架（Target Framework）设置为 **.NET Standard 2.1**。
3. 注意删除 `<ImplicitUsings>`（.NET Standard 2.1 不支持）。

### 1.2 配置 csproj

在 `.csproj` 中添加游戏 DLL 引用和 FML 引用：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <!-- 游戏 DLL 引用（通过环境变量 DUCKOV_PATH 指定游戏路径） -->
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\TeamSoda.*" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\ItemStatsSystem.dll" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\Unity*" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\Newtonsoft.Json.dll" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\FMODUnity.dll" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\ParadoxNotion.dll" />
    <Reference Include="$(DUCKOV_PATH)\Duckov_Data\Managed\UniTask*" />
    <!-- FML dll -->
    <Reference Include="path\to\FeatherMod.dll" />
  </ItemGroup>
</Project>
```

> 你也可以通过 `DUCKOV_PATH` 环境变量或 `DuckovPath` 属性指定游戏路径，详见 FML 项目自身的 `.csproj`。

### 1.3 编写第一个模组

```csharp
using FeatherMod;
using FeatherMod.Utils;
using HarmonyLib;
using System.Reflection;

public class MyFirstMod : Duckov.Modding.ModBehaviour, IHasModid
{
    string dllPath = Assembly.GetExecutingAssembly().Location;

    public string GetModid() => "MyFirstMod";

    protected override void OnAfterSetup()
    {
        // 注册 mod 路径，供 I18n / Sprite / Bundle 自动解析
        ModPathResolver.Register(GetModid(), dllPath);
        I18n.InitI18n(GetModid());

        // 自行创建 Harmony 实例并 patch 自身的 [HarmonyPatch]
        var harmony = new Harmony(GetModid());
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        // 在这里注册你的物品、配方、任务……
    }
}
```

> **关键点**：
> - 继承 **`Duckov.Modding.ModBehaviour`**（游戏引擎基类），**不**继承 `FeatherMod.ModBehaviour`
> - 实现 **`IHasModid`** 接口 — FML 工具通过此接口获取你的 mod 身份
> - `FMLBootstrap` 自动管理 Registry / EventBus 等游戏级单例——你只需调用 FML 工具方法即可

---

## 2. 模组主类

所有依赖 FML 的模组应直接继承 `Duckov.Modding.ModBehaviour`（游戏引擎基类）并实现 `IHasModid` 接口。

> **注意**：`FeatherMod.ModBehaviour` 是 FML 自身的入口类，由 ModManager 实例化。**外部模组不应继承它。**

```csharp
public class MyMod : Duckov.Modding.ModBehaviour, IHasModid
{
    string dllPath = Assembly.GetExecutingAssembly().Location;

    public string GetModid() => "MyModId";

    protected override void OnAfterSetup()
    {
        ModPathResolver.Register(GetModid(), dllPath);
        I18n.InitI18n(GetModid());

        // 自行管理 Harmony：
        var harmony = new Harmony(GetModid());
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        // 注册你的内容……
    }
}
```

### 生命周期

| 阶段 | 方法 | 说明 |
|------|------|------|
| 游戏启动 | `Awake()` | 游戏引擎调用 |
| 初始化就绪 | `OnAfterSetup()` | 执行自定义初始化：注册路径 → Harmony.PatchAll → 调用 FML 工具方法注册内容 |
| 模组卸载 | `OnBeforeDeactivate()` | 自行清理注册的资源 |

> FML 提供的 Registry / EventBus 等游戏级单例由 `FMLBootstrap` 自动管理，无需手动处理。

---

## 2.1 fml.json — 声明式模组配置

每个模组可在其根目录放置 `fml.json` 文件，声明优先级、依赖关系和自激活策略。
FML 在游戏 Rescan 模组列表时自动加载并应用。

### 文件格式

```jsonc
{
    "modid": "MyMod",           // 必填：模组标识符，必须与 info.ini 中的 name 一致
    "priority": 100,            // 可选：加载优先级（越小越先加载，默认 int.MaxValue 即最低）
    "dependencies": [           // 可选：硬依赖，被依赖的 mod 必须存在且已激活
        "FeatherMod",
        "SomeOtherMod"
    ],
    "loadAfter": [              // 可选：软依赖，仅排在目标之后加载（不要求目标存在或激活）
        "OptionalMod"
    ],
    "autoActivate": true        // 可选：若玩家未手动开启但依赖全部就绪，自动激活本 mod
}
```

### 字段说明

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `modid` | string | **是** | — | 必须与 `info.ini` 的 `name` 完全一致，否则 fml.json 被忽略 |
| `priority` | int | 否 | `int.MaxValue` | 越小越优先加载。FML 自身固定为最高优先级 |
| `dependencies` | string[] | 否 | `[]` | **硬依赖**：目标必须存在且已激活，否则本 mod 不会被自动激活 |
| `loadAfter` | string[] | 否 | `[]` | **软依赖**：仅保证排在目标之后，目标不存在或未激活时不报错 |
| `autoActivate` | bool | 否 | `false` | 设为 `true` 后，即使玩家未手动勾选，只要全部依赖就绪即自动激活 |

### 加载机制

1. 游戏 `Rescan` 模组列表时，FML 遍历所有 mod 目录读取 `fml.json`
2. **排序**：先按 `priority` 升序排列，再拓扑排序满足 `dependencies` + `loadAfter` 约束
3. **循环依赖检测**：存在环时输出具体参与 mod 名称，回退为仅按 priority 排序
4. **自激活**：`autoActivate: true` 的 mod 在全部 `dependencies` 激活后自动启用

### 示例

**基础模组（仅声明身份）**
```json
{ "modid": "MySimpleMod" }
```

**带优先级的武器包**
```json
{
    "modid": "MyWeaponPack",
    "priority": 50,
    "dependencies": ["FeatherMod"],
    "autoActivate": true
}
```

**带软依赖的大型模组**
```json
{
    "modid": "MyOverhaul",
    "priority": 200,
    "dependencies": ["FeatherMod"],
    "loadAfter": ["MyWeaponPack", "MyQuestPack"],
    "autoActivate": false
}
```

---

## 3. Identifier 标识符系统

Identifier 是 FML 统一的资源标识符，格式为 `domain:path`，类似 Minecraft 的 ResourceLocation。

### 创建

```csharp
// 双段构造
Identifier id = new Identifier("mymod", "rifle_ak47");

// 从字符串解析
Identifier id = Identifier.Parse("mymod:rifle_ak47");

// 安全解析
if (Identifier.TryParse("mymod:rifle_ak47", out Identifier? parsed))
{
    // parsed 可用
}
```

### 属性

```csharp
Identifier id = new Identifier("mymod", "coffee");
id.Domain  // → "mymod"
id.Path    // → "coffee"
id.ToString()  // → "mymod:coffee"
```

### 校验规则

- 禁止 `:`（冒号）、`\\`（反斜杠）、`..`（双点）、空字符串
- `domain` 禁止 `/`（斜杠）；`path` 允许 `/` 以支持子目录资源（如 `mymod:items/weapons/rifle`）
- 所有异常在构造时立即抛出

### 特殊用法

```csharp
// Identifier 作为 Registry 的键
RegistryManager.Instance.Registry.Set(
    new Identifier("mymod", "myregistry"), myRegistry);

// Identifier.Domain 自动推导 mod owner
ItemUtils.RegisterItem(new Identifier("mymod", "coffee"), coffeeItem);
// owner = "mymod"
```

---

## 4. 物品系统（ItemUtils）

### 4.1 ItemData — 物品数据模型

```csharp
var itemData = new ItemData
{
    itemId = 150001,
    localizationKey = "item_coffee",   // I18n key
    weight = 0.3f,
    value = 500,
    maxStackCount = 5,
    maxDurability = 0f,
    quality = 3,
    spritePath = "coffee_icon.png",
    tags = new List<string> { "Food", "Drink" },
    usages = new UsageData
    {
        useTime = 2f,
        useSound = "e_Item_Drink",
        behaviors = new List<UsageBehaviorData>
        {
            new FoodData { energyValue = 30, waterValue = 20 },
        }
    },
    modifiers = new List<ModifierData>
    {
        new ModifierData { target = ModifierTarget.Player, key = "moveSpeed", type = ModifierType.Multiplier, value = 1.1f }
    }
};
```

#### 可用 UsageBehavior

| 类 | 用途 | 关键属性 |
|----|------|----------|
| `FoodData` | 食物/饮水 | `energyValue`, `waterValue` |
| `HealData` | 治疗 | `healValue` |
| `AddBuffData` | 添加 Buff | `buff` (Buff ID), `chance` |
| `RemoveBuffData` | 移除 Buff | `buffID`, `removeLayerCount` |
| `ReturnItemData` | 使用后返还物品 | `itemTypeID`, `display` |

#### 可用 Modifier

`ModifierData` 可以给物品添加属性修正（伤害倍率、移速加成等）。

### 4.2 创建并注册物品

```csharp
// 异步创建并注册（推荐：加载阶段用，Sprite 加载走线程池 IO）
await ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "coffee"), itemData);

// 同步版本（兼容保留，加载阶段不推荐）
ItemUtils.CreateCustomItem(new Identifier("mymod", "coffee"), itemData);
```

### 4.3 仅构造不注册

```csharp
// 异步构造（推荐：加载阶段用）
Item item = await ItemUtils.GetCustomItemAsync(new Identifier("mymod", "coffee"), itemData);

// 同步版本（兼容保留）
Item item = ItemUtils.GetCustomItem(new Identifier("mymod", "coffee"), itemData);
// 自己设置额外属性...
// 然后手动注册
ItemUtils.RegisterItem(new Identifier("mymod", "coffee"), item);
```

> **便捷重载**：若已通过 `ModPathResolver.Register` 注册路径，可使用简化签名：
> ```csharp
> Item item = ItemUtils.GetCustomItem(itemData); // 自动推导 modid 和路径
> ```

### 4.4 从 AssetBundle 注册

```csharp
// 加载 AssetBundle
AssetBundle bundle = AssetUtil.LoadBundle(new Identifier("mymod", "weapons"));
// 便捷重载（需先 ModPathResolver.Register）：
// AssetBundle bundle = AssetUtil.LoadBundle("weapons");

// 注册枪支（自动复制基础枪支的属性）
ItemUtils.RegisterGun(new Identifier("mymod", "rifle"), bundle, "Rifle_Prefab");

// 注册普通物品
ItemUtils.RegisterItemFromBundle(new Identifier("mymod", "armor"), bundle, "Armor_Prefab");
```

### 4.5 创建蓝图

```csharp
var blueprintData = new BlueprintData
{
    itemId = 200001,
    localizationKey = "bp_coffee",
    formulaID = "coffee_recipe",
    // 从 ItemData 继承的属性...
};
ItemUtils.CreateCustomBluePrint(new Identifier("mymod", "coffee_bp"), blueprintData);
```

### 4.6 创建子弹

```csharp
var bulletData = new BulletData
{
    itemId = 300001,
    localizationKey = "bullet_556",
    Caliber = "5.56x45",
    damageMultiplier = 1.2f,
    ArmorPiercingGain = 0.3f,
    ExplosionRange = 0f,
    // ...
};
ItemUtils.CreateCustomBullet(new Identifier("mymod", "bullet_556"), bulletData);
```

### 4.7 TypeID 冲突自动处理

若 `itemId` 与已有物品（游戏原生或已注册）冲突，FML 从指定位置向后扫描（范围 +10000），
无空闲则兜底从 90000 开始：

```csharp
// config.itemId = 150001，若被占用则扫描 150002~160001，不行再从 90000 起
ItemUtils.CreateCustomItem(new Identifier("mymod", "coffee"), itemData);
```

### 4.8 查询与卸载

```csharp
// 按 Identifier 反查（推荐）
if (ItemUtils.TryGetCustomItem(new Identifier("mymod", "coffee"), out Item? item))
{
    // 找到物品
}

// 批量卸载
ItemUtils.UnregisterAllItem("mymod");
```

### 4.9 Sprite 加载

**推荐使用异步版本**（加载阶段：文件 IO 在线程池执行，减少主线程阻塞）：

```csharp
// 异步加载（推荐：加载阶段用，IO 在线程池 + Texture2D 在主线程）
Sprite? icon = await ItemUtils.LoadSpriteAsync(
    new Identifier("mymod", "coffee_icon.png"));

// 便捷重载（需先 ModPathResolver.Register）：
// Sprite? icon = await ItemUtils.LoadSpriteAsync("coffee_icon.png");
```

同步版本（兼容保留，加载阶段不推荐）：

```csharp
// 同步加载（仅兼容旧代码，新项目请用异步版）
Sprite? icon = ItemUtils.LoadSprite(
    new Identifier("mymod", "coffee_icon.png"));
```

---

### 4.x 原版物品反查（GameItemLookup）

引用游戏原版物品时，使用 `duckov` 域。如果只知道数字 TypeID，可以通过反查 API 获取 Identifier：

```csharp
// 已知 displayName → 直接构造
ItemEntry.Of(Identifier("duckov", "AK-47"), 1);

// 只知道数字 TypeID → 反查
if (GameItemLookup.TryGetIdentifier(1001, out var id))
    ItemEntry.Of(id, 1);

// 按标签浏览全部原版物品
if (GameItemLookup.TryFindByTag("Gun", out var guns))
{
    foreach (var gun in guns)
        Debug.Log($"Gun: {gun}");  // duckov:AK-47, duckov:M4A1, ...
}

// 遍历全部索引
foreach (var id in GameItemLookup.GetAllIdentifiers())
    Debug.Log($"Vanilla item: {id}");
```

> `GameItemLookup` 在 FML 启动时自动扫描全量原版物品（约数千条），建立 `Identifier ↔ TypeID` 双向映射。
> 查询顺序：FML 注册的自定义物品 → 原版物品反查表。

---

## 5. 合成配方（CraftingUtils）

### 5.1 数据模型

| 类型 | 说明 |
|------|------|
| `CraftingFormulaData` | 合成配方完整数据 |
| `DecomposeFormulaData` | 分解配方完整数据 |
| `ItemEntry` | 单个物品引用（支持 Identifier 和 typeID） |

`ItemEntry` 同时支持 Identifier 和 int typeID，可在同一数组中混合使用：

```csharp
// 原版物品（纯 typeID）
ItemEntry.Of(1001, 5)

// 框架物品（Identifier）
ItemEntry.Of(new Identifier("mymod", "coffee"), 10)

// 字符串快捷方式
ItemEntry.Of("mymod:coffee", 10)
```

### 5.2 添加合成配方

```csharp
// struct 方式（推荐）
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("mymod", "coffee"),
    Money = 100,
    CostItems = new[] {
        ItemEntry.Of(1001, 5),                       // 原版物品
        ItemEntry.Of("mymod:beans", 2)                // 框架物品
    },
    Result = ItemEntry.Of("mymod:coffee", 10),
    Tags = new[] { "WorkBenchAdvanced" },
    RequirePerk = "cooking"
});

// Builder 方式
CraftingUtils.AddCraftingFormula(
    CraftingFormulaData.Builder
        .Create("mymod:coffee")
        .Money(100)
        .AddCost(1001, 5)
        .AddCost("mymod:beans", 2)
        .Result("mymod:coffee", 10)
        .Tags("WorkBenchAdvanced")
        .Build());

// 传统方式（兼容，不推荐新项目使用）
CraftingUtils.AddCraftingFormula(
    formulaId: "coffee_recipe",
    money: 100,
    costItems: new[] { (1001, 5L), (1002, 2L) },
    resultItemId: 200001,
    resultItemAmount: 10,
    tags: new[] { "WorkBenchAdvanced" },
    modid: "mymod"
);
```

### 5.3 添加分解配方

```csharp
// struct 方式（推荐）
CraftingUtils.AddDecomposeFormula(new DecomposeFormulaData
{
    Id = new Identifier("mymod", "scrap_old_gun"),
    SourceItemId = new Identifier("mymod", "old_gun"),  // 被分解物品
    Money = 50,
    ResultItems = new[] {
        ItemEntry.Of(1001, 3),
        ItemEntry.Of(1002, 1)
    }
});

// 传统方式（兼容）
CraftingUtils.AddDecomposeFormula(
    itemId: 200001,
    money: 50,
    resultItems: new[] { (1001, 3L) },
    modid: "mymod"
);
```

### 5.4 卸载配方

```csharp
CraftingUtils.RemoveAllAddedFormulas("mymod");
CraftingUtils.RemoveAllAddedDecomposeFormulas("mymod");
```

### 5.5 标签匹配物品（ItemEntry 扩展）

`ItemEntry.ByTag()` 和 `WithDurabilityCost()` 支持按物品**标签**（而非精确 typeID）匹配合成成本：

```csharp
CraftingUtils.AddCraftingFormula(new CraftingFormulaData
{
    Id = new Identifier("mymod", "repair_kit"),
    Money = 200,
    CostItems = new[] {
        ItemEntry.ByTag("Armor", 1)           // 匹配任意"Armor"标签物品 ×1
            .WithDurabilityCost(true),         // 按耐久度折算消耗量
        ItemEntry.Of("Metal", 10)
    },
    Result = ItemEntry.Of("mymod:repair_kit", 1),
});
```

> `WithDurabilityCost(true)` 启用后，满耐久度物品 = 1 个，50% 耐久度 = 0.5 个。
> FML 内部通过 `TagCostRegistry` + `TagCostValidator` + `CraftingManagerPatch` 自动拦截合成流程。

### 5.6 Decompose Builder

```csharp
// Builder 方式创建分解配方
CraftingUtils.AddDecomposeFormula(
    DecomposeFormulaData.Builder
        .Create("mymod:scrap_old_gun")
        .Source("mymod:old_gun")
        .Money(50)
        .AddResult(1001, 3)
        .AddResult(1002, 1)
        .Build());
```

---

## 6. 任务系统（QuestUtils）

### 6.1 任务数据模型

FML 提供 5 种任务类型和 4 种奖励类型：

**可用 TaskData：**

| 类 | 用途 | 关键属性 |
|----|------|----------|
| `TaskRequireItem` | 提交物品 | `itemIdentifier` (Identifier?), `requiredAmount` |
| `TaskRequireMoney` | 提交金钱 | `money` |
| `TaskRequireUseItem` | 使用物品 | `itemIdentifier` (Identifier?), `amount` |
| `TaskKillCount` | 击杀目标 | `requireAmount`, `weaponIdentifier` (Identifier?), `requireEnemy`, `requireHeadshot` |

**可用 RewardData：**

| 类 | 用途 | 关键属性 |
|----|------|----------|
| `RewardGiveItem` | 给予物品 | `itemIdentifier` (Identifier?), `amount` |
| `RewardEXP` | 给予经验 | `amount` |
| `RewardMoney` | 给予金钱 | `amount` |
| `RewardUnlockItem` | 解锁商店物品 | `itemIdentifier` (Identifier?) |

### 6.2 注册任务

```csharp
var questData = new QuestData
{
    // ID 由 Identifier 管理（不再使用数字 ID）
    Id = new Identifier("mymod", "coffee_run"),
    displayName = "quest_coffee_run",
    description = "quest_coffee_run_desc",
    questGiver = QuestGiverID.Fence,
    // 🆕 或使用 QuestGiverIdentifier 绑定自定义 QuestGiver（推荐）
    QuestGiverIdentifier = new Identifier("mymod", "daily_giver"),
    requireLevel = 5,
    tasks = new List<TaskData>
    {
        new TaskRequireItem
        {
            itemIdentifier = new Identifier("mymod", "coffee"),
            requiredAmount = 5
        },
        new TaskKillCount
        {
            requireAmount = 10,
            requireEnemy = "Scav"
        }
    },
    rewards = new List<RewardData>
    {
        new RewardMoney { amount = 5000 },
        new RewardEXP { amount = 200 },
        new RewardUnlockEndowmentData
        {
            endowmentId = new Identifier("mymod", "assassin")
        },
        new RewardUnlockBuildingData
        {
            buildingId = new Identifier("mymod", "bounty_shop"),
            buildingInfo = new BuildingInfo
            {
                id = "bounty_shop",
                prefabName = "Building_Workbench",
                maxAmount = 1,
                dimensions = new Vector2Int(3, 3),
                cost = new BuildingCost { money = 5000 }
            },
            prefabName = "Building_Workbench"
        }
    }
};

// Identifier 方式（推荐）—— domain 自动推导为 owner modid
QuestUtils.RegisterQuest(new Identifier("mymod", "coffee_run"), questData);
```

> **数字 ID 全自分配 + 冲突检测**：`QuestData.ID`（从 1000 起递增）、`TaskData.id`、`RewardData.id`（从 1 起递增）
> 均由 FML 在注册时自动分配，带 **冲突检测** — 若候选 ID 已被原生游戏任务或已注册 FML 任务占用，自动递增至空闲位置。
> modder 无需手动设置（已 internal）。
> `RewardUnlockEndowmentData` 在任务完成时自动解锁指定天赋（AutoClaim），无需 modder 手动处理解锁逻辑。
>
> **QuestGiverIdentifier 自动绑定**：设置 `QuestData.QuestGiverIdentifier` 后，`RegisterQuest` 时自动将任务绑定到
> 指定的自定义 QuestGiver（通过 `QuestGiverUtils.RegisterQuestGiver` 注册），无需额外调用 `BindQuest`。
> `questGiver`（原生枚举）仍可用于引用游戏原生任务发放者，两者兼容。

### 6.3 任务关系图

> **⚠️ 重要约束**：注册任务后，modder **必须手动调用 `AddQuestRelation`** 才能在游戏中正确管理任务的前后置关系。
> `RegisterQuest` 只负责将任务登入 `QuestCollection`，**不会自动建立关系图**。
> 如果忘记调用 `AddQuestRelation`，任务将不会出现在任何前置/后续任务的关联中，
> 导致任务链断裂、后续任务无法解锁。

```csharp
// 设置任务前置/后置关系（Identifier 版本）
var current = new Identifier("mymod", "coffee_run");
var preReq  = new Identifier("mymod", "intro_quest");
var unlocks = new Identifier("mymod", "next_quest");

QuestUtils.AddQuestRelation(current, before: preReq, after: unlocks);
//                            ↑ 当前任务      ↑ 前置任务       ↑ 后置任务（完成当前后解锁）

// 也可省略前后置（只有前置或只有后置）：
QuestUtils.AddQuestRelation(current, before: preReq);      // 仅设置前置
QuestUtils.AddQuestRelation(current, after: unlocks);      // 仅设置后置
```

> 参数说明：
> - `before`：前置任务 Identifier — 完成 `before` 后，`current` 才解锁
> - `after`：后置任务 Identifier — 完成 `current` 后，`after` 才解锁
> - 至少需要设置 `before` 或 `after` 中的一个，否则调用无意义

### 6.4 任务 ID 反查（O(1)）

```csharp
// 数字 ID → Identifier（O(1) 反查，通过内部反向索引）
if (QuestUtils.TryGetQuestIdentifier(1001, out var id))
    QuestUtils.UnregisterQuest(id);

// Identifier → 数字 ID（需传给游戏原生 API 时）
if (QuestUtils.TryGetQuestId(id, out var questId))
    QuestUtils.AddQuestRelation(questId, 1002);
```

### 6.5 卸载任务

```csharp
// 按 Identifier 移除单个任务
QuestUtils.UnregisterQuest(new Identifier("mymod", "coffee_run"));

// 批量卸载
QuestUtils.UnregisterQuestAll("mymod");
```

---

### 6.6 自定义 QuestGiver（QuestGiverUtils） 🆕

游戏原生 `QuestGiverID` 是固定枚举，不可扩展。`QuestGiverUtils` 提供完整的自定义任务发放者 API，
允许 modder 注册新 QuestGiver、生成 NPC、绑定任务。自定义 ID 从 **50** 起分配，与原生枚举值（0~11）无冲突。

#### 6.6.1 注册 QuestGiver

```csharp
// 创建配置
var giverConfig = new QuestGiverConfig
{
    DisplayNameKey = "npc_daily_giver",     // 本地化键
    ActorId = "dialogue_peddler",           // DuckovDialogueActor.id（对话角色）
    Face = FaceRef.Preset("Default"),       // 捏脸
    SpawnPosition = new Vector3(15f, 0f, 5f),
    BoundQuests = new[]                     // 可选：预先绑定的任务
    {
        new Identifier("mymod", "daily_01"),
        new Identifier("mymod", "daily_02")
    }
};

// 注册（返回自定义 ID）
int giverId = QuestGiverUtils.RegisterQuestGiver(
    new Identifier("mymod", "daily_giver"), giverConfig);
```

#### 6.6.2 生成 NPC

```csharp
// 在世界空间生成 QuestGiver NPC
var npc = QuestGiverUtils.SpawnQuestGiver(
    new Identifier("mymod", "daily_giver"),
    new Vector3(20f, 0f, 10f));
```

#### 6.6.3 绑定任务

```csharp
// 方式 A：注册时通过 QuestData.QuestGiverIdentifier 自动绑定（推荐）
var questData = new QuestData
{
    displayName = "每日任务",
    QuestGiverIdentifier = new Identifier("mymod", "daily_giver"), // 🆕
    // ...
};
QuestUtils.RegisterQuest(new Identifier("mymod", "daily_01"), questData);

// 方式 B：注册后显式绑定
QuestGiverUtils.BindQuest(
    new Identifier("mymod", "daily_giver"),   // QuestGiver
    new Identifier("mymod", "daily_02"));     // Quest
```

#### 6.6.4 查询

```csharp
// 查询 GameObject
if (QuestGiverUtils.TryGetQuestGiver(new Identifier("mymod", "daily_giver"), out var go))
    Debug.Log($"NPC position: {go.transform.position}");

// 查询自定义 ID
if (QuestGiverUtils.TryGetQuestGiverId(new Identifier("mymod", "daily_giver"), out int id))
    Debug.Log($"Custom ID: {id}");

// 检查是否为自定义 ID
bool isCustom = QuestGiverUtils.IsCustomQuestGiverId(150);
```

#### 6.6.5 卸载

```csharp
// 按 Identifier 卸载
QuestGiverUtils.UnregisterQuestGiver(new Identifier("mymod", "daily_giver"));

// 批量卸载
QuestGiverUtils.UnregisterAllQuestGivers("mymod");
```

#### 6.6.6 通过 FriendlyNpcUtils 创建 QuestGiver NPC

```csharp
// 先注册 QuestGiver
QuestGiverUtils.RegisterQuestGiver(
    new Identifier("mymod", "quest_npc"), config);

// 再用 FriendlyNpcUtils 创建 NPC 并绑定
var npc = FriendlyNpcUtils.CreateFriendlyNpc(
    new Identifier("mymod", "npc_01"),
    new FriendlyNpcConfig
    {
        Role = NpcRole.QuestGiver,
        QuestGiverId = "50"  // 自定义 ID（int 字符串，≥50）
    });

// 或用 Identifier 绑定
FriendlyNpcUtils.BindQuestGiver(
    new Identifier("mymod", "npc_01"),
    new Identifier("mymod", "quest_npc"));
```

> **QuestGiverUtils API 一览**：
> 
> | 方法 | 说明 |
> |------|------|
> | `RegisterQuestGiver(Identifier, QuestGiverConfig)` | 注册自定义 QuestGiver |
> | `SpawnQuestGiver(Identifier, Vector3?, Quaternion?)` | 生成 NPC |
> | `BindQuest(Identifier, Identifier)` | 绑定任务 |
> | `TryGetQuestGiver(Identifier, out GameObject)` | 查询 NPC |
> | `TryGetQuestGiverId(Identifier, out int)` | 查询自定义 ID |
> | `IsCustomQuestGiverId(int)` | 检查是否为自定义 ID |
> | `UnregisterQuestGiver(Identifier)` | 卸载 |
> | `UnregisterAllQuestGivers(string)` | 批量卸载 |

---

## 7. 商店系统（ShopUtils）

### 7.1 注册商品

```csharp
// 使用 typeID（传统方式）
ShopUtils.AddGoods(new ShopGoodsData
{
    merchantProfileID = "Merchant_Normal",  // 商人 ID
    typeID = 150001,                        // 物品 TypeID
    maxStock = 10,                          // 最大库存
    forceUnlock = false,                    // 是否强制解锁
    priceFactor = 1.0f,                     // 价格倍率
    possibility = 1.0f                      // 出现概率
}, "mymod");  // mod 身份

// 使用 itemIdentifier（Identifier 方式，推荐）
ShopUtils.AddGoods(new ShopGoodsData
{
    merchantProfileID = "Merchant_Normal",
    itemIdentifier = new Identifier("mymod", "coffee"),  // 物品 Identifier，优先解析
    typeID = 150001,                                      // 回退 typeID（itemIdentifier 解析失败时使用）
    maxStock = 10,
    priceFactor = 1.0f
}, "mymod");
```

`ShopGoodsData` 字段说明：

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `merchantProfileID` | `string` | `"Merchant_Normal"` | 商人 profile 名称 |
| `typeID` | `int` | — | 物品 TypeID（`itemIdentifier` 未设置或解析失败时使用） |
| `itemIdentifier` | `Identifier?` | `null` | **新增**：物品 Identifier。设置后优先解析为 typeID |
| `maxStock` | `int` | `0` | 最大库存量 |
| `forceUnlock` | `bool` | `false` | 是否强制解锁 |
| `priceFactor` | `float` | `1F` | 价格倍率 |
| `possibility` | `float` | `1F` | 出现概率 |

### 7.2 查询商品

```csharp
// 查询单个商品
if (ShopUtils.TryGetGoods("Merchant_Normal", 150001, out var data))
{
    Debug.Log($"Current maxStock: {data.maxStock}");
}

// 查询商人全部商品
IReadOnlyList<ShopGoodsData> allGoods = ShopUtils.GetAllGoods("Merchant_Normal");
```

### 7.3 编辑商品

```csharp
// 按商人 + typeID 编辑
ShopUtils.EditGoods("Merchant_Normal", 150001, new ShopGoodsData
{
    maxStock = 20,
    priceFactor = 1.5f
});

// 按 Identifier 编辑（推荐）
ShopUtils.EditGoods(new Identifier("mymod", "coffee"), new ShopGoodsData
{
    maxStock = 20,
    priceFactor = 1.5f
});
```

### 7.4 移除商品

```csharp
// 移除单个商品（按商人 + typeID）
ShopUtils.RemoveGoods("Merchant_Normal", 150001);

// 移除单个商品（按 Identifier，推荐）
ShopUtils.RemoveGoods(new Identifier("mymod", "coffee"));

// 移除指定商人下的所有 FML 注册商品
ShopUtils.RemoveAllGoods("Merchant_Normal");

// 按 mod 批量卸载商品
ShopUtils.UnregisterAllGoods("mymod");

// 按 mod 批量卸载该模组创建的全部商人 profile
ShopUtils.RemoveAllProfiles("mymod");
```

### 7.5 创建新商人

```csharp
// Identifier 方式（推荐）—— Path 作为 merchantID，Domain 作为 modid
ShopUtils.CreateMerchantProfile(new Identifier("mymod", "Merchant_Drink"));

// 字符串方式（兼容旧 API）
ShopUtils.CreateMerchantProfile("MyTrader");
```

#### 7.5.1 按 Identifier 查询商品

```csharp
// 按 Identifier 查询商人全部商品（Path = merchantProfileID）
var goods = ShopUtils.GetAllGoods(new Identifier("mymod", "Merchant_Drink"));

// 查询商人 profile 是否存在
if (ShopUtils.TryGetMerchantProfile(new Identifier("mymod", "Merchant_Drink"), out var profile))
    Debug.Log($"Merchant found: {profile.merchantID}");
```

---

## 8. 音频系统（AudioUtil）

### 8.1 SFX 注册

```csharp
using FeatherMod.Audio;

AudioUtil.Instance.RegisterAudio(
    new Identifier("mymod", "gun_shot"),
    new AudioData
    {
        Path = "events/Weapons/GunShot",       // FMOD event 路径
        Eventname = "GunShot",                  // 事件名称（用于反向查询）
        MinDistance = 1f,
        MaxDistance = 500f
    }
);
```

### 8.2 BGM 控制

```csharp
// 播放内置 BGM
AudioUtil.PlayBGM("theme");

// 播放自定义 BGM 文件
AudioUtil.PlayCustomBGM("path/to/music.ogg");

// 停止
AudioUtil.StopBGM();

// 切换
AudioUtil.SwitchBGM("battle");

// 检查播放状态
bool isPlaying = AudioUtil.IsBGMPlaying();
```

### 8.3 音量控制

```csharp
// 总音量
AudioUtil.SetMasterVolume(0.8f);
float vol = AudioUtil.GetMasterVolume();

// 音乐音量
AudioUtil.SetMusicVolume(0.5f);

// SFX 音量
AudioUtil.SetSFXVolume(1.0f);

// 静音控制
AudioUtil.SetMasterMute(true);
AudioUtil.SetMusicMute(false);
AudioUtil.SetSFXMute(false);
```

---

## 9. 本地化（I18n）

### 9.1 初始化

在 `OnAfterSetup` 中调用（需先 `ModPathResolver.Register`）：

```csharp
protected override void OnAfterSetup()
{
    base.OnAfterSetup();
    ModPathResolver.Register(GetModid(), dllPath);
    I18n.InitI18n(GetModid());  // 传入 mod 标识符（如 "MyFirstMod"）
}
```

> `InitI18n` 参数为 **modid**（模组标识符字符串），不是 DLL 路径。库内部通过 `ModPathResolver.ResolveDirectory(modid)` 解析 mod 目录。

### 9.2 语言文件

在 mod 目录下创建 `assets/lang/` 文件夹，放入以下 JSON 文件：

| 文件名 | 语言 |
|--------|------|
| `en_us.json` | 英语 |
| `zh_cn.json` | 简体中文 |
| `zh_tw.json` | 繁体中文 |
| `ja_jp.json` | 日语 |
| `ru_ru.json` | 俄语 |
| `ko_kr.json` | 韩语 |
| `it_it.json` | 意大利语 |
| `fr_fr.json` | 法语 |
| `sv_se.json` | 瑞典语 |

JSON 格式：

```json
{
    "item_coffee": "Coffee",
    "item_coffee_desc": "A hot cup of coffee. Restores energy.",
    "quest_coffee_run": "Coffee Run",
    "quest_coffee_run_desc": "Bring me 5 cups of coffee."
}
```

> I18n 自动监听游戏语言切换事件（`LanguageChangedEvent`），切换语言时自动重读对应文件。

---

## 10. 事件总线（EventBus）

FML 提供统一的同步事件总线，自动桥接了 15 个游戏原生事件。

### 10.1 订阅事件

```csharp
using FeatherMod.Events;
using FeatherMod.Events.GameEvents;

// 订阅玩家金钱变化
EventBusManager.Instance.Sync.Register<MoneyChangedEvent>(e =>
{
    Debug.Log($"Money: {e.OldMoney} → {e.NowMoney}");
});

// 订阅角色受伤
EventBusManager.Instance.Sync.Register<HurtEvent>(OnHurt);

// 以 mod 身份注册（卸载时自动清理）
EventBusManager.Instance.Sync.Register<HurtEvent>(
    OnHurt, 0, RegistryManager.CurrentModid);
```

### 10.2 15 个可订阅的游戏事件

| 事件类型 | 触发时机 | 说明 |
|----------|----------|------|
| `HurtEvent` | 角色受伤 | 可标记（effect 已应用） |
| `EntityDeathEvent` | 角色死亡 | 仅观察 |
| `LevelInitializedEvent` | 关卡初始化完成 | 仅观察 |
| `MoneyChangedEvent` | 金钱变化 | 仅观察 |
| `LanguageChangedEvent` | 游戏语言切换 | 仅观察 |
| `PlayerHearSoundEvent` | 玩家听到声音 | 仅观察 |
| `SoundSpawnedEvent` | 声音产生 | 仅观察 |
| `PlayerDeathEvent` | 玩家死亡 | 仅观察 |
| `ControllingCharacterChangedEvent` | 切换控制角色 | 仅观察 |
| `ItemUnlockStateChangedEvent` | 物品解锁状态变化 | 仅观察 |
| `ItemCraftedEvent` | 物品制作成功 | 仅观察 |
| `FormulaUnlockedEvent` | 配方解锁 | 仅观察 |
| `QuestTaskFinishedEvent` | 任务目标完成 | 仅观察 |
| `CollectSaveDataEvent` | 收集存档数据 | 仅观察 |
| `KillCountChangedEvent` | 击杀数变化 | 仅观察 |

### 10.4 异步事件总线（AsyncEventBus）

`AsyncEventBus` 适用于**需要分帧执行**的场景——handler 为 `Func<T, UniTask>` 异步方法。
典型用例：大量 Sprite 加载、分批注册物品、避免单帧 IO 阻塞。

#### 10.4.1 定义异步事件

```csharp
using FeatherMod.Events;
using System.Collections.Generic;

/// <summary>Sprite 批量加载请求。</summary>
[Cancelable]
public class SpriteLoadRequestEvent : Event
{
    /// <summary>待加载 Sprite 的物品列表（Identifier → 在 ItemData 中定义的 spritePath）。</summary>
    public List<(Identifier itemId, string spriteName)> Items = new();
}
```

#### 10.4.2 加载阶段：并行加载（推荐）

**适用场景**：游戏加载中（`OnAfterSetup`），目标是最小化加载时间，进入游戏前完成。

```csharp
protected override async void OnAfterSetup()
{
    base.OnAfterSetup();
    ModPathResolver.Register(GetModid(), dllPath);

    // 并行创建物品 — 每个内部的 Sprite 加载走线程池 IO
    var coffee = ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "coffee"), coffeeData);
    var rifle  = ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "rifle"), rifleData);
    var pistol = ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "pistol"), pistolData);
    var helmet = ItemUtils.CreateCustomItemAsync(new Identifier("mymod", "helmet"), helmetData);

    // WhenAll 等待所有并行任务完成（文件 IO 在线程池并行执行）
    await UniTask.WhenAll(coffee, rifle, pistol, helmet);

    Debug.Log("[MyMod] All items created and sprites loaded.");
}
```

> **设计考量**：`CreateCustomItemAsync` 内部调用 `LoadSpriteFromDirAsync`，文件 IO 通过 `UniTask.RunOnThreadPool` 在线程池执行。
> 多个物品用 `UniTask.WhenAll` 并行创建，文件读取在线程池并发，Texture2D 创建串行回到主线程。
> 相比逐个同步 `File.ReadAllBytes`，加载时间可减少 50-70%（取决于物品数量）。

#### 10.4.3 运行时：分帧加载（AsyncEventBus）

**适用场景**：游戏运行中需要加载大量 Sprite，使用 `async UniTask` handler + `await UniTask.Yield()` 分帧避免卡顿。

```csharp
// 注册异步 handler：每帧加载一张 Sprite
EventBusManager.Instance.Async.Register<SpriteLoadRequestEvent>(
    LoadSpritesFrameByFrame, 0, RegistryManager.CurrentModid);

var evt = new SpriteLoadRequestEvent();
evt.Items.Add((new Identifier("mymod", "rifle"), "rifle_icon.png"));
evt.Items.Add((new Identifier("mymod", "pistol"), "pistol_icon.png"));
evt.Items.Add((new Identifier("mymod", "helmet"), "helmet_icon.png"));

await EventBusManager.Instance.Async.Post(evt);

/// <summary>异步：逐帧加载 Sprite，await UniTask.Yield() 分帧避免卡顿。</summary>
private async UniTask LoadSpritesFrameByFrame(SpriteLoadRequestEvent e)
{
    foreach (var (itemId, spriteName) in e.Items)
    {
        await ItemUtils.LoadSpriteAsync(itemId);
        await UniTask.Yield();  // 等待下一帧
    }
}
```

> **设计考量**：注册大量物品时，如果每件物品都同步调用 `LoadSprite`（内部 `File.ReadAllBytes`），单帧累计 IO 可能超过 16ms 导致掉帧。
> `AsyncEventBus` 基于 UniTask 的 PlayerLoop 调度，handler 通过 `await UniTask.Yield()` 将 IO 分散到多帧，保持 60fps 流畅度。
> 相比协程方案（MonoBehaviour + StartCoroutine），UniTask 零 GC 分配，无需 MonoBehaviour，性能更优。
> 对于不需要分帧的场景，继续使用 `Sync` 总线即可。

#### 10.4.4 关键 API

| 操作 | Async（UniTask） | Sync（同步） |
|------|-----------------|------------|
| 注册 | `Async.Register<T>(Func<T, UniTask> handler)` | `Sync.Register<T>(Action<T> handler)` |
| 发送 | `await Async.Post(evt)` | `Sync.Post(evt)` |
| 批量卸载 | `Async.UnregisterAll(ownerMod)` | `Sync.UnregisterAll(ownerMod)` |

---

## 11. 经济系统（EconomyUtils）

```csharp
// 查询金钱
long money = EconomyUtils.GetMoney();

// 增删
EconomyUtils.AddMoney(1000);
EconomyUtils.RemoveMoney(500);

// 直接设置
EconomyUtils.SetMoney(5000);

// 解锁物品（仅 Identifier 版本公开）
EconomyUtils.UnlockItem(new Identifier("mymod", "coffee"));
EconomyUtils.UnlockItem(new Identifier("mymod", "coffee"), needConfirm: false, showUI: true);

// 查询解锁状态
bool unlocked = EconomyUtils.IsItemUnlocked(new Identifier("mymod", "coffee"));

// 物品解锁确认流程（needConfirm = true 时使用）
EconomyUtils.ConfirmUnlockItem(new Identifier("mymod", "coffee"));
if (EconomyUtils.IsItemWaitingForUnlockConfirm(new Identifier("mymod", "coffee")))
{
    Debug.Log("Item is pending confirm...");
}

// 订阅金钱变化
EconomyUtils.OnMoneyChanged(handler);
EconomyUtils.OnItemUnlockStateChanged(e => {
    Debug.Log($"Item unlock state changed.");
});

// 简化版回调
EconomyUtils.RegisterMoneyChangedCallback((oldMoney, nowMoney) =>
{
    Debug.Log($"Money changed: {oldMoney} → {nowMoney}");
});
```

---

## 12. Buff 状态效果（BuffUtils）

```csharp
// 注册自定义 Buff（modid 从 id.Domain 自动推导）
BuffUtils.RegisterBuff(
    new Identifier("mymod", "mybuff"),
    buffPrefab  // Buff 预制体
);

// 按 ID 反查 Buff Identifier（自定义 + 游戏内置）
if (BuffUtils.TryGetBuffIdentifier(buffID, out var buffId))
{
    // buffId = Identifier("duckov", buffName) 或自定义 buff 的 Identifier
}

// 移除单个 Buff
BuffUtils.UnregisterBuff(new Identifier("mymod", "mybuff"));

// 批量卸载
BuffUtils.UnregisterAllBuffs("mymod");
```

---

## 13. 建筑系统（BuildingUtils）

### 13.1 快速开始

```csharp
// 一行注册：纯代码创建 2×2 工作台，花费 5000 金币 + 20 根原木
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "workshop"),
    Money = 5000,
    CostItems = new[] { ItemEntry.Of("duckov:Wood", 20) }
});
```

> `RegisterBuilding(BuildingConfig)` 自动完成四件事：创建 Building Prefab（Cube 模型 + 碰撞体）→ 构建 `BuildingInfo` → 解析 Identifier 为 TypeID → 写入游戏和 FML 双注册表。

---

### 13.2 BuildingConfig 完整配置

```csharp
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    // ── 必填 ──
    Id = new Identifier("mymod", "forge"),          // Identifier（domain=modid, path=建筑名）

    // ── 尺寸与外观 ──
    Dimensions = new Vector2Int(3, 3),               // 占地 3×3 网格（默认 2×2）
    PrefabName = "Building_Forge",                   // prefab 名称（用于注册标识）
    ExistingPrefabName = "Building_Workbench",        // 可选：克隆游戏已有建筑结构

    // ── 成本（走 FML ItemEntry） ──
    Money = 5000,                                    // 金币
    CostItems = new[]
    {
        ItemEntry.Of("duckov:Iron", 20),             // 精确物品
        ItemEntry.Of("duckov:Stone", 10),
        ItemEntry.ByTag("Wood", 30),                 // 标签匹配：任意木制品
        ItemEntry.ByTag("Food", 5, minQuality: 3)    // 标签 + 最低品质
    },

    // ── 数量与解锁 ──
    MaxAmount = 2,                                   // 最多同时建造 2 个（默认 1）
    UnlockedByDefault = false,                       // 需任务解锁？
    RequireBuildings = new[] { "workshop" },          // 前置建筑
    RequireQuests = new[] { "quest_intro" }           // 前置任务
});
```

#### 成本构建的三种方式

```csharp
// 方式 1：BuildingConfig（推荐）—— 自动 Identifier→TypeID 解析
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "forge"),
    Money = 5000,
    CostItems = new[] { ItemEntry.Of("duckov:Iron", 20) }
});

// 方式 2：CreateCost 辅助 —— 给已有 BuildingInfo 补成本
var info = new BuildingInfo { id = "forge" };
info.cost = BuildingUtils.CreateCost(5000,
    ItemEntry.Of("duckov:Iron", 20),
    ItemEntry.Of("duckov:Stone", 10));
BuildingUtils.RegisterBuilding(new Identifier("mymod", "forge"), info, prefab);

// 方式 3：CostItems 为空 —— 纯金币成本
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "free_shelter"),
    Money = 0,    // 免费
    // CostItems 省略 = 无物品消耗
});
```

---

### 13.3 三种注册模式

#### 模式 A：全自动（零 Unity 依赖）

```csharp
// 自动生成 Cube 模型 + 碰撞体，适合快速原型
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "storage"),
    Dimensions = new Vector2Int(2, 2),
    Money = 1000,
    CostItems = new[] { ItemEntry.Of("duckov:Wood", 10) }
});
```

#### 模式 B：克隆游戏原生建筑结构

```csharp
// 保留原建筑的 graphics/function 容器布局，替换模型
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "advanced_bench"),
    Dimensions = new Vector2Int(3, 2),
    ExistingPrefabName = "Building_Workbench",  // ← 克隆原生工作台的容器结构
    Money = 3000,
    CostItems = new[] { ItemEntry.Of("duckov:Iron", 15) }
});
```

#### 模式 C：AssetBundle 自定义模型

```csharp
// 1. 加载 AssetBundle
var bundle = AssetUtil.LoadBundle("my_buildings");
var modelPrefab = bundle.LoadAsset<GameObject>("Building_Forge_Model");

// 2. 注册建筑（自动创建外壳 + 注册 + 注入模型）
BuildingUtils.RegisterBuilding(new BuildingConfig
{
    Id = new Identifier("mymod", "forge"),
    Dimensions = new Vector2Int(3, 3),
    Money = 5000,
    CostItems = new[]
    {
        ItemEntry.Of("duckov:Iron", 20),
        ItemEntry.Of("duckov:Stone", 10)
    },
    PrefabName = "Building_Forge"
});

// 3. 注入自定义模型（替换 graphicsContainer 下的默认 Cube）
BuildingUtils.SetBuildingModel(
    new Identifier("mymod", "forge"),
    modelPrefab);
```

> **顺序要求**：`RegisterBuilding` → `SetBuildingModel`。后者依赖 Registry 中已存在的条目。

#### 模式 D：手动创建 Prefab + 原始 API

```csharp
// 用 CreateSimpleBuilding 手动创建 prefab
var prefab = BuildingUtils.CreateSimpleBuilding(
    new Identifier("mymod", "lab"),
    new Vector2Int(4, 3),
    existingPrefabName: "Building_Workbench");

// 用原始 API 注册（传入自定义 BuildingInfo）
var info = new BuildingInfo
{
    id = "lab",
    prefabName = prefab.name,
    maxAmount = 1,
    cost = BuildingUtils.CreateCost(8000,
        ItemEntry.Of("duckov:Electronics", 5))
};
BuildingUtils.RegisterBuilding(
    new Identifier("mymod", "lab"), info, prefab);
```

---

### 13.4 放置、查询与卸载

```csharp
// ===== 放置建筑 =====
BuildingUtils.PlaceBuilding(
    new Identifier("base", "area1"),         // 区域
    new Identifier("mymod", "forge"),        // 建筑
    new Vector2Int(5, 3),                     // 坐标
    BuildingRotation.Rot90                    // 旋转（Rot0 / Rot90 / Rot180 / Rot270）
);

// ===== 查询 =====
BuildingInfo? info = BuildingUtils.GetBuildingInfo(
    new Identifier("mymod", "forge"));
IReadOnlyList<Identifier> allIds = BuildingUtils.GetAllBuildingIds();
Building? prefab = BuildingUtils.GetBuildingPrefab(
    new Identifier("mymod", "forge"));

// ===== 卸载 =====
BuildingUtils.UnregisterBuilding(new Identifier("mymod", "forge"));
BuildingUtils.UnregisterAllBuildings("mymod");
```

#### 成本查询与校验

```csharp
// 查看成本明细
Cost? cost = BuildingUtils.GetBuildingCost(
    new Identifier("mymod", "forge"));
if (cost != null)
    Debug.Log($"需要 {cost.Value.money} 金币 + {cost.Value.items.Length} 种物品");

// 检查是否负担得起（委托游戏原生 EconomyManager.IsEnough）
if (BuildingUtils.CanAffordBuilding(new Identifier("mymod", "forge")))
    Debug.Log("资源充足！");

// 手动扣费（通常不需要 —— PlaceBuilding 内部已自动处理）
BuildingUtils.SpendBuildingCost(new Identifier("mymod", "forge"));
```

---

### 13.5 建造完成回调

```csharp
// 建筑建成后自动生成 NPC 商人
private Action<Building>? _onBuiltCallback;  // 保存引用以便取消

void RegisterCallbacks()
{
    _onBuiltCallback = building =>
    {
        FriendlyNpcUtils.CreateFriendlyNpc(
            new Identifier("mymod", "merchant_forge"),
            new FriendlyNpcConfig
            {
                Role = NpcRole.Merchant,
                ActorId = "merchant_alex",
                SpawnPosition = building.transform.position + new Vector3(2f, 0f, 0f),
                Face = FaceRef.Preset("Default"),
                ShopId = "mymod:forge_shop"
            });
    };

    BuildingUtils.OnBuildingBuilt(
        new Identifier("mymod", "forge"),
        _onBuiltCallback);
}

// 取消回调
void UnregisterCallbacks()
{
    if (_onBuiltCallback != null)
        BuildingUtils.OffBuildingBuilt(
            new Identifier("mymod", "forge"),
            _onBuiltCallback);
}
```

---

### 13.6 Building Prefab 结构标准

游戏原生的 `Building` 组件要求 Prefab 包含以下层级：

```
Building_XXX (GameObject)
├── Building (MonoBehaviour)         ← id, dimensions, graphicsContainer, functionContainer
├── Graphics (GameObject)            ← graphicsContainer：纯视觉层（3D 模型、材质、渲染）
│   ├── Model_XXX (MeshFilter + MeshRenderer)
│   └── (可选) Collider（放置预览时自动禁用）
└── Function (GameObject)            ← functionContainer：交互层
    ├── BoxCollider (isTrigger=true) ← 交互点击检测 + 占地碰撞
    └── areaMesh                     ← 运行时自动生成
```

| 容器 | 用途 | 碰撞体规范 |
|------|------|-----------|
| `graphicsContainer` | 展示用 3D 模型，纯视觉 | Collider 可选；放置时自动禁用 |
| `functionContainer` | 交互检测 + 格子占位 | **必须**有 BoxCollider(isTrigger=true) |

### 13.7 模型 Prefab 规格

`SetBuildingModel` 注入的是**纯视觉 Prefab**——实例化到 `graphicsContainer` 下，**不需要** `Building` 组件。

| 要求 | 说明 |
|------|------|
| **根节点** | 单个 `GameObject`，**无** `Building` 组件 |
| **Transform** | 注入时强制设为 (0,0,0)/(0,0,0)/(1,1,1) |
| **材质** | 游戏原生 Shader；注入后 `ShaderReplacer.ApplyTo()` 自动修复 |
| **碰撞体** | **严禁**放 Collider（由 functionContainer 管理） |
| **尺寸** | 视觉尺寸匹配 `dimensions`。1 单位 ≈ 1 米 |
| **导出** | AssetBundle，放 `assets/bundle/` |

---

## 14. Perk 技能树（PerkTreeUtils）

```csharp
// ===== 注册 Perk（Identifier 优先） =====

// 在技能树上注册新 Perk
// id.Domain → 推导 treeId，id.Path → perk 名称
Perk perk = PerkTreeUtils.AddPerk(
    new Identifier("mymod", "ExtraHealth"),  // Identifier（domain=modid, path=perkName）
    requirement,   // PerkRequirement
    perkIcon       // Sprite
    // 第三个参数 modid 可选，默认从 id.Domain 推导
);

// ===== 注册完整 PerkTree =====

// 从零创建完整的自定义技能树
PerkTreeUtils.RegisterPerkTree(
    new Identifier("mymod", "combat_perks"),  // Identifier（path 作为 treeId）
    horizontal: false                          // 连线方向
);
// 然后在该树上添加 Perk：
PerkTreeUtils.AddPerk(new Identifier("mymod", "combat_perks/ExtraHealth"), req, icon);
PerkTreeUtils.AddPerk(new Identifier("mymod", "combat_perks/IronWill"), req, icon);

// ===== 建立前置关系 =====

// 建立 Perk 前置关系（fromPerk → toPerk：from 是 to 的前置）
// 使用 Identifier 而非 string treeId+name
PerkTreeUtils.ConnectPerks(
    new Identifier("mymod", "ExtraHealth"),  // 前置 Perk
    new Identifier("mymod", "IronWill")      // 后置 Perk
);

// ===== 挂载 Behaviour =====

// 在已有 Perk 上挂载自定义 PerkBehaviour
MyPerkBehaviour behaviour = PerkTreeUtils.AddPerkBehaviour<MyPerkBehaviour>(
    new Identifier("mymod", "ExtraHealth"));

// ===== 解锁与移除 =====

// 强制解锁（Identifier）
PerkTreeUtils.ForceUnlock(new Identifier("mymod", "ExtraHealth"));

// 移除 Perk
PerkTreeUtils.RemovePerk(new Identifier("mymod", "ExtraHealth"));

// 批量卸载
PerkTreeUtils.RemoveAllPerks("mymod");
```

---

## 15. 天赋系统（EndowmentUtils）

> **2026-07-03 更新**：新增 `EndowmentConfig`/`EndowmentModifier` DTO，modder 用纯 C# 配置天赋，
> 无需接触 `EndowmentEntry` 等游戏内部类型，无需反射。

```csharp
// ===== 注册天赋（推荐：使用 FML DTO） =====

// 加载图标（从 assets/textures/ 目录的 PNG 文件）
var icon = ItemUtils.LoadSprite("endowment_assassin");

// modder 用纯 C# EndowmentConfig DTO 配置，FML 内部负责转换为游戏原生 EndowmentEntry
EndowmentUtils.RegisterEndowment(
    new Identifier("mymod", "assassin"),
    new EndowmentConfig
    {
        Modifiers = new[]
        {
            new EndowmentModifier
            {
                StatKey = "moveSpeed",
                Type = ModifierType.PercentageAdd,
                Value = 0.15f    // +15% 移动速度
            },
            new EndowmentModifier
            {
                StatKey = "maxHealth",
                Type = ModifierType.PercentageAdd,
                Value = -0.1f    // -10% 最大生命
            }
        },
        Icon = icon,                         // 天赋图标（null 则使用默认）
        UnlockedByDefault = false,           // false = 需任务解锁
        RequirementTextKey = "endowment_assassin_requirement"
    }
    // modid 可选，默认从 id.Domain 推导
);

// ===== 通过任务解锁天赋（Endowment 的常规解锁方式） =====

// 步骤 1：注册任务，在任务完成回调中解锁天赋
var questId = new Identifier("mymod", "quest_assassin_training");
var endowmentId = new Identifier("mymod", "assassin");

QuestUtils.RegisterQuest(new QuestData
{
    ID = questId.Path,
    NameKey = "quest_assassin_training_name",
    DescriptionKey = "quest_assassin_training_desc",
    Tasks = new QuestTask[]
    {
        new TaskKillCountData
        {
            DescriptionKey = "task_kill_enemies",
            TargetCount = 10
        }
    },
    Rewards = new QuestReward[]
    {
        new RewardMoneyData { Amount = 500 }
    },
    // 注意：QuestData 不支持直接指定"完成时解锁天赋"的回调，
    // 需要通过 EventBus 订阅 QuestTaskFinishedEvent 来实现。
}, "mymod");

// 步骤 2：订阅任务完成事件，在任务完成时解锁天赋
EventBusManager.Instance.Sync.Register<QuestTaskFinishedEvent>(e =>
{
    // 检查是否是我们关注的任务
    // 通过 Identifier 反查任务
    if (QuestUtils.TryGetQuestIdentifier(questId, out var questIdentifier))
    {
        EndowmentUtils.UnlockEndowment(endowmentId);
    }
}, 0, "mymod");

// ===== 默认解锁天赋（无需任务） =====
// 设置 UnlockedByDefault = true，进入基地后直接可选
EndowmentUtils.RegisterEndowment(
    new Identifier("mymod", "survivor"),
    new EndowmentConfig
    {
        Modifiers = new[]
        {
            new EndowmentModifier { StatKey = "maxHealth", Type = ModifierType.Add, Value = 20 }
        },
        Icon = ItemUtils.LoadSprite("endowment_survivor"),
        UnlockedByDefault = true,    // 默认解锁——直接显示在面板中并可选择
        RequirementTextKey = ""      // 默认解锁时无需解锁条件文本
    }
);

// ===== 查询 =====

// 按 Identifier 查询已注册的天赋
EndowmentEntry? entry = EndowmentUtils.GetEndowment(
    new Identifier("mymod", "assassin"));

// 安全查询
if (EndowmentUtils.TryGetEndowment(
    new Identifier("mymod", "assassin"), out var result))
{
    // 使用 result
}

// 列出指定 mod 的全部天赋 Identifier
IReadOnlyList<Identifier> ids = EndowmentUtils.GetAllEndowments("mymod");

// ===== 状态操作 =====

// 查询天赋是否已解锁（Identifier → 内部映射到 EndowmentIndex → 调原生 API）
bool unlocked = EndowmentUtils.IsEndowmentUnlocked(
    new Identifier("mymod", "assassin"));

// 解锁天赋
EndowmentUtils.UnlockEndowment(new Identifier("mymod", "assassin"));

// 选择/激活天赋
EndowmentUtils.SelectEndowment(new Identifier("mymod", "assassin"));

// 获取当前选中的天赋 Identifier（未选中时返回 null）
Identifier? current = EndowmentUtils.GetCurrentSelection();

// ===== 卸载 =====

// 移除单个天赋
EndowmentUtils.UnregisterEndowment(new Identifier("mymod", "assassin"));

// 批量卸载指定 mod 注册的全部天赋
EndowmentUtils.UnregisterAllEndowments("mymod");

// 兜底：使用强指定的 EndowmentIndex 注册（仅在需要共享枚举空间时使用）
EndowmentUtils.RegisterEndowmentWithIndex(
    new Identifier("mymod", "legacy"),
    entry,
    (EndowmentIndex)10,  // 显式指定枚举值
    "mymod"
);
```

---

## 16. 敌人系统（EnemyUtils）

```csharp
// 注册自定义敌人（modid 从 id.Domain 自动推导）
EnemyUtils.RegisterEnemy(
    new Identifier("mymod", "super_scav"),
    aiConfig,        // IStateConfig 状态机
    preset           // CharacterRandomPreset 预设
);

// 查询敌人预设（不存在时抛 ArgumentException）
CharacterRandomPreset preset = EnemyUtils.GetPreset("super_scav");

// 移除
EnemyUtils.UnregisterEnemy(new Identifier("mymod", "super_scav"));

// 批量卸载
EnemyUtils.UnregisterAllEnemies("mymod");
```

### 16.1 自定义 AI 状态机

实现 `IStateConfig` 接口来定义敌人的 AI 行为：

```csharp
using FeatherMod.Entities;

public class MyScavAI : IStateConfig
{
    public string GetInitialState() => "patrol";

    public void OnStateEnter(string state) { }
    public void OnStateUpdate(string state, float deltaTime) { }
    public void OnStateExit(string state) { }

    public Transition[] GetTransitions(string stateName)
    {
        return stateName switch
        {
            "patrol" => new[]
            {
                new Transition("chase", () => PlayerDetected(), priority: 1),
                new Transition("investigate", () => HeardNoise(), priority: 0),
            },
            "chase" => new[]
            {
                new Transition("patrol", () => PlayerLost(), priority: 1),
            },
            _ => Array.Empty<Transition>(),
        };
    }

    // 以下为 modder 自定义的条件方法
    private bool PlayerDetected() { /* 检测到玩家 */ return false; }
    private bool HeardNoise() { /* 听到声音 */ return false; }
    private bool PlayerLost() { /* 丢失玩家视野 */ return false; }
}
```

FML 的 `StateMachineToBT` 会将状态机编译为 NodeCanvas BehaviourTree。

### 16.2 生成敌人

```csharp
// 在指定位置生成已注册的敌人
CharacterMainControl enemy = EnemyUtils.SpawnEnemy(
    new Identifier("mymod", "super_scav"),
    new Vector3(10, 0, 5),
    onSpawned: (character) =>
    {
        Debug.Log($"Enemy spawned: {character.name}");
    });

// 使用 CharacterSpawnerGroup 生成（复用游戏原生生成点配置）
EnemyUtils.SpawnEnemy(
    new Identifier("mymod", "super_scav"),
    spawnerGroup,
    onSpawned: (character) => { /* ... */ });
```

### 16.3 查询与编译

```csharp
// 按 Identifier 查询已注册敌人（不存在返回 false）
if (EnemyUtils.TryGetEnemy(
    new Identifier("mymod", "super_scav"),
    out CharacterRandomPreset foundPreset))
{
    Debug.Log($"Found preset: {foundPreset.LocalizationKey}");
}

// 预编译状态机为 BehaviourTree（可在注册前验证 AI 配置合法性）
object bt = EnemyUtils.CompileStateMachine(aiConfig);
```

---

## 17. NPC 武器注入（WeaponInjectionUtils）

零 Harmony Hook，直接修改 `CharacterRandomPreset` 的 `itemsToGenerate` 数据，
向 NPC 预设或整个阵营的角色注入自定义武器。

### 按预设名注入

```csharp
using FeatherMod;

// 前缀通配：所有以 "Cname_Scav" 开头的 NPC 预设
WeaponInjectionUtils.AddWeaponToPreset("Cname_Scav*", ItemEntry.Of("mymod", "ak47"), chance: 0.5f);

// 精确匹配单个预设
WeaponInjectionUtils.AddWeaponToPreset("Cname_Boss_Wolf", ItemEntry.Of("mymod", "sniper"), chance: 0.3f);
```

### 按阵营注入

```csharp
using Duckov.Utilities;

// 向所有 Scav 阵营 NPC 注入武器
WeaponInjectionUtils.AddWeaponToTeam(Teams.scav, ItemEntry.Of("mymod", "shotgun"), chance: 0.4f);
```

### 卸载

```csharp
WeaponInjectionUtils.RemoveWeaponFromPreset("Cname_Scav*", ItemEntry.Of("mymod", "ak47"));
WeaponInjectionUtils.RemoveWeaponFromTeam(Teams.scav, ItemEntry.Of("mymod", "shotgun"));
WeaponInjectionUtils.UnregisterAllWeaponInjections("mymod");
```

### 枪/刀互斥

系统自动识别注入武器的类型（枪 / 近战），仅注入到兼容的预设槽位中——枪替换枪、
刀替换刀，不跨类型 fallback。

> **注意**：`AddWeaponToPreset` / `AddWeaponToTeam` 在调用时立即执行注入（修改 ScriptableObject 数据）。
> 建议在 `OnAfterSetup` 中调用。

---

## 18. 抽奖箱注入（LotteryBoxUtils）

通过 Harmony Patch 在 LotteryBox 被使用时自动注入物品到抽奖箱的候选池。
modder 只需调用一次注册，后续场景加载时自动生效。

### 注册

```csharp
using FeatherMod;

// 向所有名为 "LotteryBox_Gun" 开头的抽奖箱注入武器（默认与原生条目等权）
LotteryBoxUtils.AddItemToLotteryBox("LotteryBox_Gun*", ItemEntry.Of("mymod", "ak47"));

// 精确匹配
LotteryBoxUtils.AddItemToLotteryBox("LotteryBox_Boss", ItemEntry.Of("mymod", "sniper"));
```

`weight` 参数（默认 1.0）为相对权重倍数：`实际权重 = weight × 原生条目平均权重`。只追加，不缩放原生条目。
- `weight=1.0`（默认）→ 与原生条目等权
- `weight=2.0` → 权重为原生条目均值的 2 倍
- `weight=0.5` → 权重为原生条目均值的一半

### 卸载

```csharp
LotteryBoxUtils.RemoveItemFromLotteryBox("LotteryBox_Gun*", ItemEntry.Of("mymod", "ak47"));
LotteryBoxUtils.UnregisterAllLotteryInjections("mymod");
```

### 枪/刀互斥

系统自动识别抽奖箱中现有物品的类型（遍历 `candidates.entries`），仅注入匹配类型的物品。
枪只注入到枪箱，刀只注入到刀箱。类型不匹配时跳过并输出警告日志。

> **注意**：`AddItemToLotteryBox` 仅存储规则，地图加载时由 Harmony `Awake` Postfix 自动触发注入。
> 无需手动管理时机——比 WeaponInjection 更适合"场景未加载时提前注册"的场景。

---

## 19. 交互系统（InteractionUtils）

`InteractionUtils` 是 FML 的交互点管理系统。提供在世界中创建/绑定交互点、关联 View 打开方法、
按 NPC 名称查找并挂载交互等完整 API。

### 19.1 交互点两种模式

| 模式 | Handler | 用途 |
|------|---------|------|
| **View 模式** | `ViewInteractHandler` | 交互→通过 `ViewDispatcher` 打开指定 View |
| **Delegate 模式** | `DelegateInteractHandler` | 交互→调用自定义委托 |

### 19.2 Spawn（创建新交互点）

```csharp
using FeatherMod.Interaction;
using UnityEngine;

// 在世界坐标创建 View 交互点
var point = InteractionUtils.SpawnViewInteract(
    new Identifier("mymod", "trading_post"),
    position: new Vector3(10, 0, 5),
    viewType: GameViews.Shop,
    viewParam: "merchant_stock");
// → 自动创建 GameObject + BoxCollider(Trigger) + "Interact" 图层

// 在世界坐标创建自定义委托交互点
InteractionUtils.SpawnCustomInteract(
    new Identifier("mymod", "secret_button"),
    position: new Vector3(3, 1, 8),
    onInteract: () => Debug.Log("Secret triggered!"));
```

### 19.3 Attach（挂载到已有对象）

```csharp
// 给已有 GameObject 挂载交互
InteractionUtils.AttachViewInteract(
    new Identifier("mymod", "door_terminal"),
    target: someGameObject,
    viewType: GameViews.PerkTree,
    viewParam: "mymod_perktree");

// 按名称查找 NPC 并挂载交互
InteractionUtils.AttachToNPC(
    new Identifier("mymod", "talk_to_merchant"),
    npcName: "Merchant_Fence",
    viewType: GameViews.Shop);
```

### 19.4 查询与卸载

```csharp
// 查询已注册交互点
if (InteractionUtils.TryGetInteractPoint(id, out var go))
    Debug.Log($"Found: {go.name}");

// 移除单个交互点（自动 Destroy GameObject）
InteractionUtils.RemoveInteract(id);

// 批量卸载指定 mod 的全部交互点
InteractionUtils.RemoveAllInteracts("mymod");
```

### 19.5 内置 View 类型（GameViews）

```csharp
// 以下 6 个内置 View 类型已由 InteractionUtils.Init() 自动注册打开方法：
GameViews.PerkTree   // Perk 技能树
GameViews.Building   // 建造面板（BuilderView）
GameViews.Endowment  // 天赋选择面板
GameViews.Crafting   // 过滤式合成界面
GameViews.Shop       // 商店（需自行注册打开方法）
GameViews.Quest      // 任务（需自行注册打开方法）

// 自定义 View 注册打开方法：
ViewDispatcher.Register(
    new Identifier("mymod", "custom_view"),
    param => MyCustomView.Open(param),
    "mymod");
```

---

## 20. UI 系统与控件桥接（GameUIUtils）

`GameUIUtils` 桥接游戏原生 UI 系统，提供控件克隆（继承原生视觉风格）、
样式提取和快捷 View 打开。

### 20.1 控件克隆

克隆自 `GameplayDataSettings.UIPrefabs`，自动继承精灵/材质/字体/着色器，视觉与游戏原生一致：

```csharp
using FeatherMod.UI;

// 克隆游戏原生按钮（含正确颜色/字体/精灵）
GameUIUtils.CloneButton(parentTransform, "确认", () => Debug.Log("Clicked"));

// 克隆物品图标显示
var itemDisplay = GameUIUtils.CloneItemDisplay(parentTransform);

// 克隆物品槽位
var slot = GameUIUtils.CloneSlotDisplay(parentTransform);

// 克隆库存条目
var inventoryEntry = GameUIUtils.CloneInventoryEntry(parentTransform);

// 克隆滚动区域
var scrollRect = GameUIUtils.CloneScrollRect(parentTransform);
```

### 20.2 样式查询

```csharp
// 获取游戏主字体（从活跃 View 的 TextMeshProUGUI 提取）
var font = GameUIUtils.GetGameFont();

// 提取 UI 配色方案（从活跃 View 的 [SerializeField] Color 字段）
var palette = GameUIUtils.GetColorPalette();
// palette.TextPrimary    → 主文本色
// palette.PanelBackground → 面板背景色
// palette.ButtonNormal   → 按钮常态色
// palette.ButtonHighlight→ 按钮高亮色
```

### 20.3 快捷 View 打开

```csharp
// 打开过滤式合成界面（仅显示指定工作台的配方）
GameUIUtils.OpenCraftingView(new[] { "Forge", "WorkBenchAdvanced" });

// 打开库存设备面板
GameUIUtils.OpenInventoryDevice(playerInventory);
```

### 20.4 代码端 UI 构建器（SimpleViewBuilder）

`SimpleViewBuilder` 适用于简单面板场景，已内置游戏原生按钮支持：

```csharp
using FeatherMod.UI;

var panel = SimpleViewBuilder.Create("MyModPanel")
    .AddTitle("欢迎使用")
    .AddText("这是一个代码创建的 UI 面板。")
    .AddGameButton("游戏风格按钮", () => Debug.Log("Clicked!"))
    //      ↑ 克隆自 GameplayDataSettings.UIPrefabs.Button
    //        视觉与游戏原生按钮完全一致
    .AddGamePanel("子面板标题")
    //      ↑ 创建半透明背景面板
    .AddButton("普通按钮", () => Debug.Log("Basic"))
    .AddCloseButton()
    .Build();
```

> **注意**：`SimpleViewBuilder` 适用于 15% 的简单 UI 场景。对于更复杂的 UI，
> 推荐使用 Harmony Postfix 注入模式或 `GameUIUtils` 控件克隆。

---

## 21. 物品容器（ContainerUtils）

`ContainerUtils` 提供轻量级物品容器管理，包装游戏原生 API，不实现完整的 Inventory 系统。

### 21.1 容器 CRUD

```csharp
using FeatherMod;

// 创建容器
var config = ContainerUtils.CreateContainer(
    new Identifier("mymod", "storage_box"),
    slotCount: 20,
    modid: "mymod");

// 查询容器
var existing = ContainerUtils.GetContainer(new Identifier("mymod", "storage_box"));
if (existing != null)
    Debug.Log($"Container has {existing.SlotCount} slots");

// 销毁容器（注意：不转移容器内物品）
ContainerUtils.DestroyContainer(new Identifier("mymod", "storage_box"));
```

### 21.2 物品转移

```csharp
// 放入物品
ContainerUtils.PutItem(
    containerId: new Identifier("mymod", "storage_box"),
    slot: 0,
    item: ItemEntry.Of("mymod:coffee", 5));

// 取出物品（自动通过 ItemUtilities 转移到玩家库存）
ItemEntry? taken = ContainerUtils.TakeItem(
    containerId: new Identifier("mymod", "storage_box"),
    slot: 0,
    amount: 3);
```

### 21.3 绑定到建筑

容器可绑定到已有建筑——建筑建造完成时自动挂载交互处理器：

```csharp
ContainerUtils.BindDeviceToBuilding(
    buildingId: new Identifier("mymod", "storage_warehouse"),
    containerId: new Identifier("mymod", "storage_box"),
    viewType: GameViews.Crafting);
// → 建筑建成后，functionContainer 上自动挂载 ViewInteractHandler
```

### 21.4 批量卸载

```csharp
ContainerUtils.RemoveAllContainers("mymod");
```

> **注意**：`RemoveAllContainers` 仅清除 FML 内部跟踪的容器数据，
> 不负责销毁容器中的游戏内物品对象。

---

## 22. 自定义设置面板（ModOptionsRegistry）

```csharp
using FeatherMod.Options;

ModOptionsRegistry.RegisterPanel("mymod", "My Mod Settings", builder =>
{
    // 开关
    builder.AddToggle("enable_feature", true, "Enable Feature");

    // 滑块
    builder.AddSlider("difficulty", 1.0f, 0.5f, 3.0f, "Difficulty Multiplier");

    // 下拉菜单
    builder.AddDropdown("mode", new[] {"Easy", "Normal", "Hard"}, 1, "Mode");

    // 按钮
    builder.AddButton("Reset Settings", () => ResetDefaults());
});
```

面板出现在游戏设置 → Custom Options 标签页中。所有设置值自动通过 `OptionsManager` 持久化。

---

## 23. AssetBundle 加载（AssetUtil）

```csharp
// 从 mod 目录加载（路径: assets/bundle/{bundleName}）
AssetBundle? bundle = AssetUtil.LoadBundle(new Identifier("mymod", "weapons"));

// 便捷重载（需先 ModPathResolver.Register）：
// AssetBundle? bundle = AssetUtil.LoadBundle("weapons");

// 从指定目录加载
AssetBundle? bundle = AssetUtil.LoadBundleFromDir(modDirectory, "weapons");

// 加载好的 AssetBundle 会被缓存，重复调用返回同一实例

// 卸载指定 Bundle
AssetUtil.UnloadBundle(modDirectory, "weapons");

// 卸载全部已缓存 Bundle（通常在 OnBeforeDeactivate 中调用）
AssetUtil.UnloadAllBundles();
```

> AssetBundle 文件放在 `assets/bundle/` 目录下。

---

## 24. 笔记系统（NoteUtils）

提供游戏内可收集笔记的注册、解锁和世界空间拾取物生成。笔记有"已解锁"和"已阅读"两个状态，支持条件门控（`RequireNoteIndexUnlocked`）。

```csharp
// 注册笔记（运行时注入到 NoteIndex）
NoteUtils.RegisterNote(
    new Identifier("mymod", "lore_01"),
    new NoteConfig
    {
        TitleKey = "Note_lore_01_Title",
        ContentKey = "Note_lore_01_Content",
        Image = myImage,
        Hidden = false        // true 则不计入总数
    });

// 解锁笔记
NoteUtils.Unlock(new Identifier("mymod", "lore_01"));

// 解锁并打开笔记 UI
NoteUtils.UnlockAndShow(new Identifier("mymod", "lore_01"));

// 状态查询
bool unlocked = NoteUtils.IsUnlocked(new Identifier("mymod", "lore_01"));
bool read = NoteUtils.IsRead(new Identifier("mymod", "lore_01"));

// 统计
int total = NoteUtils.GetTotalCount();
int unlockedCount = NoteUtils.GetUnlockedCount();

// 在世界空间生成可拾取笔记（支持拾取交互）
NoteUtils.SpawnPickup(new Identifier("mymod", "lore_01"), new Vector3(10f, 0f, 5f));

// 按 modid 批量卸载
NoteUtils.UnregisterAllNotes("MyMod");
```

> 笔记的本地化键遵循 `Note_{key}_Title` / `Note_{key}_Content` 规则，与游戏原生一致。
> FML 通过 `SetNoteDynamic()` 运行时注入，无需修改 Excel 资产。

### 事件

通过 EventBus 订阅笔记状态变更：

```csharp
EventBusManager.Instance.Sync.Register<NoteUnlockedEvent>(evt =>
    Debug.Log($"笔记解锁: {evt.NoteId}"));

EventBusManager.Instance.Sync.Register<NoteReadEvent>(evt =>
    Debug.Log($"笔记已读: {evt.NoteId}"));
```

---

## 25. 钓鱼系统（FishingUtils）

提供钓鱼池注册、特殊配对外加钓鱼统计属性查询。

```csharp
// 注册钓鱼池（水域 → 鱼种 + 权重）
FishingUtils.RegisterFishingPool(
    new Identifier("mymod", "mountain_lake"),
    new FishingPoolConfig
    {
        WaterId = new Identifier("mymod", "mountain_lake"),
        Entries = new[]
        {
            new FishingPoolEntry { FishId = new Identifier("mymod", "salmon"), Weight = 0.5f, MinQuality = 2 },
            new FishingPoolEntry { FishId = new Identifier("mymod", "trout"), Weight = 0.3f }
        },
        MinLuck = 0.1f,
        MaxLuck = 1.0f
    });

// 注册特殊配对（精确 baitID → fishID 映射，含概率）
FishingUtils.RegisterSpecialCatch(
    new Identifier("mymod", "worm_bait"),
    new Identifier("mymod", "golden_fish"),
    0.1f);  // 10% 概率

// 钓鱼统计属性查询
float fishingTime = FishingUtils.GetFishingTime(mainCharacter);
float difficulty  = FishingUtils.GetFishingDifficulty(fishItem);
float quality     = FishingUtils.GetFishingQualityFactor(mainCharacter);

// 按 modid 批量卸载
FishingUtils.UnregisterAll("MyMod");
```

> 鱼物品仍需通过 `ItemUtils.CreateCustomItem` 创建——`FishingUtils` 只管理"什么鱼可以从哪钓到"。
> 特殊配对在 `FishSpawner.Awake` 时通过 Harmony Postfix 自动注入，对 modder 透明。

---

## 26. 友善 NPC（FriendlyNpcUtils）

基于 `CharacterRandomPreset.CreateCharacterAsync` 创建完整的可见 NPC（自动附带 `CharacterModel`、`CustomFaceInstance`、`Animator` 等组件）。

> **重要**：必须在 NPC 创建前通过 `FriendlyNpcConfig.ActorId` 指定 `DuckovDialogueActor.id`，否则对话系统无法找到发言者。该 id 需与 DialogueUtils 的 `actorId` 参数一致。

### 26.1 新版 API（推荐）

```csharp
// ── 第 1 步：注册预设 ──
var config = new FriendlyNpcConfig
{
    DisplayNameKey = "npc_merchant_name",
    ActorId = "merchant_actor",         // DuckovDialogueActor.id（对话系统查找用）
    Role = NpcRole.Merchant,            // Merchant / QuestGiver / Companion / DialogueOnly
    Face = FaceRef.Preset("Duck_Default"), // 捏脸（见下方 FaceRef 模式表）
    Model = ModelRef.GamePrefab("CharacterModel_Duck_Jeff"), // 模型
    Team = Teams.middle,                // 友善阵营
    SpawnPosition = new Vector3(10f, 0f, 5f),
    ShopId = "merchant_shop",
    QuestGiverId = "daily_01",
    HeadEquipment = ItemEntry.Of("duckov:CowboyHat", 1),  // 头部装备
    BodyEquipment = ItemEntry.Of("duckov:Vest_A", 1),     // 身体装备
};
var preset = FriendlyNpcUtils.RegisterFriendlyNpc(new Identifier("mymod", "merchant_01"), config);

// ── 第 2 步：异步生成 ──
var npc = await FriendlyNpcUtils.SpawnFriendlyNpcAsync(new Identifier("mymod", "merchant_01"));
// npc 现在是一个完整的可见角色，带 CharacterModel + CustomFaceInstance + Collider
```

#### FaceRef 捏脸模式

| 模式 | 用法 | 效果 |
|------|------|------|
| `Preset` | `FaceRef.Preset("Duck_Default")` | 引用 Resources 中已有 `CustomFacePreset` |
| `PlayerFace` | `FaceRef.PlayerFace()` | 使用玩家当前捏脸数据 |
| `Custom` | `FaceRef.Custom(parts)` | 按 8 个部件 ID 自定义组合 |
| `FromJson` | `FaceRef.FromJson(json)` | 从游戏原生 `CustomFaceSettingData` JSON 字符串创建 |

```csharp
// ── FromJson 示例：从文件加载已保存的捏脸数据 ──
string json = File.ReadAllText(Path.Combine(modDir, "faces", "npc_laozheng.json"));
Face = FaceRef.FromJson(json),
```
> JSON 格式与 `CustomFaceUtils.GetPlayerFaceJson()` 输出一致，也可从游戏存档导出。

### 26.2 旧版 API（已废弃）

```csharp
// ⚠️ 已标记 [Obsolete]，返回的是临时占位 GameObject，NPC 不可见
// 请改用 RegisterFriendlyNpc + SpawnFriendlyNpcAsync
var go = FriendlyNpcUtils.CreateFriendlyNpc(id, config);
```

### 26.3 角色类型（NpcRole）

| 枚举值 | 行为 |
|--------|------|
| `Merchant` | 交互打开商店 UI（需 `ShopId`） |
| `QuestGiver` | 交互打开任务 UI（需 `QuestGiverId`） |
| `Companion` | NPC 跟随玩家 |
| `DialogueOnly` | 仅对话，不绑定额外交互 |

### 26.4 其他 API

```csharp
// 世界空间对话气泡
FriendlyNpcUtils.ShowBubble(new Identifier("mymod", "merchant_01"), "欢迎！", 3f);
FriendlyNpcUtils.ShowBubbleLocalized(id, "dialogue_welcome", 3f);

// 绑定商店 / 任务
FriendlyNpcUtils.BindShop(id, new Identifier("mymod", "shop"));
FriendlyNpcUtils.BindQuestGiver(id, "daily_01");
FriendlyNpcUtils.BindQuestGiver(id, new Identifier("mymod", "quest_giver_custom"));

// 销毁 / 批量卸载
FriendlyNpcUtils.RemoveNpc(id);
FriendlyNpcUtils.RemoveAllNpcs("MyMod");
```

> **技术说明**：NPC 创建基于游戏原生的 `CharacterRandomPreset` + `CreateCharacterAsync`（与 `EnemyUtils.SpawnEnemy` 同路径）。所有 `[SerializeField] private` 字段经 Krafs.Publicizer 编译期公开，直接赋值无需反射。Preset 字段参考游戏原生友善 NPC（Ming/Fo）的配置值。

---

## 27. 捏脸系统（CustomFaceUtils）

提供从官方捏脸数据串（JSON）导入/导出捏脸数据的能力，让 Mod 可以动态修改玩家或任意角色的外观。

游戏原生的捏脸数据格式是 `CustomFaceSettingData` 结构体（Duckov 内置），通过 `DataToJson()` / `JsonToData()` 进行 JSON 序列化。`CustomFaceUtils` 在此之上封装了 FML 风格的便捷 API。

### 27.1 JSON 格式说明

官方捏脸数据串是游戏 `CustomFaceSettingData.DataToJson()` 输出的标准 JSON，结构如下：

```json
{
  "savedSetting": false,
  "headSetting": {
    "mainColor": { "r": 0.129, "g": 0.129, "b": 0.129, "a": 1 },
    "headScaleOffset": 0,
    "foreheadHeight": 0.07,
    "foreheadRound": 0.782
  },
  "hairID": 1,
  "hairInfo": { "radius": 0, "color": {...}, "height": 0, "scale": 1, "twist": 0, ... },
  "eyeID": 3,
  "eyeInfo": { "radius": 0.23, "color": {...}, "height": 0.089, "scale": 1.179, ... },
  "eyebrowID": 0,
  "eyebrowInfo": { ... },
  "mouthID": 20,
  "mouthInfo": { ... },
  "tailID": 1,
  "tailInfo": { ... },
  "footID": 1,
  "footInfo": { ... },
  "wingID": 0,
  "wingInfo": { ... }
}
```

每个 `*Info` 对象包含：`radius`（半径）、`color`（Color RGBA）、`height`（高度）、`heightOffset`（偏移）、`scale`（缩放）、`twist`（扭曲）、`distanceAngle`（距离角度，0-90）、`leftRightAngle`（左右角度，-90~90）。

### 27.2 玩家主角捏脸

```csharp
// 从官方捏脸 JSON 字符串设置玩家外观
string faceJson = "{\"savedSetting\":false,\"headSetting\":{...}}";
bool ok = CustomFaceUtils.SetPlayerFaceFromJson(faceJson);

// 导出玩家当前捏脸为 JSON 字符串
string current = CustomFaceUtils.GetPlayerFaceJson();

// 使用原生 CustomFaceSettingData 结构体
CustomFaceUtils.SetPlayerFaceFromData(nativeFaceData);
CustomFaceSettingData data = CustomFaceUtils.GetPlayerFaceAsData();

// 验证 JSON 串是否合法
bool valid = CustomFaceUtils.ValidateJson(faceJson);
```

### 27.3 任意角色捏脸

通过 `CustomFaceInstance` 组件对任意角色（包括 NPC）进行捏脸操作：

```csharp
// 获取任意角色上的 CustomFaceInstance 组件
var faceInstance = someCharacter.GetComponent<CustomFaceInstance>();

// 从 JSON 设置
CustomFaceUtils.SetFaceFromJson(faceInstance, faceJson);

// 导出为 JSON
string json = CustomFaceUtils.GetFaceJson(faceInstance);

// 使用原生结构体
CustomFaceUtils.LoadFaceFromData(faceInstance, nativeData);
CustomFaceSettingData data = CustomFaceUtils.GetFaceAsData(faceInstance);
```

### 27.4 获取玩家主角的 CustomFaceInstance

```csharp
// 在场景中查找玩家主角的 CustomFaceInstance
var playerFace = CustomFaceUtils.GetPlayerFaceInstance();
if (playerFace != null)
{
    CustomFaceUtils.SetFaceFromJson(playerFace, myJson);
}
```

> `CustomFaceUtils.GetPlayerFaceInstance()` 通过 `FindObjectOfType<MainCharacterFace>()` 查找玩家主角的面部实例。
> 如果主角不在场景中（如主菜单），返回 `null`。

### 27.5 将捏脸应用到 NPC

`CustomFaceUtils` 用于**运行时**修改已存在角色的捏脸。如果要在 NPC **创建时**指定捏脸，使用 `FaceRef.FromJson()`（详见 §26.1）：

```csharp
// NPC 创建时指定捏脸（推荐——避免运行时查找 CustomFaceInstance）
var config = new FriendlyNpcConfig
{
    Face = FaceRef.FromJson(faceJson),  // 从 JSON 直接创建捏脸
    // ...其他配置
};
FriendlyNpcUtils.RegisterFriendlyNpc(id, config);
```

> **区别**：`CustomFaceUtils.SetFaceFromJson(instance, json)` 修改**已生成**角色的捏脸（需要先获取 `CustomFaceInstance` 组件）；`FaceRef.FromJson(json)` 在 NPC **创建时**通过 `CharacterRandomPreset.facePreset` 设置捏脸（更早、更可靠）。

---

## 28. 天气系统（WeatherUtils）

提供天气/季节查询、强制覆盖、风暴信息和温度防护属性查询。

```csharp
// 天气查询（FML WeatherType 枚举，隐藏 Snow=22 细节）
WeatherType weather = WeatherUtils.GetCurrentWeather();
// → Sunny / Cloudy / Rainy / Snow / Stormy / SevereStormy

// 季节查询
SeasonType season = WeatherUtils.GetCurrentSeason();
// → Spring / Summer / Autumn / Winter

// 强制覆盖天气（调试/剧情用）
WeatherUtils.ForceWeather(WeatherType.Stormy);

// 取消强制覆盖
WeatherUtils.ResetWeather();

// 便捷判断
bool isRaining = WeatherUtils.IsRaining();
bool isSnowing = WeatherUtils.IsSnowing();

// 风暴等级（0=无风暴，1=Stormy_I，2=Stormy_II）
int level = WeatherUtils.GetStormLevel();
bool inStorm = WeatherUtils.IsStormActive();

// 温度查询
float cold = WeatherUtils.GetColdLevel();  // -10 ~ +10
float heat = WeatherUtils.GetHeatLevel();

// 防护属性（基于 ItemStatsSystem 的 StormProtection / ColdProtection / HeatProtection）
float stormProt = WeatherUtils.GetStormProtection(playerCharacter);
float coldProt  = WeatherUtils.GetColdProtection(playerCharacter);
float heatProt  = WeatherUtils.GetHeatProtection(playerCharacter);
```

### 事件

```csharp
EventBusManager.Instance.Sync.Register<StormStartedEvent>(_ =>
    Debug.Log("风暴开始！"));

EventBusManager.Instance.Sync.Register<StormEndedEvent>(_ =>
    Debug.Log("风暴结束。"));
```

---

## 29. 多场景（MultiSceneUtils）

提供关卡内子场景加载、传送和跨场景持久数据存储。

```csharp
// 注册自定义场景（Identifier → 游戏原生 sceneID 映射）
MultiSceneUtils.RegisterScene(
    new Identifier("mymod", "custom_boss_room"),
    "Level_Desert_Boss");

// 加载子场景
MultiSceneUtils.LoadSubScene(new Identifier("mymod", "custom_boss_room"));

// 传送（加载 + 传送到指定位置/坐标）
MultiSceneUtils.TeleportTo(new Identifier("mymod", "custom_boss_room"), "boss_spawn");
MultiSceneUtils.TeleportTo(new Identifier("mymod", "custom_boss_room"), new Vector3(100f, 0f, 200f));

// 查询当前场景
Identifier? current = MultiSceneUtils.GetCurrentSubScene();
string displayName = MultiSceneUtils.GetSceneDisplayName(new Identifier("duckov", "Base"));

// 关卡内跨场景持久数据（基于 MultiSceneCore.inLevelData）
MultiSceneUtils.SetLevelData("boss_defeated", true);
bool defeated = MultiSceneUtils.GetLevelData<bool>("boss_defeated") ?? false;

// 物体场景归属迁移
MultiSceneUtils.MoveToScene(myNpc, new Identifier("mymod", "custom_boss_room"));
MultiSceneUtils.MoveToMainScene(myNpc);
```

### 事件

```csharp
EventBusManager.Instance.Sync.Register<SceneLoadFinishedEvent>(evt =>
    Debug.Log($"场景加载完成: {evt.SceneId}"));

EventBusManager.Instance.Sync.Register<SubSceneChangedEvent>(evt =>
    Debug.Log($"子场景切换: {evt.FromScene} → {evt.ToScene}"));
```

---

## 30. 对话系统（DialogueUtils）

提供世界空间气泡和全屏字幕对话两种模式。全屏字幕通过游戏原生 `DialogueUI`（订阅 `DialogueTree.OnDialogueStarted` / `OnSubtitlesRequest` / `OnDialogueFinished` 事件）显示。

> **前置条件**：`PlaySubtitle` / `PlaySubtitles` 依赖 `DuckovDialogueActor.Get(actorId)` 查找发言者——必须先在 NPC 的 `FriendlyNpcConfig.ActorId` 中注册匹配的 id。

### 30.1 世界空间气泡

```csharp
// 在 NPC 头顶显示
DialogueUtils.ShowBubble(new Identifier("mymod", "merchant"), "欢迎！有什么可以帮你的？", 3f);

// 在任意坐标显示
DialogueUtils.ShowBubbleAt(new Vector3(10f, 1.5f, 5f), "这里看起来很有趣…");
```

### 30.2 全屏字幕对话

```csharp
// ── 单行字幕 ──
DialogueUtils.PlaySubtitle("merchant_actor", "欢迎光临我的小店！");

// ── 字幕序列（多行按顺序播放，空格/点击推进）──
await DialogueUtils.PlaySubtitles("merchant_actor", new[]
{
    new SubtitleLine { Text = "欢迎光临！你需要点什么？" },
    new SubtitleLine { ActorId = "player_actor", TextKey = "dialogue_player_reply_01" },
    new SubtitleLine { Text = "好的，这是你的物品。" }
});
```

### 30.3 对话流程说明

```
DialogueUtils.PlaySubtitles()
  → 触发 DialogueTree.OnDialogueStarted   → DialogueUI 显示主面板 + 禁用输入
  → 循环每行：
      → DialogueTree.RequestSubtitles()   → DialogueUI.DoSubtitle（打字机动画 + 音效）
      → 等待用户点击确认
  → 触发 DialogueTree.OnDialogueFinished  → DialogueUI 隐藏 + 恢复输入
```

| 参数 | 说明 |
|------|------|
| `ActorId` | `DuckovDialogueActor.id`。为空时使用 `defaultActorId` |
| `Text` | 直接文本（优先级高于 TextKey）。作为 `LocalizedStatement` 的 key 传入，无翻译时原样返回 |
| `TextKey` | 本地化键。通过 FeatherMod `I18n` 预注入 → `LocalizationManager` → `ToPlainText()` 解析 |

> `DuckovDialogueActor.OnEnable()` 自动调用 `Register(this)`，`OnDisable()` 自动调用 `Unregister(this)`——无需手动注册。
> `DialogueUI` 是单例（`DialogueUI.instance`），由游戏场景自动管理。

---

## 31. 注册表系统（Registry）

### 24.1 基本操作

所有模块的数据都通过 `IRegistry<T>` 管理：

```csharp
using FeatherMod.Register;

// 获取元注册表
var meta = RegistryManager.Instance.Registry;

// 读取注册表
var audioRegistry = meta.Get(new Identifier("FeatherMod", "audio"));

// 遍历注册表
foreach (var entry in meta)
{
    Debug.Log($"{entry.Key}: {entry.Value}");
}
```

### 24.2 三种 Registry 实现

| 实现 | 特点 | 使用场景 |
|------|------|----------|
| `SimpleRegistry<T>` | CRUD + owner 追踪 + `OnRemoved` 回调 | 常规模块（Quest / Buff / Building） |
| `NonAlterableSimpleRegistry<T>` | 写入后不可覆盖 | 元注册表 |
| `ReverseLookupRegistry<T, TKey>` | 按 native key 反查 Identifier | Audio / Items |

### 24.3 创建自定义 Registry

```csharp
// 创建自定义注册表
public class MyCustomRegistry : SimpleRegistry<MyType>
{
    protected override void OnRemoved(Identifier id, MyType value, string? modid)
    {
        // 自动清理 native 侧资源
        GameObject.Destroy(value.gameObject);
    }
}

// 注册到元表
var meta = RegistryManager.Instance.Registry;
meta.Set(new Identifier("mymod", "myregistry"), myRegistry, "mymod");
```

---

## 32. 模组卸载生命周期

FML 自动处理模组卸载时的清理工作，**无需手动编写卸载逻辑**。

当游戏卸载你的模组时，`OnBeforeDeactivate` 自动执行：

```
1. GameEventAdapters.TearDown()
   → 解除所有原生事件订阅（-=）

2. EventBusManager.Clear()
   → 清空同步/异步总线所有 handler

3. RegistryManager.RemoveAllByOwner("mymod")
   → 遍历元表所有注册表
   → 按 modid 批量卸载
   → 各注册表的 OnRemoved 回调自动清理 native 侧
```

这意味着：
- ✅ 所有通过 FML API 注册的物品 / 配方 / 任务 / Buff / 建筑 / Perk / 商店商品 / 音频等自动卸载
- ✅ 所有 EventBus 订阅自动解除
- ✅ 所有原生事件桥接自动解除
- ❌ 不需要手动维护 `Dictionary` 追踪注册资源
- ❌ 不需要手动 `UnregisterAll`

---

## 33. NPC 装备系统（EquipmentUtils）

管理 NPC 身体/头部/背包装备。生成前通过 `FriendlyNpcConfig` 配置，或通过 `EquipmentUtils` API 动态管理。

> **技术说明**：装备通过 `CharacterRandomPreset.itemsToGenerate` 注入，由 `CreateCharacterAsync` 在生成时自动装备到对应槽位（`ArmorSlot` / `HelmatSlot` / `BackpackSlot`）。运行时装备修改待后续基于 `CharacterItemControl` 实现。

### 33.1 生成时配置（推荐）

```csharp
// 方式 1：直接在 FriendlyNpcConfig 中配置
var config = new FriendlyNpcConfig
{
    HeadEquipment = ItemEntry.Of("duckov:CowboyHat", 1),
    BodyEquipment = ItemEntry.Of("duckov:Vest_A", 1),
};
FriendlyNpcUtils.RegisterFriendlyNpc(id, config);

// 方式 2：通过 EquipmentUtils API 配置
EquipmentUtils.ConfigureNpcEquipment(id, EquipmentSlot.Head, ItemEntry.Of("duckov:Fedora", 1));
```

### 33.2 装备槽位

```csharp
public enum EquipmentSlot
{
    Head,      // 头部（头盔/帽子）
    Body,      // 身体（护甲/衣服）
    Backpack   // 背包
}
```

### 33.3 API 参考

```csharp
// 配置装备（在 RegisterFriendlyNpc 之前或之后调用均可）
EquipmentUtils.ConfigureNpcEquipment(id, EquipmentSlot.Head, item);

// 查询已配置的装备
if (EquipmentUtils.TryGetConfiguredEquipment(id, EquipmentSlot.Body, out var item))
    Debug.Log($"Body: {item}");

// 清除指定槽位
EquipmentUtils.ClearConfiguredEquipment(id, EquipmentSlot.Head);

// 清除全部
EquipmentUtils.ClearAllEquipment(id);

// 运行时设置（已生成的 NPC）
bool applied = EquipmentUtils.SetNpcEquipment(id, EquipmentSlot.Head, item);

// 运行时查询
var equip = EquipmentUtils.GetNpcEquipment(id, EquipmentSlot.Body);

// 运行时清除
EquipmentUtils.ClearNpcEquipment(id, EquipmentSlot.Backpack);
```

> `SetNpcEquipment` / `ClearNpcEquipment` 运行时版本待基于 `CharacterItemControl` 的物品槽位系统实现（`ArmorSlot` / `HelmatSlot` / `BackpackSlot`）。

---

## 34. 附录：项目结构参考

### 推荐目录结构

```
MyMod/
├── MyMod.csproj
├── MyMod.cs                    # ModBehaviour 主类
├── assets/
│   ├── bundle/
│   │   └── weapons             # AssetBundle 文件
│   ├── lang/
│   │   ├── en_us.json          # 语言文件
│   │   └── zh_cn.json
│   └── textures/
│       └── coffee_icon.png     # 物品图标
├── bin/                        # 构建输出
└── README.md
```

### 常用命名空间速查

| 命名空间 | 包含 |
|----------|------|
| `FeatherMod` | `ItemUtils`, `CraftingUtils`, `QuestUtils`, `ShopUtils`, `EconomyUtils`, `BuffUtils`, `BuildingUtils`, `PerkTreeUtils`, `EnemyUtils`, `AssetUtil`, `I18n`, `ModBehaviour`, `ContainerUtils`, `NoteUtils`, `FishingUtils`, `FriendlyNpcUtils`, `CustomFaceUtils`, `WeatherUtils`, `MultiSceneUtils`, `DialogueUtils` |
| `FeatherMod.Utils` | `Identifier`, `Singleton<T>`, `ModPathResolver` |
| `FeatherMod.Interaction` | `InteractionUtils`, `ViewDispatcher`, `GameViews`, `InteractionRegistry`, `InteractionEntry` |
| `FeatherMod.Interaction.Components` | `ViewInteractHandler`, `DelegateInteractHandler` |
| `FeatherMod.UI` | `GameUIUtils`, `GameUIColorPalette`, `SimpleViewBuilder`, `InteractTemplates` |

### ModPathResolver — 路径注册

`ModPathResolver` 是 FML 的 mod 目录解析器。在 `OnAfterSetup` 中显式注册后，`I18n`、`ItemUtils.LoadSprite`、`AssetUtil.LoadBundle` 等的便捷重载才能正确解析 mod 目录：

```csharp
protected override void OnAfterSetup()
{
    base.OnAfterSetup(); // GetModid() 在此之后可用
    string dllPath = Assembly.GetExecutingAssembly().Location;
    ModPathResolver.Register(GetModid(), dllPath);
}
```

> 未注册时，`ResolveDirectory(modid)` 返回 `null`，便捷重载将退回到 FML 自身 DLL 目录路径，可能导致资源加载失败。
| `FeatherMod.Register` | `IRegistry<T>`, `SimpleRegistry<T>`, `NonAlterableSimpleRegistry<T>`, `ReverseLookupRegistry<T,TKey>`, `RegistryManager` |
| `FeatherMod.Audio` | `AudioUtil`, `AudioData` |
| `FeatherMod.Events` | `EventBusManager`, `EventBus`, `AsyncEventBus` |
| `FeatherMod.Events.GameEvents` | `HurtEvent`, `MoneyChangedEvent`, 等 15 个事件类型 |
| `FeatherMod.Options` | `ModOptionsRegistry`, `ModOptionsBuilder` |
| `FeatherMod.Entities` | `IStateConfig`, `Transition`, `StateMachineToBT` |
| `FeatherMod.Items` | `ItemData`, `BulletData`, `BlueprintData`, `UsageData`, `ModifierData` |
| `FeatherMod.Quests` | `QuestData`, `TaskData`, `RewardData` 及其子类 |
| `FeatherMod.Shop` | `ShopGoodsData` |

---

_如有疑问，请在 GitHub Issues 中提出。_
