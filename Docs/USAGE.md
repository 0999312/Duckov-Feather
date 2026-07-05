# Fast-Modding-Lib 使用文档 / Usage Guide

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
19. [自定义设置面板（ModOptionsRegistry）](#19-自定义设置面板modoptionsregistry)
20. [AssetBundle 加载（AssetUtil）](#20-assetbundle-加载assetutil)
21. [注册表系统（Registry）](#21-注册表系统registry)
22. [模组卸载生命周期](#22-模组卸载生命周期)
23. [附录：项目结构参考](#23-附录项目结构参考)

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
    <Reference Include="path\to\FastModdingLib.dll" />
  </ItemGroup>
</Project>
```

> 你也可以通过 `DUCKOV_PATH` 环境变量或 `DuckovPath` 属性指定游戏路径，详见 FML 项目自身的 `.csproj`。

### 1.3 编写第一个模组

```csharp
using FastModdingLib;
using FastModdingLib.Utils;
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
> - 继承 **`Duckov.Modding.ModBehaviour`**（游戏引擎基类），**不**继承 `FastModdingLib.ModBehaviour`
> - 实现 **`IHasModid`** 接口 — FML 工具通过此接口获取你的 mod 身份
> - `FMLBootstrap` 自动管理 Registry / EventBus 等游戏级单例——你只需调用 FML 工具方法即可

---

## 2. 模组主类

所有依赖 FML 的模组应直接继承 `Duckov.Modding.ModBehaviour`（游戏引擎基类）并实现 `IHasModid` 接口。

> **注意**：`FastModdingLib.ModBehaviour` 是 FML 自身的入口类，由 ModManager 实例化。**外部模组不应继承它。**

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
        "FastModdingLib",
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
    "dependencies": ["FastModdingLib"],
    "autoActivate": true
}
```

**带软依赖的大型模组**
```json
{
    "modid": "MyOverhaul",
    "priority": 200,
    "dependencies": ["FastModdingLib"],
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

> **数字 ID 全自分配**：`QuestData.ID`（从 1000 起递增）、`TaskData.id`、`RewardData.id`（从 1 起递增）
> 均由 FML 在注册时自动分配。modder 无需手动设置（已 internal）。
> `RewardUnlockEndowmentData` 在任务完成时自动解锁指定天赋（AutoClaim），无需 modder 手动处理解锁逻辑。

### 6.3 任务关系图

```csharp
// 设置任务前置/后置关系（Identifier 版本）
var current = new Identifier("mymod", "coffee_run");
var preReq  = new Identifier("mymod", "intro_quest");
var unlocks = new Identifier("mymod", "next_quest");

QuestUtils.AddQuestRelation(current, before: preReq, after: unlocks);
```

### 6.4 任务 ID 反查

```csharp
// 数字 ID → Identifier
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
ShopUtils.CreateMerchantProfile("MyTrader");
```

---

## 8. 音频系统（AudioUtil）

### 8.1 SFX 注册

```csharp
using FastModdingLib.Audio;

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
using FastModdingLib.Events;
using FastModdingLib.Events.GameEvents;

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
using FastModdingLib.Events;
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

```csharp
// ===== 注册与放置 =====

// 注册自定义建筑（modid 从 id.Domain 自动推导）
BuildingUtils.RegisterBuilding(
    new Identifier("mymod", "workbench"),
    buildingInfo,   // BuildingInfo 数据
    prefab          // Building 预制体
);

// 放置建筑（通过反射调用 BuildingManager.BuyAndPlace）
// areaId 和 buildingId 均为 Identifier，FML 内部映射为原生 string ID
BuildingUtils.PlaceBuilding(
    new Identifier("base", "area1"),        // 区域 Identifier
    new Identifier("mymod", "workbench"),   // 建筑 Identifier
    new Vector2Int(2, 2),                    // 坐标
    BuildingRotation.Rot0                    // 旋转
);

// ===== 查询（Identifier 优先） =====

// 按 Identifier 查询 BuildingInfo（优先查 Registry，再回退到 native collection）
BuildingInfo? info = BuildingUtils.GetBuildingInfo(
    new Identifier("mymod", "workbench"));

// 获取所有已注册的建筑 Identifier 列表
IReadOnlyList<Identifier> allIds = BuildingUtils.GetAllBuildingIds();

// ===== 卸载 =====

// 移除单个建筑
BuildingUtils.UnregisterBuilding(new Identifier("mymod", "workbench"));

// 批量卸载指定 mod 注册的全部建筑
BuildingUtils.UnregisterAllBuildings("mymod");

// ===== 建筑建成回调 =====

// 注册建筑建成回调（modid 自动从 id.Domain 推导）
BuildingUtils.OnBuildingBuilt(
    new Identifier("mymod", "workbench"),
    onBuilt);  // Action<Building>

// 取消建筑建成回调
BuildingUtils.OffBuildingBuilt(
    new Identifier("mymod", "workbench"),
    onBuilt);  // 需传入与 OnBuildingBuilt 相同的 Action 引用

// ===== 代码端创建建筑 =====

// 纯代码创建简易 Building（无需 Unity 编辑器预制体）
Building building = BuildingUtils.CreateSimpleBuilding(
    new Identifier("mymod", "workbench"),
    new Vector2Int(2, 2),                     // 占地尺寸
    existingPrefabName: "Building_Workbench"); // 可选：克隆已有建筑结构

// 设置建筑模型
BuildingUtils.SetBuildingModel(
    new Identifier("mymod", "workbench"),
    myModelPrefab);
```

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
using FastModdingLib.Entities;

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
using FastModdingLib;

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
using FastModdingLib;

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

## 19. 自定义设置面板（ModOptionsRegistry）

```csharp
using FastModdingLib.Options;

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

## 20. AssetBundle 加载（AssetUtil）

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

## 18.1 代码端 UI 构建器（SimpleViewBuilder）

当只需要简单面板（标题+文本+按钮）且不想用 Unity 编辑器搭建 Canvas 时，
可使用 `SimpleViewBuilder` 纯代码创建：

```csharp
using FastModdingLib.UI;

// 创建简单面板
var panel = SimpleViewBuilder.Create("MyModPanel")
    .AddTitle("欢迎使用")
    .AddText("这是一个代码创建的 UI 面板。")
    .AddButton("执行操作", () => Debug.Log("Button clicked!"))
    .AddCloseButton()
    .Build();
```

> **注意**：`SimpleViewBuilder` 适用于 15% 的简单 UI 场景。对于更复杂的 UI，
> 推荐使用 Harmony Postfix 注入模式——在已有游戏 View 的 `Setup()` 中追加按钮/条目。
> 详见 [FML-REFERENCE.md](FML-REFERENCE.md) §2.3 的"UI 注入辅助"条目及 §3 案例方案。

---

## 21. 注册表系统（Registry）

### 19.1 基本操作

所有模块的数据都通过 `IRegistry<T>` 管理：

```csharp
using FastModdingLib.Register;

// 获取元注册表
var meta = RegistryManager.Instance.Registry;

// 读取注册表
var audioRegistry = meta.Get(new Identifier("fastmoddinglib", "audio"));

// 遍历注册表
foreach (var entry in meta)
{
    Debug.Log($"{entry.Key}: {entry.Value}");
}
```

### 19.2 三种 Registry 实现

| 实现 | 特点 | 使用场景 |
|------|------|----------|
| `SimpleRegistry<T>` | CRUD + owner 追踪 + `OnRemoved` 回调 | 常规模块（Quest / Buff / Building） |
| `NonAlterableSimpleRegistry<T>` | 写入后不可覆盖 | 元注册表 |
| `ReverseLookupRegistry<T, TKey>` | 按 native key 反查 Identifier | Audio / Items |

### 19.3 创建自定义 Registry

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

## 22. 模组卸载生命周期

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

## 23. 附录：项目结构参考

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
| `FastModdingLib` | `ItemUtils`, `CraftingUtils`, `QuestUtils`, `ShopUtils`, `EconomyUtils`, `BuffUtils`, `BuildingUtils`, `PerkTreeUtils`, `EnemyUtils`, `AssetUtil`, `I18n`, `ModBehaviour` |
| `FastModdingLib.Utils` | `Identifier`, `Singleton<T>`, `ModPathResolver` |

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
| `FastModdingLib.Register` | `IRegistry<T>`, `SimpleRegistry<T>`, `NonAlterableSimpleRegistry<T>`, `ReverseLookupRegistry<T,TKey>`, `RegistryManager` |
| `FastModdingLib.Audio` | `AudioUtil`, `AudioData` |
| `FastModdingLib.Events` | `EventBusManager`, `EventBus`, `AsyncEventBus` |
| `FastModdingLib.Events.GameEvents` | `HurtEvent`, `MoneyChangedEvent`, 等 15 个事件类型 |
| `FastModdingLib.Options` | `ModOptionsRegistry`, `ModOptionsBuilder` |
| `FastModdingLib.Entities` | `IStateConfig`, `Transition`, `StateMachineToBT` |
| `FastModdingLib.Items` | `ItemData`, `BulletData`, `BlueprintData`, `UsageData`, `ModifierData` |
| `FastModdingLib.Quests` | `QuestData`, `TaskData`, `RewardData` 及其子类 |
| `FastModdingLib.Shop` | `ShopGoodsData` |

---

_如有疑问，请在 GitHub Issues 中提出。_
